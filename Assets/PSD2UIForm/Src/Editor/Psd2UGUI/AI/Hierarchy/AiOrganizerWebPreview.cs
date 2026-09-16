using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>从真正的候选 Prefab 渲染图片，并用生成元数据映射可选区域。</summary>
    internal static class AiOrganizerWebPreview
    {
        internal static byte[] Capture(AiOrganizerPreview preview, AiAnalysisPackageDocument package, List<AiOrganizerWebNode> nodes)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(preview.PreviewPath);
            if (asset == null) throw new InvalidOperationException("候选 Prefab 不存在。");
            float width = package.document?.width > 0 ? package.document.width : preview.DocumentSize.x;
            float height = package.document?.height > 0 ? package.document.height : preview.DocumentSize.y;
            if (width <= 0 || height <= 0) throw new InvalidOperationException("无法取得 PSD 的真实尺寸，不能生成准确预览。");
            float scale = Mathf.Min(1, 1600f / Mathf.Max(width, height));
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture render = null;
            Texture2D image = null;
            var oldActive = RenderTexture.active;
            try
            {
                var root = new GameObject("UI Workbench Capture", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true)) canvas.renderMode = RenderMode.WorldSpace;
                foreach (var scaler in instance.GetComponentsInChildren<CanvasScaler>(true)) scaler.enabled = false;
                var cameraObject = new GameObject("UI Workbench Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.cameraType = CameraType.Game;
                camera.orthographic = true; camera.orthographicSize = height / 2;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 2000;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.allowHDR = false; camera.allowMSAA = false;
                render = RenderTexture.GetTemporary(Mathf.Max(1, Mathf.RoundToInt(width * scale)), Mathf.Max(1, Mathf.RoundToInt(height * scale)), 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = render;
                root.GetComponent<Canvas>().worldCamera = camera;
                Canvas.ForceUpdateCanvases();
                if (instance.transform is RectTransform layout) LayoutRebuilder.ForceRebuildLayoutImmediate(layout);
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else
                {
                    var request = new RenderPipeline.StandardRequest { destination = render };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                        throw new InvalidOperationException("当前渲染管线不支持独立预览渲染请求。");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                RenderTexture.active = render;
                image = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); image.Apply();
                var addresses = preview.PreviewNodeAddresses();
                foreach (var node in nodes)
                {
                    if (!addresses.TryGetValue(node.id, out string address)) continue;
                    Transform target = instance.transform;
                    foreach (string segment in address.Split('/'))
                    {
                        if (!int.TryParse(segment, out int index) || index < 0 || index >= target.childCount) { target = null; break; }
                        target = target.GetChild(index);
                    }
                    if (!(target is RectTransform targetRect) || !target.gameObject.activeInHierarchy) continue;
                    var corners = new Vector3[4]; targetRect.GetWorldCorners(corners);
                    float minX = 1, minY = 1, maxX = 0, maxY = 0;
                    foreach (var corner in corners)
                    {
                        var p = camera.WorldToViewportPoint(corner);
                        minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                        minY = Mathf.Min(minY, 1 - p.y); maxY = Mathf.Max(maxY, 1 - p.y);
                    }
                    node.bounds = new RectData { x = Mathf.Clamp01(minX), y = Mathf.Clamp01(minY),
                        w = Mathf.Clamp01(maxX) - Mathf.Clamp01(minX), h = Mathf.Clamp01(maxY) - Mathf.Clamp01(minY) };
                }
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = oldActive;
                EditorSceneManager.ClosePreviewScene(scene);
                if (render != null) RenderTexture.ReleaseTemporary(render);
                if (image != null) Object.DestroyImmediate(image);
            }
        }
    }
}
