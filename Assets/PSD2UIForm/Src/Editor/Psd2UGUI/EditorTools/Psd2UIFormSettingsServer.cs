using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>
    /// PSD2UIForm 设置页面的 HTTP 服务器（页面与 /config、/save 同源）。
    ///
    /// 设计要点（都是之前"设置页和 asset 值两边不一致"踩过的坑）：
    ///  1. 页面通过 http://localhost:PORT/ 打开，而不是 file://。file:// 页面去 fetch http://localhost 属于
    ///     跨源 + 回环私网访问，浏览器可能直接拦掉，页面会静默退回表单默认值。
    ///  2. Unity 的 API（SerializedObject / ScriptableSingleton / AssetDatabase）只能在主线程调用，但读取一律不碰主线程：
    ///     - GET /config 只读主线程维护好的 JSON 快照（cachedConfigJson），任何情况下都能立刻应答。
    ///       之前这里是"派发到主线程 + 等结果"，而主线程在域重载/脚本编译期间不消费 delayCall，
    ///       请求超时后页面退回表单默认值 —— 表现出来就是"刷新时不一致，点一下 Unity 又一致了"。
    ///     - POST /save 的主线程部分（AssetDatabase 限制）改成"两段式"：HTTP 线程做完能独立完成的事
    ///       就立刻应答，主线程只负责落盘。绝不能像之前那样在 HTTP 线程里死等主线程 ——
    ///       Unity 编辑器失去焦点时（用户正开在浏览器里的设置页）主循环会被节流，
    ///       delayCall 长时间不被消费，表现就是"点了保存，必须点一下 Unity 才成功"。
    ///       唤醒手段：请求到达时主动调 EditorApplication.QueuePlayerLoopUpdate()，
    ///       并在收到请求后一段时间内保持编辑器 tick（否则 player loop 停了就没人消费队列）。
    ///       数据永不丢：队列里的内容会在主线程下一次 update、域重载或退出 Unity 时兜底落盘。
    ///  3. 保存时立刻落盘：
    ///     - UGUIParser（Assets/…/Psd2UIFormConfig.asset）走 AssetDatabase 保存；
    ///     - Psd2UIFormSettings（ProjectSettings/Psd2UIFormSettings.asset）不是 AssetDatabase 资源，
    ///       AssetDatabase.SaveAssets() 管不到它，必须显式调 SaveInstance()，否则重启 Unity 就丢。
    ///  4. 页面在成功读到配置之前不允许保存（守卫脚本由本类注入）。
    /// </summary>
    public class Psd2UIFormSettingsServer
    {
        private const int Port = 9527;

        private static readonly string BaseUrl = "http://localhost:" + Port;

        private HttpListener listener;
        private Thread listenerThread;
        private volatile bool running;
        private string configPath;
        private string pageHtml;
        private UGUIParser config;

        /// <summary>
        /// 配置 JSON 快照。由 Unity 主线程维护（Start 时建立，之后每 0.5s 复查一次变更），HTTP 线程只读。
        /// 这样 GET /config 永远不需要等主线程，也就不会再出现"页面拿不到配置、退回默认值"。
        /// </summary>
        private volatile string cachedConfigJson;

        private double nextCacheRefreshTime;
        private const double CacheRefreshIntervalSeconds = 0.5;

        /// <summary>
        /// 保存响应最多等主线程多久确认。超时不影响正确性：请求已经进了应用队列，
        /// 编辑器一有更新（或域重载 / 退出 Unity）就会落盘，这里只决定响应里报
        /// applied=true 还是"排队中"。绝不能像之前那样死等 30s —— 那正是
        /// "点了保存要等点一下 Unity 才成功"的原因。
        /// </summary>
        private const double SaveAckWaitSeconds = 2.0;

        /// <summary>
        /// 收到 HTTP 请求后保持编辑器 tick 的时长。编辑器失去焦点时 player loop 会被节流甚至停掉，
        /// 而队列（delayCall / update）就靠它消费；这段时间内主动续上，保存才能立刻落盘。
        /// </summary>
        private const double KeepAliveSeconds = 15.0;

        /// <summary>保持 tick 的截止时刻（DateTime.UtcNow.Ticks），HTTP 线程写、主线程读。</summary>
        private long keepAliveUntilTicks;

        /// <summary>最近一次成功从 asset 读到的 JSON（保存失败时用来把快照回滚成真实值）。</summary>
        private volatile string lastGoodConfigJson;

        // ---------------- 待应用的保存任务：HTTP 线程写入，编辑器主线程消费 ----------------
        private readonly object saveLock = new object();
        private string pendingSaveJson;
        private string pendingSaveQueuedAt;
        private ManualResetEventSlim pendingSaveDone;
        private volatile string pendingSaveError;
        private volatile bool savePending;
        private static bool wakeFailedLogged;

        /// <summary>当前正在运行的服务实例（域重载 / 退出 Unity 时用它关闭监听，避免端口泄漏）。</summary>
        internal static Psd2UIFormSettingsServer Current;

        public bool IsRunning => running;

        [InitializeOnLoadMethod]
        private static void RegisterShutdownHooks()
        {
            EditorApplication.quitting += ShutdownCurrent;
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownCurrent;
        }

        private static void ShutdownCurrent()
        {
            Psd2UIFormSettingsServer server = Current;
            Current = null;
            if (server != null)
            {
                server.Stop();
            }
        }

        public void Start(UGUIParser targetConfig, string htmlPath)
        {
            Stop();

            config = targetConfig;
            if ((Object)config == (Object)null)
            {
                Debug.LogError("[Psd2UIForm] 打开设置失败：没有拿到 Psd2UIFormConfig（UGUIParser）资源，请先选中该 asset。");
                return;
            }
            if (string.IsNullOrEmpty(htmlPath) || !File.Exists(htmlPath))
            {
                Debug.LogError("[Psd2UIForm] 打开设置失败：找不到设置页面文件 " + htmlPath);
                return;
            }

            string tempDir = Path.Combine(Application.temporaryCachePath, "Psd2UIFormSettings");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            configPath = Path.Combine(tempDir, "current-config.json");

            // 先在主线程建立配置快照：页面第一次 GET /config 就直接拿到它，不需要等主线程
            RefreshCache(true);

            // 顺手把"改了但没落盘"的改动写进 asset（不脏就什么都不做）。
            // 否则 Unity 内存里的值（= 页面显示的值）和磁盘 .asset 会一直是两套，
            // 用文本编辑器打开 .asset 对比时就会觉得"两边不一致"。
            try
            {
                AssetDatabase.SaveAssetIfDirty(config);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Psd2UIForm] 落盘未保存的配置改动失败: " + e.Message);
            }

            pageHtml = BuildPageHtml(htmlPath);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "settings.html"), pageHtml, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Psd2UIForm] 写出临时设置页面失败: " + e.Message);
            }

            if (!StartHttpServer())
            {
                EditorUtility.DisplayDialog("错误",
                    "设置页面服务启动失败。\n端口 " + Port + " 可能已被其它程序占用，请先关闭占用该端口的程序再重试。", "确定");
                return;
            }

            Current = this;

            // 主线程心跳：定期把 asset 的最新值刷进缓存（在 Inspector 里改了值，页面刷新也能看到）
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            nextCacheRefreshTime = EditorApplication.timeSinceStartup + CacheRefreshIntervalSeconds;

            Application.OpenURL(BaseUrl + "/");
            Debug.Log("[Psd2UIForm] 设置页面已打开: " + BaseUrl + "/  配置来源: " + AssetDatabase.GetAssetPath(config));
        }

        public void Stop()
        {
            // 关掉服务前先把排队中的保存落盘。Stop() 走的都是主线程路径
            // （域重载 / 退出 Unity / 重新打开设置页），所以这里能安全地碰 AssetDatabase。
            // 这样即使编辑器一直没 tick，数据也不会丢。
            try
            {
                ApplyPendingSave();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Psd2UIForm] 关闭前落盘待保存配置失败: " + e.Message);
            }

            running = false;

            // 主线程心跳只在编辑器主线程有效，换个线程访问会抛异常，这里静默兜底
            try
            {
                EditorApplication.update -= OnEditorUpdate;
            }
            catch
            {
            }

            if (listener != null)
            {
                try
                {
                    if (listener.IsListening)
                    {
                        listener.Stop();
                    }
                }
                catch
                {
                }
                try
                {
                    listener.Close();
                }
                catch
                {
                }
                listener = null;
            }

            // 后台线程会在 GetContext() 抛异常后自行结束（IsBackground = true，不阻塞域重载）
            listenerThread = null;
            pageHtml = null;

            if (Current == this)
            {
                Current = null;
            }
        }

        // ------------------------------------------------------------------ HTTP

        private bool StartHttpServer()
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(BaseUrl + "/");
                listener.Start();
            }
            catch (Exception e)
            {
                Debug.LogError("[Psd2UIForm] 启动设置服务器失败: " + e.Message);
                if (listener != null)
                {
                    try
                    {
                        listener.Close();
                    }
                    catch
                    {
                    }
                    listener = null;
                }
                running = false;
                return false;
            }

            running = true;
            listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Psd2UIFormSettingsServer"
            };
            listenerThread.Start();
            return true;
        }

        private void ListenLoop()
        {
            while (running)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception e)
                {
                    // Stop() 会让 GetContext() 抛错，属于正常退出，不报错
                    if (running)
                    {
                        Debug.LogError("[Psd2UIForm] HTTP 监听中断: " + e.Message);
                    }
                    break;
                }

                try
                {
                    HandleRequest(context);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Psd2UIForm] HTTP 错误: " + e.Message);
                    TryCloseQuietly(context);
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            // 允许从 file:// 页面访问（若用户手动双击临时目录里的 settings.html）
            response.AddHeader("Access-Control-Allow-Private-Network", "true");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            string path = request.Url == null ? string.Empty : request.Url.AbsolutePath;

            // 任何请求都续一次保活：页面还开着就说明用户在用，这段时间让编辑器保持 tick，
            // 否则玩家 loop 一停，排队的主线程工作（保存落盘）就没人消费了。
            Interlocked.Exchange(ref keepAliveUntilTicks, DateTime.UtcNow.AddSeconds(KeepAliveSeconds).Ticks);

            // 页面本身：和 /config、/save 同源，彻底避免 file:// 的跨域/私网限制
            if (path == "/" || path == "/index.html")
            {
                WriteText(response, "text/html; charset=utf-8", pageHtml ?? string.Empty);
                return;
            }

            if (path == "/config" && request.HttpMethod == "GET")
            {
                // 只读缓存，不碰主线程：主线程被编译 / 域重载占住时也能正常应答，
                // 不会再出现"请求超时 → 页面退回表单默认值 → 看起来和 Unity 不一致"
                string json = cachedConfigJson;
                string source = "cache";

                if (string.IsNullOrEmpty(json))
                {
                    // 快照还没建立（例如刚 reload 完就被请求）：退回上一次导出的快照文件
                    try
                    {
                        if (File.Exists(configPath))
                        {
                            json = File.ReadAllText(configPath);
                            source = "file";
                        }
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrEmpty(json))
                {
                    response.StatusCode = 503;
                    WriteText(response, "text/plain; charset=utf-8",
                        "config not ready: Unity 还没建立起配置快照（通常刚经历脚本重编译），请稍后刷新页面。");
                    return;
                }

                response.AddHeader("X-Psd2UIForm-Config-Source", source);
                WriteText(response, "application/json; charset=utf-8", json);
                return;
            }

            if (path == "/save" && request.HttpMethod == "POST")
            {
                string body;
                using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                {
                    body = reader.ReadToEnd();
                }

                bool applied;
                string error = SubmitSave(body, out applied);

                if (error != null)
                {
                    WriteText(response, "application/json; charset=utf-8",
                        "{\"success\":false,\"applied\":false,\"pending\":false,\"error\":\"" + EscapeJson(error) + "\"}");
                }
                else if (applied)
                {
                    WriteText(response, "application/json; charset=utf-8",
                        "{\"success\":true,\"applied\":true,\"pending\":false}");
                }
                else
                {
                    // 值已经收下了，只是 Unity 还在后台没轮到落盘 —— 不是失败
                    WriteText(response, "application/json; charset=utf-8",
                        "{\"success\":true,\"applied\":false,\"pending\":true}");
                }
                return;
            }

            if (path == "/save-status" && request.HttpMethod == "GET")
            {
                // 页面拿不到 applied=true 时轮询它，确认最终有没有写进 asset
                WriteText(response, "application/json; charset=utf-8",
                    "{\"pending\":" + (savePending ? "true" : "false")
                    + ",\"error\":\"" + EscapeJson(pendingSaveError) + "\"}");
                return;
            }

            response.StatusCode = 404;
            WriteText(response, "text/plain; charset=utf-8", "not found: " + path);
        }

        private static void WriteText(HttpListenerResponse response, string contentType, string body)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(body ?? string.Empty);
            response.ContentType = contentType;
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private static void TryCloseQuietly(HttpListenerContext context)
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
            }
        }

        /// <summary>
        /// HTTP 线程调用：提交一次保存。
        ///
        /// 拆成两步，响应绝不依赖主线程可用性：
        ///   1. HTTP 线程做能独立完成的事：校验、更新内存快照（页面立刻能读回新值）、入队、唤醒编辑器；
        ///   2. 主线程被唤醒后写进 asset。最多等 SaveAckWaitSeconds 秒确认，
        ///      等不到就先回"已入队"（applied=false / pending=true），数据仍在队列里，稍后一定落盘。
        ///
        /// 这样就不会再出现"点了保存要等点一下 Unity 才成功"—— 因为 Unity 编辑器失去焦点时
        /// 主循环被节流，delayCall 是靠主循环消费的，死等它等于把浏览器卡死。
        /// </summary>
        private string SubmitSave(string body, out bool applied)
        {
            applied = false;

            if (string.IsNullOrWhiteSpace(body))
            {
                return "请求体为空";
            }
            string trimmed = body.TrimStart();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                return "请求体不是合法的配置 JSON";
            }
            if (!trimmed.Contains("defaultTextType") || !trimmed.Contains("sharedAssetsOutput"))
            {
                return "配置 JSON 缺少必要字段（可能不是设置页面提交的内容）";
            }

            string queuedAt = Timestamp();

            // 1) 先让页面立刻读回刚提交的值（GET /config 读的就是这份快照）
            cachedConfigJson = body;

            ManualResetEventSlim done = new ManualResetEventSlim(false);
            lock (saveLock)
            {
                pendingSaveJson = body;
                pendingSaveQueuedAt = queuedAt;
                pendingSaveDone = done;
                pendingSaveError = null;
                savePending = true;
            }

            // 2) 唤醒编辑器：失去焦点时 player loop 不会自己跑，队列就没人消费
            WakeEditor();

            // 3) 最多等一小会儿确认；等不到也不影响正确性，只是响应里标成"排队中"
            bool completed = done.Wait(TimeSpan.FromSeconds(SaveAckWaitSeconds));
            if (!completed)
            {
                Debug.Log("[Psd2UIForm] 保存已入队（" + queuedAt + "），主线程暂未执行落盘："
                    + "编辑器在后台被节流了。值已存进内存快照，编辑器一有更新就会写入 asset。");
                return null;
            }

            string error = pendingSaveError;
            applied = error == null;
            return error;
        }

        /// <summary>
        /// 主线程执行：把排队中的保存真正写进 asset。
        /// 由 EditorApplication.update 驱动；域重载 / 退出 Unity / Stop() 时也会兜底调一次，
        /// 保证"只要提交过就一定落盘"。
        /// </summary>
        private void ApplyPendingSave()
        {
            string json;
            string queuedAt;
            ManualResetEventSlim done;

            lock (saveLock)
            {
                json = pendingSaveJson;
                if (json == null)
                {
                    return;
                }
                queuedAt = pendingSaveQueuedAt;
                done = pendingSaveDone;
                pendingSaveJson = null;
                pendingSaveDone = null;
            }

            string error = ApplyConfig(json);

            lock (saveLock)
            {
                pendingSaveError = error;
                savePending = false;
            }

            Debug.Log("[Psd2UIForm] 保存" + (error == null ? "已写入 asset" : "失败")
                + " " + Timestamp() + "（入队于 " + queuedAt + "）"
                + (error == null ? string.Empty : "：" + error));

            if (done != null)
            {
                done.Set();
            }
        }

        /// <summary>
        /// 请求编辑器跑一次更新。编辑器失去焦点时 player loop 会被节流，
        /// 而 EditorApplication.delayCall / update 都靠它消费 —— 不主动请求，
        /// 排队的回调就会一直躺着，直到用户点回 Unity。
        /// </summary>
        private static void WakeEditor()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception e)
            {
                // 后台线程调用失败只影响"立刻落盘"，数据仍在队列里，不影响正确性
                if (!wakeFailedLogged)
                {
                    wakeFailedLogged = true;
                    Debug.LogWarning("[Psd2UIForm] 请求编辑器更新失败（保存会在编辑器下次更新时落盘）: " + e.Message);
                }
            }
        }

        private static string Timestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        // ------------------------------------------------------------------ 页面注入

        private string BuildPageHtml(string htmlPath)
        {
            string html = File.ReadAllText(htmlPath);

            string script =
                "\n    <!-- 由 Psd2UIFormSettingsServer 注入：配置来源与保存地址 -->\n" +
                "    <script>\n" +
                "        window.saveUrl = '" + BaseUrl + "/save';\n" +
                "        window.saveStatusUrl = '" + BaseUrl + "/save-status';\n" +
                "        window.__psd2uiformLoaded = false;\n" +
                "        (function () {\n" +
                "            function fail(msg) {\n" +
                "                window.__psd2uiformLoadError = msg;\n" +
                "                console.error('[PSD2UIForm] 读取 Unity 配置失败: ' + msg);\n" +
                "                if (!document.body) { return; }\n" +
                "                var bar = document.getElementById('psd2uiform-load-error');\n" +
                "                if (!bar) {\n" +
                "                    bar = document.createElement('div');\n" +
                "                    bar.id = 'psd2uiform-load-error';\n" +
                "                    bar.style.cssText = 'position:sticky;top:0;z-index:9999;margin:0 0 16px;padding:10px 16px;" +
                "border-radius:10px;background:#b91c1c;color:#fff;font:14px/1.6 Inter,sans-serif;text-align:center';\n" +
                "                    document.body.insertBefore(bar, document.body.firstChild);\n" +
                "                }\n" +
                "                bar.textContent = '⚠️ 没能从 Unity 读到配置（' + msg + '）。加载成功前请不要点「保存设置」，' +\n" +
                "                    '否则会把下面显示的表单默认值写回 asset。请稍后刷新页面，或在 Unity 里重新点一次「⚙️ 打开设置」。';\n" +
                "            }\n" +
                // 加载失败自动重试：Unity 编译脚本 / 域重载期间主线程会短暂不可用，
                // 一次失败就退回表单默认值，是「刷新时不一致、点一下 Unity 又一致」的直接原因。
                "            function load(attempt) {\n" +
                "                attempt = attempt || 0;\n" +
                "                fetch('" + BaseUrl + "/config', { cache: 'no-store' })\n" +
                "                    .then(function (r) { if (!r.ok) { throw new Error('HTTP ' + r.status); } return r.json(); })\n" +
                "                    .then(function (cfg) {\n" +
                "                        loadSettings(cfg);\n" +
                "                        window.__psd2uiformLoaded = true;\n" +
                "                        window.__psd2uiformLoadError = null;\n" +
                "                        var bar = document.getElementById('psd2uiform-load-error');\n" +
                "                        if (bar && bar.parentNode) { bar.parentNode.removeChild(bar); }\n" +
                "                        console.log('[PSD2UIForm] 已从 Unity 读取配置:', cfg);\n" +
                "                    })\n" +
                "                    .catch(function (e) {\n" +
                "                        var msg = e && e.message ? e.message : String(e);\n" +
                "                        if (attempt < 5) {\n" +
                "                            var wait = 300 * Math.pow(2, attempt);\n" +
                "                            console.warn('[PSD2UIForm] 读取失败，' + wait + 'ms 后重试(' + (attempt + 1) + '/5): ' + msg);\n" +
                "                            setTimeout(function () { load(attempt + 1); }, wait);\n" +
                "                            return;\n" +
                "                        }\n" +
                "                        fail(msg);\n" +
                "                    });\n" +
                "            }\n" +
                "            if (document.readyState === 'loading') {\n" +
                "                document.addEventListener('DOMContentLoaded', load);\n" +
                "            } else {\n" +
                "                load();\n" +
                "            }\n" +
                "        })();\n" +
                "    </script>\n";

            const string marker = "</body>";
            int index = html.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return html + script;
            }
            return html.Substring(0, index) + script + html.Substring(index);
        }

        // ------------------------------------------------------------------ 读写配置

        /// <summary>
        /// 主线程心跳，干三件事：
        ///  1. 排队中的保存立刻落盘（不等 0.5s 心跳，主线程来了就写）；
        ///  2. 保活期内主动请求 player loop 更新——编辑器失去焦点时它会被节流，
        ///     而排队的工作正是靠它消费，不续上就会一直等到用户点回 Unity；
        ///  3. 定期把 asset 的最新值刷进缓存（在 Inspector 里改了值，页面刷新也能看到）。
        /// </summary>
        private void OnEditorUpdate()
        {
            if (!running)
            {
                return;
            }

            if (savePending)
            {
                ApplyPendingSave();
            }

            if (DateTime.UtcNow.Ticks < Volatile.Read(ref keepAliveUntilTicks))
            {
                WakeEditor();
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextCacheRefreshTime)
            {
                return;
            }
            nextCacheRefreshTime = now + CacheRefreshIntervalSeconds;

            RefreshCache(false);
        }

        /// <summary>
        /// 主线程调用：读取 asset 生成 JSON 快照放进缓存；内容有变化时同步写一份到 configPath（诊断用）。
        /// 必须在主线程执行（SerializedObject / ScriptableSingleton / AssetDatabase 的限制）。
        /// </summary>
        private void RefreshCache(bool forceLog, bool ignorePendingSave = false)
        {
            if ((Object)config == (Object)null)
            {
                return;
            }

            // 有排队中的保存时先别刷：这一刻 asset 里还是旧值，刷下去会把刚提交的新值
            // 从快照里冲掉，页面一刷新就又"不一致"了。保存落盘时用 ignorePendingSave=true 刷新。
            if (savePending && !ignorePendingSave)
            {
                return;
            }

            try
            {
                string json = BuildConfigJson();
                lastGoodConfigJson = json;
                bool changed = json != cachedConfigJson;
                cachedConfigJson = json;

                if (changed || forceLog)
                {
                    try
                    {
                        File.WriteAllText(configPath, json, new UTF8Encoding(false));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Psd2UIForm] 写出配置快照失败: " + e.Message);
                    }
                }

                if (forceLog)
                {
                    Debug.Log("[Psd2UIForm] 配置快照已建立，来源 " + AssetDatabase.GetAssetPath(config) + ":\n" + json);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Psd2UIForm] 读取配置失败: " + e.Message);
            }
        }

        private string BuildConfigJson()
        {
            SerializedObject serializedObject = new SerializedObject(config);
            serializedObject.Update();

            Psd2UIFormSettings settings = ScriptableSingleton<Psd2UIFormSettings>.Instance;

            ConfigData data = new ConfigData
            {
                autoCropNineSlice = settings != null && settings.AutoCropMinimalNineSlice,
                defaultTextType = ReadInt(serializedObject, "defaultTextType", (int)GUIType.TMPText),
                defaultImageType = ReadInt(serializedObject, "defaultImageType", (int)GUIType.Image),
                defaultButtonComponentTypeName = serializedObject.FindProperty("defaultButtonComponentTypeName").stringValue,
                forceUseTMP = ReadBool(serializedObject, "forceUseTMP", true),
                sharedAssetsOutput = ReadString(serializedObject, "sharedAssetsOutput", string.Empty),
                sharedPrefabOutput = ReadString(serializedObject, "sharedPrefabOutput", string.Empty),
                convertZh2En = ReadBool(serializedObject, "convertZh2En", false),
                readmeDoc = ReadString(serializedObject, "readmeDoc", string.Empty),
                aiProvider = ReadAiProviderKind(serializedObject.FindProperty("aiProviderConfig")),
                showCliWindow = ReadAiProviderShowCliWindow(serializedObject.FindProperty("aiProviderConfig")),
                codexUseCustomApi = ReadAiConnectionUseCustomApi(serializedObject.FindProperty("aiProviderConfig"), "codexConnection"),
                codexApiUrl = ReadAiConnectionUrl(serializedObject.FindProperty("aiProviderConfig"), "codexConnection"),
                claudeUseCustomApi = ReadAiConnectionUseCustomApi(serializedObject.FindProperty("aiProviderConfig"), "claudeConnection"),
                claudeApiUrl = ReadAiConnectionUrl(serializedObject.FindProperty("aiProviderConfig"), "claudeConnection"),
                codexApiKeyConfigured = AiProviderSecretStore.HasKey(AiProviderKind.CodexCli),
                claudeApiKeyConfigured = AiProviderSecretStore.HasKey(AiProviderKind.ClaudeCodeCli)
            };

            return JsonUtility.ToJson(data, true);
        }

        /// <summary>把页面 POST 上来的配置写回 asset，并立刻落盘。返回 null 表示成功，否则是错误信息。</summary>
        private string ApplyConfig(string json)
        {
            try
            {
                ConfigData data = JsonUtility.FromJson<ConfigData>(json);
                if (data == null)
                {
                    return "配置 JSON 解析失败";
                }

                SerializedObject serializedObject = new SerializedObject(config);
                serializedObject.Update();
                WriteInt(serializedObject, "defaultTextType", data.defaultTextType);
                WriteInt(serializedObject, "defaultImageType", data.defaultImageType);
                if (data.defaultButtonComponentTypeName != null)
                    WriteString(serializedObject, "defaultButtonComponentTypeName", data.defaultButtonComponentTypeName);
                WriteBool(serializedObject, "forceUseTMP", data.forceUseTMP);
                WriteString(serializedObject, "sharedAssetsOutput", data.sharedAssetsOutput);
                WriteString(serializedObject, "sharedPrefabOutput", data.sharedPrefabOutput);
                WriteBool(serializedObject, "convertZh2En", data.convertZh2En);
                WriteString(serializedObject, "readmeDoc", data.readmeDoc);
                WriteAiProviderKind(serializedObject.FindProperty("aiProviderConfig"), data.aiProvider);
                WriteAiProviderShowCliWindow(serializedObject.FindProperty("aiProviderConfig"), data.showCliWindow);
                WriteAiConnection(serializedObject.FindProperty("aiProviderConfig"), "codexConnection", data.codexUseCustomApi, data.codexApiUrl);
                WriteAiConnection(serializedObject.FindProperty("aiProviderConfig"), "claudeConnection", data.claudeUseCustomApi, data.claudeApiUrl);
                AiProviderKind activeProvider = data.aiProvider == (int)AiProviderKind.ClaudeCodeCli ? AiProviderKind.ClaudeCodeCli : AiProviderKind.CodexCli;
                if (data.clearApiKey)
                    AiProviderSecretStore.Clear(activeProvider);
                else if (!string.IsNullOrWhiteSpace(data.apiKey))
                    AiProviderSecretStore.Save(activeProvider, data.apiKey);
                serializedObject.ApplyModifiedProperties();

                // 立刻落盘：只改内存不落盘，就是"页面/Inspector 一套值、.asset 文件另一套值"的成因
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                AssetDatabase.SaveAssets();

                // 自动裁剪九宫格存在 ProjectSettings/Psd2UIFormSettings.asset：
                // 它不是 AssetDatabase 资源，必须显式 SaveInstance()，否则重启 Unity 就丢
                Psd2UIFormSettings settings = ScriptableSingleton<Psd2UIFormSettings>.Instance;
                if (settings != null)
                {
                    settings.AutoCropMinimalNineSlice = data.autoCropNineSlice;
                    ScriptableSingleton<Psd2UIFormSettings>.SaveInstance();
                }

                // 保存后立刻重建快照：页面下一次 GET /config 拿到的就是刚落盘的这组值。
                // ignorePendingSave=true 是因为此刻 pendingSaveJson 还没被清掉（清在 ApplyPendingSave 里），
                // 不加这个开关这次刷新会被"有排队保存就别刷"的保护挡掉。
                RefreshCache(false, true);

                Debug.Log("[Psd2UIForm] ✅ 配置已保存到 " + AssetDatabase.GetAssetPath(config)
                    + "（自动裁剪九宫格 -> ProjectSettings/Psd2UIFormSettings.asset）");
                return null;
            }
            catch (Exception e)
            {
                // 落盘失败：把快照回滚成 asset 里的真实值，
                // 否则页面会一直显示一组其实没写进去的值（又变成"两边不一致"）
                cachedConfigJson = lastGoodConfigJson;
                Debug.LogError("[Psd2UIForm] 保存配置失败: " + e);
                return e.Message;
            }
        }

        private static SerializedProperty FindProperty(SerializedObject serializedObject, string name)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("[Psd2UIForm] 配置字段缺失（可能版本不匹配）: " + name);
            }
            return property;
        }

        private static int ReadInt(SerializedObject serializedObject, string name, int fallback)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            return property == null ? fallback : property.intValue;
        }

        private static bool ReadBool(SerializedObject serializedObject, string name, bool fallback)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            return property == null ? fallback : property.boolValue;
        }

        private static string ReadString(SerializedObject serializedObject, string name, string fallback)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            return property == null ? fallback : property.stringValue;
        }

        private static void WriteInt(SerializedObject serializedObject, string name, int value)
        {
            SerializedProperty property = FindProperty(serializedObject, name);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void WriteBool(SerializedObject serializedObject, string name, bool value)
        {
            SerializedProperty property = FindProperty(serializedObject, name);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void WriteString(SerializedObject serializedObject, string name, string value)
        {
            SerializedProperty property = FindProperty(serializedObject, name);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static int ReadAiProviderKind(SerializedProperty configProperty)
        {
            SerializedProperty provider = configProperty == null ? null : configProperty.FindPropertyRelative("provider");
            return provider != null && provider.intValue == (int)AiProviderKind.ClaudeCodeCli
                ? (int)AiProviderKind.ClaudeCodeCli
                : (int)AiProviderKind.CodexCli;
        }

        private static bool ReadAiProviderShowCliWindow(SerializedProperty configProperty)
        {
            SerializedProperty showCliWindow = configProperty == null ? null : configProperty.FindPropertyRelative("showCliWindow");
            return showCliWindow == null || showCliWindow.boolValue;
        }

        private static void WriteAiProviderKind(SerializedProperty configProperty, int providerKind)
        {
            SerializedProperty provider = configProperty == null ? null : configProperty.FindPropertyRelative("provider");
            if (provider != null)
            {
                provider.intValue = providerKind == (int)AiProviderKind.ClaudeCodeCli
                    ? (int)AiProviderKind.ClaudeCodeCli
                    : (int)AiProviderKind.CodexCli;
            }
        }

        private static void WriteAiProviderShowCliWindow(SerializedProperty configProperty, bool value)
        {
            SerializedProperty showCliWindow = configProperty == null ? null : configProperty.FindPropertyRelative("showCliWindow");
            if (showCliWindow != null)
            {
                showCliWindow.boolValue = value;
            }
        }

        private static bool ReadAiConnectionUseCustomApi(SerializedProperty configProperty, string connectionName)
        {
            SerializedProperty connection = configProperty == null ? null : configProperty.FindPropertyRelative(connectionName);
            SerializedProperty useCustomApi = connection == null ? null : connection.FindPropertyRelative("useCustomApi");
            return useCustomApi != null && useCustomApi.boolValue;
        }

        private static string ReadAiConnectionUrl(SerializedProperty configProperty, string connectionName)
        {
            SerializedProperty connection = configProperty == null ? null : configProperty.FindPropertyRelative(connectionName);
            SerializedProperty url = connection == null ? null : connection.FindPropertyRelative("customApiUrl");
            return url == null ? string.Empty : url.stringValue;
        }

        private static void WriteAiConnection(SerializedProperty configProperty, string connectionName, bool useCustomApi, string apiUrl)
        {
            SerializedProperty connection = configProperty == null ? null : configProperty.FindPropertyRelative(connectionName);
            if (connection == null) return;

            SerializedProperty mode = connection.FindPropertyRelative("useCustomApi");
            SerializedProperty url = connection.FindPropertyRelative("customApiUrl");
            if (mode != null) mode.boolValue = useCustomApi;
            if (url != null) url.stringValue = apiUrl ?? string.Empty;
        }

        [Serializable]
        private class ConfigData
        {
            public bool autoCropNineSlice;
            public int defaultTextType;
            public int defaultImageType;
            public string defaultButtonComponentTypeName;
            public bool forceUseTMP;
            public string sharedAssetsOutput;
            public string sharedPrefabOutput;
            public bool convertZh2En;
            public string readmeDoc;
            public int aiProvider;
            public bool showCliWindow = true;
            public bool codexUseCustomApi;
            public string codexApiUrl;
            public bool claudeUseCustomApi;
            public string claudeApiUrl;
            public bool codexApiKeyConfigured;
            public bool claudeApiKeyConfigured;
            public string apiKey;
            public bool clearApiKey;
        }
    }
}
