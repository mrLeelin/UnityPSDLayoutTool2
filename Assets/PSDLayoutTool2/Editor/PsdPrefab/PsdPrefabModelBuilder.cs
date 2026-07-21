namespace PsdLayoutTool2
{
    using System;
    using PhotoshopFile;
    using UnityEngine;

    /// <summary>把当前 PSD 解析结果转换为与 Unity 写入器解耦的中间模型。</summary>
    public static class PsdPrefabModelBuilder
    {
        public static PsdPrefabDocumentModel Build(PsdFile psd)
        {
            if (psd == null)
            {
                throw new ArgumentNullException("psd");
            }

            PsdEmbeddedLayoutManifest manifest = psd.EmbeddedLayoutManifest;
            if (manifest != null && manifest.IsUsable)
            {
                return BuildFromManifest(psd, manifest);
            }

            return BuildFromNativeLayers(psd);
        }

        private static PsdPrefabDocumentModel BuildFromManifest(
            PsdFile psd,
            PsdEmbeddedLayoutManifest manifest)
        {
            var model = new PsdPrefabDocumentModel
            {
                sourceFingerprint = manifest.documentFingerprint,
                width = manifest.document != null ? manifest.document.width : psd.Width,
                height = manifest.document != null ? manifest.document.height : psd.Height,
                resolution = manifest.document != null ? manifest.document.resolution : 72f
            };

            foreach (PsdEmbeddedLayoutLayer source in manifest.layers)
            {
                if (source == null || string.IsNullOrEmpty(source.layerId))
                {
                    continue;
                }

                var node = new PsdPrefabNodeModel
                {
                    stableId = source.layerId,
                    parentStableId = source.parentId ?? string.Empty,
                    siblingIndex = source.siblingIndex,
                    name = source.name ?? string.Empty,
                    kind = ParseKind(source.kind, source.text != null),
                    visible = source.visible,
                    opacity = Mathf.Clamp01(source.opacity),
                    bounds = ToRect(source.bounds),
                    contentFingerprint = source.fingerprint ?? string.Empty,
                    assetFingerprint = source.fingerprint ?? string.Empty
                };

                if (source.text != null)
                {
                    node.text = new PsdPrefabTextModel
                    {
                        contents = source.text.contents ?? string.Empty,
                        fontFamily = source.text.fontName ?? string.Empty,
                        fontSize = source.text.fontSize
                    };
                }

                model.nodes.Add(node);
            }

            return model;
        }

        private static PsdPrefabDocumentModel BuildFromNativeLayers(PsdFile psd)
        {
            var model = new PsdPrefabDocumentModel
            {
                sourceFingerprint = string.Empty,
                width = psd.Width,
                height = psd.Height,
                resolution = 72f
            };

            for (int index = 0; index < psd.Layers.Count; index++)
            {
                Layer layer = psd.Layers[index];
                AddNativeNode(model, layer, string.Empty, index);
            }

            return model;
        }

        private static void AddNativeNode(
            PsdPrefabDocumentModel model,
            Layer layer,
            string parentId,
            int siblingIndex)
        {
            string stableId = BuildFallbackStableId(parentId, siblingIndex, layer.Name);
            var node = new PsdPrefabNodeModel
            {
                stableId = stableId,
                parentStableId = parentId,
                siblingIndex = siblingIndex,
                name = layer.Name ?? string.Empty,
                kind = layer.IsTextLayer ? PsdPrefabNodeKind.Text :
                    (layer.Children != null && layer.Children.Count > 0 ? PsdPrefabNodeKind.Group : PsdPrefabNodeKind.Image),
                visible = layer.Visible,
                opacity = Mathf.Clamp01(layer.Opacity / 255f),
                bounds = layer.Rect
            };

            if (layer.IsTextLayer)
            {
                PsdTextStyle style = layer.TextStyle ?? PsdTextStyle.CreateDefault(layer.FontSize);
                node.text = new PsdPrefabTextModel
                {
                    contents = layer.Text ?? string.Empty,
                    fontFamily = layer.FontName ?? string.Empty,
                    fontSize = layer.FontSize,
                    fillColor = layer.FillColor,
                    lineHeight = style.LineHeight,
                    effect = new PsdPrefabTextEffectModel
                    {
                        hasOutline = style.StrokeEnabled,
                        outlineColor = style.StrokeColor,
                        outlineWidth = style.StrokeWidth,
                        hasShadow = style.ShadowEnabled,
                        shadowColor = style.ShadowColor,
                        shadowOffsetX = Mathf.Cos(style.ShadowAngle * Mathf.Deg2Rad) * style.ShadowDistance,
                        shadowOffsetY = Mathf.Sin(style.ShadowAngle * Mathf.Deg2Rad) * style.ShadowDistance,
                        shadowSoftness = style.ShadowBlur
                    }
                };
            }

            node.contentFingerprint = BuildContentFingerprint(node);
            node.assetFingerprint = node.contentFingerprint;
            model.nodes.Add(node);

            if (layer.Children == null)
            {
                return;
            }

            for (int index = 0; index < layer.Children.Count; index++)
            {
                AddNativeNode(model, layer.Children[index], stableId, index);
            }
        }

        private static PsdPrefabNodeKind ParseKind(string kind, bool hasText)
        {
            if (hasText || string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase))
            {
                return PsdPrefabNodeKind.Text;
            }

            if (string.Equals(kind, "group", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "folder", StringComparison.OrdinalIgnoreCase))
            {
                return PsdPrefabNodeKind.Group;
            }

            return PsdPrefabNodeKind.Image;
        }

        private static Rect ToRect(PsdEmbeddedLayoutBounds bounds)
        {
            return bounds == null ? default(Rect) : new Rect(bounds.x, bounds.y, bounds.width, bounds.height);
        }

        private static string BuildFallbackStableId(string parentId, int siblingIndex, string name)
        {
            string input = (parentId ?? string.Empty) + "/" + siblingIndex + "/" + (name ?? string.Empty);
            return "native_" + ComputeFnv1a(input);
        }

        private static string BuildContentFingerprint(PsdPrefabNodeModel node)
        {
            string text = node.text == null ? string.Empty : node.text.contents;
            string input = node.stableId + "|" + node.kind + "|" + node.bounds + "|" + text;
            return ComputeFnv1a(input);
        }

        private static string ComputeFnv1a(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return hash.ToString("x8");
            }
        }
    }
}
