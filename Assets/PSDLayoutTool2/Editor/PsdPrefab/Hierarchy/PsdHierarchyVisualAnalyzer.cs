namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    /// <summary>
    /// 为 Prefab 节点生成视觉预览截图，用于 AI 视觉相似度分析
    /// </summary>
    internal static class PsdHierarchyVisualAnalyzer
    {
        private const int ThumbnailSize = 256;
        private const string TempScreenshotDirectory = "Library/PSDLayoutTool2/VisualAnalysis";

        /// <summary>
        /// 为指定节点生成缩略图预览
        /// </summary>
        internal static bool TryCaptureThumbnail(
            GameObject node,
            out Texture2D thumbnail,
            out string error)
        {
            thumbnail = null;
            error = string.Empty;

            if (node == null)
            {
                error = "节点为空";
                return false;
            }

            RectTransform rectTransform = node.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                error = "节点缺少 RectTransform 组件";
                return false;
            }

            try
            {
                // 确保节点可见
                bool wasActive = node.activeSelf;
                if (!wasActive)
                {
                    node.SetActive(true);
                }

                Canvas canvas = node.GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    error = "节点不在 Canvas 下";
                    if (!wasActive) node.SetActive(wasActive);
                    return false;
                }

                // 获取节点的屏幕空间矩形
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);

                float minX = corners.Min(c => c.x);
                float maxX = corners.Max(c => c.x);
                float minY = corners.Min(c => c.y);
                float maxY = corners.Max(c => c.y);

                int width = Mathf.Max(1, Mathf.CeilToInt(maxX - minX));
                int height = Mathf.Max(1, Mathf.CeilToInt(maxY - minY));

                if (width <= 0 || height <= 0)
                {
                    error = "节点尺寸无效";
                    if (!wasActive) node.SetActive(wasActive);
                    return false;
                }

                // 创建渲染纹理
                RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24);
                RenderTexture previousActive = RenderTexture.active;

                Camera renderCamera = new GameObject("TempRenderCamera").AddComponent<Camera>();
                renderCamera.targetTexture = renderTexture;
                renderCamera.clearFlags = CameraClearFlags.SolidColor;
                renderCamera.backgroundColor = new Color(0, 0, 0, 0);
                renderCamera.orthographic = true;
                renderCamera.orthographicSize = height / 2f;
                renderCamera.transform.position = new Vector3(
                    (minX + maxX) / 2f,
                    (minY + maxY) / 2f,
                    -10f);

                // 渲染
                renderCamera.Render();

                // 读取到 Texture2D
                RenderTexture.active = renderTexture;
                Texture2D fullTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                fullTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                fullTexture.Apply();

                // 缩放到缩略图尺寸
                thumbnail = ScaleTexture(fullTexture, ThumbnailSize, ThumbnailSize);

                // 清理
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.DestroyImmediate(renderCamera.gameObject);
                UnityEngine.Object.DestroyImmediate(fullTexture);

                if (!wasActive)
                {
                    node.SetActive(wasActive);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "截图失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 批量生成节点缩略图
        /// </summary>
        internal static Dictionary<string, Texture2D> CaptureThumbnails(
            PsdHierarchyChatContext context,
            IEnumerable<string> nodeIds)
        {
            var thumbnails = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null)
            {
                return thumbnails;
            }

            foreach (string nodeId in nodeIds ?? Array.Empty<string>())
            {
                if (!context.TryGetNodePath(nodeId, out string path))
                {
                    continue;
                }

                Transform transform = FindTransformByPath(stage.prefabContentsRoot.transform, path);
                if (transform == null)
                {
                    continue;
                }

                if (TryCaptureThumbnail(transform.gameObject, out Texture2D thumbnail, out string _))
                {
                    thumbnails[nodeId] = thumbnail;
                }
            }

            return thumbnails;
        }

        /// <summary>
        /// 保存缩略图到临时目录
        /// </summary>
        internal static string SaveThumbnail(Texture2D thumbnail, string nodeId)
        {
            if (thumbnail == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return string.Empty;
            }

            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), TempScreenshotDirectory);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string filename = $"node_{SanitizeFilename(nodeId)}_{DateTime.Now:yyyyMMddHHmmss}.png";
                string path = Path.Combine(directory, filename);

                byte[] bytes = thumbnail.EncodeToPNG();
                File.WriteAllBytes(path, bytes);

                return path;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 清理临时截图文件
        /// </summary>
        internal static void CleanupTempScreenshots()
        {
            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), TempScreenshotDirectory);
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory, "*.png"))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // 忽略单个文件删除失败
                        }
                    }
                }
            }
            catch
            {
                // 忽略清理失败
            }
        }

        private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source == null)
            {
                return null;
            }

            // 保持宽高比
            float sourceAspect = (float)source.width / source.height;
            float targetAspect = (float)targetWidth / targetHeight;

            int finalWidth, finalHeight;
            if (sourceAspect > targetAspect)
            {
                finalWidth = targetWidth;
                finalHeight = Mathf.RoundToInt(targetWidth / sourceAspect);
            }
            else
            {
                finalHeight = targetHeight;
                finalWidth = Mathf.RoundToInt(targetHeight * sourceAspect);
            }

            RenderTexture rt = RenderTexture.GetTemporary(finalWidth, finalHeight);
            RenderTexture previousActive = RenderTexture.active;

            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(finalWidth, finalHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, finalWidth, finalHeight), 0, 0);
            result.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        private static Transform FindTransformByPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string[] segments = path.Split('/');
            Transform current = root;

            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                // 跳过根节点名称
                if (current == root && segment == root.name)
                {
                    continue;
                }

                // 处理重名节点的索引标记 (例如: "Node#1")
                string nodeName = segment;
                int occurrence = 0;
                int hashIndex = segment.IndexOf('#');
                if (hashIndex > 0 && hashIndex < segment.Length - 1)
                {
                    nodeName = segment.Substring(0, hashIndex);
                    if (int.TryParse(segment.Substring(hashIndex + 1), out int parsedOccurrence))
                    {
                        occurrence = parsedOccurrence;
                    }
                }

                Transform found = null;
                int currentOccurrence = 0;
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (string.Equals(child.name, nodeName, StringComparison.Ordinal))
                    {
                        if (currentOccurrence == occurrence)
                        {
                            found = child;
                            break;
                        }
                        currentOccurrence++;
                    }
                }

                if (found == null)
                {
                    return null;
                }

                current = found;
            }

            return current;
        }

        private static string SanitizeFilename(string filename)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(filename.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }
    }
}
