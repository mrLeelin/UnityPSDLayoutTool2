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
            Shutdown(); Error = string.Empty;
            try
            {
                SessionState.SetInt(ResumePortKey, port);
                // Default preview cache holds ~30 entries; a larger library would keep
                // evicting previews so some prefabs never produced a thumbnail.
                try { AssetPreview.SetPreviewTextureCacheSize(512); } catch { }
                Refresh();
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(); Port = port;
                worker = new Thread(Listen) { IsBackground = true, Name = "PSD Common Preview" };
                worker.Start(); return true;
            }
            catch (Exception exception) { Error = exception.Message; Stop(); return false; }
        }

        /// <summary>Stops the service and forgets the resume port, so it stays down
        /// until started again. Used by the Stop button.</summary>
        internal static void Stop()
        {
            SessionState.EraseInt(ResumePortKey);
            Shutdown();
        }

        /// <summary>Releases the socket without clearing the resume port, so the service
        /// comes back automatically after an assembly reload.</summary>
        private static void Shutdown()
        {
            TcpListener active = listener; listener = null; Port = 0;
            if (active != null) { try { active.Stop(); } catch { } }
            lock (Sync) { PreviewCache.Clear(); texturePaths = new Dictionary<string, string>(); }
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

            foreach (PsdCommonPrefabCatalogEntry entry in catalog.prefabs)
            {
                if (entry == null || entry.prefab == null || string.IsNullOrEmpty(entry.guid)) continue;
                Item item = Add(result, paths, entry.guid, "Prefab", entry.key, entry.assetPath, 0, 0, root);
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
                Item item = Add(result, paths, entry.guid, "Texture", entry.key, entry.assetPath, sprite.rect.width, sprite.rect.height, root);
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

        private const string Page = "<!doctype html><meta charset=utf-8><title>Common Assets</title><style>body{margin:0;background:#101318;color:#e8edf5;font:14px Arial}header{padding:20px 28px;border-bottom:1px solid #293341}h1{margin:0;font-size:20px}input{margin-top:14px;width:280px;padding:9px;background:#1b222c;border:1px solid #34465e;color:#fff}.grid{padding:20px;display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:12px}.card{background:#1b222c;border:1px solid #304157;padding:12px}.card img{width:100%;height:120px;object-fit:contain;background:#101318}.name{font-weight:bold;margin-top:8px}.meta{color:#aab6c5;font-size:12px;margin-top:5px;word-break:break-all}button{margin-top:9px;background:#2778d8;color:#fff;border:0;padding:6px 10px;cursor:pointer}</style><header><h1>Common Asset Library</h1><input id=q placeholder='Search name or path'></header><main class=grid id=g></main><script>let all=[];async function load(){all=(await fetch('/api/catalog').then(r=>r.json())).items;draw()}load();setInterval(load,2000);q.oninput=draw;function draw(){let qv=q.value.toLowerCase();g.innerHTML='';all.filter(x=>(x.name+x.path).toLowerCase().includes(qv)).forEach(x=>{let e=document.createElement('article');e.className='card';if(x.image){let i=document.createElement('img');i.src=x.image;e.append(i)}e.innerHTML+='<div class=name>'+x.kind+' · '+x.name+'</div><div class=meta>'+x.size+'</div><div class=meta>'+x.path+'</div>';let b=document.createElement('button');b.textContent='Copy name';b.onclick=()=>navigator.clipboard.writeText(x.name);e.append(b);g.append(e)})}</script>";
    }
}
