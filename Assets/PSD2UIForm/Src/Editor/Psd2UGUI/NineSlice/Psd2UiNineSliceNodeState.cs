namespace UGF.EditorTools.Psd2UGUI.NineSlice
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// PsdLayerNode 上的九宫格状态读写。
    ///
    /// 数据落在节点自身（<see cref="PsdLayerNode.nineSliceEnabled"/> 与四个边距字段），
    /// 随 Prefab 一起序列化，因此是"可撤销、可持久化、可随 Prefab 迁移"的：
    ///   · Hierarchy 行上的九宫格标识只反映 <see cref="PsdLayerNode.NineSliceEnabled"/>，不参与推断；
    ///   · 只有勾选了手动九宫格，导出图片时才会用节点记录的边距覆盖命名规则/像素推断的结果。
    ///
    /// 边距的坐标空间 = 该节点导出 PNG 的像素空间（找不到导出 PNG 时用 Render() 实时渲染的图，
    /// 它与导出同源，所以坐标空间一致）。
    /// </summary>
    internal static class Psd2UiNineSliceNodeState
    {
        /// <summary>文本型图层标签里显式九宫格边距的说明，供窗口提示用。</summary>
        internal const string ExplicitBorderTagHint = "图层名 |9slice=左,上,右,下";

        /// <summary>
        /// 只有 Image / Background 两种 UIType 参与九宫格。
        /// 与 <see cref="PsdLayerNode.SupportsNineSlice"/> 保持一致，便于在编辑器中集中排查。
        /// </summary>
        internal static PsdLayerNode ResolveSourceNode(PsdLayerNode node)
        {
            var visited = new HashSet<PsdLayerNode>();
            var current = node;
            while (current != null && current.HasAssetReference())
            {
                if (!visited.Add(current))
                {
                    return node;
                }
                if (!current.TryResolveReferencedNode(out var source) || source == null)
                {
                    break;
                }
                current = source;
            }
            return current;
        }

        internal static bool IsCandidate(PsdLayerNode node)
        {
            if (node == null || !node.SupportsNineSlice)
            {
                return false;
            }

            node = ResolveSourceNode(node);

            // 组合节点保留入口；叶节点只检查源图尺寸，不在 Hierarchy 重绘时渲染图片。
            if (node.HasChildLayerNodes())
            {
                return true;
            }

            var layer = node.GetBoundPsdLayer();
            if (layer != null && layer.Width > 0 && layer.Height > 0)
            {
                return true;
            }

            // 源图层为空时，已有的导出图片仍可用于设置九宫格。
            return TryResolveExportedSpriteAsset(node, out _);
        }

        internal static Psd2UiNineSliceBorder GetBorder(PsdLayerNode node)
        {
            node = ResolveSourceNode(node);
            if (node == null)
            {
                return null;
            }

            return new Psd2UiNineSliceBorder(node.nineSliceLeft, node.nineSliceTop, node.nineSliceRight, node.nineSliceBottom);
        }

        internal static void SetEnabled(PsdLayerNode node, bool enabled, string undoLabel = "切换九宫格")
        {
            node = ResolveSourceNode(node);
            if (node == null || node.nineSliceEnabled == enabled)
            {
                return;
            }

            Undo.RecordObject(node, undoLabel);
            node.nineSliceEnabled = enabled;
            MarkDirty(node);
        }

        internal static void SetBorder(PsdLayerNode node, Psd2UiNineSliceBorder border, string undoLabel = "修改九宫格边距")
        {
            node = ResolveSourceNode(node);
            if (node == null || border == null)
            {
                return;
            }

            if (node.nineSliceLeft == border.Left && node.nineSliceTop == border.Top &&
                node.nineSliceRight == border.Right && node.nineSliceBottom == border.Bottom)
            {
                return;
            }

            Undo.RecordObject(node, undoLabel);
            node.nineSliceLeft = border.Left;
            node.nineSliceTop = border.Top;
            node.nineSliceRight = border.Right;
            node.nineSliceBottom = border.Bottom;
            MarkDirty(node);
        }

        internal static void SetEnabledAndBorder(PsdLayerNode node, bool enabled, Psd2UiNineSliceBorder border, string undoLabel = "设置九宫格")
        {
            node = ResolveSourceNode(node);
            if (node == null)
            {
                return;
            }

            Undo.RecordObject(node, undoLabel);
            node.nineSliceEnabled = enabled;
            if (border != null)
            {
                node.nineSliceLeft = border.Left;
                node.nineSliceTop = border.Top;
                node.nineSliceRight = border.Right;
                node.nineSliceBottom = border.Bottom;
            }

            MarkDirty(node);
        }

        /// <summary>清除节点上记录的全部九宫格数据（开关与边距一起复位）。</summary>
        internal static void Clear(PsdLayerNode node, string undoLabel = "清除九宫格")
        {
            node = ResolveSourceNode(node);
            if (node == null)
            {
                return;
            }

            Undo.RecordObject(node, undoLabel);
            node.nineSliceEnabled = false;
            node.nineSliceLeft = 0;
            node.nineSliceTop = 0;
            node.nineSliceRight = 0;
            node.nineSliceBottom = 0;
            MarkDirty(node);
        }

        /// <summary>
        /// 快捷开关：打开时若还没有边距，会先自动推断一次，避免"亮起来但没有任何边距"。
        /// </summary>
        internal static void Toggle(PsdLayerNode node)
        {
            node = ResolveSourceNode(node);
            if (node == null)
            {
                return;
            }

            if (node.nineSliceEnabled)
            {
                SetEnabled(node, false, "关闭九宫格");
                return;
            }

            if (!node.HasNineSliceBorder && TryInferBorder(node, out Psd2UiNineSliceBorder inferred, out string method, out string error))
            {
                SetEnabledAndBorder(node, true, inferred, "打开九宫格（自动推断：" + method + "）");
                return;
            }

            SetEnabled(node, true, "打开九宫格");
        }

        /// <summary>推断一次并把结果写进节点（不改变开关状态）。</summary>
        internal static bool TryAutoFillBorder(PsdLayerNode node, out string method, out string error)
        {
            node = ResolveSourceNode(node);
            method = string.Empty;
            if (!TryInferBorder(node, out Psd2UiNineSliceBorder inferred, out method, out error))
            {
                return false;
            }

            SetBorder(node, inferred, "自动推断九宫格边距");
            return true;
        }

        /// <summary>
        /// 推断边距：优先使用图层名上的显式边距（<c>|9slice=左,上,右,下</c>），
        /// 否则用 PSDLayoutTool2 的像素三重推断。
        /// </summary>
        internal static bool TryInferBorder(PsdLayerNode node, out Psd2UiNineSliceBorder border, out string method, out string error)
        {
            node = ResolveSourceNode(node);
            border = null;
            method = string.Empty;
            error = null;

            if (node == null)
            {
                error = "没有选中节点。";
                return false;
            }

            Psd2UiNineSliceNameRule rule;
            if (Psd2UiNineSliceNameRules.TryParse(node.GetSourceLayerName(), out rule) && rule.HasExplicitBorder)
            {
                border = rule.ExplicitBorder;
                method = ExplicitBorderTagHint;
                return true;
            }

            Texture2D texture = ResolvePreviewTexture(node, out string source);
            if (texture == null)
            {
                error = "该节点还没有可分析的图像，请先导出图片或刷新预览。";
                return false;
            }

            Psd2UiNineSliceInference inference;
            if (!Psd2UiNineSliceTextureProcessor.TryAnalyze(texture, out inference, out error))
            {
                error = (error ?? "推断失败。") + "（来源：" + source + "）";
                return false;
            }

            border = inference.Border;
            method = inference.Method + " · " + inference.Confidence;
            return true;
        }

        /// <summary>
        /// 解析用于编辑与预览的纹理，依次尝试：
        ///   ① 节点已导出的 PNG —— 就是最终写入 spriteBorder 的那张图，所见即所得；
        ///   ② 直接按节点渲染 —— 与导出用同一套 Render()，边距像素坐标与最终 PNG 严格同源，
        ///      而且不依赖编辑器宿主与预览缓存，Prefab 隔离模式下也能出图（这是首版取不到图的原因）；
        ///   ③ 宿主提供的 PSD 图层预览 —— 兜底，仅在 ①② 都失败时用。
        /// ①③ 返回的资源归 Unity 所有；② 是本类自建的临时纹理，由 <see cref="ReleaseRenderFallback"/> 释放。
        /// </summary>
        internal static Texture2D ResolvePreviewTexture(PsdLayerNode node, out string sourceDescription)
        {
            node = ResolveSourceNode(node);
            sourceDescription = string.Empty;
            if (node == null)
            {
                return null;
            }

            string exportedPath;
            if (TryResolveExportedSpriteAsset(node, out exportedPath))
            {
                Texture2D exported = AssetDatabase.LoadAssetAtPath<Texture2D>(exportedPath);
                if (exported != null)
                {
                    sourceDescription = "导出图 " + exportedPath;
                    return exported;
                }
            }

            // 直接渲染优先于宿主预览：宿主预览走的是 RenderPreview()，分辨率/采样可能与导出结果不同，
            // 拿它当基准会让参考线位置和最终 PNG 对不上。
            Texture2D rendered = AcquireRenderFallback(node);
            if (rendered != null)
            {
                sourceDescription = "节点渲染 " + rendered.width + " x " + rendered.height + "（与导出的 PNG 同源）";
                return rendered;
            }

            node.RefreshPreviewTexture();
            if (node.PreviewTexture != null)
            {
                sourceDescription = "PSD 图层预览（尚未导出图片）";
                return node.PreviewTexture;
            }

            sourceDescription = "无可用图像";
            return null;
        }

        private static PsdLayerNode s_RenderFallbackNode;
        private static Texture2D s_RenderFallbackTexture;

        /// <summary>
        /// 按需渲染一张节点预览并缓存：同一节点复用，换节点时先释放上一张。
        /// 这是"宿主不可用"时的最后一道兜底，保证窗口不会空着。
        /// </summary>
        private static Texture2D AcquireRenderFallback(PsdLayerNode node)
        {
            if ((object)s_RenderFallbackNode == node && s_RenderFallbackTexture != null)
            {
                return s_RenderFallbackTexture;
            }

            ReleaseRenderFallback();
            Texture2D texture = PsdLayerNodeEditorOps.CreateNineSliceSourceTexture(node);
            if (texture != null)
            {
                s_RenderFallbackNode = node;
                s_RenderFallbackTexture = texture;
            }

            return texture;
        }

        /// <summary>释放实时渲染出来的临时预览纹理。窗口关闭 / 切换节点时调用。</summary>
        internal static void ReleaseRenderFallback()
        {
            if (s_RenderFallbackTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(s_RenderFallbackTexture);
                s_RenderFallbackTexture = null;
            }

            s_RenderFallbackNode = null;
        }

        /// <summary>
        /// 找出该节点当前引用的导出 PNG。
        /// 生成物里 Image / RawImage 指向的就是导出图，比按名字猜路径可靠。
        /// </summary>
        internal static bool TryResolveExportedSpriteAsset(PsdLayerNode node, out string assetPath)
        {
            node = ResolveSourceNode(node);
            assetPath = null;
            if (node == null)
            {
                return false;
            }

            Image image = node.GetComponent<Image>();
            if (image == null)
            {
                image = node.GetComponentInChildren<Image>(true);
            }

            if (image != null && image.sprite != null)
            {
                string path = AssetDatabase.GetAssetPath(image.sprite);
                if (IsPngAssetPath(path))
                {
                    assetPath = path;
                    return true;
                }
            }

            RawImage rawImage = node.GetComponent<RawImage>();
            if (rawImage == null)
            {
                rawImage = node.GetComponentInChildren<RawImage>(true);
            }

            if (rawImage != null && rawImage.texture != null)
            {
                string path = AssetDatabase.GetAssetPath(rawImage.texture);
                if (IsPngAssetPath(path))
                {
                    assetPath = path;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 把边距写入导出 PNG 的 TextureImporter.spriteBorder，让改动立刻可见（不必等下次重新生成）。
        /// </summary>
        internal static bool ApplyBorderToImportedSprite(PsdLayerNode node, string texturePath, Psd2UiNineSliceBorder border, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(texturePath))
            {
                error = "找不到该节点导出的 PNG，边距已记录在节点上，下次生成 UIForm 时会生效。";
                return false;
            }

            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                error = "该资源不是 TextureImporter：" + texturePath;
                return false;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Psd2UiNineSliceBorder effective = border;
            if (texture != null)
            {
                effective = ClampBorder(border, texture.width, texture.height);
            }

            importer.spriteBorder = Psd2UiNineSliceTextureProcessor.ToUnityBorder(effective);
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>按节点当前开关状态，把边距写入导出 PNG。</summary>
        internal static bool TryApplyCurrentBorderToExportedSprite(PsdLayerNode node, out string texturePath, out string error)
        {
            texturePath = null;
            error = null;
            if (node == null)
            {
                error = "没有选中节点。";
                return false;
            }

            if (!TryResolveExportedSpriteAsset(node, out texturePath))
            {
                error = "找不到该节点导出的 PNG（可能还没导出过图片）。";
                return false;
            }

            return ApplyBorderToImportedSprite(node, texturePath, GetBorder(node), out error);
        }

        /// <summary>收集同一 Psd2UIFormConverter 下所有支持九宫格的节点，供窗口左侧列表使用。</summary>
        internal static void CollectCandidates(PsdLayerNode node, List<PsdLayerNode> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            if (node == null)
            {
                return;
            }

            Transform scope = null;
            Psd2UIFormConverter converter = node.GetComponentInParent<Psd2UIFormConverter>();
            if (converter != null)
            {
                scope = converter.transform;
            }
            else if (node.transform.root != null)
            {
                scope = node.transform.root;
            }

            if (scope == null)
            {
                if (IsCandidate(node))
                {
                    results.Add(node);
                }

                return;
            }

            PsdLayerNode[] all = scope.GetComponentsInChildren<PsdLayerNode>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (IsCandidate(all[index]))
                {
                    results.Add(all[index]);
                }
            }
        }

        internal static Psd2UiNineSliceBorder CreateDefaultBorder(int width, int height)
        {
            int horizontal = Mathf.Max(0, Mathf.Min(Mathf.Max(0, width - 2) / 4, 16));
            int vertical = Mathf.Max(0, Mathf.Min(Mathf.Max(0, height - 2) / 4, 16));
            return new Psd2UiNineSliceBorder(horizontal, vertical, horizontal, vertical);
        }

        internal static Psd2UiNineSliceBorder ClampBorder(Psd2UiNineSliceBorder border, int width, int height)
        {
            if (border == null)
            {
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                return border;
            }

            int left = Mathf.Clamp(border.Left, 0, Mathf.Max(0, width - 2));
            int right = Mathf.Clamp(border.Right, 0, Mathf.Max(0, width - left - 2));
            int top = Mathf.Clamp(border.Top, 0, Mathf.Max(0, height - 2));
            int bottom = Mathf.Clamp(border.Bottom, 0, Mathf.Max(0, height - top - 2));
            return new Psd2UiNineSliceBorder(left, top, right, bottom);
        }

        internal static bool BordersEqual(Psd2UiNineSliceBorder left, Psd2UiNineSliceBorder right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return left.Left == right.Left && left.Top == right.Top && left.Right == right.Right && left.Bottom == right.Bottom;
        }

        /// <summary>把节点改动落到 Prefab / 场景，并刷新 Hierarchy 上那颗九宫格标识。</summary>
        internal static void MarkDirty(PsdLayerNode node)
        {
            if (node == null)
            {
                return;
            }

            EditorUtility.SetDirty(node);

            if (PrefabUtility.IsPartOfPrefabInstance(node))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            }

            EditorApplication.RepaintHierarchyWindow();
            SceneView.RepaintAll();
        }

        private static bool IsPngAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
