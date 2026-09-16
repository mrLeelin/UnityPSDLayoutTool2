using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>本机同源工作台。请求入队与 Unity 执行完成分别确认。</summary>
    [InitializeOnLoad]
    public sealed class AiOrganizerWebServer : IDisposable
    {
        internal static AiOrganizerWebServer Current { get; private set; }
        internal string Url => _origin + "/#" + _token;
        internal AiOrganizerWebSession Session { get; private set; }
        readonly object _gate = new object();
        readonly string _token = Guid.NewGuid().ToString("N");
        string _origin, _html;
        HttpListener _listener;
        Thread _thread;
        volatile bool _running;
        volatile string _cachedState = "{}";
        volatile byte[] _cachedPreview;
        PendingCommand _pending;
        double _nextRefresh;
        double _nextTick;
        long _lastRequestTicks = DateTime.UtcNow.Ticks;
        Action _signalTick;

        sealed class PendingCommand { internal string Id, Json; }

        static AiOrganizerWebServer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopCurrent;
            EditorApplication.quitting += StopCurrent;
        }

        /// <summary>供编辑器自动化从已保存的 PSD 编辑 Prefab 打开工作台。</summary>
        public static string OpenAsset(string assetPath, bool openBrowser = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var source = asset != null ? asset.GetComponent<Psd2UIFormConverter>() : null;
            if (source == null) throw new InvalidOperationException("请选择包含 PSD 编辑树的 Prefab。");
            return Open(source, openBrowser);
        }

        [MenuItem("Tools/PSD2UIForm/AI 整理 UI（网页）")]
        static void OpenSelected()
        {
            var source = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Psd2UIFormConverter>() : null;
            if (source == null) { EditorUtility.DisplayDialog("AI 整理 UI", "请先选中 PSD 编辑树根节点。", "确定"); return; }
            Open(source);
        }

        [MenuItem("Tools/PSD2UIForm/AI 整理 UI（原生窗口）")]
        static void OpenNative()
        {
            var source = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Psd2UIFormConverter>() : null;
            if (source != null) AiOrganizerWindow.Open(source);
            else EditorUtility.DisplayDialog("AI 整理 UI", "请先选中 PSD 编辑树根节点。", "确定");
        }

        internal static string Open(Psd2UIFormConverter source, bool openBrowser = true)
        {
            if (Current == null || Current.Session.Source != source || Current.Session.State.sourceChanged || !string.IsNullOrEmpty(Current.Session.State.publishedPath))
            {
                StopCurrent();
                var server = new AiOrganizerWebServer();
                try { server.Start(source); Current = server; }
                catch { server.Dispose(); throw; }
            }
            if (openBrowser) Application.OpenURL(Current.Url);
            return Current.Url;
        }

        void Start(Psd2UIFormConverter source)
        {
            var page = Resources.Load<TextAsset>("Psd2UIFormOrganizer");
            if (page == null) throw new FileNotFoundException("找不到 Psd2UIFormOrganizer.html 工作台资源。");
            _html = page.text;
            var tickMethod = typeof(EditorApplication).GetMethod("SignalTick", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (tickMethod != null) _signalTick = (Action)Delegate.CreateDelegate(typeof(Action), tickMethod);
            Session = new AiOrganizerWebSession(source);
            // 让操作系统分配空闲端口，多个 Editor 各自拥有独立工作台。
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            _origin = "http://127.0.0.1:" + port;
            _listener = new HttpListener(); _listener.Prefixes.Add(_origin + "/"); _listener.Start();
            Cache(); _running = true;
            EditorApplication.update += Update;
            _thread = new Thread(Listen) { IsBackground = true, Name = "PSD UI Workbench" }; _thread.Start();
        }

        void Cache()
        {
            _cachedPreview = Session.PreviewPng;
            _cachedState = JsonUtility.ToJson(Session.State);
        }

        void Update()
        {
            if (!_running) return;
            PendingCommand pending;
            lock (_gate) pending = _pending;
            if (pending != null)
            {
                Session.State.commandError = "";
                try { Session.Execute(JsonUtility.FromJson<AiOrganizerWebCommand>(pending.Json)); }
                catch (Exception ex) { Session.State.commandError = ex.Message; }
                Session.State.lastRequestId = pending.Id;
                Cache();
                lock (_gate) _pending = null;
            }
            if (EditorApplication.timeSinceStartup >= _nextRefresh)
            {
                _nextRefresh = EditorApplication.timeSinceStartup + 0.4;
                Session.Tick(); Cache();
            }
            if (EditorApplication.timeSinceStartup >= _nextTick &&
                (Session.State.running || pending != null || DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastRequestTicks) < TimeSpan.TicksPerSecond * 5))
            {
                _nextTick = EditorApplication.timeSinceStartup + 0.1;
                if (_signalTick != null) _signalTick();
                else EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        void Listen()
        {
            var listener = _listener;
            while (_running)
            {
                HttpListenerContext context;
                try { context = listener.GetContext(); }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                try { Handle(context); }
                catch (Exception) { try { Reply(context, 500, "text/plain", "工作台请求失败。"); } catch { } }
                finally { try { context.Response.Close(); } catch { } }
            }
        }

        internal static bool IsAuthorized(string origin, string expectedOrigin, string token, string expectedToken)
        {
            return !string.IsNullOrEmpty(expectedToken) && token == expectedToken &&
                (string.IsNullOrEmpty(origin) || string.Equals(origin, expectedOrigin, StringComparison.Ordinal));
        }

        void Handle(HttpListenerContext context)
        {
            var request = context.Request;
            if (!request.IsLocal || request.Url.GetLeftPart(UriPartial.Authority) != _origin) { Reply(context, 403, "text/plain", "仅允许本机工作台访问。"); return; }
            string path = request.Url.AbsolutePath;
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            if (path == "/" && request.HttpMethod == "GET") { Reply(context, 200, "text/html; charset=utf-8", _html); return; }
            if (path == "/favicon.ico" && request.HttpMethod == "GET") { Reply(context, 204, "image/x-icon", ""); return; }
            string token = path == "/preview.png" ? request.QueryString["token"] : request.Headers["X-Organizer-Token"];
            if (!IsAuthorized(request.Headers["Origin"], _origin, token, _token)) { Reply(context, 403, "text/plain", "会话已失效，请从 Unity 重新打开。"); return; }
            if (path == "/state" && request.HttpMethod == "GET")
            {
                Interlocked.Exchange(ref _lastRequestTicks, DateTime.UtcNow.Ticks);
                Reply(context, 200, "application/json; charset=utf-8", _cachedState); return;
            }
            if (path == "/preview.png" && request.HttpMethod == "GET")
            {
                var png = _cachedPreview;
                if (png == null) { Reply(context, 404, "text/plain", "尚未生成预览。"); return; }
                context.Response.ContentType = "image/png"; context.Response.ContentLength64 = png.Length;
                context.Response.OutputStream.Write(png, 0, png.Length); return;
            }
            if (path == "/command" && request.HttpMethod == "POST")
            {
                if (request.ContentLength64 < 0 || request.ContentLength64 > 32768) { Reply(context, 413, "text/plain", "反馈过长。"); return; }
                string json;
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8)) json = reader.ReadToEnd();
                string id = Guid.NewGuid().ToString("N");
                lock (_gate)
                {
                    if (_pending != null) { Reply(context, 409, "text/plain", "Unity 正在执行上一条命令，请稍后重试。"); return; }
                    _pending = new PendingCommand { Id = id, Json = json };
                }
                // HTTP 线程不调用 Unity API；主线程 Update 持续取出队列并刷新执行结果。
                Interlocked.Exchange(ref _lastRequestTicks, DateTime.UtcNow.Ticks);
                Reply(context, 202, "application/json", "{\"requestId\":\"" + id + "\",\"queued\":true}"); return;
            }
            Reply(context, 404, "text/plain", "请求不存在。");
        }

        static void Reply(HttpListenerContext context, int status, string type, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            context.Response.StatusCode = status; context.Response.ContentType = type;
            context.Response.ContentLength64 = bytes.Length; context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        internal static void StopCurrent() { var server = Current; Current = null; server?.Dispose(); }

        public void Dispose()
        {
            _running = false;
            EditorApplication.update -= Update;
            _listener?.Close(); _listener = null;
            if (_thread != null && _thread.IsAlive) _thread.Join(500);
            Session?.Dispose();
        }
    }
}
