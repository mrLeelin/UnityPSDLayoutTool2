namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using UnityEditor;
    using UnityEditor.Compilation;
    using UnityEditor.Callbacks;
    using UnityEngine;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;
    using UnityEngine.Rendering;
    using UnityEngine.UI;

    internal static class PsdCommonAssetPreviewServer
    {
        [Serializable] private sealed class Payload { public List<Item> items = new List<Item>(); }
        [Serializable] private sealed class Item { public string id; public string kind; public string name; public string path; public string size; public string image; }
        /// <summary>Cached PNG for one asset, kept across refreshes until its source file changes.</summary>
        private sealed class PreviewEntry { public byte[] png; public string version; }
        private const int MaxEncodesPerRefresh = 8;
        private const double RefreshIntervalSeconds = 2d;
        private static readonly object Sync = new object();
        private static TcpListener listener;
        private static Thread worker;
        private static Payload payload;
        private static Dictionary<string, string> texturePaths = new Dictionary<string, string>();

        /// <summary>Persistent preview cache. Never rebuilt wholesale: entries survive
        /// refreshes so an asset whose async preview was not ready keeps its earlier PNG.</summary>
        private static readonly Dictionary<string, PreviewEntry> PreviewCache = new Dictionary<string, PreviewEntry>();
        private static double nextPreviewRefresh;
        internal static int Port { get; private set; }
        internal static string Error { get; private set; }
        internal static bool IsRunning => listener != null;

        /// <summary>SessionState survives assembly reloads but is cleared when the Editor
        /// exits, which matches how long the preview service should stay alive.</summary>
        private const string ResumePortKey = "PsdLayoutTool2.PreviewServer.ResumePort";

        internal static bool Start(int port)
        {
            // 幂等：已经在这个端口上跑着就直接算成功。
            // 否则下面的 Shutdown 会先关掉自己再重新绑定，而 Shutdown 只要没把 socket
            // 放干净，重新绑定就会 WSAEADDRINUSE，还会留下一个没人 accept 的孤儿监听。
            if (IsRunning && Port == port)
            {
                return true;
            }

            Shutdown();
            Error = string.Empty;
            try
            {
                SessionState.SetInt(ResumePortKey, port);
                // Default preview cache holds ~30 entries; a larger library would keep
                // evicting previews so some prefabs never produced a thumbnail.
                try { AssetPreview.SetPreviewTextureCacheSize(512); } catch { }
                Refresh();
                listener = new TcpListener(IPAddress.Any, port);
                TryEnableAddressReuse(listener);
                listener.Start(); Port = port;
                worker = new Thread(Listen) { IsBackground = true, Name = "PSD Common Preview" };
                worker.Start(); return true;
            }
            catch (Exception exception)
            {
                // 注意顺序：Stop() 会清掉 Error，必须先 Stop 再把本次原因写回去。
                Stop();
                Error = DescribeListenError(port, exception);
                return false;
            }
        }

        /// <summary>Stops the service and forgets the resume port, so it stays down
        /// until started again. Used by the Stop button.</summary>
        internal static void Stop()
        {
            SessionState.EraseInt(ResumePortKey);
            Shutdown();
            // 主动停止之后，不该再把上一次的失败原因一直挂在界面上。
            Error = string.Empty;
        }

        /// <summary>Releases the socket without clearing the resume port, so the service
        /// comes back automatically after an assembly reload.</summary>
        private static void Shutdown()
        {
            TcpListener active = listener; listener = null; Port = 0;
            if (active != null)
            {
                try
                {
                    active.Stop();
                }
                catch (Exception exception)
                {
                    // 这里以前是裸 catch{}：Stop 一抛异常 socket 就被漏掉，端口永远绑不上，
                    // 而界面上只会显示一句「套接字地址只允许使用一次」，完全看不出真正的原因。
                    Debug.LogWarning("[PSDLayoutTool2] 预览服务关闭监听失败：" + exception.Message);
                }

                // Stop() 中途失败时补一刀，尽量别把端口漏在外面。
                try { active.Server.Close(); } catch { }
            }

            lock (Sync) { PreviewCache.Clear(); texturePaths = new Dictionary<string, string>(); }
        }

        /// <summary>给监听 socket 打开地址复用。Windows 上只要还有上一次的客户端连接停在
        /// TIME_WAIT，重新绑定同一端口就会失败；打开 ReuseAddress 才谈得上「停止后立刻重启」。</summary>
        private static void TryEnableAddressReuse(TcpListener target)
        {
            try
            {
                target.Server.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            catch (Exception)
            {
                /* 个别平台不允许改；真绑不上时由 Start 的 catch 统一报错。 */
            }
        }

        /// <summary>Windows 的网络错误文案由 FormatMessage 生成，尾部本来就带 \r\n，
        /// 经 Mono 回传时还可能跟一整段 \0 填充（实测 95 个）。直接显示会很难看。</summary>
        private static string CleanMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "未知错误";
            }

            int terminator = message.IndexOf('\0');
            if (terminator >= 0)
            {
                message = message.Substring(0, terminator);
            }

            return message.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        /// <summary>把绑不上端口的几种 socket 错误翻成「用户知道下一步该做什么」的话。
        /// 实测（Windows，本机复现）：同一端口上已经有一个监听 socket 时，
        /// 不带 ReuseAddress 报 10048「套接字地址只允许使用一次」，
        /// 带 ReuseAddress 反而报 10013「以访问权限不允许的方式…」——
        /// 两种都只说明「端口被占着」，光看原文根本不知道该换端口还是重启编辑器。</summary>
        private static string DescribeListenError(int port, Exception exception)
        {
            SocketException socketException = exception as SocketException;
            if (socketException != null)
            {
                if (socketException.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                    socketException.SocketErrorCode == SocketError.AccessDenied)
                {
                    return "端口 " + port + " 已被占用。可能是上一次的预览服务没退干净，" +
                           "也可能是别的程序在用这个端口。换一个端口再启动；" +
                           "如果占用的就是刚用过的端口，重启 Unity 才能释放它。";
                }

                if (socketException.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    return "端口 " + port + " 在本机不可用（通常是被系统或安全软件保留了）。换一个端口再试。";
                }
            }

            return "端口 " + port + " 无法监听：" + CleanMessage(exception.Message);
        }

        internal static string GetLocalAddress()
        {
            if (!IsRunning) return string.Empty;
            foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)) return "http://" + address + ":" + Port + "/";
            return "http://127.0.0.1:" + Port + "/";
        }

        [InitializeOnLoadMethod]
        private static void RegisterShutdown()
        {
            // Release the socket but keep the resume port: a recompile should not
            // take the service down for whoever is browsing it.
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting -= Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.update -= RefreshPreviews;
            EditorApplication.update += RefreshPreviews;
            EditorApplication.delayCall += Resume;
        }

        /// <summary>Restarts the service after an assembly reload if it was running before.
        /// Runs through delayCall so the AssetDatabase is ready to build the catalog.</summary>
        private static void Resume()
        {
            if (IsRunning) return;
            int port = SessionState.GetInt(ResumePortKey, 0);
            if (port < 1 || port > 65535) return;
            Start(port);
        }

        private static void RefreshPreviews()
        {
            if (!IsRunning || EditorApplication.timeSinceStartup < nextPreviewRefresh) return;
            nextPreviewRefresh = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            Refresh();
        }

        /// <summary>Rebuilds the catalog snapshot and publishes it together with the
        /// lookup tables under one lock, so a served item id always resolves.</summary>
        private static void Refresh()
        {
            try { BuildPayload(); } catch (Exception exception) { Error = exception.Message; }
        }
        private static void Listen()
        {
            while (listener != null)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => Handle(client));
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { return; }
            }
        }

        private static void Handle(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
            {
                stream.ReadTimeout = 5000;
                string request = reader.ReadLine();
                if (string.IsNullOrEmpty(request)) return;
                string[] parts = request.Split(' '); string path = parts.Length > 1 ? parts[1] : "/";
                while (!string.IsNullOrEmpty(reader.ReadLine())) { }
                if (parts[0] != "GET") { Write(stream, 405, "text/plain", Encoding.UTF8.GetBytes("GET only")); return; }
                if (path == "/") { Write(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page)); return; }
                if (path == "/api/catalog") { lock (Sync) Write(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))); return; }
                if (path.StartsWith("/asset/", StringComparison.Ordinal))
                {
                    string id = Uri.UnescapeDataString(path.Substring(7));
                    PreviewEntry entry;
                    lock (Sync) { PreviewCache.TryGetValue(id, out entry); }

                    // Only ever serve re-encoded PNG. Source files may be .psd/.tga/.exr,
                    // which no browser can decode even though the URL claims image/png.
                    if (entry != null && entry.png != null) { Write(stream, 200, "image/png", entry.png); return; }
                }
                Write(stream, 404, "text/plain", Encoding.UTF8.GetBytes("Not found"));
            }
        }

        private static Payload BuildPayload()
        {
            PsdCommonAssetCatalog catalog = PsdCommonAssetCatalog.Load();
            if (catalog == null || catalog.needsRefresh) catalog = PsdCommonAssetCatalog.CreateOrRefresh();
            var result = new Payload();
            var paths = new Dictionary<string, string>();
            var live = new HashSet<string>();
            string root = Directory.GetParent(Application.dataPath).FullName;
            int budget = MaxEncodesPerRefresh;

            // The page's Copy button works on real asset names, so it must show the name of the
            // file on disk (Common_Prefab_Btn). The catalog only stores the stripped key (Btn)
            // plus the path, so derive the full name from the path rather than the key.
            PsdCommonAssetNamingSnapshot naming = PsdLayoutProjectSettings.instance.ResolveCommonAssetNaming();

            foreach (PsdCommonPrefabCatalogEntry entry in catalog.prefabs)
            {
                if (entry == null || entry.prefab == null || string.IsNullOrEmpty(entry.guid)) continue;
                string fullName = PrefixedName(entry.assetPath, entry.key, naming.prefabPrefix, "Common_Prefab_");
                Item item = Add(result, paths, entry.guid, "Prefab", fullName, entry.assetPath, 0, 0, root);
                live.Add(entry.guid);
                if (entry.prefab.transform is RectTransform)
                    EnsureUiPreview(entry.guid, item, entry.prefab, ref budget);
                else
                    EnsurePreview(entry.guid, item, paths, ref budget, () => AssetPreview.GetAssetPreview(entry.prefab), Rect.zero);
            }

            foreach (PsdCommonTextureCatalogEntry entry in catalog.textures)
            {
                if (entry == null || entry.sprite == null || string.IsNullOrEmpty(entry.guid)) continue;
                Sprite sprite = entry.sprite;
                string fullName = PrefixedName(entry.assetPath, entry.key, naming.texturePrefix, "Common_Texture_");
                Item item = Add(result, paths, entry.guid, "Texture", fullName, entry.assetPath, sprite.rect.width, sprite.rect.height, root);
                live.Add(entry.guid);
                EnsurePreview(entry.guid, item, paths, ref budget, () => sprite.texture, sprite.rect);
            }

            lock (Sync)
            {
                // Drop only entries whose asset left the catalog; keep every other PNG.
                var stale = new List<string>();
                foreach (KeyValuePair<string, PreviewEntry> pair in PreviewCache)
                    if (!live.Contains(pair.Key)) stale.Add(pair.Key);
                foreach (string key in stale) PreviewCache.Remove(key);

                texturePaths = paths;
                payload = result;
            }
            return result;
        }

        /// <summary>Encodes a preview PNG if missing or outdated, then points the item at it.
        /// Leaves <see cref="Item.image"/> empty while the async preview is not ready, so the
        /// page shows no element rather than a broken image; the next refresh fills it in.</summary>
        private static void EnsurePreview(string id, Item item, Dictionary<string, string> paths, ref int budget, Func<Texture> resolve, Rect region)
        {
            string version = DescribeVersion(paths, id);
            PreviewEntry cached;
            lock (Sync) { PreviewCache.TryGetValue(id, out cached); }
            if (cached != null && cached.png != null && cached.version == version)
            {
                item.image = "/asset/" + Uri.EscapeDataString(id);
                return;
            }

            if (budget <= 0) return;
            Texture source = null;
            try { source = resolve(); } catch { }
            if (source == null) return;

            budget--;
            byte[] png = EncodeThumbnail(source, region);
            if (png == null) return;
            lock (Sync) { PreviewCache[id] = new PreviewEntry { png = png, version = version }; }
            item.image = "/asset/" + Uri.EscapeDataString(id);
        }

        private static string DescribeVersion(Dictionary<string, string> paths, string id)
        {
            string file;
            if (!paths.TryGetValue(id, out file) || string.IsNullOrEmpty(file)) return "0";
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists) return "0";
                return info.Length + ":" + info.LastWriteTimeUtc.Ticks;
            }
            catch { return "0"; }
        }

        // AssetPreview does not reliably render Canvas graphics. Render UI in an isolated
        // preview scene and cache the PNG, including changes to referenced sprites/materials.
        private static void EnsureUiPreview(string id, Item item, GameObject prefab, ref int budget)
        {
            string version = AssetDatabase.GetAssetDependencyHash(item.path).ToString();
            PreviewEntry cached;
            lock (Sync) { PreviewCache.TryGetValue(id, out cached); }
            if (cached != null && cached.version == version)
            {
                item.image = "/asset/" + Uri.EscapeDataString(id);
                return;
            }
            if (budget <= 0) return;
            budget--;
            try
            {
                byte[] png = CaptureUiPrefab(prefab);
                lock (Sync) { PreviewCache[id] = new PreviewEntry { png = png, version = version }; }
                item.image = "/asset/" + Uri.EscapeDataString(id);
            }
            catch (Exception exception) { Error = item.path + ": " + exception.Message; }
        }

        private static byte[] CaptureUiPrefab(GameObject prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var root = new GameObject("Common Prefab Preview", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var sourceRect = (RectTransform)prefab.transform;
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(
                    Mathf.Max(1, sourceRect.rect.width), Mathf.Max(1, sourceRect.rect.height));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.SetActive(true);
                foreach (var nested in instance.GetComponentsInChildren<Canvas>(true))
                    nested.renderMode = RenderMode.WorldSpace;
                foreach (var scaler in instance.GetComponentsInChildren<CanvasScaler>(true))
                    scaler.enabled = false;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)instance.transform);
                Canvas.ForceUpdateCanvases();

                var bounds = new Bounds();
                bool hasBounds = false;
                var corners = new Vector3[4];
                foreach (var graphic in instance.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    graphic.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        if (!hasBounds) { bounds = new Bounds(corner, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(corner);
                    }
                }
                if (!hasBounds) throw new InvalidOperationException("Prefab has no visible UI graphics.");
                float width = Mathf.Max(1, bounds.size.x) * 1.1f;
                float height = Mathf.Max(1, bounds.size.y) * 1.1f;
                float scale = Mathf.Min(1, 512f / Mathf.Max(width, height));
                target = RenderTexture.GetTemporary(Mathf.Max(1, Mathf.CeilToInt(width * scale)),
                    Mathf.Max(1, Mathf.CeilToInt(height * scale)), 24, RenderTextureFormat.ARGB32);
                var cameraObject = new GameObject("Common Prefab Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = height / 2;
                camera.transform.position = bounds.center - Vector3.forward * 1000;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000 + bounds.size.z;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else
                {
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                        throw new InvalidOperationException("Render pipeline does not support preview rendering.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>Re-encodes any readable or unreadable texture to PNG through the GPU.
        /// Uses an sRGB render target so colors match the Editor under Linear color space,
        /// and reads back only <paramref name="region"/> when the asset is an atlas sub-sprite.</summary>
        private static byte[] EncodeThumbnail(Texture source, Rect region)
        {
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                int width = source.width, height = source.height;
                if (width <= 0 || height <= 0) return null;

                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, target);
                RenderTexture.active = target;

                Rect read = region.width > 0f && region.height > 0f
                    ? new Rect(
                        Mathf.Clamp(region.x, 0f, width),
                        Mathf.Clamp(region.y, 0f, height),
                        Mathf.Min(region.width, width - Mathf.Clamp(region.x, 0f, width)),
                        Mathf.Min(region.height, height - Mathf.Clamp(region.y, 0f, height)))
                    : new Rect(0f, 0f, width, height);
                if (read.width < 1f || read.height < 1f) return null;

                readable = new Texture2D((int)read.width, (int)read.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(read, 0, 0);
                readable.Apply();
                return readable.EncodeToPNG();
            }
            catch { return null; }
            finally
            {
                // Restore the active target on every path; an early return used to leave it dangling.
                RenderTexture.active = previous;
                if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>Rebuilds the full asset name the page should display and copy.
        /// <paramref name="key"/> is the catalog's stripped key and is deliberately NOT used as the
        /// name: for a file called <c>Common_Prefab_KaTongFbBtn_1.prefab</c> the key is only
        /// <c>KaTongFbBtn_1</c>, so handing the key to the Copy button silently drops the prefix.
        /// Prefer the file name on disk; fall back to prefix + key only if the path is unusable.</summary>
        private static string PrefixedName(string assetPath, string key, string configuredPrefix, string defaultPrefix)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (!string.IsNullOrEmpty(fileName)) return fileName;
            }

            return (string.IsNullOrEmpty(configuredPrefix) ? defaultPrefix : configuredPrefix) + key;
        }

        private static Item Add(Payload result, Dictionary<string, string> paths, string id, string kind, string name, string assetPath, float width, float height, string root)
        {
            string fullPath = string.IsNullOrEmpty(assetPath) ? string.Empty : Path.Combine(root, assetPath);
            long bytes = !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            bool sized = width > 0f && height > 0f;
            var item = new Item
            {
                id = id,
                kind = kind,
                name = name,
                path = assetPath,
                size = sized ? width + " x " + height + " px | " + bytes / 1024 + " KB" : bytes / 1024 + " KB",
                image = string.Empty
            };
            result.items.Add(item);
            if (!string.IsNullOrEmpty(fullPath)) paths[id] = fullPath;
            return item;
        }

        private static void Write(NetworkStream stream, int status, string type, byte[] body)
        {
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " OK\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length);
        }

        private const string Page = "<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>Common Assets</title><style>body{margin:0;background:#101318;color:#e8edf5;font:14px Arial}header{padding:20px 28px;border-bottom:1px solid #293341}h1{margin:0;font-size:20px}input{margin-top:14px;width:280px;max-width:100%;padding:9px;background:#1b222c;border:1px solid #34465e;color:#fff}.grid{padding:20px;display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:12px}.card{background:#1b222c;border:1px solid #304157;padding:12px}.card img{width:100%;height:120px;object-fit:contain;background:#101318}.name{font-weight:bold;margin-top:8px}.meta{color:#aab6c5;font-size:12px;margin-top:5px;word-break:break-all}button{margin-top:9px;background:#2778d8;color:#fff;border:0;padding:6px 10px;cursor:pointer}button.ok{background:#1f9d55}button.bad{background:#c0392b}</style><header><h1>Common Asset Library</h1><input id=q placeholder='Search name or path'></header><main class=grid id=g></main><script> var all=[],sig=''; var qEl=document.getElementById('q'),gEl=document.getElementById('g'); /* 复制文本。页面常常是通过局域网 IP（http://172.16.2.138:52343/）打开的， 那不是「安全上下文」，navigator.clipboard 直接是 undefined， 点一次就抛 TypeError 且页面上毫无反应。所以这里必须带 execCommand 兜底。 */ function copyText(t){ if(navigator.clipboard&&navigator.clipboard.writeText){ try{return navigator.clipboard.writeText(t).then(function(){return true},function(){return legacyCopy(t)})}catch(e){} } return Promise.resolve(legacyCopy(t)); } function legacyCopy(t){ var ta=document.createElement('textarea'); ta.value=t;ta.setAttribute('readonly',''); ta.style.cssText='position:fixed;top:0;left:0;width:1px;height:1px;opacity:0;border:0;padding:0;margin:0'; document.body.appendChild(ta); var ok=false; try{ta.focus();ta.select();ta.setSelectionRange(0,ta.value.length);ok=document.execCommand('copy')}catch(e){ok=false} document.body.removeChild(ta); return ok; } function flash(b,txt,bad){ clearTimeout(b._t);b.textContent=txt;b.className=bad?'bad':'ok'; b._t=setTimeout(function(){b.textContent='Copy name';b.className=''},1400); } function makeCard(x){ var e=document.createElement('article');e.className='card'; if(x.image){var i=document.createElement('img');i.src=x.image;e.appendChild(i)} var n=document.createElement('div');n.className='name';n.textContent=x.kind+' · '+x.name;e.appendChild(n); var s=document.createElement('div');s.className='meta';s.textContent=x.size;e.appendChild(s); var p=document.createElement('div');p.className='meta';p.textContent=x.path;e.appendChild(p); var b=document.createElement('button');b.type='button';b.textContent='Copy name'; b.onclick=function(){ copyText(x.name).then(function(ok){ if(ok){flash(b,'已复制')} else{flash(b,'复制失败',true);window.prompt('浏览器拦下了自动复制，请手动复制：',x.name)} }); }; e.appendChild(b);return e; } function draw(){ var v=qEl.value.toLowerCase(),f=document.createDocumentFragment(); gEl.innerHTML=''; all.filter(function(x){return ((x.name||'')+(x.path||'')).toLowerCase().indexOf(v)>=0}).forEach(function(x){f.appendChild(makeCard(x))}); gEl.appendChild(f); } /* 轮询只在目录内容真的变了时才重画：否则每 2 秒重建一次 DOM， 既让图片反复重新加载，也会把刚点出来的「已复制」冲掉。 */ function load(){ fetch('/api/catalog').then(function(r){return r.json()}).then(function(d){ var items=d.items||[],s=JSON.stringify(items); if(s===sig)return; sig=s;all=items;draw(); }).catch(function(){}); } qEl.oninput=draw; load(); setInterval(load,2000); </script>";
    }
}
