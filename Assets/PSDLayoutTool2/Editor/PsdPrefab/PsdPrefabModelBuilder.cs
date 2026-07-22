namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using PhotoshopFile;
    using UnityEngine;

    /// <summary>把当前 PSD 解析结果转换为与 Unity 写入器解耦的中间模型。</summary>
    public static class PsdPrefabModelBuilder
    {
        private const string NineSliceTagPattern =
            @"(?:\|9slice\s*=\s*|\[9slice\s*:\s*)([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*\]?";

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
                    name = RemoveNineSliceTag(source.name ?? string.Empty),
                    kind = ParseKind(source.kind, source.text != null),
                    visible = source.visible,
                    opacity = Mathf.Clamp01(source.opacity),
                    bounds = ToRect(source.bounds),
                    contentFingerprint = source.fingerprint ?? string.Empty,
                    assetFingerprint = source.fingerprint ?? string.Empty,
                    nineSlice = source.nineSlice != null && source.nineSlice.enabled
                        ? new PsdPrefabNineSliceModel
                        {
                            left = source.nineSlice.left,
                            top = source.nineSlice.top,
                            right = source.nineSlice.right,
                            bottom = source.nineSlice.bottom
                        }
                        : null
                };

                if (source.text != null)
                {
                    node.text = new PsdPrefabTextModel
                    {
                        contents = NormalizeTextLineEndings(source.text.contents ?? string.Empty),
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
            string stableId = PsdStableLayerIdUtility.Create(layer.Id, parentId, siblingIndex, layer.Name).value;
            var node = new PsdPrefabNodeModel
            {
                stableId = stableId,
                parentStableId = parentId,
                siblingIndex = siblingIndex,
                name = RemoveNineSliceTag(layer.Name ?? string.Empty),
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

            // Pixel identity must be established before the broader content hash.
            // Feeding Content back into assetFingerprint would make image changes
            // invisible because the first Content call sees an empty asset value.
            node.assetFingerprint = PsdHierarchyFingerprints.Asset(layer.Channels.Select(channel =>
                new KeyValuePair<short, byte[]>(channel.ID, channel.ImageData)));
            node.contentFingerprint = PsdHierarchyFingerprints.Content(node);
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

        private static string RemoveNineSliceTag(string name)
        {
            return Regex.Replace(name ?? string.Empty, NineSliceTagPattern, string.Empty, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Converts Photoshop's carriage-return text separators to Unity's line-feed separator.
        /// TMP treats a standalone carriage return as a horizontal cursor reset, which makes
        /// multiline PSD text render on top of itself instead of advancing to the next line.
        /// </summary>
        private static string NormalizeTextLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\u2028", "\n")
                .Replace("\u2029", "\n");
        }
    }
}
