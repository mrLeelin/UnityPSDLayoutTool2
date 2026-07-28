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

    internal static class PsdCommonAssetPreviewServer
    {
        [Serializable] private sealed class Payload { public List<Item> items = new List<Item>(); }
        [Serializable] private sealed class Item { public string id; public string kind; public string name; public string path; public string size; public string image; }
        private static readonly object Sync = new object();
        private static TcpListener listener;
        private static Thread worker;
        private static Payload payload;
        private static Dictionary<string, string> texturePaths = new Dictionary<string, string>();
        private static Dictionary<string, byte[]> previewImages = new Dictionary<string, byte[]>();
        private static double nextPreviewRefresh;
        internal static int Port { get; private set; }
        internal static string Error { get; private set; }
        internal static bool IsRunning => listener != null;

        internal static bool Start(int port)
        {
            Stop(); Error = string.Empty;
            try
            {
                payload = BuildPayload();
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(); Port = port;
                worker = new Thread(Listen) { IsBackground = true, Name = "PSD Common Preview" };
                worker.Start(); return true;
            }
            catch (Exception exception) { Error = exception.Message; Stop(); return false; }
        }

        internal static void Stop()
        {
            TcpListener active = listener; listener = null; Port = 0;
            if (active != null) { try { active.Stop(); } catch { } }
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
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting -= Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.update -= RefreshPreviews;
            EditorApplication.update += RefreshPreviews;
        }

        private static void RefreshPreviews()
        {
            if (!IsRunning || EditorApplication.timeSinceStartup < nextPreviewRefresh) return;
            nextPreviewRefresh = EditorApplication.timeSinceStartup + 2d;
            try { payload = BuildPayload(); } catch { }
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
                    string id = Uri.UnescapeDataString(path.Substring(7)); string file;
                    byte[] preview;
                    lock (Sync)
                    {
                        if (previewImages.TryGetValue(id, out preview)) { Write(stream, 200, "image/png", preview); return; }
                        texturePaths.TryGetValue(id, out file);
                    }
                    if (!string.IsNullOrEmpty(file) && File.Exists(file)) { Write(stream, 200, "image/png", File.ReadAllBytes(file)); return; }
                }
                Write(stream, 404, "text/plain", Encoding.UTF8.GetBytes("Not found"));
            }
        }

        private static Payload BuildPayload()
        {
            PsdCommonAssetCatalog catalog = PsdCommonAssetCatalog.Load();
            if (catalog == null || catalog.needsRefresh) catalog = PsdCommonAssetCatalog.CreateOrRefresh();
            var result = new Payload(); var paths = new Dictionary<string, string>(); var previews = new Dictionary<string, byte[]>(); string root = Directory.GetParent(Application.dataPath).FullName;
            foreach (PsdCommonPrefabCatalogEntry entry in catalog.prefabs)
            {
                Add(result, paths, entry.guid, "Prefab", entry.key, entry.assetPath, 0, 0, false, root);
                Texture2D thumbnail = AssetPreview.GetAssetPreview(entry.prefab);
                if (thumbnail == null)
                {
                    AssetPreview.GetAssetPreview(entry.prefab);
                    continue;
                }
                if (thumbnail != null)
                {
                    byte[] png = EncodeThumbnail(thumbnail);
                    if (png != null)
                    {
                        previews[entry.guid] = png;
                        result.items[result.items.Count - 1].image = "/asset/" + Uri.EscapeDataString(entry.guid);
                    }
                }
            }
            foreach (PsdCommonTextureCatalogEntry entry in catalog.textures)
                Add(result, paths, entry.guid, "Texture", entry.key, entry.assetPath, entry.sprite.rect.width, entry.sprite.rect.height, true, root);
            lock (Sync) { texturePaths = paths; previewImages = previews; }
            return result;
        }

        private static byte[] EncodeThumbnail(Texture2D source)
        {
            try
            {
                var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, target); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); readable.Apply();
                byte[] png = readable.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(readable); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); return png;
            }
            catch { return null; }
        }

        private static void Add(Payload result, Dictionary<string, string> paths, string id, string kind, string name, string assetPath, float width, float height, bool image, string root)
        {
            string fullPath = Path.Combine(root, assetPath); long bytes = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            result.items.Add(new Item { id = id, kind = kind, name = name, path = assetPath, size = image ? width + " x " + height + " px | " + bytes / 1024 + " KB" : bytes / 1024 + " KB", image = image ? "/asset/" + Uri.EscapeDataString(id) : string.Empty });
            if (image) paths[id] = fullPath;
        }

        private static void Write(NetworkStream stream, int status, string type, byte[] body)
        {
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " OK\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length);
        }

        private const string Page = "<!doctype html><meta charset=utf-8><title>Common Assets</title><style>body{margin:0;background:#101318;color:#e8edf5;font:14px Arial}header{padding:20px 28px;border-bottom:1px solid #293341}h1{margin:0;font-size:20px}input{margin-top:14px;width:280px;padding:9px;background:#1b222c;border:1px solid #34465e;color:#fff}.grid{padding:20px;display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:12px}.card{background:#1b222c;border:1px solid #304157;padding:12px}.card img{width:100%;height:120px;object-fit:contain;background:#101318}.name{font-weight:bold;margin-top:8px}.meta{color:#aab6c5;font-size:12px;margin-top:5px;word-break:break-all}button{margin-top:9px;background:#2778d8;color:#fff;border:0;padding:6px 10px;cursor:pointer}</style><header><h1>Common Asset Library</h1><input id=q placeholder='Search name or path'></header><main class=grid id=g></main><script>let all=[];async function load(){all=(await fetch('/api/catalog').then(r=>r.json())).items;draw()}load();setInterval(load,2000);q.oninput=draw;function draw(){let qv=q.value.toLowerCase();g.innerHTML='';all.filter(x=>(x.name+x.path).toLowerCase().includes(qv)).forEach(x=>{let e=document.createElement('article');e.className='card';if(x.image){let i=document.createElement('img');i.src=x.image;e.append(i)}e.innerHTML+='<div class=name>'+x.kind+' · '+x.name+'</div><div class=meta>'+x.size+'</div><div class=meta>'+x.path+'</div>';let b=document.createElement('button');b.textContent='Copy name';b.onclick=()=>navigator.clipboard.writeText(x.name);e.append(b);g.append(e)})}</script>";
    }
}
