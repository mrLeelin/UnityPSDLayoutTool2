namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 以本机网页代替 UIElements 窗口来编辑项目配置。
    /// 页面只负责显示与提交，所有读写都走 PsdLayoutProjectSettings 的配置 API，
    /// 不再直接操作 SerializedProperty —— 那样会让页面上没暴露的字段被默认值覆盖。
    /// </summary>
    internal sealed class PsdLayoutProjectSettingsWebServer
    {
        // 注意：PSD2UIForm 插件的设置页也占了 9527（Psd2UIFormSettingsServer.cs），
        // 两边都写死同一个端口时，谁后启动谁绑定失败。这里让开，改用 9528。
        private const int Port = 9528;
        private const string Url = "http://localhost:9528/";

        /// <summary>页面轮询间隔（秒）。之前每帧刷新会在编辑器里持续产生垃圾。</summary>
        private const double RefreshIntervalSeconds = 0.4d;

        private readonly object gate = new object();
        private HttpListener listener;
        private volatile bool running;
        private volatile string cachedJson;
        private string pendingJson;
        /// <summary>页面按钮触发的动作名。由监听线程排队、主线程 OnEditorUpdate 执行。</summary>
        private string pendingAction;
        /// <summary>页面请求组件类型候选、但候选表还没扫时的标志。同样由主线程消费。</summary>
        private bool typeScanRequested;
        private volatile string lastError = string.Empty;
        private PsdLayoutProjectSettings settings;
        private double nextRefresh;
        private string previewAddress = string.Empty;
        private double nextPreviewAddressRefresh;
        internal static PsdLayoutProjectSettingsWebServer Current { get; private set; }

        internal static void Open(PsdLayoutProjectSettings target)
        {
            if (Current != null) Current.Stop();
            var server = new PsdLayoutProjectSettingsWebServer();
            if (server.Start(target))
            {
                Current = server;
                Application.OpenURL(Url);
            }
        }

        private bool Start(PsdLayoutProjectSettings target)
        {
            settings = target;
            RefreshCache();
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(Url);
                listener.Start();
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "[PSDLayoutTool2] 网页设置服务启动失败：" + e.Message +
                    "。如果同时开着另一个 Unity 工程，它可能已经占用了 " + Port + " 端口。");
                Stop();
                return false;
            }

            running = true;
            var thread = new Thread(ListenLoop) { IsBackground = true, Name = "PsdLayoutSettingsWebServer" };
            thread.Start();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;
            return true;
        }

        private void Stop()
        {
            running = false;
            EditorApplication.update -= OnEditorUpdate;
            if (listener != null) { try { listener.Close(); } catch { } listener = null; }
            if (Current == this) Current = null;
        }

        private static void Shutdown() { if (Current != null) Current.Stop(); }

        private void OnEditorUpdate()
        {
            string json = null;
            string action = null;
            bool wantTypes;
            lock (gate)
            {
                json = pendingJson; pendingJson = null;
                action = pendingAction; pendingAction = null;
                wantTypes = typeScanRequested; typeScanRequested = false;
            }
            if (!string.IsNullOrEmpty(json)) ApplyJson(json);
            if (!string.IsNullOrEmpty(action)) RunAction(action);
            if (wantTypes && (imageTypeCandidates == null || buttonTypeCandidates == null))
            {
                /* 扫描程序集必须在主线程（监听线程只负责按要求置标志）。
                   失败时把缓存置成空表，免得每个编辑器 tick 都重试一次昂贵的扫描。 */
                try
                {
                    EnsureTypeCandidates();
                }
                catch (Exception e)
                {
                    Debug.LogError("[PSDLayoutTool2] 扫描本机组件类型失败：" + e);
                    imageTypeCandidates = new List<TypeCandidate>();
                    buttonTypeCandidates = new List<TypeCandidate>();
                }
            }

            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            RefreshCache();
        }

        private void RefreshCache()
        {
            if (settings == null) return;
            try
            {
                cachedJson = BuildConfigJson().ToString(Formatting.None);
            }
            catch (Exception e)
            {
                Debug.LogError("[PSDLayoutTool2] 读取项目配置失败：" + e.Message);
            }
        }

        private void ListenLoop()
        {
            while (running)
            {
                HttpListenerContext context = null;
                try
                {
                    context = listener.GetContext();
                    Handle(context);
                }
                catch (Exception e)
                {
                    if (!running) break;
                    /* 原先是裸 catch{}：Handle 一抛异常，响应既不写也不关，
                       浏览器端的 fetch 就无限等待，表现成「保存中…」永远不变，
                       而且 Unity 控制台里连一条线索都没有。现在记录并把错误回给页面。 */
                    Debug.LogError("[PSDLayoutTool2] 网页设置请求处理失败：" + e);
                    TryWriteError(context, e);
                }
            }
        }

        /// <summary>
        /// 请求处理中途抛异常时，也必须给客户端一个回应。
        /// 否则浏览器端的 fetch 会一直挂着（这正是「保存中…」卡死的成因）。
        /// </summary>
        private static void TryWriteError(HttpListenerContext context, Exception e)
        {
            if (context == null) return;
            try
            {
                HttpListenerResponse response = context.Response;
                response.StatusCode = 500;
                string text = new JObject
                {
                    ["ok"] = false,
                    ["error"] = e.GetType().Name + ": " + e.Message,
                }.ToString(Formatting.None);
                Write(response, text, "application/json; charset=utf-8");
            }
            catch
            {
                try { context.Response.Close(); } catch { }
            }
        }

        private void Handle(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
            string path = context.Request.Url == null ? "/" : context.Request.Url.AbsolutePath;
            if (context.Request.HttpMethod == "OPTIONS") { response.StatusCode = 204; response.Close(); return; }
            if (path == "/" || path == "/index.html") { Write(response, Page(), "text/html; charset=utf-8"); return; }
            if (path == "/config" && context.Request.HttpMethod == "GET")
            {
                Write(response, cachedJson ?? "{}", "application/json; charset=utf-8");
                return;
            }

            if (path == "/typesuggest" && context.Request.HttpMethod == "GET")
            {
                string rawQuery = context.Request.Url == null ? string.Empty : context.Request.Url.Query;
                string kind = ReadQueryParam(rawQuery, "kind");
                string text = ReadQueryParam(rawQuery, "q");
                string current = ReadQueryParam(rawQuery, "cur");
                List<TypeCandidate> candidates = kind == "button" ? buttonTypeCandidates : imageTypeCandidates;
                if (candidates == null)
                {
                    /* 候选表还没建好：让主线程去扫，这次先回「未就绪」，页面稍后自动重试。
                       绝不能在这里自己扫 —— Handle() 跑在监听线程上。 */
                    lock (gate) typeScanRequested = true;
                    Write(response, "{\"ok\":true,\"ready\":false,\"total\":0,\"items\":[]}",
                        "application/json; charset=utf-8");
                    return;
                }

                Write(response, BuildSuggestJson(candidates, text, current), "application/json; charset=utf-8");
                return;
            }

            if (path == "/save" && context.Request.HttpMethod == "POST")
            {
                string body;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    body = reader.ReadToEnd();
                }

                lock (gate) pendingJson = body;
                /* 千万不要在这里调用 Unity 编辑器 API。
                   Handle() 跑在后台监听线程上（见 ListenLoop），而那类 API 只能在主线程调用：
                   这一行原本是 EditorApplication.QueuePlayerLoopUpdate()，它必然抛异常，
                   异常又被 ListenLoop 的 catch 吞掉，响应既不写出也不关闭，
                   浏览器端就永远停在「保存中…」（已实测复现：POST /save 无响应，
                   但配置其实已经写入了）。
                   pendingJson 交给主线程的 OnEditorUpdate 消费即可：它每个编辑器 tick
                   都会读一次，下面 0.4 秒的刷新循环本身就依赖这个 tick 在跑。 */
                Write(response, "{\"ok\":true}", "application/json; charset=utf-8");
                return;
            }

            if (path.StartsWith("/action/", StringComparison.Ordinal) &&
                context.Request.HttpMethod == "POST")
            {
                string name = path.Substring("/action/".Length);
                lock (gate) pendingAction = name;
                /* 和 /save 同一条原则：动作必须在主线程执行才能在 Handle 里跑，
                   这里只负责排队，先回执，结果由页面轮询 /config 拿到。 */
                Write(response, "{\"ok\":true,\"queued\":true}", "application/json; charset=utf-8");
                return;
            }

            response.StatusCode = 404;
            response.Close();
        }

        /// <summary>
        /// 执行页面按钮触发的动作。只在主线程（OnEditorUpdate）调用，
        /// 因为 AssetDatabase / Application 这类 API 只允许在主线程使用。
        /// </summary>
        private void RunAction(string name)
        {
            try
            {
                switch (name)
                {
                    case "catalog-refresh":
                        PsdLayoutProjectSettingsEditor.PingCommonAssetCatalog(
                            PsdCommonAssetCatalog.CreateOrRefresh());
                        break;
                    case "preview-start":
                        PsdCommonAssetPreviewServer.Start(settings.ResolvePreviewServerPort());
                        break;
                    case "preview-stop":
                        PsdCommonAssetPreviewServer.Stop();
                        break;
                    case "preview-open":
                    {
                        string address = PsdCommonAssetPreviewServer.GetLocalAddress();
                        if (!string.IsNullOrEmpty(address))
                        {
                            Application.OpenURL(address);
                        }

                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PSDLayoutTool2] 执行网页操作失败（" + name + "）：" + e);
            }

            RefreshCache();
        }

        /// <summary>
        /// 预览服务的局域网地址。BuildConfigJson 每 0.4 秒跑一次，而 GetLocalAddress
        /// 会做一次 DNS 查询，不能每次都调；这里按 5 秒缓存，服务停止时立刻清空。
        /// </summary>
        private string ResolvePreviewAddress()
        {
            if (!PsdCommonAssetPreviewServer.IsRunning)
            {
                previewAddress = string.Empty;
                return string.Empty;
            }

            if (string.IsNullOrEmpty(previewAddress) ||
                EditorApplication.timeSinceStartup >= nextPreviewAddressRefresh)
            {
                nextPreviewAddressRefresh = EditorApplication.timeSinceStartup + 5d;
                try
                {
                    previewAddress = PsdCommonAssetPreviewServer.GetLocalAddress();
                }
                catch (Exception)
                {
                    previewAddress = string.Empty;
                }
            }

            return previewAddress;
        }

        /* ---------- 组件类型候选（供页面模糊查询） ----------
           页面上「Image 组件类型」「Button 组件类型」是自由文本，手打 UnityEngine.UI.Image
           这种全名很费劲。这里把本机程序集里可用的类型列出来，页面边打边查。

           线程模型：扫描程序集只能在主线程，所以监听线程发现候选表是 null 时只置
           typeScanRequested 标志，真正的扫描在 OnEditorUpdate 里做；扫描完成之后的
           过滤 / 排序是纯字符串运算，监听线程直接读缓存即可。 */

        /// <summary>一个可选的组件类型。扫描后只读。</summary>
        private sealed class TypeCandidate
        {
            internal TypeCandidate(string full, string shortName, string assembly, string scope)
            {
                Full = full;
                Short = shortName;
                Assembly = assembly;
                Scope = scope;
            }

            internal readonly string Full;
            internal readonly string Short;
            internal readonly string Assembly;
            internal readonly string Scope;
        }

        /// <summary>补全列表上限。给太多反而不好挑。</summary>
        private const int SuggestLimit = 40;

        private static volatile List<TypeCandidate> imageTypeCandidates;
        private static volatile List<TypeCandidate> buttonTypeCandidates;

        /// <summary>
        /// 扫描当前已加载的全部程序集，收集 Image 子类与全部 MonoBehaviour。
        /// 只在主线程调用。编辑器专用程序集里的类型挂不到运行时 Prefab 上，直接排除。
        /// </summary>
        private static void EnsureTypeCandidates()
        {
            if (imageTypeCandidates != null && buttonTypeCandidates != null) return;

            var images = new List<TypeCandidate>();
            var buttons = new List<TypeCandidate>();
            Type imageBase = typeof(UnityEngine.UI.Image);
            Type behaviourBase = typeof(MonoBehaviour);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Assembly assembly = assemblies[a];
                string assemblyName;
                try { assemblyName = assembly.GetName().Name; }
                catch (Exception) { continue; }
                if (assemblyName == null) continue;
                if (assemblyName.EndsWith(".Editor", StringComparison.Ordinal) ||
                    assemblyName.EndsWith("-Editor", StringComparison.Ordinal)) continue;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException partial) { types = partial.Types; }
                catch (Exception) { continue; }
                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !type.IsClass || type.IsAbstract) continue;
                    if (type.IsGenericTypeDefinition || type.ContainsGenericParameters) continue;
                    if (!behaviourBase.IsAssignableFrom(type)) continue;
                    string full = type.FullName;
                    if (string.IsNullOrEmpty(full)) continue;
                    string scope = ScopeOf(full, type.Name);
                    if (scope.StartsWith("UnityEditor", StringComparison.Ordinal)) continue;

                    var candidate = new TypeCandidate(full, type.Name, assemblyName, scope);
                    if (imageBase.IsAssignableFrom(type)) images.Add(candidate);
                    buttons.Add(candidate);
                }
            }

            images.Sort(CompareCandidates);
            buttons.Sort(CompareCandidates);
            imageTypeCandidates = images;
            buttonTypeCandidates = buttons;
        }

        private static int CompareCandidates(TypeCandidate x, TypeCandidate y)
        {
            int rank = ScopeRank(x.Scope).CompareTo(ScopeRank(y.Scope));
            return rank != 0 ? rank : string.CompareOrdinal(x.Full, y.Full);
        }

        /// <summary>排序偏好：Unity 自带 UI 类型最靠前，其次引擎类型，最后才是项目/第三方类型。</summary>
        private static int ScopeRank(string scope)
        {
            if (scope == "UnityEngine.UI") return 0;
            if (scope != null && scope.StartsWith("UnityEngine", StringComparison.Ordinal)) return 1;
            return 2;
        }

        /// <summary>完整类型名去掉类型名本身即命名空间（嵌套类型会带 +）。</summary>
        private static string ScopeOf(string full, string shortName)
        {
            if (string.IsNullOrEmpty(full) || string.IsNullOrEmpty(shortName)) return string.Empty;
            int cut = full.Length - shortName.Length - 1;
            return cut > 0 ? full.Substring(0, cut) : string.Empty;
        }

        /// <summary>把匹配结果拼成页面要的 JSON。纯字符串运算，监听线程可用。</summary>
        private static string BuildSuggestJson(List<TypeCandidate> candidates, string query, string current)
        {
            var root = new JObject();
            root["ready"] = true;
            root["total"] = candidates.Count;
            var items = new JArray();
            var hits = new List<TypeCandidate>();
            var scores = new List<int>();
            var qualities = new List<int>();
            string q = (query ?? string.Empty).Trim();
            string qLower = q.ToLowerInvariant();
            for (int i = 0; i < candidates.Count; i++)
            {
                int score = ScoreCandidate(q, candidates[i], current);
                if (score < 0) continue;
                hits.Add(candidates[i]);
                scores.Add(score);
                qualities.Add(MatchWeight(candidates[i].Full, qLower));
            }

            var order = new List<int>();
            for (int i = 0; i < hits.Count; i++) order.Add(i);
            /* 先看匹配强度，再看词首命中数（多的靠前），再看命名空间偏好
               （Unity 自带类型优先），然后才是名字长短，最后按字母序保证结果稳定。 */
            order.Sort(delegate (int x, int y)
            {
                if (scores[x] != scores[y]) return scores[x].CompareTo(scores[y]);
                if (qualities[x] != qualities[y]) return qualities[y].CompareTo(qualities[x]);
                int rank = ScopeRank(hits[x].Scope).CompareTo(ScopeRank(hits[y].Scope));
                if (rank != 0) return rank;
                if (hits[x].Full.Length != hits[y].Full.Length) return hits[x].Full.Length.CompareTo(hits[y].Full.Length);
                return string.CompareOrdinal(hits[x].Full, hits[y].Full);
            });

            int count = Math.Min(order.Count, SuggestLimit);
            for (int i = 0; i < count; i++)
            {
                TypeCandidate c = hits[order[i]];
                items.Add(new JObject
                {
                    ["value"] = c.Full,
                    ["name"] = c.Short,
                    ["scope"] = c.Scope,
                    ["assembly"] = c.Assembly,
                    ["marks"] = MatchMarks(c.Full, qLower),
                });
            }

            root["matched"] = hits.Count;
            root["items"] = items;
            return root.ToString(Formatting.None);
        }

        /// <summary>
        /// 模糊匹配打分：越小越靠前，-1 表示不匹配。空查询返回最低优先级，
        /// 这样页面聚焦空输入时也能直接列出候选（按命名空间偏好排序）。
        /// </summary>
        private static int ScoreCandidate(string query, TypeCandidate candidate, string current)
        {
            if (string.IsNullOrEmpty(query))
            {
                /* 空查询（页面聚焦时）把字段当前值顶到第一行：用户一眼看到现在用的是什么，
                   按回车就能确认，不用在几十行里找。其余候选并列最低优先级。 */
                return candidate.Full == current ? 0 : 7;
            }
            string q = query.ToLowerInvariant();
            string full = candidate.Full.ToLowerInvariant();
            string shortName = candidate.Short.ToLowerInvariant();
            if (full == q) return 0;
            if (shortName == q) return 1;
            if (shortName.StartsWith(q, StringComparison.Ordinal)) return 2;
            if (full.EndsWith("." + q, StringComparison.Ordinal)) return 3;
            if (shortName.IndexOf(q, StringComparison.Ordinal) >= 0) return 4;
            if (full.IndexOf(q, StringComparison.Ordinal) >= 0) return 5;
            if (IsSubsequence(full, q)) return 6;
            return -1;
        }

        /// <summary>query 的字符是否按顺序出现在 text 里（uib → UnityEngine.UI.Button）。</summary>
        private static bool IsSubsequence(string text, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            int i = 0;
            for (int j = 0; j < text.Length && i < query.Length; j++)
            {
                if (text[j] == query[i]) i++;
            }

            return i == query.Length;
        }

        /// <summary>
        /// 同分候选的排序质量分：按「匹配到的字符落在哪种词首」加权。
        /// 命名空间/类型的分段起点（点号后、开头）权重 2，驼峰或全大写缩写的词首权重 1，
        /// 词中间 0 分；整段查询都能落在某一档词首上时再加一个大分。
        ///
        /// 实测价值：uib 在 UnityEngine.UI.Button 上得 505（U 段首 + I 缩写 + B 段首），
        /// 在 MyGame.UI.MyButton 上只有 504（U 段首 + I + B 驼峰，b 只落在驼峰上），
        /// 于是 UI.Button 排前面 —— 这正是用户打 uib 时想要的第一个候选。
        /// </summary>
        private static int MatchWeight(string text, string queryLower)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(queryLower)) return 0;
            for (int floor = 2; floor >= 1; floor--)
            {
                int weight = WeightedMatch(text, queryLower, floor, null);
                if (weight >= 0) return weight + (floor == 2 ? 1000 : 500);
            }

            int loose = WeightedMatch(text, queryLower, 0, null);
            return loose < 0 ? 0 : loose;
        }

        /// <summary>匹配到的字符位置，交给页面高亮（uib 命中的是哪个 u / i / b）。</summary>
        private static JArray MatchMarks(string text, string queryLower)
        {
            var marks = new JArray();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(queryLower)) return marks;
            var positions = new List<int>();
            for (int floor = 2; floor >= 0; floor--)
            {
                positions.Clear();
                if (WeightedMatch(text, queryLower, floor, positions) < 0) continue;
                for (int k = 0; k < positions.Count; k++)
                {
                    int begin = positions[k];
                    int length = 1;
                    while (k + 1 < positions.Count && positions[k + 1] == positions[k] + 1)
                    {
                        length++;
                        k++;
                    }

                    marks.Add(new JArray(begin, length));
                }

                return marks;
            }

            return marks;
        }

        /// <summary>
        /// 只在权重 &gt;= floor 的位置上匹配：匹配不上返回 -1，匹配上返回累计权重。
        /// positions 非空时顺便把命中的下标记下来（供高亮用）。
        /// </summary>
        private static int WeightedMatch(string text, string queryLower, int floor, List<int> positions)
        {
            int total = 0;
            int i = 0;
            for (int j = 0; j < text.Length && i < queryLower.Length; j++)
            {
                if (char.ToLowerInvariant(text[j]) != queryLower[i]) continue;
                int weight = WordStartWeight(text, j);
                if (weight < floor) continue;
                total += weight;
                if (positions != null) positions.Add(j);
                i++;
            }

            return i == queryLower.Length ? total : -1;
        }

        /// <summary>0 = 词中间，1 = 驼峰 / 全大写缩写的词首，2 = 命名空间或类型的分段起点。</summary>
        private static int WordStartWeight(string text, int index)
        {
            if (index <= 0) return 2;
            char previous = text[index - 1];
            if (previous == '.' || previous == '_' || previous == '+') return 2;
            if (!char.IsUpper(text[index])) return 0;
            if (char.IsLower(previous)) return 1;
            /* 全大写缩写（UI、GUI、TMP）：连续大写里的最后一个算一个词首 */
            return index + 1 >= text.Length || !char.IsUpper(text[index + 1]) ? 1 : 0;
        }

        /// <summary>从 Url.Query 里取一个参数（Unity 里没有 System.Web 那套）。</summary>
        private static string ReadQueryParam(string query, string name)
        {
            if (string.IsNullOrEmpty(query)) return string.Empty;
            string[] parts = query.TrimStart('?').Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                if (!string.Equals(parts[i].Substring(0, eq), name, StringComparison.Ordinal)) continue;
                string raw = parts[i].Substring(eq + 1);
                try { return Uri.UnescapeDataString(raw.Replace("+", " ")); }
                catch (Exception) { return raw; }
            }

            return string.Empty;
        }

        private static void Write(HttpListenerResponse response, string text, string contentType)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            using (Stream stream = response.OutputStream) stream.Write(bytes, 0, bytes.Length);
        }

        private JObject BuildConfigJson()
        {
            var root = new JObject();

            PsdLayoutProjectUiComponentSnapshot ui = settings.ResolveUiComponentSettings();
            root["imageComponentTypeName"] = ui.imageComponentType.FullName;
            root["buttonComponentTypeName"] = ui.buttonComponentType.FullName;

            PsdLayoutProjectOutputSnapshot output = settings.ResolveOutputSettings();
            root["outputMode"] = (int)output.outputMode;
            root["outputFolderName"] = output.outputFolderName;
            root["atlasOutputPath"] = output.atlasOutputPath;
            root["textureOutputPath"] = output.textureOutputPath;
            root["prefabOutputPath"] = output.prefabOutputPath;

            PsdCommonAssetNamingSnapshot naming = settings.ResolveCommonAssetNaming();
            root["prefabPrefix"] = naming.prefabPrefix;
            root["texturePrefix"] = naming.texturePrefix;

            root["autoCropNineSlice"] = settings.ResolveNineSliceSettings().autoCropOnExport;

            PsdHierarchyAiSettingsSnapshot ai = settings.ResolveHierarchyAiSettings();
            root["aiProvider"] = (int)ai.provider;
            root["aiModel"] = ai.customModel;
            root["aiEffort"] = ai.reasoningEffort;
            root["aiEndpoint"] = ai.customEndpoint;
            root["aiHasApiKey"] = HasApiKey(ai.provider);

            var clis = new JArray();
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed = PsdHierarchyAiCliDiscovery.FindInstalled();
            for (int index = 0; index < installed.Count; index++)
            {
                var item = new JObject
                {
                    ["provider"] = (int)installed[index].provider,
                    ["name"] = installed[index].displayName,
                    ["modelHint"] = installed[index].defaultModelHint,
                    ["effortHint"] = installed[index].reasoningEffortHint,
                    // 模型名是「建议」：页面上做成可手填的下拉，所以仍要保留输入框。
                    ["modelSuggestions"] = new JArray(installed[index].modelSuggestions),
                    // 思考程度是封闭枚举，按 CLI 逐个给全，供下拉直接列选项。
                    ["effortLevels"] = new JArray(installed[index].reasoningEffortLevels),
                };
                // Pi 的模型目录每台机器不同（还支持 cc-switch 之类的自定义 provider），
                // 静态示例会和用户实际用的对不上，所以运行时读 ~/.pi/agent/ 覆盖。
                if (installed[index].provider == PsdHierarchyAiProvider.Pi &&
                    PsdHierarchyAiPiCatalog.TryLoad(out string[] piModels, out string[] piLevels, out string piDefault))
                {
                    if (piModels.Length > 0)
                    {
                        item["modelSuggestions"] = new JArray(piModels);
                        item["modelHint"] = "例如 " + string.Join("、", piModels);
                    }

                    if (piLevels.Length > 0)
                    {
                        item["effortLevels"] = new JArray(piLevels);
                        item["effortHint"] = string.Join(" / ", piLevels);
                    }

                    // 页面用它显示「留空时实际会用哪个模型」，只作提示、不参与保存。
                    item["defaultModel"] = piDefault;
                }

                clis.Add(item);
            }

            root["availableClis"] = clis;
            // 配置里选的 CLI 现在没装时，页面上要能照实显示出来而不是掉回「不启用」。
            root["providerInstalled"] = !ai.isConfigured || ContainsProvider(installed, ai.provider);
            PsdHierarchyCleanupExecutionSettingsSnapshot cleanup =
                settings.ResolveHierarchyCleanupExecutionSettings();
            root["cleanupBackend"] = (int)cleanup.backend;

            // ---- 公共资源库：映射表规模 + 共享预览服务状态 ----
            PsdCommonAssetCatalog catalog = PsdCommonAssetCatalog.Load();
            root["catalogExists"] = catalog != null;
            root["catalogPrefabCount"] =
                catalog == null || catalog.prefabs == null ? 0 : catalog.prefabs.Count;
            root["catalogTextureCount"] =
                catalog == null || catalog.textures == null ? 0 : catalog.textures.Count;
            root["catalogNeedsRefresh"] = catalog != null && catalog.needsRefresh;

            root["previewServerPort"] = settings.ResolvePreviewServerPort();
            root["previewServerRunning"] = PsdCommonAssetPreviewServer.IsRunning;
            root["previewServerAddress"] = ResolvePreviewAddress();
            root["previewServerError"] = PsdCommonAssetPreviewServer.Error ?? string.Empty;

            root["lastError"] = lastError;
            return root;
        }

        private static bool ContainsProvider(
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed,
            PsdHierarchyAiProvider provider)
        {
            for (int index = 0; index < installed.Count; index++)
            {
                if (installed[index].provider == provider)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static bool HasApiKey(PsdHierarchyAiProvider provider)
        {
            if (provider == PsdHierarchyAiProvider.None)
            {
                return false;
            }

            try
            {
                return new PsdHierarchyAiSecretStore().HasApiKey(ProjectRoot(), provider);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 以当前配置为基底，只覆盖请求里真正出现的字段。
        /// 这样旧页面、部分提交或新增字段都不会把没提交的设置清空。
        /// </summary>
        private void ApplyJson(string json)
        {
            try
            {
                JObject data = JObject.Parse(json);
                var messages = new List<string>();

                // ---- UI 组件类型 ----
                if (HasAny(data, "imageComponentTypeName", "buttonComponentTypeName"))
                {
                    PsdLayoutProjectUiComponentSnapshot ui = settings.ResolveUiComponentSettings();
                    string imageType = ReadString(data, "imageComponentTypeName", ui.imageComponentType.FullName);
                    string buttonType = ReadString(data, "buttonComponentTypeName", ui.buttonComponentType.FullName);
                    if (!settings.TrySetUiComponentTypes(imageType, buttonType, out string uiError) &&
                        !string.IsNullOrEmpty(uiError))
                    {
                        messages.Add(uiError);
                    }
                }

                // ---- 输出与公共资源命名 ----
                if (HasAny(data, "outputMode", "outputFolderName", "atlasOutputPath", "textureOutputPath", "prefabOutputPath"))
                {
                    PsdLayoutProjectOutputSnapshot output = settings.ResolveOutputSettings();
                    settings.SetOutputSettings(
                        (PsdImporter.OutputDirectoryMode)ReadInt(data, "outputMode", (int)output.outputMode),
                        ReadString(data, "outputFolderName", output.outputFolderName),
                        output.fixedOutputPath,
                        output.prefabMode,
                        ReadString(data, "atlasOutputPath", output.atlasOutputPath),
                        ReadString(data, "textureOutputPath", output.textureOutputPath),
                        ReadString(data, "prefabOutputPath", output.prefabOutputPath),
                        output.spriteAtlasVersion);
                }

                if (HasAny(data, "prefabPrefix", "texturePrefix"))
                {
                    PsdCommonAssetNamingSnapshot naming = settings.ResolveCommonAssetNaming();
                    if (!settings.TrySetCommonAssetPrefixes(
                            ReadString(data, "prefabPrefix", naming.prefabPrefix),
                            ReadString(data, "texturePrefix", naming.texturePrefix),
                            out string namingError) &&
                        !string.IsNullOrEmpty(namingError))
                    {
                        messages.Add(namingError);
                    }
                }

                // ---- AI 层级整理 ----
                if (HasAny(data, "aiProvider", "aiModel", "aiEffort", "aiEndpoint"))
                {
                    PsdHierarchyAiSettingsSnapshot ai = settings.ResolveHierarchyAiSettings();
                    int providerValue = ReadInt(data, "aiProvider", (int)ai.provider);
                    if (providerValue == (int)PsdHierarchyAiProvider.None)
                    {
                        settings.ClearHierarchyAiSettings();
                    }
                    else if (!settings.TrySetHierarchyAiSettings(
                                 (PsdHierarchyAiProvider)providerValue,
                                 ReadString(data, "aiEndpoint", ai.customEndpoint),
                                 ReadString(data, "aiModel", ai.customModel),
                                 ReadString(data, "aiEffort", ai.reasoningEffort),
                                 out string aiError))
                    {
                        messages.Add(aiError);
                    }
                }

                // API Key：请求里带 aiApiKey 才动，空串表示清除。
                if (data["aiApiKey"] != null || data.Value<bool?>("aiClearApiKey") == true)
                {
                    PsdHierarchyAiSettingsSnapshot ai = settings.ResolveHierarchyAiSettings();
                    if (ai.provider != PsdHierarchyAiProvider.None)
                    {
                        var store = new PsdHierarchyAiSecretStore();
                        string key = data.Value<string>("aiApiKey") ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            store.ClearApiKey(ProjectRoot(), ai.provider);
                        }
                        else
                        {
                            store.SaveApiKey(ProjectRoot(), ai.provider, key);
                        }
                    }
                }

                // ---- Prefab 清理执行后端 ----
                if (HasAny(data, "cleanupBackend"))
                {
                    PsdHierarchyCleanupExecutionSettingsSnapshot cleanup =
                        settings.ResolveHierarchyCleanupExecutionSettings();
                    var cleanupBackend = (PsdHierarchyCleanupExecutionBackend)ReadInt(
                        data, "cleanupBackend", (int)cleanup.backend);

                    // 先校验再写：SetHierarchyCleanupExecutionBackend 碰到未定义的枚举值会抛
                    // ArgumentException，那会被下面的 catch 吞掉，导致同一次请求里其它字段
                    // 也一起不生效（而且只显示一句看不懂的异常）。
                    var cleanupCandidate = new PsdHierarchyCleanupExecutionSettingsSnapshot(cleanupBackend);
                    if (!cleanupCandidate.TryValidate(out string cleanupError))
                    {
                        messages.Add(cleanupError);
                    }
                    else
                    {
                        settings.SetHierarchyCleanupExecutionBackend(cleanupBackend);
                    }
                }

                // ---- 共享预览服务端口 ----
                if (HasAny(data, "previewServerPort"))
                {
                    // 注意：TrySetPreviewServerPort 在「值没变」时也返回 false，但 error 是空的；
                    // 只有端口越界才会带 error。所以这里判的是 error，不是返回值。
                    if (!settings.TrySetPreviewServerPort(
                            ReadInt(data, "previewServerPort", settings.ResolvePreviewServerPort()),
                            out string portError) &&
                        !string.IsNullOrEmpty(portError))
                    {
                        messages.Add(portError);
                    }
                }

                // ---- 九宫格：导出时是否自动裁剪 ----
                if (data["autoCropNineSlice"] != null)
                {
                    settings.SetNineSliceAutoCrop(data.Value<bool>("autoCropNineSlice"));
                }

                lastError = messages.Count == 0 ? string.Empty : string.Join("\n", messages);
                if (messages.Count > 0)
                {
                    Debug.LogWarning("[PSDLayoutTool2] 网页设置未全部生效：" + lastError);
                }
            }
            catch (Exception e)
            {
                lastError = e.Message;
                Debug.LogError("[PSDLayoutTool2] 网页设置保存失败：" + e.Message);
            }

            RefreshCache();
        }

        private static bool HasAny(JObject data, params string[] keys)
        {
            for (int index = 0; index < keys.Length; index++)
            {
                if (data[keys[index]] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadString(JObject data, string key, string fallback)
        {
            JToken token = data[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                return fallback ?? string.Empty;
            }

            return token.Value<string>() ?? string.Empty;
        }

        private static int ReadInt(JObject data, string key, int fallback)
        {
            JToken token = data[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                return fallback;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            // 页面上 <select> 的 value 是字符串，这里容错解析，避免整段保存失败。
            return int.TryParse(token.Value<string>(), out int parsed) ? parsed : fallback;
        }

        // 页面 HTML 用 @"..." 原样字符串承载，因此内部一律用单引号，避免出现需要转义的 ""。
        private static string Page() => @"<!doctype html>
<html lang='zh-CN'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>PSD Layout Tool 设置</title>
<link rel='preconnect' href='https://fonts.googleapis.com'>
<link rel='preconnect' href='https://fonts.gstatic.com' crossorigin>
<link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap' rel='stylesheet'>
<style>
*{margin:0;padding:0;box-sizing:border-box}
[hidden]{display:none!important}
:root{
--bg:#f5f7fa;--text:#1a1a1a;--muted:#6b7280;--muted-2:#4b5563;
--line:#e5e7eb;--line-soft:#f0f0f0;--field-bg:#f9fafb;
--blue:#3b82f6;--blue-dark:#2563eb;--teal:#14b8a6;--teal-dark:#0f766e;
--r:16px;--r-sm:12px;--r-xs:10px;
--shadow:0 1px 3px rgba(0,0,0,.08),0 1px 2px rgba(0,0,0,.04);
--shadow-hover:0 4px 12px rgba(0,0,0,.1),0 2px 4px rgba(0,0,0,.06);
}
body{font-family:'Inter',-apple-system,BlinkMacSystemFont,'Segoe UI','Microsoft YaHei',sans-serif;
background:var(--bg);color:var(--text);line-height:1.6;min-height:100vh}

/* ---------- 左侧导航栏 ---------- */
.sidebar{position:fixed;top:0;left:0;bottom:0;width:264px;z-index:20;
display:flex;flex-direction:column;gap:20px;padding:24px 18px;
background:#fff;border-right:1px solid var(--line)}
.brand{display:flex;align-items:center;gap:12px;padding:0 6px}
.brand-mark{width:48px;height:48px;flex:0 0 auto;display:grid;place-items:center;
font-size:26px;border-radius:var(--r-sm);
background:linear-gradient(135deg,#3b82f6 0%,#2563eb 100%);
box-shadow:0 4px 12px rgba(59,130,246,.28)}
.brand-name{display:block;font-size:15px;font-weight:700}
.brand-sub{display:block;font-size:13px;color:var(--muted);margin-top:2px}
.nav{display:flex;flex-direction:column;gap:6px;flex:1;min-height:0;overflow-y:auto}
.nav-cap{font-size:11px;font-weight:700;letter-spacing:1px;text-transform:uppercase;
color:var(--muted);padding:6px 12px 8px}
.nav-item{display:flex;align-items:center;gap:10px;padding:11px 14px;
border-radius:var(--r-xs);color:var(--muted-2);text-decoration:none;
font-size:14px;font-weight:600;white-space:nowrap;transition:all .2s}
.nav-item svg{width:16px;height:16px;flex:0 0 auto;opacity:.8}
.nav-item:hover{background:var(--field-bg);color:var(--text)}
.nav-item.is-on{color:#fff;background:linear-gradient(135deg,#3b82f6 0%,#2563eb 100%);
box-shadow:0 4px 12px rgba(59,130,246,.3)}
.nav-item.is-on svg{opacity:1}
.sidebar-foot{border-top:1px solid var(--line);padding-top:16px;
display:flex;flex-direction:column;gap:12px}
#status{display:flex;align-items:center;gap:8px;font-size:13px;color:var(--muted);
word-break:break-word}
#status::before{content:'';width:8px;height:8px;flex:0 0 auto;border-radius:50%;
background:#d1d5db;transition:background .2s}
#status.ok{color:#065f46}
#status.ok::before{background:#10b981}
#status.err{color:#b91c1c}
#status.err::before{background:#ef4444}

/* ---------- 按钮 ---------- */
.btn{border:none;border-radius:var(--r-sm);font:inherit;font-size:15px;font-weight:600;
cursor:pointer;transition:all .2s;box-shadow:0 1px 2px rgba(0,0,0,.05)}
.btn:hover{transform:translateY(-2px);box-shadow:0 4px 12px rgba(0,0,0,.15)}
.btn:active{transform:translateY(0)}
.btn-primary{width:100%;padding:14px 20px;color:#fff;
background:linear-gradient(135deg,#3b82f6 0%,#2563eb 100%)}
.btn-secondary{padding:11px 20px;font-size:14px;border-radius:var(--r-xs);
background:#fff;color:var(--muted);border:2px solid var(--line)}
.btn-secondary:hover{background:var(--field-bg);border-color:#d1d5db;transform:none;
box-shadow:0 1px 2px rgba(0,0,0,.05)}
.btn[disabled]{cursor:default;opacity:.85;transform:none;box-shadow:none}
.btn.is-busy{cursor:progress;transform:none;box-shadow:none;opacity:.85}
.btn.is-done{background:linear-gradient(135deg,#10b981 0%,#059669 100%)}
.btn.is-fail{background:linear-gradient(135deg,#ef4444 0%,#dc2626 100%)}

/* ---------- 保存结果浮层 ----------
   侧栏底部的状态文字在窄屏和视线之外都容易漏看，所以保存结果
   另外弹一个固定定位的浮层，任何布局下都在同一位置可见。 */
#toast{position:fixed;top:20px;right:20px;z-index:99;display:flex;align-items:center;
gap:10px;max-width:min(420px,calc(100vw - 40px));padding:13px 18px;
border-radius:12px;border:1px solid var(--line);background:#fff;color:var(--muted);
font-size:14px;font-weight:600;box-shadow:0 10px 30px rgba(15,23,42,.14);
opacity:0;transform:translateY(-10px);pointer-events:none;
transition:opacity .2s ease,transform .2s ease}
#toast.is-on{opacity:1;transform:translateY(0)}
#toast::before{content:'';width:9px;height:9px;flex:0 0 auto;border-radius:50%;
background:#d1d5db}
#toast.ok{border-color:#a7f3d0;color:#065f46;background:#f0fdf4}
#toast.ok::before{background:#10b981}

/* 连不上 Unity 设置服务时的常驻提示条。
   设置服务在 Unity 重新编译脚本（domain reload）时会被停掉，
   此时页面还在、服务已经没了，用户点保存只会失败且不明原因。 */
#offline{margin:0 0 20px;padding:15px 18px;border-radius:var(--r-sm);
border:1px solid #fed7aa;background:#fff7ed;color:#9a3412;font-size:14px;line-height:1.65}
#offline[hidden]{display:none}
#offline strong{display:block;margin-bottom:4px;font-size:15px}
#offline code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;
font-size:13px;background:#ffedd5;border-radius:4px;padding:1px 6px}
#toast.err{border-color:#fecaca;color:#b91c1c;background:#fef2f2}
#toast.err::before{background:#ef4444}

/* ---------- 内容区 ---------- */
.content{margin-left:264px;padding:40px 36px 80px}
.content-inner{max-width:920px;margin:0 auto}
.page-header{text-align:center;margin-bottom:40px}
.page-icon{font-size:48px;margin-bottom:16px}
.page-header h1{font-size:32px;font-weight:700;margin-bottom:8px}
.page-subtitle{font-size:16px;color:var(--muted)}

.card{background:#fff;border-radius:var(--r);padding:32px;margin-bottom:24px;
box-shadow:var(--shadow);transition:box-shadow .2s;scroll-margin-top:24px}
.card:hover{box-shadow:var(--shadow-hover)}
.card-header{display:flex;align-items:center;gap:12px;margin-bottom:24px;
padding-bottom:20px;border-bottom:2px solid var(--line-soft)}
.card-icon{width:48px;height:48px;flex:0 0 auto;display:flex;align-items:center;
justify-content:center;font-size:28px;border-radius:var(--r-sm)}
.card-icon.green{background:linear-gradient(135deg,#10b981 0%,#059669 100%)}
.card-icon.blue{background:linear-gradient(135deg,#3b82f6 0%,#2563eb 100%)}
.card-icon.orange{background:linear-gradient(135deg,#f59e0b 0%,#d97706 100%)}
.card-icon.violet{background:linear-gradient(135deg,#8b5cf6 0%,#6d28d9 100%)}
.card-icon.teal{background:linear-gradient(135deg,#14b8a6 0%,#0f766e 100%)}
.card-title{flex:1;min-width:0}
.card-title h2{font-size:20px;font-weight:600;margin-bottom:4px}
.card-title p{font-size:14px;color:var(--muted)}
.badge{padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;
letter-spacing:.5px;white-space:nowrap}
.badge-success{background:#d1fae5;color:#065f46}
.badge-warn{background:#fef3c7;color:#92400e}
.badge-idle{background:#e5e7eb;color:#4b5563}

/* ---------- 表单 ---------- */
.form-group{margin-bottom:24px}
.form-group:last-child{margin-bottom:0}
.form-label{display:block;font-size:14px;font-weight:600;margin-bottom:8px}
.form-help{display:block;font-size:13px;color:var(--muted);margin-top:6px}
.form-input,.form-select{width:100%;padding:12px 16px;font-family:inherit;font-size:14px;
color:var(--text);background:var(--field-bg);border:2px solid var(--line);
border-radius:var(--r-xs);transition:all .2s}
.form-input::placeholder{color:#9ca3af}
.form-input:hover,.form-select:hover{background:#fff;border-color:#d1d5db}
.form-input:focus,.form-select:focus{outline:none;background:#fff;
border-color:var(--blue);box-shadow:0 0 0 3px rgba(59,130,246,.1)}
.form-select{appearance:none;-webkit-appearance:none;-moz-appearance:none;
cursor:pointer;padding-right:40px}
.select-wrap{position:relative}
.select-wrap::after{content:'';position:absolute;right:16px;top:50%;width:8px;height:8px;
border-right:2px solid var(--muted);border-bottom:2px solid var(--muted);
transform:translateY(-70%) rotate(45deg);pointer-events:none}
.form-grid{display:grid;grid-template-columns:1fr 1fr;gap:16px}
/* ---------- 可手填下拉（模型名称 / 思考程度） ----------
   用 input + 候选面板而不是 form-select：模型名是开放集合，必须允许填列表外的值。
   面板用 absolute 贴在输入框下方；这一层不是滚动容器，不会被裁掉。 */
.combo{position:relative}
.combo .form-input{padding-right:52px}
.combo-toggle{position:absolute;right:6px;top:50%;transform:translateY(-50%);
width:36px;height:30px;display:flex;align-items:center;justify-content:center;
background:transparent;border:0;border-radius:6px;cursor:pointer;
font-size:15px;line-height:1;color:var(--muted);transition:background .15s,color .15s}
.combo-toggle:hover{background:var(--field-bg);color:var(--text)}
.combo-panel{position:absolute;left:0;right:0;top:calc(100% + 6px);z-index:400;
background:#fff;border:1px solid var(--line);border-radius:var(--r-sm);
box-shadow:0 12px 32px rgba(15,23,42,.18);padding:6px;
max-height:260px;overflow-y:auto}
.combo-panel[hidden]{display:none}
.combo-item{display:flex;align-items:center;gap:10px;padding:9px 11px;border-radius:8px;
cursor:pointer;font-size:14px;line-height:1.35}
.combo-item:hover,.combo-item.is-active{background:var(--field-bg);box-shadow:inset 0 0 0 1px #bfdbfe}
.combo-value{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.combo-value b{color:var(--blue-dark);font-weight:700}
.combo-note{padding:10px 12px;color:var(--muted);font-size:13px;line-height:1.5}
.combo-act{flex:0 0 auto;font-size:12px;color:var(--blue-dark);font-weight:600}
.combo-foot{margin-top:6px;padding:8px 11px 4px;border-top:1px solid var(--line);
color:var(--muted);font-size:12px;line-height:1.5}
/* ---------- 开关行 ---------- */
.switch-row{display:flex;align-items:center;gap:14px;padding:14px;border:1px solid var(--line);
border-radius:var(--r-sm);background:var(--field-bg)}
.switch-text{flex:1;min-width:0}
.switch-main{display:block;font-size:14px;font-weight:600;margin-bottom:4px}
.switch-sub{display:block;font-size:13px;color:var(--muted);line-height:1.5}
.switch{position:relative;flex:0 0 auto;width:52px;height:30px;display:inline-block}
.switch input{position:absolute;opacity:0;width:100%;height:100%;margin:0;cursor:pointer;z-index:2}
.switch-track{position:absolute;inset:0;background:#cbd5e1;border-radius:999px;
transition:background .18s ease}
.switch-track::after{content:'';position:absolute;top:3px;left:3px;width:24px;height:24px;
border-radius:50%;background:#fff;box-shadow:0 1px 3px rgba(15,23,42,.3);
transition:transform .18s ease}
.switch input:checked + .switch-track{background:#2563eb}
.switch input:checked + .switch-track::after{transform:translateX(22px)}
.switch input:focus-visible + .switch-track{box-shadow:0 0 0 3px rgba(37,99,235,.28)}
/* ---------- 九宫标签规则表 ---------- */
.rules-panel{margin-top:6px;border:1px solid var(--line);border-radius:var(--r-sm);
padding:16px;background:var(--field-bg)}
.rules-title{font-size:14px;font-weight:600;margin-bottom:12px}
.rule-item{display:flex;align-items:center;gap:12px;padding:9px 0;flex-wrap:wrap}
.rule-item + .rule-item{border-top:1px solid var(--line)}
.rule-tag{flex:0 0 auto;font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;font-size:12px;font-weight:600;
color:#1d4ed8;background:#dbeafe;border:1px solid #bfdbfe;border-radius:6px;padding:3px 9px}
.rule-desc{flex:1;min-width:0;font-size:13px;color:var(--text)}
.rule-example{flex:0 0 auto;font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;font-size:12px;color:var(--muted)}
.rules-foot{margin-top:14px;padding-top:14px;border-top:1px solid var(--line);
font-size:13px;color:var(--muted);line-height:1.6}
.rules-foot code{font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;background:#e5e7eb;padding:2px 6px;border-radius:4px;color:var(--text)}
/* ---------- 组件类型候选下拉 ----------
   面板由 JS 建在 body 下并用 position:fixed 定位：卡片和滚动容器都可能把它裁掉。 */
.suggest-panel{position:fixed;z-index:3000;background:#fff;border:1px solid var(--line);
border-radius:var(--r-sm);box-shadow:0 12px 32px rgba(15,23,42,.18);padding:6px;
max-height:300px;overflow-y:auto;display:none}
.suggest-panel.is-open{display:block}
.suggest-item{display:flex;align-items:center;gap:10px;padding:9px 11px;border-radius:8px;
cursor:pointer;font-size:14px;line-height:1.35;flex-wrap:wrap}
.suggest-item:hover,.suggest-item.is-active{background:var(--field-bg);box-shadow:inset 0 0 0 1px #bfdbfe}
.suggest-name{font-weight:600;flex:0 0 auto}
.suggest-name b,.suggest-scope b,.suggest-match b{color:var(--blue-dark);font-weight:700}
.suggest-scope{color:var(--muted);font-size:12px;flex:1;min-width:0;overflow:hidden;
text-overflow:ellipsis;white-space:nowrap}
.suggest-match{flex:1 0 100%;font-size:12px;color:var(--muted);margin-top:1px;
word-break:break-all}
.suggest-assembly{color:#9ca3af;font-size:11px;flex:0 0 auto}
.suggest-note{padding:10px 12px;color:var(--muted);font-size:13px;line-height:1.5}

/* ---------- 提示条 ---------- */
.info-banner{background:linear-gradient(135deg,#eff6ff 0%,#dbeafe 100%);
border-left:4px solid var(--blue);padding:16px 20px;border-radius:var(--r-sm);
margin-bottom:24px;font-size:14px;line-height:1.6}
.info-banner strong{display:block;color:#1e40af;margin-bottom:4px}
.info-banner.warn{background:linear-gradient(135deg,#fffbeb 0%,#fef3c7 100%);
border-left-color:#f59e0b}
.info-banner.warn strong{color:#92400e}
.info-banner.danger{background:linear-gradient(135deg,#fef2f2 0%,#fee2e2 100%);
border-left-color:#ef4444}
.info-banner.danger strong{color:#b91c1c}
#lastError{display:none;white-space:pre-wrap}

/* ---------- AI 智能体选择 ---------- */
.ai-provider-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}
.ai-provider-card{appearance:none;width:100%;padding:18px;display:flex;align-items:center;
gap:14px;text-align:left;color:var(--text);font:inherit;cursor:pointer;
background:var(--field-bg);border:2px solid var(--line);border-radius:var(--r-sm);
transition:border-color .2s,box-shadow .2s,background .2s}
.ai-provider-card:hover,.ai-provider-card.is-selected{background:#f0fdfa;border-color:var(--teal)}
.ai-provider-card.is-selected{box-shadow:0 0 0 3px rgba(20,184,166,.14)}
.ai-provider-mark{width:42px;height:42px;flex:0 0 auto;display:grid;place-items:center;
border-radius:var(--r-xs);color:#fff;font-size:18px;font-weight:700;
background:linear-gradient(135deg,#14b8a6,#0f766e)}
.ai-provider-card.is-none .ai-provider-mark{background:linear-gradient(135deg,#9ca3af,#6b7280)}
.ai-provider-card.is-warn .ai-provider-mark{background:linear-gradient(135deg,#f59e0b,#d97706)}
.ai-provider-copy{min-width:0;flex:1}
.ai-provider-name{display:block;font-size:16px;font-weight:700}
.ai-provider-description{display:block;margin-top:2px;font-size:13px;color:var(--muted)}
.ai-provider-selected{display:none;font-size:12px;font-weight:700;color:var(--teal-dark);
white-space:nowrap}
.ai-provider-card.is-selected .ai-provider-selected{display:block}
.ai-api-row{display:grid;grid-template-columns:3fr 2fr;gap:14px}

/* ---------- 公共资源库 / 共享预览 ---------- */
.action-row{display:flex;flex-wrap:wrap;gap:12px}
.share-status{display:flex;align-items:center;gap:12px;padding:14px 16px;margin-bottom:18px;
background:var(--field-bg);border:1px solid var(--line);border-radius:var(--r-sm);
font-size:15px;font-weight:600;color:var(--muted)}
.share-status.is-on{color:#065f46;background:#ecfdf5;border-color:#a7f3d0}
.share-dot{width:10px;height:10px;flex:0 0 auto;border-radius:50%;background:#9ca3af}
.share-status.is-on .share-dot{background:#10b981;box-shadow:0 0 0 4px rgba(16,185,129,.16)}
.share-body{min-width:0;flex:1}
.share-url{display:block;margin-top:4px;font-size:13px;font-weight:400;color:var(--muted);
word-break:break-all}
.share-status.is-on .share-url{color:#047857}
.share-counts{font-size:14px;color:var(--text);font-weight:600}

@media (max-width:960px){
.sidebar{position:static;width:auto;flex-direction:row;align-items:center;gap:14px;
padding:14px 16px;overflow:visible}
.brand-mark{width:38px;height:38px;font-size:20px}
/* 窄屏顶栏要同时放下导航、连接状态和保存按钮。品牌名会让它挤到换行
   （PSD Layout Tool 折成三行，顶栏从 64px 涨到 100px），
   所以这里只留图标；状态文字是功能信息，必须保住。 */
.brand-name,.brand-sub,.nav-cap{display:none}
/* 横向空间不够时，让「导航」成为唯一的滚动区，把状态和保存按钮钉在右边。
   原来是整个 .sidebar 一起横向滚动，分组一多保存按钮就被顶到视口外
   （实测 5 个分组时右边界到 989px，860px 视口下要横向拖才能点到保存）。 */
.nav{flex-direction:row;gap:6px;flex:1 1 auto;min-width:0;
overflow-x:auto;overflow-y:hidden}
.nav-item{padding:9px 12px;font-size:13px;flex:0 0 auto}
.sidebar-foot{margin-left:12px;flex-direction:row;align-items:center;gap:12px;
border-top:0;padding-top:0;flex:0 0 auto}
/* 这里原来把字号设成 0（只留一个圆点），结果是窄屏下
   保存反馈的文字被彻底隐藏，看起来像「点了没反应」。改成正常字号。 */
#status{font-size:12px;gap:6px;white-space:nowrap}
#status::before{width:7px;height:7px}
#toast{left:16px;right:16px;top:12px;max-width:none}
.btn-primary{width:auto;padding:10px 18px;font-size:14px}
.content{margin-left:0;padding:24px 16px 60px}
.page-header{margin-bottom:28px}
.page-icon{font-size:40px}
.page-header h1{font-size:28px}
.page-subtitle{font-size:14px}
.card{padding:24px}
.card-icon{width:42px;height:42px;font-size:24px}
.form-grid,.ai-api-row,.ai-provider-grid{grid-template-columns:1fr}
}
</style>
</head>
<body>

<div id='toast' role='status' aria-live='polite'></div>

<aside class='sidebar'>
  <div class='brand'>
    <div class='brand-mark'>🧩</div>
    <div>
      <span class='brand-name'>PSD Layout Tool</span>
      <span class='brand-sub'>全局配置</span>
    </div>
  </div>

  <nav class='nav'>
    <div class='nav-cap'>设置分组</div>
    <a class='nav-item is-on' href='#sec-ui'>
      <svg viewBox='0 0 20 20' fill='none' stroke='currentColor' stroke-width='1.6'><rect x='2.5' y='2.5' width='6.2' height='6.2' rx='1.6'/><rect x='11.3' y='2.5' width='6.2' height='6.2' rx='1.6'/><rect x='2.5' y='11.3' width='6.2' height='6.2' rx='1.6'/><rect x='11.3' y='11.3' width='6.2' height='6.2' rx='1.6'/></svg>
      <span>默认组件类型</span>
    </a>
    <a class='nav-item' href='#sec-nine'>
      <svg viewBox='0 0 20 20' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'><rect x='2.5' y='2.5' width='15' height='15' rx='2'/><path d='M7.5 2.5v15M12.5 2.5v15M2.5 7.5h15M2.5 12.5h15'/></svg>
      <span>九宫格检测</span>
    </a>
    <a class='nav-item' href='#sec-out'>
      <svg viewBox='0 0 20 20' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'><path d='M2.5 6.1c0-.9.8-1.7 1.7-1.7h3l1.7 1.9h5.9c.9 0 1.7.8 1.7 1.7v5.5c0 .9-.8 1.7-1.7 1.7H4.2c-.9 0-1.7-.8-1.7-1.7V6.1Z'/></svg>
      <span>输出配置</span>
    </a>
    <a class='nav-item' href='#sec-ai'>
      <svg viewBox='0 0 20 20' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'><path d='M9.4 2.6 11 7l4.4 1.6L11 10.2 9.4 14.6 7.8 10.2 3.4 8.6 7.8 7 9.4 2.6Z'/><path d='m15.4 13.2.8 2.2 2.2.8-2.2.8-.8 2.2-.8-2.2-2.2-.8 2.2-.8.8-2.2Z'/></svg>
      <span>AI 层级整理</span>
    </a>
    <a class='nav-item' href='#sec-cleanup'>
      <svg viewBox='0 0 20 20' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'><path d='M3 6.2h7.4'/><path d='M3 13.8h3.6'/><circle cx='14.6' cy='6.2' r='2.4'/><circle cx='10.6' cy='13.8' r='2.4'/></svg>
      <span>Prefab 清理执行</span>
    </a>
    <a class='nav-item' href='#sec-share'>
      <svg viewBox='0 0 20 20' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'><circle cx='10' cy='10' r='2.2'/><path d='M6.4 6.4a5.1 5.1 0 0 0 0 7.2'/><path d='M13.6 6.4a5.1 5.1 0 0 1 0 7.2'/><path d='M4.2 4.2a8.2 8.2 0 0 0 0 11.6'/><path d='M15.8 4.2a8.2 8.2 0 0 1 0 11.6'/></svg>
      <span>公共资源库</span>
    </a>
  </nav>

  <div class='sidebar-foot'>
    <div id='status'>连接中…</div>
    <button id='save' class='btn btn-primary'>💾 保存并同步</button>
  </div>
</aside>

<main class='content'>
<div class='content-inner'>

  <header class='page-header'>
    <div class='page-icon'>🖼️</div>
    <h1>全局配置</h1>
    <p class='page-subtitle'>项目导入、Prefab 生成、公共资源命名与 AI 整理</p>
  </header>

  <div id='offline' hidden>
    <strong>连不上 Unity 设置服务</strong>
    <span>当前地址 <code id='offlineHost'></code> 没有响应。Unity 重新编译脚本时会自动停掉这个服务，
    请回到 Unity 菜单重新打开设置面板；端口不一致时以 Unity 打开的地址为准。</span>
  </div>

  <div id='lastError' class='info-banner danger'></div>

  <section class='card' id='sec-ui'>
    <div class='card-header'>
      <div class='card-icon blue'>📝</div>
      <div class='card-title'>
        <h2>默认组件类型</h2>
        <p>生成节点时挂载的组件类</p>
      </div>
    </div>
    <div class='form-group'>
      <label class='form-label' for='imageComponentTypeName'>Image 组件类型</label>
      <input class='form-input' id='imageComponentTypeName' placeholder='UnityEngine.UI.Image'>
      <span class='form-help'>生成图片节点时挂载的组件类名。可写完整命名空间，也可写唯一类名；必须继承 UnityEngine.UI.Image。</span>
    </div>
    <div class='form-group'>
      <label class='form-label' for='buttonComponentTypeName'>Button 组件类型</label>
      <input class='form-input' id='buttonComponentTypeName' placeholder='UnityEngine.UI.Button'>
      <span class='form-help'>生成按钮时挂载的组件类名，可用任意 MonoBehaviour，包括自定义 Button。</span>
    </div>
  </section>

  <section class='card' id='sec-nine'>
    <div class='card-header'>
      <div class='card-icon green'>🎯</div>
      <div class='card-title'>
        <h2>九宫格检测</h2>
        <p>基于三重推断的智能边框检测</p>
      </div>
      <span class='badge badge-success' id='nineBadge'>已启用</span>
    </div>

    <div class='info-banner'>
      <strong>三重推断算法已启用</strong>
      视觉边缘检测 + 重复模式分析 + 圆角保护。图层名带九宫标签时，导入会先推断边框再生成 Sprite，
      并且碰到烘焙好的卡片或标签美术会自动保留原图。
    </div>

    <div class='switch-row'>
      <div class='switch-text'>
        <span class='switch-main'>导出时自动裁剪</span>
        <span class='switch-sub'>按九宫边框把 PNG 裁到最小可拉伸尺寸。关闭时边框照常生效但保留原始像素 —— 手动量的边距和烘焙美术不会因裁剪错位。</span>
      </div>
      <label class='switch'>
        <input type='checkbox' id='autoCropNineSlice'>
        <span class='switch-track'></span>
      </label>
    </div>

    <div class='rules-panel'>
      <div class='rules-title'>支持的九宫格标签（写在 PSD 图层名里）</div>
      <div class='rule-item'>
        <span class='rule-tag'>|9slice</span>
        <span class='rule-desc'>自动推断九宫格边界（推荐）</span>
        <span class='rule-example'>例: bg|9slice</span>
      </div>
      <div class='rule-item'>
        <span class='rule-tag'>|9slice=L,T,R,B</span>
        <span class='rule-desc'>显式指定边界（左、上、右、下像素）</span>
        <span class='rule-example'>例: panel|9slice=12,15,12,15</span>
      </div>
      <div class='rule-item'>
        <span class='rule-tag'>|h3slice</span>
        <span class='rule-desc'>横向三切（左-中-右，适合进度条）</span>
        <span class='rule-example'>例: progressbar|h3slice</span>
      </div>
      <div class='rule-item'>
        <span class='rule-tag'>|v3slice</span>
        <span class='rule-desc'>纵向三切（上-中-下）</span>
        <span class='rule-example'>例: scrollbar|v3slice</span>
      </div>
      <div class='rule-item'>
        <span class='rule-tag'>[方括号形式]</span>
        <span class='rule-desc'>与上面等价，例如 [9slice] / [v3slice]</span>
        <span class='rule-example'>例: scrollbar[v3slice]</span>
      </div>
      <div class='rule-item'>
        <span class='rule-tag'>jiugong* 前缀</span>
        <span class='rule-desc'>Figma 兼容写法，写在图层名开头</span>
        <span class='rule-example'>例: jiugongh3_dibankuan_3</span>
      </div>
      <div class='rules-foot'>
        <strong>提示：</strong> 推荐用 <code>|9slice</code>，算法会自动找最佳边界。
        带显式边界但分析失败时会自动退回名称规则；两种写法都不需要手动填边距。
      </div>
    </div>
  </section>

  <section class='card' id='sec-out'>
    <div class='card-header'>
      <div class='card-icon orange'>📦</div>
      <div class='card-title'>
        <h2>输出配置</h2>
        <p>定义 Prefab、图集与贴图的输出位置</p>
      </div>
    </div>
    <div class='form-grid'>
      <div class='form-group'>
        <label class='form-label' for='outputMode'>资源输出位置</label>
        <div class='select-wrap'><select class='form-select' id='outputMode'><option value='0'>与 PSD 同目录</option><option value='1'>固定输出路径</option></select></div>
      </div>
      <div class='form-group'>
        <label class='form-label' for='outputFolderName'>输出文件夹名</label>
        <input class='form-input' id='outputFolderName'>
        <span class='form-help'>仅「固定输出路径」模式使用。</span>
      </div>
    </div>
    <div class='form-group'>
      <label class='form-label' for='prefabOutputPath'>Prefab 输出路径</label>
      <input class='form-input' id='prefabOutputPath'>
    </div>
    <div class='form-grid'>
      <div class='form-group'>
        <label class='form-label' for='atlasOutputPath'>图集输出路径</label>
        <input class='form-input' id='atlasOutputPath'>
      </div>
      <div class='form-group'>
        <label class='form-label' for='textureOutputPath'>贴图输出路径</label>
        <input class='form-input' id='textureOutputPath'>
      </div>
    </div>
  </section>

  <section class='card' id='sec-ai'>
    <div class='card-header'>
      <div class='card-icon green'>✦</div>
      <div class='card-title'>
        <h2>AI 层级整理</h2>
        <p>选择 AI 整理层级时使用的本机智能体</p>
      </div>
      <span class='badge badge-success' id='cliBadge'>检测中…</span>
    </div>

    <div class='form-group'>
      <label class='form-label'>AI 模型</label>
      <div class='ai-provider-grid' id='aiProviderOptions' role='radiogroup' aria-label='AI 模型'></div>
      <select id='aiProvider' hidden></select>
      <span class='form-help'>只列出本机已安装的 CLI。选「不启用」时下面的参数会隐藏，AI 整理会提示先来这里选择。</span>
    </div>

    <div id='noCli' class='info-banner warn' hidden>
      <strong>本机未检测到任何 CLI</strong>
      Claude / Codex / Grok / Pi 都没有找到。安装后需要让 Unity 重新加载才能探测到，通常重启编辑器即可。
    </div>

    <div id='aiDetails' hidden>
      <div class='form-grid'>
        <div class='form-group'>
          <label class='form-label' for='aiModel'>模型名称</label>
          <div class='combo' id='aiModelCombo'>
            <input class='form-input' id='aiModel' placeholder='留空 = 用 CLI 自己配置的' autocomplete='off' spellcheck='false'>
            <button type='button' class='combo-toggle' id='aiModelDrop' aria-label='展开模型候选'>▾</button>
            <div class='combo-panel' id='aiModelPanel' role='listbox' hidden></div>
          </div>
          <span class='form-help' id='modelHint'></span>
        </div>
        <div class='form-group'>
          <label class='form-label' for='aiEffort'>思考程度</label>
          <div class='combo' id='aiEffortCombo'>
            <input class='form-input' id='aiEffort' placeholder='留空 = 用 CLI 自己配置的' autocomplete='off' spellcheck='false'>
            <button type='button' class='combo-toggle' id='aiEffortDrop' aria-label='展开思考程度档位'>▾</button>
            <div class='combo-panel' id='aiEffortPanel' role='listbox' hidden></div>
          </div>
          <span class='form-help' id='effortHint'></span>
        </div>
      </div>
      <div class='ai-api-row'>
        <div class='form-group'>
          <label class='form-label' for='aiEndpoint'>API 地址</label>
          <input class='form-input' id='aiEndpoint' placeholder='留空 = 调用本机 CLI'>
          <span class='form-help' id='endpointHint'></span>
        </div>
        <div class='form-group'>
          <label class='form-label' for='aiApiKey'>API Key</label>
          <input class='form-input' id='aiApiKey' type='password' placeholder='留空表示不修改'>
          <span class='form-help' id='keyHint'></span>
        </div>
      </div>
      <div class='form-group'>
        <button class='btn btn-secondary' id='clearKey'>清除本机保存的 Key</button>
      </div>
    </div>
  </section>

  <section class='card' id='sec-cleanup'>
    <div class='card-header'>
      <div class='card-icon violet'>🧹</div>
      <div class='card-title'>
        <h2>Prefab 清理执行</h2>
        <p>层级整理计划由谁来执行</p>
      </div>
    </div>
    <div class='form-group'>
      <label class='form-label' for='cleanupBackend'>执行后端</label>
      <div class='select-wrap'><select class='form-select' id='cleanupBackend'><option value='0'>Native Unity（默认）</option><option value='1'>Unity CLI Runner（可选）</option></select></div>
      <span class='form-help'>Native Unity 在当前编辑器里直接执行，不需要外部工具；Unity CLI Runner 是可选的外部执行器。</span>
    </div>
    <div id='cleanupBanner' class='info-banner'></div>
  </section>

  <section class='card' id='sec-share'>
    <div class='card-header'>
      <div class='card-icon teal'>📡</div>
      <div class='card-title'>
        <h2>公共资源库</h2>
        <p>配置公共资源前缀、刷新映射表，再开着服务给同组的人浏览</p>
      </div>
      <span class='badge badge-idle' id='catalogBadge'>读取中…</span>
    </div>

    <div class='form-grid'>
      <div class='form-group'>
        <label class='form-label' for='prefabPrefix'>Prefab 前缀</label>
        <input class='form-input' id='prefabPrefix'>
      </div>
      <div class='form-group'>
        <label class='form-label' for='texturePrefix'>Texture 前缀</label>
        <input class='form-input' id='texturePrefix'>
      </div>
    </div>
    <span class='form-help' style='margin-bottom:24px'>两个前缀用于公共资源命名，末尾下划线会自动补全，且不能相同。</span>

    <div class='form-group'>
      <label class='form-label'>映射表</label>
      <div class='share-counts' id='catalogSummary'>读取中…</div>
      <span class='form-help'>映射表记录项目里所有符合前缀的公共 Prefab 与贴图，末尾下划线会自动补全。改了前缀或新导入公共资源后要重新生成，否则导入器会暂停增量更新。</span>
      <div class='action-row' style='margin-top:14px'>
        <button class='btn btn-secondary' id='catalogRefresh'>🔄 生成 / 刷新公共资源映射表</button>
      </div>
    </div>

    <div id='catalogBanner' class='info-banner'></div>

    <div class='form-group' style='margin-top:24px'>
      <label class='form-label' for='previewServerPort'>预览服务端口</label>
      <input class='form-input' id='previewServerPort' type='number' min='1' max='65535' inputmode='numeric'>
      <span class='form-help'>默认 52342。启动后同事用下面显示的局域网地址就能打开图库，对方不需要装 Unity，也不需要连你的编辑器。</span>
    </div>

    <div class='share-status' id='shareStatus'>
      <span class='share-dot'></span>
      <div class='share-body'>
        <span id='shareStatusText'>服务已停止</span>
        <span class='share-url' id='shareUrl'></span>
      </div>
    </div>

    <div class='action-row'>
      <button class='btn btn-secondary' id='previewStart'>▶ 启动服务</button>
      <button class='btn btn-secondary' id='previewStop'>■ 停止服务</button>
      <button class='btn btn-secondary' id='previewOpen'>在本机浏览器打开</button>
    </div>

    <div id='previewBanner' class='info-banner'></div>
  </section>

</div>
</main>

<script>
var TEXT_FIELDS=['imageComponentTypeName','buttonComponentTypeName','outputFolderName','prefabOutputPath','atlasOutputPath','textureOutputPath','prefabPrefix','texturePrefix','aiModel','aiEffort','aiEndpoint'];
var statusEl=document.getElementById('status');
var errorEl=document.getElementById('lastError');
var clis=[];
var hasKey=false;
var providerSig=null;
function byId(id){return document.getElementById(id);}
function active(el){return document.activeElement===el;}
function el(tag,cls,text){var e=document.createElement(tag);if(cls)e.className=cls;if(text!=null)e.textContent=String(text);return e;}
var statusLockUntil=0;
var toastEl=document.getElementById('toast');
var toastTimer=null;
var offlineEl=document.getElementById('offline');
var offlineHostEl=document.getElementById('offlineHost');
var pollFailures=0;
var pollBusy=false;

/* 带超时的 fetch。
   不加超时的话，只要服务端不回应（Unity 重编译会停掉服务；请求在服务端被异常打断时
   响应也可能永远不写出来），fetch 就会一直 pending，按钮永远停在「保存中…」。 */
function fetchWithTimeout(url,opts,timeoutMs){
  return new Promise(function(resolve,reject){
    var ac=window.AbortController?new AbortController():null;
    var timedOut=false;
    var timer=setTimeout(function(){
      timedOut=true;
      if(ac)ac.abort();
      reject(new Error('超时：'+Math.round(timeoutMs/1000)+' 秒没有响应'));
    },timeoutMs);
    var o=opts||{};
    if(ac)o.signal=ac.signal;
    fetch(url,o).then(
      function(r){clearTimeout(timer);resolve(r);},
      function(e){
        clearTimeout(timer);
        reject(timedOut?new Error('超时：'+Math.round(timeoutMs/1000)+' 秒没有响应'):e);
      });
  });
}

/* 连续多次轮询失败才提示，避免网络偶发抖动时闪一下。 */
function notePollFailure(){
  pollFailures++;
  if(pollFailures<3||!offlineEl)return;
  if(offlineHostEl)offlineHostEl.textContent=location.host;
  offlineEl.hidden=false;
}
function clearPollFailure(){
  pollFailures=0;
  if(offlineEl)offlineEl.hidden=true;
}

function setStatus(text,kind){statusEl.textContent=text;statusEl.className=kind||'';}

/* 保存结果写入状态栏，并在 holdMs 内禁止轮询改写。
   否则 1.5 秒一次的轮询会立刻把「已保存」换成「已连接」，
   看起来就像点了没反应。 */
function setStatusHold(text,kind,holdMs){
  setStatus(text,kind);
  statusLockUntil=Date.now()+(holdMs||4000);
}

/* 轮询专用：锁定窗口内不覆盖状态栏。 */
function setPollingStatus(text,kind){
  if(Date.now()<statusLockUntil)return;
  setStatus(text,kind);
}

/* 除状态栏外再弹一个固定定位浮层：窄屏下状态栏文字以前是被隐藏的，
   而且用户在页面中部时也不会去看侧栏底部。 */
function showToast(text,kind){
  if(!toastEl)return;
  toastEl.textContent=text;
  toastEl.className='is-on '+(kind||'');
  clearTimeout(toastTimer);
  toastTimer=setTimeout(function(){toastEl.className=kind||'';},3200);
}

/* 已改动但尚未保存成功的字段：轮询不许覆盖。
   否则保存失败时输入会被悄悄还原，同样表现得像「点了没反应」。 */
var dirty={};

function applyField(id,value){
  if(dirty[id])return;
  var f=byId(id);
  if(!f||active(f))return;
  /* checkbox 不能用字符串赋值：f.value='false' 仍然算勾选。
     轮询期间也不能覆盖正在被点击的开关。 */
  if(f.type==='checkbox'){f.checked=value===true||value==='true';return;}
  f.value=value==null?'':String(value);
}

function renderProviders(cfg){
  clis=cfg.availableClis||[];
  var sel=byId('aiProvider');
  var sig=String(cfg.aiProvider)+'|'+(cfg.providerInstalled?1:0)+'|'+clis.map(function(c){return c.provider;}).join(',');
  if(sig!==providerSig){
    providerSig=sig;
    sel.innerHTML='';
    var none=document.createElement('option');
    none.value='-1';none.textContent='不启用';sel.appendChild(none);
    clis.forEach(function(c){
      var o=document.createElement('option');
      o.value=String(c.provider);o.textContent=c.name;sel.appendChild(o);
    });
    if(!cfg.providerInstalled){
      var off=document.createElement('option');
      off.value=String(cfg.aiProvider);off.textContent='配置中的模型（当前不可用）';sel.appendChild(off);
    }
    buildProviderCards(cfg);
  }
  if(!active(sel)&&!dirty.aiProvider)sel.value=String(cfg.aiProvider);
  byId('noCli').hidden=clis.length>0;
  var badge=byId('cliBadge');
  badge.textContent=clis.length?('本机已装 '+clis.length+' 个 CLI'):'未检测到 CLI';
  badge.className='badge '+(clis.length?'badge-success':'badge-warn');
  refreshDetailVisibility();
  markSelectedCard();
}

function buildProviderCards(cfg){
  var box=byId('aiProviderOptions');
  box.innerHTML='';
  var items=[{provider:-1,mark:'—',name:'不启用',description:'关闭 AI 层级整理',off:true}];
  clis.forEach(function(c){
    items.push({
      provider:c.provider,
      mark:(c.name||'?').charAt(0).toUpperCase(),
      name:c.name,
      description:'使用本机 '+(c.name||'CLI')
    });
  });
  if(!cfg.providerInstalled){
    items.push({
      provider:cfg.aiProvider,mark:'!',warn:true,
      name:'配置中的模型（当前不可用）',
      description:'该 CLI 未检测到，安装并重启 Unity 后可用'
    });
  }
  items.forEach(function(it){
    var card=document.createElement('button');
    card.type='button';
    card.className='ai-provider-card'+(it.off?' is-none':'')+(it.warn?' is-warn':'');
    card.setAttribute('data-provider',String(it.provider));
    card.setAttribute('role','radio');
    var copy=el('span','ai-provider-copy');
    copy.appendChild(el('span','ai-provider-name',it.name));
    copy.appendChild(el('span','ai-provider-description',it.description));
    card.appendChild(el('span','ai-provider-mark',it.mark));
    card.appendChild(copy);
    card.appendChild(el('span','ai-provider-selected','已选择'));
    card.addEventListener('click',function(){
      byId('aiProvider').value=String(it.provider);
      dirty.aiProvider=true;
      refreshDetailVisibility();
      markSelectedCard();
    });
    box.appendChild(card);
  });
}

function markSelectedCard(){
  var cur=byId('aiProvider').value;
  var cards=document.querySelectorAll('.ai-provider-card');
  for(var i=0;i<cards.length;i++){
    var on=cards[i].getAttribute('data-provider')===cur;
    cards[i].classList.toggle('is-selected',on);
    cards[i].setAttribute('aria-checked',on?'true':'false');
  }
}

function currentCli(){
  var p=parseInt(byId('aiProvider').value,10);
  for(var i=0;i<clis.length;i++){if(clis[i].provider===p)return clis[i];}
  return null;
}

function refreshDetailVisibility(){
  var provider=parseInt(byId('aiProvider').value,10);
  var box=byId('aiDetails');
  if(provider===-1){box.hidden=true;return;}
  box.hidden=false;
  var info=currentCli();
  byId('modelHint').textContent=info
    ?('留空时使用 '+info.name+' CLI 自身配置的模型'+(info.defaultModel?'（当前默认 '+info.defaultModel+'）':'')+'。点输入框右侧的 ▾ 或直接手填。'+
      (info.modelHint?('常见取值：'+info.modelHint+'。'):''))
    :'留空时使用 CLI 自身配置的模型。';
  byId('effortHint').textContent=info
    ?('留空时使用 CLI 自身配置。可选 '+info.effortHint+'，点右侧 ▾ 直接选。')
    :'留空时使用 CLI 自身配置。';
  byId('endpointHint').textContent=info?('留空表示调用本机 '+info.name+' CLI，不需要 API Key；填写后才走自定义 API。'):'留空表示调用本机 CLI。';
  byId('keyHint').textContent=hasKey?'本机已加密保存。留空表示不修改，点下面的按钮可以删除。':'本机未保存。走本地 CLI 时不需要填写。';
  /* 提示文字已经跟着 provider 换了，开着的候选面板也必须换成新 CLI 的档位，
     否则会留着上一个 CLI 的列表让人误选。 */
  if(comboCtx.aiModel&&!comboCtx.aiModel.panel.hidden)comboRender('aiModel');
  if(comboCtx.aiEffort&&!comboCtx.aiEffort.panel.hidden)comboRender('aiEffort');
}

/* Prefab 清理执行后端的说明文字。和 Inspector 一样，切换后端时给出对应级别的提示：
   Native Unity 是常规路径（info），Unity CLI Runner 功能较少（warning）。 */
function updateCleanupBanner(){
  var box=byId('cleanupBanner');
  if(!box)return;
  var v=parseInt(byId('cleanupBackend').value,10);
  if(isNaN(v))v=0;
  box.className='info-banner'+(v===1?' warn':'');
  box.textContent=v===1
    ? 'Unity CLI Runner 已启用：支持组件 Prefab 提取与私有资源改名。'
    : 'Native Unity 已启用：层级清理、组件 Prefab 提取、私有资源改名、校验与失败处理都在当前 Unity 编辑器内执行。';
}

/* 九宫格：开关状态 + 徽标文案。徽标反映「自动裁剪」是否开启，
   而不是「九宫检测是否可用」—— 检测本身始终在跑。 */
function updateNineSliceBanner(){
  var badge=byId('nineBadge');
  if(badge){
    var on=byId('autoCropNineSlice').checked;
    badge.textContent=on?'自动裁剪':'保留原图';
    badge.className='badge '+(on?'badge-warn':'badge-success');
  }
}

/* 公共资源库：映射表规模 + 共享预览服务的实时状态。
   页面每 1.5 秒回读一次 /config，所以启动 / 停止之后按钮和状态会自己跟上，
   动作接口本身只回「已排队」，执行结果全靠这里回读。 */
function updateSharePanel(cfg){
  var exists=!!cfg.catalogExists;
  var stale=!!cfg.catalogNeedsRefresh;
  var summary=byId('catalogSummary');
  if(summary)summary.textContent=exists
    ? ('公共 Prefab ' + (cfg.catalogPrefabCount||0) + ' 个 · 公共贴图 ' + (cfg.catalogTextureCount||0) + ' 张')
    : '映射表还没生成，点下面的按钮创建。';

  var badge=byId('catalogBadge');
  if(badge){
    badge.textContent=stale?'待刷新':(exists?'已就绪':'未生成');
    badge.className=stale?'badge badge-warn':(exists?'badge badge-success':'badge badge-idle');
  }

  var banner=byId('catalogBanner');
  if(banner){
    banner.className='info-banner'+(stale?' warn':'');
    banner.textContent=stale
      ? '前缀改动后映射表已失效，导入器暂停了增量更新。刷新一次即可恢复。'
      : '公共图与公共 Prefab 都由这张映射表解析；刷新只扫描项目，不改动已有资源。';
  }

  /* 启停服务要等主线程执行、再等这次轮询回读，中间有一段时间状态没变。
     这段时间按钮必须保持禁用：否则用户能在间隙里再点一次「启动服务」，
     重复 Start 会把监听端口撞成「每个套接字地址只允许使用一次」。 */
  if(actionPending&&(actionPending.expect(cfg)||Date.now()>=actionPending.until)){
    actionPending=null;
  }
  var pending=actionPending;

  var running=!!cfg.previewServerRunning;
  var box=byId('shareStatus');
  if(box)box.className='share-status'+(running?' is-on':'');
  var text=byId('shareStatusText');
  if(text)text.textContent=pending?pending.label:(running?'服务运行中':'服务已停止');
  var url=byId('shareUrl');
  if(url)url.textContent=running&&cfg.previewServerAddress
    ? ('同事可直接打开：' + cfg.previewServerAddress) : '';

  var startBtn=byId('previewStart');
  var stopBtn=byId('previewStop');
  var openBtn=byId('previewOpen');
  if(startBtn){startBtn.disabled=running||!!pending;if(!pending)startBtn.textContent=START_LABEL;}
  if(stopBtn){stopBtn.disabled=!running||!!pending;if(!pending)stopBtn.textContent=STOP_LABEL;}
  if(openBtn)openBtn.disabled=!running;

  var pb=byId('previewBanner');
  if(pb){
    var err=cfg.previewServerError||'';
    if(err){
      pb.className='info-banner danger';
      pb.textContent='预览服务出错：' + err +
        (/只允许使用一次|套接字的尝试|socket address/i.test(err)
          ? ' 端口被占用了：换一个端口再点「启动服务」；如果占用的正是刚用过的端口，需要重启 Unity 才能释放那个监听。'
          : '');
    }else if(running){
      pb.className='info-banner';
      pb.textContent='服务已监听本机所有网卡。同一个局域网里的同事用上面地址就能浏览公共图与 Prefab，不需要 Unity；关掉编辑器服务会自动停止。';
    }else{
      pb.className='info-banner';
      pb.textContent='点「启动服务」之后，项目里的公共图与公共 Prefab 会以网页形式开放给同组的人浏览。';
    }
  }
}

/* ---------- 可手填下拉（模型名称 / 思考程度） ----------
   沿用 .suggest-* 那套「焦点留在输入框上」的键盘模型，
   区别是候选集是本地算出来的（不查服务端），而且面板由容器自己定位。 */
var comboCtx={};
var comboActive=-1;

function comboSetup(inputId,panelId){
  var input=byId(inputId);
  var panel=byId(panelId);
  if(!input||!panel)return;
  comboCtx[inputId]={input:input,panel:panel,items:[],active:-1,note:''};

  input.addEventListener('focus',function(){comboOpen(inputId);});
  input.addEventListener('input',function(){
    dirty[inputId]=true;
    /* 输入时按已打的内容过滤；已经打开的键盘高亮要重置，否则选中的还是上一批。 */
    var ctx=comboCtx[inputId];
    ctx.active=-1;
    comboRender(inputId);
  });
  input.addEventListener('keydown',function(ev){
    var ctx=comboCtx[inputId];
    var open=!ctx.panel.hidden;
    if(ev.key==='ArrowDown'){
      ev.preventDefault();
      if(open)comboMove(inputId,1);else comboOpen(inputId);
      return;
    }
    if(ev.key==='ArrowUp'){if(open){ev.preventDefault();comboMove(inputId,-1);}return;}
    if(ev.key==='Escape'){if(open){ev.preventDefault();comboClose(inputId);}return;}
    if(ev.key==='Enter'&&open&&ctx.active>=0){ev.preventDefault();comboPick(inputId,ctx.active);return;}
    if(ev.key==='Tab'&&open&&ctx.active>=0){ev.preventDefault();comboPick(inputId,ctx.active);return;}
    if(ev.key==='Enter'&&open)comboClose(inputId);
  });
  input.addEventListener('blur',function(){
    /* blur 早于点击：给面板上的 mousedown 一点时间先处理，
       否则点候选会先被这里关掉、什么也没选中。 */
    setTimeout(function(){
      var ctx=comboCtx[inputId];
      if(ctx&&!ctx.panel.hidden&&!ctx.panel.contains(document.activeElement))comboClose(inputId);
    },140);
  });

  var toggle=byId(inputId==='aiModel'?'aiModelDrop':'aiEffortDrop');
  if(toggle){
    /* mousedown 里 preventDefault：不把焦点让给按钮，
       否则输入框先 blur 关面板、按钮再 toggle 又把空面板打开。 */
    toggle.addEventListener('mousedown',function(ev){
      ev.preventDefault();
      var ctx=comboCtx[inputId];
      if(ctx.panel.hidden)comboOpen(inputId);else comboClose(inputId);
    });
  }
}

/* 候选来源：模型名是建议列表（仍可手填），思考程度是该 CLI 的封闭档位全集。
   注意 aiModel 模式下不能因为列表为空就什么都不画——手填本来就是这个字段的用法。 */
function comboSource(inputId){
  var info=currentCli();
  var provider=parseInt(byId('aiProvider').value,10);
  if(inputId==='aiModel'){
    if(!info){
      return {items:[],note:'选择「不启用」时不需要填模型；填了也只在本机 CLI 下生效。',
              foot:'可以手填任何该 CLI 认的模型名。'};
    }
    if(provider===-1){
      return {items:[],note:'当前是「不启用」，模型名不会生效。',
              foot:'可以手填任何该 CLI 认的模型名。'};
    }
    return {items:info.modelSuggestions||[],
            default:info.defaultModel||'',
            foot:info.modelSuggestions&&info.modelSuggestions.length
              ? '上面只是常见取值，也可以手填其它模型名。'
              : '该 CLI 没有内置候选，直接手填模型名即可。'};
  }

  var levels=info?(info.effortLevels||[]):[];
  if(!levels.length){
    return {items:[],note:info?('没有列到 '+info.name+' 的思考程度档位，可以直接手填。')
                             :'选择具体 CLI 后这里会列出可用档位。',
            foot:'留空表示不传该参数，完全使用 CLI 自身的配置。'};
  }
  return {items:levels,
          foot:'留空表示不传该参数（使用 CLI 自身配置）。档位会原样传给 '+
               (info?info.name:'CLI')+'，填列表外的值不会报错、只会被忽略。'};
}

function comboOpen(inputId){
  var ctx=comboCtx[inputId];
  if(!ctx)return;
  ctx.active=-1;
  comboRender(inputId);
  ctx.panel.hidden=false;
}

function comboClose(inputId){
  var ctx=comboCtx[inputId];
  if(!ctx)return;
  ctx.panel.hidden=true;
  ctx.active=-1;
}

function comboCloseAll(){
  comboClose('aiModel');
  comboClose('aiEffort');
}

function comboRender(inputId){
  var ctx=comboCtx[inputId];
  if(!ctx)return;
  var src=comboSource(inputId);
  var q=String(ctx.input.value||'').trim().toLowerCase();
  var items=src.items||[];
  /* 已经打进去的值别在候选里重复一遍，看着像多出来一项。 */
  var list=items.filter(function(v){return String(v).toLowerCase()!==q;});
  if(q)list=list.filter(function(v){return String(v).toLowerCase().indexOf(q)>=0;});
  ctx.items=list;
  ctx.note=src.note||'';

  var panel=ctx.panel;
  panel.textContent='';
  if(!list.length){
    panel.appendChild(el('div','combo-note',
      src.note||(q?'没有匹配的候选，按原样保存即可。':'没有可选项，直接手填即可。')));
  }else{
    list.forEach(function(v,index){
      var row=el('div','combo-item');
      row.setAttribute('role','option');
      row.title=String(v);
      var span=el('span','combo-value');
      span.appendChild(comboMark(String(v),q));
      row.appendChild(span);
      if(String(v)===String(ctx.input.value||''))row.appendChild(el('span','combo-act','当前'));
      else if(src.default&&String(v)===String(src.default))row.appendChild(el('span','combo-act','默认'));
      /* mousedown 早于 blur：preventDefault 把焦点留在输入框上 */
      row.addEventListener('mousedown',function(ev){ev.preventDefault();comboPick(inputId,index);});
      panel.appendChild(row);
    });
  }
  if(src.foot)panel.appendChild(el('div','combo-foot',src.foot));

  /* 手填模式下没候选就不弹一个空面板挡着输入框。 */
  if(!list.length&&!src.foot){
    panel.hidden=true;
    return;
  }
  panel.hidden=false;
  comboMarkActive(inputId);
}

function comboMark(value,query){
  var span=document.createElement('span');
  var text=String(value);
  var at=query?text.toLowerCase().indexOf(query):-1;
  if(at<0){span.appendChild(document.createTextNode(text));return span;}
  if(at>0)span.appendChild(document.createTextNode(text.substring(0,at)));
  span.appendChild(el('b',null,text.substr(at,query.length)));
  if(at+query.length<text.length)span.appendChild(document.createTextNode(text.substring(at+query.length)));
  return span;
}

function comboMarkActive(inputId){
  var ctx=comboCtx[inputId];
  if(!ctx)return;
  var rows=ctx.panel.querySelectorAll('.combo-item');
  for(var i=0;i<rows.length;i++){
    var on=i===ctx.active;
    rows[i].classList.toggle('is-active',on);
    if(on&&rows[i].scrollIntoView)rows[i].scrollIntoView({block:'nearest'});
  }
}

function comboMove(inputId,delta){
  var ctx=comboCtx[inputId];
  if(!ctx||!ctx.items.length)return;
  var total=ctx.items.length;
  ctx.active=ctx.active<0?(delta>0?0:total-1):(ctx.active+delta+total)%total;
  comboMarkActive(inputId);
}

function comboPick(inputId,index){
  var ctx=comboCtx[inputId];
  if(!ctx)return;
  var value=ctx.items[index];
  if(value==null)return;
  ctx.input.value=String(value);
  dirty[inputId]=true;
  comboClose(inputId);
  setStatusHold('已填入 '+value+'，记得点保存同步','ok',5000);
}

/* 点空白处、滚动、改窗口大小都收掉面板；切换分组时也一样。 */
document.addEventListener('mousedown',function(ev){
  Object.keys(comboCtx).forEach(function(id){
    var ctx=comboCtx[id];
    if(ctx.panel.hidden)return;
    if(ctx.panel.contains(ev.target))return;
    if(ev.target===ctx.input)return;
    if(ev.target.id===(id==='aiModel'?'aiModelDrop':'aiEffortDrop'))return;
    comboClose(id);
  });
});
window.addEventListener('scroll',comboCloseAll,true);
window.addEventListener('resize',comboCloseAll);

/* comboSetup 放在 renderProviders 之后：函数声明会提升，但首次聚焦时
   currentCli() 读的 clis 数组得先被 renderProviders 填好。 */

function load(){
  if(pollBusy)return;
  pollBusy=true;
  fetchWithTimeout('/config',{cache:'no-store'},4000).then(function(r){return r.json();}).then(function(cfg){
    clearPollFailure();
    TEXT_FIELDS.forEach(function(id){applyField(id,cfg[id]);});
    if(!dirty.outputMode)applyField('outputMode',cfg.outputMode);
    if(!dirty.cleanupBackend)applyField('cleanupBackend',cfg.cleanupBackend);
    updateCleanupBanner();
    if(!dirty.autoCropNineSlice)applyField('autoCropNineSlice',cfg.autoCropNineSlice);
    updateNineSliceBanner();
    if(!dirty.previewServerPort)applyField('previewServerPort',cfg.previewServerPort);
    updateSharePanel(cfg);
    hasKey=!!cfg.aiHasApiKey;
    renderProviders(cfg);
    if(cfg.lastError){
      errorEl.style.display='block';
      errorEl.innerHTML='';
      errorEl.appendChild(el('strong',null,'部分设置未生效'));
      errorEl.appendChild(document.createTextNode(cfg.lastError));
      setPollingStatus('已连接 · 有设置未生效','err');
    }else{
      errorEl.style.display='none';
      setPollingStatus('已连接 · '+new Date().toLocaleTimeString(),'ok');
    }
  }).catch(function(){
    /* 把当前地址带上：端口改过之后，旧标签页会一直停在这里，
       显示 host 能立刻看出自己开的是哪个端口。 */
    setPollingStatus('等待 Unity 服务 · '+location.host,'');
    notePollFailure();
  }).then(function(){pollBusy=false;});
}

function payload(){
  var body={};
  TEXT_FIELDS.forEach(function(id){var f=byId(id);if(f)body[id]=f.value;});
  body.outputMode=parseInt(byId('outputMode').value,10);
  body.cleanupBackend=parseInt(byId('cleanupBackend').value,10);
  body.autoCropNineSlice=!!byId('autoCropNineSlice').checked;
  var port=parseInt(byId('previewServerPort').value,10);
  if(!isNaN(port))body.previewServerPort=port;
  body.aiProvider=parseInt(byId('aiProvider').value,10);
  var key=byId('aiApiKey').value;
  if(key!=='')body.aiApiKey=key;
  return body;
}

function post(body,toastText,button){
  if(button&&button.disabled)return;
  var label=button?(button.getAttribute('data-label')||button.textContent):null;
  var cls=button?(button.getAttribute('data-cls')||button.className):null;
  if(button){
    button.setAttribute('data-label',label);
    button.setAttribute('data-cls',cls);
    button.disabled=true;
    button.className=cls+' is-busy';
    button.textContent='保存中…';
  }
  fetchWithTimeout('/save',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)},8000)
    .then(function(r){
      if(!r.ok)throw new Error('HTTP '+r.status);
      return r.json();
    })
    .then(function(){
      dirty={};
      setStatusHold('已保存 · '+new Date().toLocaleTimeString(),'ok',5000);
      showToast(toastText||'已保存到 Unity','ok');
      /* 服务端只是把请求排进队列，真正写配置发生在主线程的下一个编辑器 tick。
         回读太快会读到旧值，看起来像「保存没生效」。 */
      setTimeout(load,800);
      if(button){
        button.className=cls+' is-done';
        button.textContent='✓ 已保存';
        setTimeout(function(){
          button.className=cls;
          button.textContent=label;
          button.disabled=false;
        },2200);
      }
    })
    .catch(function(err){
      var msg=(err&&err.message)?err.message:'无法连接 Unity 服务';
      var isTimeout=/超时/.test(msg);
      /* 超时时不能一口咬定「没保存成功」：服务端可能已经写入配置，只是响应没回来
         （这正是本次修复的那个 bug 的表现）。 */
      setStatusHold(isTimeout?'保存超时 · Unity 没有响应':'保存失败 · '+msg,'err',8000);
      showToast(isTimeout
        ?'保存超时：Unity 没有响应。如果刚重新编译过脚本，请重新打开设置页。'
        :'保存失败：'+msg,'err');
      notePollFailure();
      if(button){
        button.className=cls+' is-fail';
        button.textContent='保存失败，点此重试';
        button.disabled=false;
      }
    });
}

/* 启停服务是「改变运行态」的动作：服务端只是把名字排进队列，
   真正执行在 Unity 主线程的下一个 tick，页面要再等一轮轮询才看得到新状态。
   所以按钮不能一收到 HTTP 回执就解锁 —— 那会留出 1.5 秒的重复点击窗口。 */
var actionPending=null;
var START_LABEL=byId('previewStart').textContent;
var STOP_LABEL=byId('previewStop').textContent;

function postAction(name,toastText,button,pending){
  if(button&&button.disabled)return;
  var label=button?button.textContent:null;
  if(pending){
    actionPending={
      label:pending.label,
      expect:pending.expect,
      until:Date.now()+12000
    };
    /* 立刻给一句反馈：状态文字平时要等下一次轮询（最长 1.5 秒）才更新，
       这段时间页面看起来像「点了没反应」。 */
    var pendingText=byId('shareStatusText');
    if(pendingText)pendingText.textContent=pending.label;
  }
  if(button){button.disabled=true;button.textContent=pending?pending.label:'执行中…';}
  fetchWithTimeout('/action/'+name,{method:'POST'},8000)
    .then(function(r){
      if(!r.ok)throw new Error('HTTP '+r.status);
      return r.json();
    })
    .then(function(){
      showToast(toastText||'已发送到 Unity','ok');
      /* 回读交给轮询：执行结果只能从 /config 的运行态字段看出来。 */
      setTimeout(load,600);
    })
    .catch(function(err){
      var msg=(err&&err.message)?err.message:'无法连接 Unity 服务';
      setStatusHold('操作失败 · '+msg,'err',8000);
      showToast('操作失败：'+msg,'err');
      notePollFailure();
      actionPending=null;
      if(button){button.textContent=label;button.disabled=false;}
    })
    .then(function(){
      /* 不改变运行态的动作（刷新映射表、打开浏览器）可以立刻恢复按钮；
         启停服务则交给 updateSharePanel，等运行态真的变了再解锁。 */
      if(button&&!pending){button.textContent=label;button.disabled=false;}
    });
}

byId('catalogRefresh').addEventListener('click',function(){
  postAction('catalog-refresh','正在扫描公共资源，稍后自动刷新结果',byId('catalogRefresh'));
});
byId('previewStart').addEventListener('click',function(){
  postAction('preview-start','正在启动预览服务',byId('previewStart'),{
    label:'正在启动…',
    expect:function(cfg){return !!cfg.previewServerRunning;}
  });
});
byId('previewStop').addEventListener('click',function(){
  postAction('preview-stop','正在停止预览服务',byId('previewStop'),{
    label:'正在停止…',
    expect:function(cfg){return !cfg.previewServerRunning;}
  });
});
byId('previewOpen').addEventListener('click',function(){
  postAction('preview-open','已在本机浏览器打开图库',byId('previewOpen'));
});

/* ---------- 组件类型模糊查询 ----------
   「Image / Button 组件类型」手打全名太费劲，候选由 Unity 侧提供：
   /typesuggest?kind=image|button&q=xxx。第一次请求时 Unity 还在扫程序集，
   服务端会先回 ready:false，这里过一会儿自动重试。 */
var TYPE_SUGGEST={imageComponentTypeName:'image',buttonComponentTypeName:'button'};
var suggestPanel=null,suggestOwner=null,suggestItems=[],suggestActive=-1,suggestToken=0;
var suggestTimer=null,suggestRetry=null;

function suggestEl(){
  if(suggestPanel)return suggestPanel;
  suggestPanel=document.createElement('div');
  suggestPanel.className='suggest-panel';
  suggestPanel.setAttribute('role','listbox');
  document.body.appendChild(suggestPanel);
  return suggestPanel;
}

/* 面板贴着输入框：下面放得下就往下弹，放不下就翻到输入框上方。 */
function suggestPlace(){
  if(!suggestOwner)return;
  var r=suggestOwner.getBoundingClientRect();
  var panel=suggestEl();
  panel.style.left=r.left+'px';
  panel.style.width=Math.max(r.width,280)+'px';
  var below=window.innerHeight-r.bottom-14;
  if(below<200&&r.top>below){
    panel.style.top='auto';
    panel.style.bottom=(window.innerHeight-r.top+6)+'px';
    panel.style.maxHeight=Math.max(140,r.top-20)+'px';
  }else{
    panel.style.bottom='auto';
    panel.style.top=(r.bottom+6)+'px';
    panel.style.maxHeight=Math.max(140,Math.min(300,below))+'px';
  }
}

function suggestClose(){
  if(suggestTimer){clearTimeout(suggestTimer);suggestTimer=null;}
  if(suggestRetry){clearTimeout(suggestRetry);suggestRetry=null;}
  if(suggestPanel){suggestPanel.classList.remove('is-open');suggestPanel.textContent='';}
  suggestItems=[];suggestActive=-1;suggestOwner=null;
}

/* 标出匹配到的字符：先找连续子串，找不到再按「字符依序出现」标出来（uib → U.I.B）。 */
function suggestRanges(text,query){
  var t=String(text).toLowerCase(),q=String(query||'').toLowerCase();
  if(!q)return [];
  var hit=t.indexOf(q);
  if(hit>=0)return [[hit,q.length]];
  var pos=[],at=0;
  for(var k=0;k<q.length;k++){
    var found=t.indexOf(q.charAt(k),at);
    if(found<0)return [];
    pos.push([found,1]);
    at=found+1;
  }
  return pos;
}

function suggestMark(text,query,cls){
  var span=el('span',cls);
  var ranges=suggestRanges(text,query);
  if(!ranges.length){span.textContent=text;return span;}
  var at=0;
  ranges.forEach(function(rg){
    if(rg[0]>at)span.appendChild(document.createTextNode(String(text).substring(at,rg[0])));
    span.appendChild(el('b',null,String(text).substr(rg[0],rg[1])));
    at=rg[0]+rg[1];
  });
  if(at<String(text).length)span.appendChild(document.createTextNode(String(text).substring(at)));
  return span;
}

function suggestRender(query,data){
  var panel=suggestEl();
  panel.textContent='';
  if(!data||!data.ready){
    panel.appendChild(el('div','suggest-note','正在读取本机的组件类型，稍等一下…'));
    panel.classList.add('is-open');
    suggestPlace();
    return;
  }
  suggestItems=data.items||[];
  if(!suggestItems.length){
    panel.appendChild(el('div','suggest-note',
      '没有匹配的类型（本机共 '+data.total+' 个可用类型）。也可以直接填完整类名。'));
    panel.classList.add('is-open');
    suggestPlace();
    return;
  }
  var lower=String(query||'').toLowerCase();
  suggestItems.forEach(function(it,index){
    var row=el('div','suggest-item');
    row.setAttribute('role','option');
    row.title=it.value;
    /* 短名/命名空间只在「查询是其连续子串」时高亮；uib 这种跳着匹配的情况
       高亮不出来，改由下面一行完整类型名 + 服务端给的匹配区间来表示。 */
    var nameHit=!!lower&&String(it.name||'').toLowerCase().indexOf(lower)>=0;
    var scopeHit=!nameHit&&!!lower&&String(it.scope||'').toLowerCase().indexOf(lower)>=0;
    row.appendChild(suggestMark(it.name,nameHit?query:'','suggest-name'));
    var scope=el('span','suggest-scope');
    if(it.scope)scope.appendChild(suggestMark(it.scope,scopeHit?query:'',null));
    row.appendChild(scope);
    /* 程序集名等于命名空间首段时（UnityEngine.UI 这种）就不重复显示一遍了 */
    var assembly=String(it.assembly||'');
    if(assembly&&assembly!==String(it.scope||'')&&assembly!==String(it.scope||'').split('.')[0])
      row.appendChild(el('span','suggest-assembly',assembly));
    if(!nameHit&&!scopeHit&&it.marks&&it.marks.length){
      var line=el('div','suggest-match');
      var at=0;
      it.marks.forEach(function(rg){
        if(rg[0]>at)line.appendChild(document.createTextNode(it.value.substring(at,rg[0])));
        line.appendChild(el('b',null,it.value.substr(rg[0],rg[1])));
        at=rg[0]+rg[1];
      });
      if(at<it.value.length)line.appendChild(document.createTextNode(it.value.substring(at)));
      row.appendChild(line);
    }
    /* mousedown 早于 blur：先 preventDefault 把焦点留在输入框上，否则面板会先被 blur 关掉 */
    row.addEventListener('mousedown',function(ev){ev.preventDefault();suggestPick(index);});
    panel.appendChild(row);
  });
  suggestActive=-1;
  panel.classList.add('is-open');
  suggestPlace();
}

function suggestMove(delta){
  if(!suggestPanel||!suggestItems.length)return;
  var total=suggestItems.length;
  suggestActive=suggestActive<0?(delta>0?0:total-1):(suggestActive+delta+total)%total;
  var rows=suggestPanel.querySelectorAll('.suggest-item');
  for(var i=0;i<rows.length;i++){
    var on=i===suggestActive;
    rows[i].classList.toggle('is-active',on);
    if(on&&rows[i].scrollIntoView)rows[i].scrollIntoView({block:'nearest'});
  }
}

function suggestPick(index){
  var it=suggestItems[index];
  if(!it||!suggestOwner)return;
  var input=suggestOwner;
  input.value=it.value;
  dirty[input.id]=true;
  suggestClose();
  setStatusHold('已填入 '+it.value+'，记得点保存同步','ok',5000);
}

function suggestQuery(kind,query,current){
  suggestToken++;
  var token=suggestToken;
  if(suggestRetry){clearTimeout(suggestRetry);suggestRetry=null;}
  /* cur 是字段里现有的值：空查询时服务端会把它排到第一行，方便直接确认。 */
  var url='/typesuggest?kind='+kind+'&q='+encodeURIComponent(query)+'&cur='+encodeURIComponent(current||'');
  fetchWithTimeout(url,{cache:'no-store'},5000)
    .then(function(r){if(!r.ok)throw new Error('HTTP '+r.status);return r.json();})
    .then(function(data){
      if(token!==suggestToken||!suggestOwner)return;
      suggestRender(query,data);
      if(!data||!data.ready){
        /* 主线程还在扫程序集：过一会儿再问一次，问到了自然就画出来 */
        suggestRetry=setTimeout(function(){
          if(token===suggestToken&&suggestOwner)suggestQuery(kind,query,current);
        },800);
      }
    })
    .catch(function(err){
      if(token!==suggestToken||!suggestOwner)return;
      var panel=suggestEl();
      panel.textContent='';
      panel.appendChild(el('div','suggest-note',
        '查询失败：'+((err&&err.message)||'无法连接 Unity 服务')));
      panel.classList.add('is-open');
      suggestPlace();
    });
}

function suggestOpen(input,showAll){
  var kind=TYPE_SUGGEST[input.id];
  if(!kind)return;
  suggestOwner=input;
  /* 聚焦时按空查询列全部候选：字段里通常已经有完整类名，拿它当查询只会剩一两行，
     反而看不到有什么可选。开始打字之后才按输入内容过滤。 */
  suggestQuery(kind,showAll?'':input.value.trim(),input.value.trim());
}

function suggestSchedule(input){
  if(suggestTimer)clearTimeout(suggestTimer);
  suggestTimer=setTimeout(function(){suggestOpen(input,false);},140);
}

Object.keys(TYPE_SUGGEST).forEach(function(id){
  var input=byId(id);
  if(!input)return;
  /* 关掉浏览器自带的自动填充/纠错，否则会和这个候选面板打架 */
  input.setAttribute('autocomplete','off');
  input.setAttribute('spellcheck','false');
  input.setAttribute('aria-autocomplete','list');
  input.addEventListener('input',function(){dirty[id]=true;suggestSchedule(input);});
  input.addEventListener('focus',function(){suggestOpen(input,true);});
  input.addEventListener('keydown',function(ev){
    var open=!!suggestPanel&&suggestPanel.classList.contains('is-open')&&suggestOwner===input;
    if(ev.key==='ArrowDown'){
      ev.preventDefault();
      if(open)suggestMove(1);else suggestOpen(input,true);
      return;
    }
    if(ev.key==='ArrowUp'){if(open){ev.preventDefault();suggestMove(-1);}return;}
    if(ev.key==='Escape'){if(open){ev.preventDefault();suggestClose();}return;}
    if(ev.key==='Enter'&&open&&suggestActive>=0){ev.preventDefault();suggestPick(suggestActive);return;}
    if(ev.key==='Tab'&&open&&suggestActive>=0){ev.preventDefault();suggestPick(suggestActive);return;}
    if(ev.key==='Enter'&&open)suggestClose();
  });
  input.addEventListener('blur',function(){
    setTimeout(function(){if(suggestOwner===input)suggestClose();},120);
  });
});
window.addEventListener('scroll',function(){if(suggestOwner)suggestPlace();},true);
window.addEventListener('resize',function(){if(suggestOwner)suggestPlace();});
document.addEventListener('mousedown',function(ev){
  if(!suggestOwner)return;
  if(suggestPanel&&suggestPanel.contains(ev.target))return;
  if(ev.target===suggestOwner)return;
  suggestClose();
});

byId('save').addEventListener('click',function(){post(payload(),'配置已同步到 Unity',byId('save'));});
byId('aiProvider').addEventListener('change',function(){
  dirty.aiProvider=true;refreshDetailVisibility();markSelectedCard();
  /* 换成别的 CLI，候选集完全不同，收掉旧面板免得看起来像还在选上一个模型的档位。 */
  comboClose('aiModel');comboClose('aiEffort');
});
/* 绑定下拉最迟放在这里：renderProviders 定义完了，clis 也由 load() 填过一遍。 */
comboSetup('aiModel','aiModelPanel');
comboSetup('aiEffort','aiEffortPanel');
byId('outputMode').addEventListener('change',function(){dirty.outputMode=true;});
byId('cleanupBackend').addEventListener('change',function(){
  dirty.cleanupBackend=true;updateCleanupBanner();
});
byId('autoCropNineSlice').addEventListener('change',function(){
  dirty.autoCropNineSlice=true;updateNineSliceBanner();
});
byId('previewServerPort').addEventListener('input',function(){dirty.previewServerPort=true;});
TEXT_FIELDS.forEach(function(id){
  var f=byId(id);
  if(f)f.addEventListener('input',function(){dirty[id]=true;});
});
byId('clearKey').addEventListener('click',function(){
  byId('aiApiKey').value='';
  post({aiClearApiKey:true},'已清除本机保存的 Key',byId('clearKey'));
});

var sections=[].slice.call(document.querySelectorAll('.content section[id]'));
var links=[].slice.call(document.querySelectorAll('.nav-item'));
function syncNav(){
  var cur=sections.length?sections[0].id:null;
  for(var i=0;i<sections.length;i++){
    if(sections[i].getBoundingClientRect().top<=150)cur=sections[i].id;
  }
  /* 滚到最底部时补一次：末尾分组如果比视口矮，它的 top 可能永远进不了 150px 阈值，
     高亮就会停在倒数第二组（新增第 4 组后踩到）。 */
  if(sections.length&&window.innerHeight+window.scrollY>=document.body.scrollHeight-2){
    cur=sections[sections.length-1].id;
  }
  links.forEach(function(a){a.classList.toggle('is-on',a.getAttribute('href')==='#'+cur);});
}
links.forEach(function(a){a.addEventListener('click',function(){
  links.forEach(function(b){b.classList.toggle('is-on',b===a);});
});});
window.addEventListener('scroll',syncNav,{passive:true});
window.addEventListener('resize',syncNav);
syncNav();

load();
setInterval(load,1500);
</script>
</body>
</html>";
    }
}
