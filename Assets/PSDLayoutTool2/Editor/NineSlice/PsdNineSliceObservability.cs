namespace PsdLayoutTool2
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    [InitializeOnLoad]
    internal static class PsdNineSliceObservability
    {
        private const float IconSize = 13f;
        private static readonly Color NineSliceColor = new Color(0.36f, 0.73f, 0.63f, 0.95f);
        private static readonly Color ImageColor = new Color(0.56f, 0.59f, 0.64f, 0.95f);
        private static readonly List<Component> HierarchyComponents = new List<Component>();

        static PsdNineSliceObservability()
        {
            EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchyMarker;
            Editor.finishedDefaultHeaderGUI += DrawInspectorDiagnostic;
        }

        internal static bool TryDescribe(GameObject gameObject, out NineSliceDiagnostic diagnostic)
        {
            return TryDescribeAnyImage(gameObject, out diagnostic) && diagnostic.isNineSlice;
        }

        internal static bool TryDescribeAnyImage(GameObject gameObject, out NineSliceDiagnostic diagnostic)
        {
            diagnostic = default(NineSliceDiagnostic);
            if (gameObject == null) return false;

            Image image = gameObject.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                Vector4 border = image.sprite.border;
                diagnostic = new NineSliceDiagnostic(image.sprite, image.type == Image.Type.Sliced &&
                    PsdNineSliceImportPolicy.HasSpriteBorder(border.x, border.y, border.z, border.w), border);
                return true;
            }

            RawImage rawImage = gameObject.GetComponent<RawImage>();
            if (rawImage != null && rawImage.texture != null)
            {
                diagnostic = new NineSliceDiagnostic(rawImage.texture, false, Vector4.zero);
                return true;
            }

            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                Vector4 border = renderer.sprite.border;
                diagnostic = new NineSliceDiagnostic(renderer.sprite, PsdNineSliceImportPolicy.HasSpriteBorder(
                    border.x, border.y, border.z, border.w), border);
                return true;
            }

            return false;
        }

        private static void DrawHierarchyMarker(int instanceId, Rect selectionRect)
        {
            if (!PsdLayoutProjectSettings.instance.ResolveNineSliceSettings().showImageMarkers) return;
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (!TryDescribeAnyImage(gameObject, out NineSliceDiagnostic diagnostic)) return;

            float labelWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(gameObject.name)).x;
            gameObject.GetComponents(HierarchyComponents);
            int componentCount = HierarchyComponents.Count;
            HierarchyComponents.Clear();
            if (!TryGetHierarchyMarkerRect(selectionRect, labelWidth, componentCount, out Rect iconRect)) return;
            if (GUI.Button(iconRect, new GUIContent(string.Empty, diagnostic.ToTooltip() + "\n点击打开九宫编辑器"), GUIStyle.none))
            {
                OpenNineSliceEditor(diagnostic.asset);
            }

            DrawGlyph(iconRect, diagnostic.isNineSlice);
        }

        internal static bool TryGetHierarchyMarkerRect(Rect selectionRect, float labelWidth,
            int componentCount, out Rect iconRect)
        {
            // 标记跟随名称，右侧为 Unity 的 Prefab 操作、状态标记及第三方组件图标留出空间。
            // 窄窗口、长名称或组件过多时不绘制，避免遮住名称或抢占其他图标的点击区域。
            float x = selectionRect.x + 18f + labelWidth + 4f;
            float rightLimit = selectionRect.xMax - 64f - componentCount * 16f;
            iconRect = new Rect(x,
                selectionRect.y + (selectionRect.height - IconSize) * 0.5f, IconSize, IconSize);
            return selectionRect.height >= IconSize && iconRect.xMax <= rightLimit;
        }

        internal static bool OpenNineSliceEditor(UnityEngine.Object asset)
        {
            string assetPath = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return false;
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null && PsdNineSliceAssetState.TryReadLayerIdentity(importer.userData, out uint layerId))
            {
                // Generated PNGs exported before the source identity was recorded only
                // carry the layer id; resolve the PSD from the export convention or the
                // project instead of degrading to the single-texture editor.
                string recordedSourcePsdPath;
                PsdNineSliceAssetState.TryReadSourcePsdIdentity(importer.userData, out recordedSourcePsdPath);
                string sourcePsdPath;
                if (PsdNineSliceSourcePsdResolver.TryResolve(assetPath, layerId, recordedSourcePsdPath, out sourcePsdPath))
                {
                    PsdNineSliceWindow.Open(sourcePsdPath, layerId);
                    return true;
                }
            }
            PsdNineSliceWindow.Open(assetPath);
            return true;
        }

        internal static bool OpenNineSliceEditor(Sprite sprite)
        {
            return OpenNineSliceEditor((UnityEngine.Object)sprite);
        }

        private static void DrawInspectorDiagnostic(Editor editor)
        {
            GameObject gameObject = editor == null ? null : editor.target as GameObject;
            if (!TryDescribeAnyImage(gameObject, out NineSliceDiagnostic diagnostic)) return;
            EditorGUILayout.Space(3f);
            EditorGUILayout.HelpBox(diagnostic.ToInspectorText(), MessageType.Info);
        }

        private static void DrawGlyph(Rect rect, bool isNineSlice)
        {
            const float line = 1f;
            Color color = isNineSlice ? NineSliceColor : ImageColor;
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.16f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, line), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - line, rect.width, line), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, line, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - line, rect.y, line, rect.height), color);
            if (!isNineSlice) return;
            EditorGUI.DrawRect(new Rect(rect.x + rect.width / 3f, rect.y, line, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * 2f / 3f, rect.y, line, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height / 3f, rect.width, line), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height * 2f / 3f, rect.width, line), color);
        }

        internal readonly struct NineSliceDiagnostic
        {
            internal NineSliceDiagnostic(UnityEngine.Object asset, bool isNineSlice, Vector4 border)
            {
                this.asset = asset;
                this.isNineSlice = isNineSlice;
                this.border = border;
            }

            internal readonly UnityEngine.Object asset;
            internal readonly bool isNineSlice;
            internal readonly Vector4 border;

            internal string ToTooltip()
            {
                return (isNineSlice ? "九宫图片" : "普通图片") + " · " + asset.name +
                    (isNineSlice ? " · L/B/R/T: " + FormatBorder() : "");
            }

            internal string ToInspectorText()
            {
                return (isNineSlice ? "九宫诊断\n已启用九宫：" : "图片诊断\n普通图片：") + asset.name +
                    (isNineSlice ? "\nBorder（左 / 下 / 右 / 上）：" + FormatBorder() : "");
            }

            private string FormatBorder()
            {
                return border.x.ToString("0.##") + " / " + border.y.ToString("0.##") +
                    " / " + border.z.ToString("0.##") + " / " + border.w.ToString("0.##");
            }
        }
    }
}
