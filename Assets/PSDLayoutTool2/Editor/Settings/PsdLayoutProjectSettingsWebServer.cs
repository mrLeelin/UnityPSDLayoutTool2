namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using UnityEditor;
    using UnityEngine;

    internal sealed class PsdLayoutProjectSettingsWebServer
    {
        private const int Port = 9527;
        private const string Url = "http://localhost:9527/";
        private readonly object gate = new object();
        private HttpListener listener;
        private volatile bool running;
        private volatile string cachedJson;
        private string pendingJson;
        private PsdLayoutProjectSettings settings;
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
                Debug.LogError("[PSDLayoutTool2] 网页设置服务启动失败: " + e.Message);
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
            lock (gate) { json = pendingJson; pendingJson = null; }
            if (!string.IsNullOrEmpty(json)) ApplyJson(json);
            RefreshCache();
        }

        private void RefreshCache()
        {
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            serialized.Update();
            cachedJson = JsonUtility.ToJson(ReadSnapshot(serialized));
        }

        private void ListenLoop()
        {
            while (running)
            {
                try { Handle(listener.GetContext()); }
                catch { if (!running) break; }
            }
        }

        private void Handle(HttpListenerContext context)
        {
            var response = context.Response;
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
            string path = context.Request.Url == null ? "/" : context.Request.Url.AbsolutePath;
            if (context.Request.HttpMethod == "OPTIONS") { response.StatusCode = 204; response.Close(); return; }
            if (path == "/" || path == "/index.html") { Write(response, Page()); return; }
            if (path == "/config" && context.Request.HttpMethod == "GET") { Write(response, cachedJson ?? "{}"); return; }
            if (path == "/save" && context.Request.HttpMethod == "POST")
            {
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    lock (gate) pendingJson = reader.ReadToEnd();
                EditorApplication.QueuePlayerLoopUpdate();
                Write(response, "{\"queued\":true}");
                return;
            }
            response.StatusCode = 404; response.Close();
        }

        private static void Write(HttpListenerResponse response, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            response.ContentType = text.TrimStart().StartsWith("{") ? "application/json; charset=utf-8" : "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            using (Stream stream = response.OutputStream) stream.Write(bytes, 0, bytes.Length);
        }

        private void ApplyJson(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<SettingsSnapshot>(json);
                var serialized = new SerializedObject(settings);
                serialized.Update();
                Undo.RecordObject(settings, "Update PSD Layout Tool web settings");
                SetInt(serialized, "outputSettings.outputMode", data.outputMode);
                SetString(serialized, "outputSettings.outputFolderName", data.outputFolderName);
                SetString(serialized, "outputSettings.atlasOutputPath", data.atlasOutputPath);
                SetString(serialized, "outputSettings.textureOutputPath", data.textureOutputPath);
                SetString(serialized, "outputSettings.prefabOutputPath", data.prefabOutputPath);
                SetString(serialized, "uiComponentSettings.imageComponentTypeName", data.imageComponentTypeName);
                SetString(serialized, "uiComponentSettings.buttonComponentTypeName", data.buttonComponentTypeName);
                SetString(serialized, "commonAssetNamingSettings.prefabPrefix", data.prefabPrefix);
                SetString(serialized, "commonAssetNamingSettings.texturePrefix", data.texturePrefix);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                RefreshCache();
            }
            catch (Exception e) { Debug.LogError("[PSDLayoutTool2] 网页设置保存失败: " + e.Message); }
        }

        private static void SetString(SerializedObject o, string path, string value) { var p = o.FindProperty(path); if (p != null) p.stringValue = value ?? string.Empty; }
        private static void SetInt(SerializedObject o, string path, int value) { var p = o.FindProperty(path); if (p != null) p.intValue = value; }
        private static string GetString(SerializedObject o, string path) { var p = o.FindProperty(path); return p == null ? string.Empty : p.stringValue; }
        private static int GetInt(SerializedObject o, string path) { var p = o.FindProperty(path); return p == null ? 0 : p.intValue; }

        [Serializable] private sealed class SettingsSnapshot
        {
            public int outputMode;
            public string outputFolderName, atlasOutputPath, textureOutputPath, prefabOutputPath;
            public string imageComponentTypeName, buttonComponentTypeName, prefabPrefix, texturePrefix;
        }

        private static SettingsSnapshot ReadSnapshot(SerializedObject o) => new SettingsSnapshot
        {
            outputMode = GetInt(o, "outputSettings.outputMode"),
            outputFolderName = GetString(o, "outputSettings.outputFolderName"),
            atlasOutputPath = GetString(o, "outputSettings.atlasOutputPath"),
            textureOutputPath = GetString(o, "outputSettings.textureOutputPath"),
            prefabOutputPath = GetString(o, "outputSettings.prefabOutputPath"),
            imageComponentTypeName = GetString(o, "uiComponentSettings.imageComponentTypeName"),
            buttonComponentTypeName = GetString(o, "uiComponentSettings.buttonComponentTypeName"),
            prefabPrefix = GetString(o, "commonAssetNamingSettings.prefabPrefix"),
            texturePrefix = GetString(o, "commonAssetNamingSettings.texturePrefix")
        };

        private static string Page() => @"<!doctype html><html lang='zh-CN'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>PSD Layout Tool 设置</title><style>*{box-sizing:border-box}body{margin:0;background:#f3f6fa;color:#172033;font:14px -apple-system,BlinkMacSystemFont,'Segoe UI','Microsoft YaHei',sans-serif}main{max-width:920px;margin:32px auto;padding:0 22px}.card{background:#fff;border:1px solid #e5e9f0;border-radius:16px;padding:32px;margin:0 0 22px;box-shadow:0 8px 24px rgba(25,42,70,.08)}.hero{display:flex;align-items:center;gap:16px}.icon{width:50px;height:50px;border-radius:13px;background:#3478ee;color:#fff;display:grid;place-items:center;font-size:25px}.title{font-size:21px;font-weight:700}.sub{color:#6b7890;margin-top:5px}.divider{height:1px;background:#e7ebf1;margin:24px 0}.section-title{font-size:16px;font-weight:700;margin-bottom:18px}.field{margin:0 0 18px}label{display:block;font-weight:600;margin:0 0 8px}small{display:block;color:#718097;margin-top:7px;line-height:1.5}input,select{width:100%;height:46px;border:2px solid #e1e6ee;border-radius:10px;background:#fbfcfe;color:#172033;padding:0 14px;font-size:14px;outline:none}input:focus,select:focus{border-color:#3478ee;box-shadow:0 0 0 3px rgba(52,120,238,.12)}.toggle{display:flex;align-items:center;justify-content:space-between;border:2px solid #e1e6ee;border-radius:12px;padding:17px 18px}.toggle b{display:block}.toggle span{color:#718097;font-size:13px;display:block;margin-top:5px}.switch{width:52px;height:30px;border-radius:20px;background:#3478ee;padding:3px}.knob{width:24px;height:24px;background:#fff;border-radius:50%;margin-left:22px}.actions{display:flex;align-items:center;gap:14px;margin-top:8px}button{height:42px;border:0;border-radius:9px;padding:0 20px;background:#3478ee;color:#fff;font-weight:600;cursor:pointer}#status{color:#4d6b8f}@media(max-width:640px){main{margin:12px auto;padding:0 12px}.card{padding:22px}.title{font-size:18px}}</style></head><body><main><section class='card'><div class='hero'><div class='icon'>▤</div><div><div class='title'>PSD Layout Tool 全局配置</div><div class='sub'>项目导入、Prefab 生成与公共资源规则</div></div></div><div class='divider'></div><div class='section-title'>默认组件类型</div><div class='field'><label>文本组件</label><select id='imageComponentTypeName'><option>TextMeshPro（推荐）</option><option>UnityEngine.UI.Text</option></select><small>TMP 提供更好的渲染质量和性能。</small></div><div class='field'><label>图片组件</label><select id='buttonComponentTypeName'><option>Image（标准）</option><option>RawImage</option></select><small>Image 支持九宫格和填充模式。</small></div><div class='field'><label>默认 Button 组件类型</label><input id='buttonComponentTypeName2' placeholder='UnityEngine.UI.Button'><small>填写 MonoBehaviour 完整类名，适用于 Button 和 TMPButton。</small></div><div class='toggle'><div><b>使用 TextMeshPro</b><span>导入文本优先使用 TMP 组件</span></div><div class='switch'><div class='knob'></div></div></div></section><section class='card'><div class='hero'><div class='icon'>↗</div><div><div class='title'>输出配置</div><div class='sub'>控制导出目录与公共资源命名</div></div></div><div class='divider'></div><div class='field'><label>资源输出位置</label><select id='outputMode'><option value='0'>与 PSD 同目录</option><option value='1'>固定输出路径</option></select></div><div class='field'><label>输出文件夹名</label><input id='outputFolderName'></div><div class='field'><label>Prefab 输出路径</label><input id='prefabOutputPath'></div><div class='field'><label>图集输出路径</label><input id='atlasOutputPath'></div><div class='field'><label>贴图输出路径</label><input id='textureOutputPath'></div><div class='field'><label>Prefab 前缀</label><input id='prefabPrefix'></div><div class='field'><label>Texture 前缀</label><input id='texturePrefix'></div><div class='actions'><button onclick='save()'>保存并同步</button><span id='status'>连接中…</span></div></section></main><script>const fields=['outputMode','outputFolderName','prefabOutputPath','atlasOutputPath','textureOutputPath','prefabPrefix','texturePrefix','imageComponentTypeName','buttonComponentTypeName'];const statusEl=document.getElementById('status');function load(){fetch('/config',{cache:'no-store'}).then(r=>r.json()).then(x=>{fields.forEach(id=>{const e=document.getElementById(id);if(e)e.value=x[id]??''});document.getElementById('buttonComponentTypeName2').value=x.buttonComponentTypeName||'';statusEl.textContent='● 已连接 · '+new Date().toLocaleTimeString()}).catch(()=>statusEl.textContent='○ 等待 Unity 服务')}function save(){const x={};fields.forEach(id=>{const e=document.getElementById(id);if(e)x[id]=e.value});x.buttonComponentTypeName=document.getElementById('buttonComponentTypeName2').value;fetch('/save',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(x)}).then(()=>{statusEl.textContent='✓ 已排队保存';setTimeout(load,350)})}load();setInterval(load,900)</script></body></html>";
    }
}
