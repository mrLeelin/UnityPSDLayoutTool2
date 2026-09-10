using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using cn.efunstudio.psdreader.Authorization;
using PsdBinaryUtilityNamespace;

namespace cn.efunstudio.psdreader.PsdParser
{
    public static class PsdLayerRenderer
    {
public static PsdRenderedImage RenderPreview(this PsdDocument document, bool includeHiddenLayers = false, bool applyClippingMasks = true)
        {
            float previewScale = ResolvePreviewRenderScale(document?.Width ?? 0, document?.Height ?? 0);
            return RenderDocument(document, includeHiddenLayers, applyClippingMasks, isPreviewRender: true, previewScale);
        }

        public static PsdRenderedImage Render(this PsdDocument document, bool includeHiddenLayers = false, bool applyClippingMasks = true)
        {
            return RenderDocument(document, includeHiddenLayers, applyClippingMasks, isPreviewRender: false, 1f);
        }

        public static PsdRenderedImage RenderPreview(this PsdLayer layer, bool includeHiddenLayers = false, bool applyClippingMasks = true)
        {
            float previewScale = ResolvePreviewRenderScale(layer?.Width ?? 0, layer?.Height ?? 0);
            return RenderLayer(layer, includeHiddenLayers, applyClippingMasks, isPreviewRender: true, previewScale);
        }

        public static PsdRenderedImage Render(this PsdLayer layer, bool includeHiddenLayers = false, bool applyClippingMasks = true)
        {
            return RenderLayer(layer, includeHiddenLayers, applyClippingMasks, isPreviewRender: false, 1f);
        }

        public static PsdRenderedImage MergeLayers(IEnumerable<PsdLayer> layers, bool includeHiddenLayers = false, bool applyClippingMasks = true, bool isPreviewRender = false)
        {
            if (layers == null)
            {
                return null;
            }
            PsdLayer[] validLayers = layers.Where((PsdLayer item) => item != null).ToArray();
            if (validLayers.Length == 0)
            {
                return null;
            }
            using (isPreviewRender ? null : EnterRenderBuildScope())
            {
                return RenderLayerSequence(validLayers, includeHiddenLayers, applyClippingMasks, isPreviewRender);
            }
        }

        internal static string ComputePreviewProtectionFingerprint(this PsdLayer layer)
        {
            if (layer == null)
            {
                return string.Empty;
            }
            // 已删除 License/输出保护：仅保留稳定指纹，便于缓存键。
            return AlwaysAuthorized.Sha256Hex(BuildLayerPathKey(layer) + "|" + BuildLayerNormalizedBounds(layer) + "|" + ComputeLayerWatermarkSalt(layer));
        }

        public static byte MultiplyAlpha(byte alpha, float opacity)
        {
            opacity = Math.Min(1f, Math.Max(0f, opacity));
            return (byte)Math.Round((float)(int)alpha * opacity, MidpointRounding.AwayFromZero);
        }

        public static ushort MultiplyAlpha(ushort alpha, float opacity)
        {
            opacity = Math.Min(1f, Math.Max(0f, opacity));
            return (ushort)Math.Round((float)(int)alpha * opacity, MidpointRounding.AwayFromZero);
        }

        public static byte MultiplyAlpha(byte alpha, byte alphaMask)
        {
            return (byte)((alpha * alphaMask + 127) / 255);
        }

        public static ushort MultiplyAlpha(ushort alpha, ushort alphaMask)
        {
            return (ushort)((uint)(alpha * alphaMask + 32767) / 65535u);
        }

        private static PsdRenderedImage RenderDocument(PsdDocument document, bool includeHiddenLayers, bool applyClippingMasks, bool isPreviewRender, float previewScale)
        {
            if (document == null)
            {
                return null;
            }
            using ((!isPreviewRender) ? EnterRenderBuildScope() : null)
            {
                int renderWidth = ScalePreviewDimension(document.Width, previewScale);
                int renderHeight = ScalePreviewDimension(document.Height, previewScale);
                int renderBitDepth = ResolveRenderBitDepth(document.Depth, isPreviewRender);
                PsdRenderedImage renderedImage = new PsdRenderedImage(0, 0, renderWidth, renderHeight, BuildDocumentPathKey(document), BuildNormalizedBounds(0, 0, renderWidth, renderHeight, renderWidth, renderHeight), ComputeDocumentWatermarkSalt(document), enableProtection: true, isPreviewRender, renderBitDepth);
                ComposeLayers(renderedImage, document.Childs.OfType<PsdLayer>().ToArray(), includeHiddenLayers, applyClippingMasks, isPreviewRender, previewScale, renderBitDepth);
                return renderedImage;
            }
        }

        private static PsdRenderedImage RenderLayer(PsdLayer layer, bool includeHiddenLayers, bool applyClippingMasks, bool isPreviewRender, float previewScale)
        {
            if (layer == null)
            {
                return null;
            }
            using (isPreviewRender ? null : EnterRenderBuildScope())
            {
                int renderBitDepth = ResolveRenderBitDepth(layer.Depth, isPreviewRender);
                PsdRenderedImage renderedImage = RenderInternal(layer, includeHiddenLayers, applyClippingMasks, isRootCall: true, isPreviewRender, previewScale, renderBitDepth);
                return renderedImage;
            }
        }

        private static PsdRenderedImage RenderLayerSequence(PsdLayer[] layers, bool includeHiddenLayers, bool applyClippingMasks, bool isPreviewRender)
        {
            if (layers != null && layers.Length != 0)
            {
                if (!TryGetLayerSequenceBounds(layers, includeHiddenLayers, out var left, out var top, out var width, out var height))
                {
                    return null;
                }
                float previewScale = (isPreviewRender ? ResolvePreviewRenderScale(width, height) : 1f);
                int renderLeft = ScalePreviewCoordinate(left, previewScale);
                int renderTop = ScalePreviewCoordinate(top, previewScale);
                int renderWidth = ScalePreviewDimension(width, previewScale);
                int renderHeight = ScalePreviewDimension(height, previewScale);
                int renderBitDepth = ResolveRenderBitDepth(ResolveSequenceBitDepth(layers), isPreviewRender);
                int watermarkSalt = ComputeMergedLayersWatermarkSalt(layers);
                PsdRenderedImage renderedImage = new PsdRenderedImage(renderLeft, renderTop, renderWidth, renderHeight, BuildMergedLayersPathKey(layers), BuildNormalizedBounds(renderLeft, renderTop, renderWidth, renderHeight, renderWidth, renderHeight), watermarkSalt, enableProtection: true, isPreviewRender, renderBitDepth);
                ComposeLayers(renderedImage, layers, includeHiddenLayers, applyClippingMasks, isPreviewRender, previewScale, renderBitDepth);
                return renderedImage;
            }
            return null;
        }

        private static IDisposable EnterRenderBuildScope()
        {
            return AlwaysAuthorized.CreateNoopScope();
        }

        private static PsdRenderedImage RenderGroup(PsdLayer layer, bool includeHiddenLayers, bool applyClippingMasks, bool enableProtection, bool isPreviewRender, float previewScale, int renderBitDepth)
        {
            int left = ScalePreviewCoordinate(layer.Left, previewScale);
            int top = ScalePreviewCoordinate(layer.Top, previewScale);
            int renderWidth = ScalePreviewDimension(layer.Width, previewScale);
            int renderHeight = ScalePreviewDimension(layer.Height, previewScale);
            PsdRenderedImage renderedImage = new PsdRenderedImage(left, top, renderWidth, renderHeight, BuildLayerPathKey(layer), BuildNormalizedBounds(left, top, renderWidth, renderHeight, renderWidth, renderHeight), ComputeLayerWatermarkSalt(layer), enableProtection, isPreviewRender, renderBitDepth);
            ComposeLayers(renderedImage, layer.Childs, includeHiddenLayers, applyClippingMasks, isPreviewRender, previewScale, renderBitDepth);
            renderedImage.MultiplyOpacity(layer.Opacity);
            return renderedImage;
        }

        private static void ComposeLayers(PsdRenderedImage canvas, PsdLayer[] layers, bool includeHiddenLayers, bool applyClippingMasks, bool isPreviewRender, float previewScale, int renderBitDepth)
        {
            if (canvas == null || canvas.IsEmpty || layers == null || layers.Length == 0)
            {
                return;
            }
            byte[] clippingMask8 = null;
            ushort[] clippingMask16 = null;
            foreach (PsdLayer layer in layers)
            {
                if (layer == null || (!includeHiddenLayers && !layer.IsVisible))
                {
                    continue;
                }
                PsdRenderedImage renderedLayer = RenderInternal(layer, includeHiddenLayers, applyClippingMasks, isRootCall: false, isPreviewRender, previewScale, renderBitDepth);
                if (renderedLayer == null || renderedLayer.IsEmpty)
                {
                    continue;
                }
                if (applyClippingMasks && layer.IsClipping)
                {
                    if (!canvas.IsHighBitDepth)
                    {
                        if (clippingMask8 != null)
                        {
                            ApplyClippingMask(renderedLayer, clippingMask8, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
                        }
                    }
                    else if (clippingMask16 != null)
                    {
                        ApplyClippingMask(renderedLayer, clippingMask16, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
                    }
                }
                CompositeImage(canvas, renderedLayer);
                if (!layer.IsClipping)
                {
                    if (canvas.IsHighBitDepth)
                    {
                        clippingMask16 = BuildPlacedAlphaMask16(renderedLayer, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
                        clippingMask8 = null;
                    }
                    else
                    {
                        clippingMask8 = BuildPlacedAlphaMask(renderedLayer, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
                        clippingMask16 = null;
                    }
                }
            }
        }

        private static PsdRenderedImage RenderLeaf(PsdLayer layer, bool enableProtection, bool isPreviewRender, float previewScale, int renderBitDepth)
        {
            if (layer != null && layer.HasImage && layer.Width > 0 && layer.Height > 0)
            {
                Channel redChannel = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Red);
                Channel greenChannel = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Green);
                Channel blueChannel = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Blue);
                Channel alphaChannel = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Alpha);
                Channel maskChannel = layer.Channels.FirstOrDefault((Channel item) => item.Type == ChannelType.Mask);
                if (redChannel != null && greenChannel != null && blueChannel != null)
                {
                    int left = ScalePreviewCoordinate(layer.Left, previewScale);
                    int top = ScalePreviewCoordinate(layer.Top, previewScale);
                    int renderWidth = ScalePreviewDimension(layer.Width, previewScale);
                    int renderHeight = ScalePreviewDimension(layer.Height, previewScale);
                    PsdRenderedImage renderedImage = new PsdRenderedImage(left, top, renderWidth, renderHeight, BuildLayerPathKey(layer), BuildNormalizedBounds(left, top, renderWidth, renderHeight, renderWidth, renderHeight), ComputeLayerWatermarkSalt(layer), enableProtection, isPreviewRender, renderBitDepth);
                    LayerMask layerMask = ((layer.Records != null) ? layer.Records.Mask : null);
                    float opacity = layer.Opacity;
                    bool isHighBitDepth = renderBitDepth >= 16;
                    for (int renderY = 0; renderY < renderHeight; renderY++)
                    {
                        int sourceY = (isPreviewRender ? ScalePreviewSampleCoordinate(renderY, previewScale, layer.Height) : renderY);
                        for (int renderX = 0; renderX < renderWidth; renderX++)
                        {
                            int sourceX = ((!isPreviewRender) ? renderX : ScalePreviewSampleCoordinate(renderX, previewScale, layer.Width));
                            int sourceIndex = sourceY * layer.Width + sourceX;
                            if (!isHighBitDepth)
                            {
                                byte alpha = ((alphaChannel != null) ? alphaChannel.Data[sourceIndex] : byte.MaxValue);
                                alpha = MultiplyAlpha(alpha, opacity);
                                if (maskChannel != null && layerMask != null && maskChannel.Data != null && maskChannel.Data.Length != 0)
                                {
                                    int sourceCanvasX = layer.Left + sourceX;
                                    int sourceCanvasY = layer.Top + sourceY;
                                    if (sourceCanvasX >= layerMask.Left && sourceCanvasX < layerMask.Right && sourceCanvasY >= layerMask.Top && sourceCanvasY < layerMask.Bottom)
                                    {
                                        int maskX = sourceCanvasX - layerMask.Left;
                                        int maskIndex = (sourceCanvasY - layerMask.Top) * layerMask.Width + maskX;
                                        alpha = (byte)((maskIndex >= 0 && maskIndex < maskChannel.Data.Length) ? MultiplyAlpha(alpha, maskChannel.Data[maskIndex]) : 0);
                                    }
                                    else
                                    {
                                        alpha = 0;
                                    }
                                }
                                renderedImage.SetPixel(renderX, renderY, redChannel.Data[sourceIndex], greenChannel.Data[sourceIndex], blueChannel.Data[sourceIndex], alpha);
                                continue;
                            }
                            ushort alpha16 = ((alphaChannel != null) ? ReadChannelSample16(alphaChannel, sourceIndex) : ushort.MaxValue);
                            alpha16 = MultiplyAlpha(alpha16, opacity);
                            if (maskChannel != null && layerMask != null && ((maskChannel.Data16 != null && maskChannel.Data16.Length != 0) || (maskChannel.Data != null && maskChannel.Data.Length != 0)))
                            {
                                int sourceCanvasX = layer.Left + sourceX;
                                int sourceCanvasY = layer.Top + sourceY;
                                if (sourceCanvasX >= layerMask.Left && sourceCanvasX < layerMask.Right && sourceCanvasY >= layerMask.Top && sourceCanvasY < layerMask.Bottom)
                                {
                                    int maskX = sourceCanvasX - layerMask.Left;
                                    int maskIndex = (sourceCanvasY - layerMask.Top) * layerMask.Width + maskX;
                                    alpha16 = (ushort)(TryReadChannelSample16(maskChannel, maskIndex, out var maskAlpha16) ? MultiplyAlpha(alpha16, maskAlpha16) : 0);
                                }
                                else
                                {
                                    alpha16 = 0;
                                }
                            }
                            renderedImage.SetPixel(renderX, renderY, ReadChannelSample16(redChannel, sourceIndex), ReadChannelSample16(greenChannel, sourceIndex), ReadChannelSample16(blueChannel, sourceIndex), alpha16);
                        }
                    }
                    return renderedImage;
                }
                return null;
            }
            return null;
        }

        private static PsdRenderedImage RenderInternal(PsdLayer layer, bool includeHiddenLayers, bool applyClippingMasks, bool isRootCall, bool isPreviewRender, float previewScale, int renderBitDepth)
        {
            if (layer == null)
            {
                return null;
            }
            if (!layer.IsGroup)
            {
                return RenderLeaf(layer, isRootCall, isPreviewRender, previewScale, renderBitDepth);
            }
            return RenderGroup(layer, includeHiddenLayers, applyClippingMasks, isRootCall, isPreviewRender, previewScale, renderBitDepth);
        }

        private static bool TryGetLayerSequenceBounds(PsdLayer[] layers, bool includeHiddenLayers, out int left, out int top, out int width, out int height)
        {
            left = 0;
            top = 0;
            width = 0;
            height = 0;
            bool hasBounds = false;
            int right = 0;
            int bottom = 0;
            foreach (PsdLayer layer in layers)
            {
                if (layer != null && (includeHiddenLayers || layer.IsVisible) && layer.Width > 0 && layer.Height > 0)
                {
                    if (!hasBounds)
                    {
                        left = layer.Left;
                        top = layer.Top;
                        right = layer.Right;
                        bottom = layer.Bottom;
                        hasBounds = true;
                    }
                    else
                    {
                        left = Math.Min(left, layer.Left);
                        top = Math.Min(top, layer.Top);
                        right = Math.Max(right, layer.Right);
                        bottom = Math.Max(bottom, layer.Bottom);
                    }
                }
            }
            if (!hasBounds)
            {
                return false;
            }
            width = Math.Max(0, right - left);
            height = Math.Max(0, bottom - top);
            if (width > 0)
            {
                return height > 0;
            }
            return false;
        }

        private static int ResolveSequenceBitDepth(PsdLayer[] layers)
        {
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                if (layers[layerIndex] != null && layers[layerIndex].Depth == 16)
                {
                    return 16;
                }
            }
            return 8;
        }

        private static string BuildMergedLayersPathKey(PsdLayer[] layers)
        {
            if (layers != null && layers.Length != 0)
            {
                StringBuilder pathBuilder = new StringBuilder("merged:");
                foreach (PsdLayer layer in layers)
                {
                    if (layer != null)
                    {
                        if (pathBuilder.Length > 7)
                        {
                            pathBuilder.Append('|');
                        }
                        pathBuilder.Append(BuildLayerPathKey(layer));
                    }
                }
                return pathBuilder.ToString();
            }
            return string.Empty;
        }

        private static int ComputeMergedLayersWatermarkSalt(PsdLayer[] layers)
        {
            if (layers != null && layers.Length != 0)
            {
                int salt = 41;
                foreach (PsdLayer layer in layers)
                {
                    if (layer != null)
                    {
                        salt = (salt * 131) ^ ComputeLayerWatermarkSalt(layer);
                    }
                }
                return salt;
            }
            return 0;
        }

        private static int ComputeDocumentWatermarkSalt(PsdDocument document)
        {
            if (document != null)
            {
                int salt = 0xFDD ^ document.Width;
                salt = (salt * 197) ^ document.Height;
                PsdLayer[] childLayers = document.Childs;
                if (childLayers != null)
                {
                    salt = (salt * 17) ^ childLayers.Length;
                }
                return salt;
            }
            return 0;
        }

        private static int ComputeLayerWatermarkSalt(PsdLayer layer)
        {
            if (layer == null)
            {
                return 0;
            }
            int salt = 0xBC5 ^ layer.Left;
            salt = (salt * 197) ^ layer.Top;
            salt = (salt * 31) ^ layer.Width;
            salt = (salt * 17) ^ layer.Height;
            string name = layer.Name;
            if (!string.IsNullOrEmpty(name))
            {
                for (int characterIndex = 0; characterIndex < name.Length; characterIndex++)
                {
                    salt = (salt * 33) ^ name[characterIndex];
                }
            }
            return salt;
        }

        private static string BuildDocumentPathKey(PsdDocument document)
        {
            if (document != null)
            {
                return $"document[{document.Width}x{document.Height}]";
            }
            return "document[0]";
        }

        private static string BuildLayerPathKey(PsdLayer layer)
        {
            if (layer != null)
            {
                StringBuilder pathBuilder = new StringBuilder(128);
                Stack<string> pathSegments = new Stack<string>();
                for (PsdLayer currentLayer = layer; currentLayer != null; currentLayer = currentLayer.Parent)
                {
                    pathSegments.Push($"{NormalizePathSegment(currentLayer.Name)}[{ResolveSiblingOrdinal(currentLayer)}]");
                }
                pathBuilder.Append(BuildDocumentPathKey(layer.Document));
                while (pathSegments.Count > 0)
                {
                    pathBuilder.Append('/');
                    pathBuilder.Append(pathSegments.Pop());
                }
                return pathBuilder.ToString();
            }
            return "layer[0]";
        }

        private static int ResolveSiblingOrdinal(PsdLayer layer)
        {
            if (layer != null)
            {
                PsdLayer[] siblings = ((layer.Parent != null) ? layer.Parent.Childs : layer.Document?.Childs?.OfType<PsdLayer>().ToArray());
                if (siblings == null)
                {
                    return 0;
                }
                int siblingIndex = 0;
                while (true)
                {
                    if (siblingIndex < siblings.Length)
                    {
                        if (siblings[siblingIndex] == layer)
                        {
                            break;
                        }
                        siblingIndex++;
                        continue;
                    }
                    return 0;
                }
                return siblingIndex;
            }
            return 0;
        }

        private static string NormalizePathSegment(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "layer";
            }
            string trimmedName = rawName.Trim();
            StringBuilder normalizedName = new StringBuilder(trimmedName.Length);
            for (int characterIndex = 0; characterIndex < trimmedName.Length; characterIndex++)
            {
                char character = char.ToLowerInvariant(trimmedName[characterIndex]);
                if (char.IsLetterOrDigit(character))
                {
                    normalizedName.Append(character);
                }
                else if (character == '_' || character == '-' || char.IsWhiteSpace(character))
                {
                    normalizedName.Append('_');
                }
            }
            if (normalizedName.Length > 0)
            {
                return normalizedName.ToString();
            }
            string fallbackHash = AlwaysAuthorized.Sha256Hex(trimmedName);
            if (string.IsNullOrEmpty(fallbackHash))
            {
                return "layer";
            }
            return "layer_" + fallbackHash.Substring(0, Math.Min(10, fallbackHash.Length));
        }

        private static string BuildLayerNormalizedBounds(PsdLayer layer)
        {
            if (layer == null)
            {
                return "0:0:0:0";
            }
            int canvasWidth = ((layer.Document != null) ? layer.Document.Width : layer.Width);
            int canvasHeight = ((layer.Document == null) ? layer.Height : layer.Document.Height);
            return BuildNormalizedBounds(layer.Left, layer.Top, layer.Width, layer.Height, canvasWidth, canvasHeight);
        }

        private static string BuildNormalizedBounds(int left, int top, int width, int height, int canvasWidth, int canvasHeight)
        {
            int safeCanvasWidth = Math.Max(1, canvasWidth);
            int safeCanvasHeight = Math.Max(1, canvasHeight);
            int right = left + Math.Max(0, width);
            int bottom = top + Math.Max(0, height);
            return QuantizeNormalized(left, safeCanvasWidth) + ":" + QuantizeNormalized(top, safeCanvasHeight) + ":" + QuantizeNormalized(right, safeCanvasWidth) + ":" + QuantizeNormalized(bottom, safeCanvasHeight);
        }

        private static int QuantizeNormalized(int value, int maxValue)
        {
            return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, (double)value / (double)Math.Max(1, maxValue))) * 10000.0, MidpointRounding.AwayFromZero);
        }

        private static float ResolvePreviewRenderScale(int width, int height)
        {
            int maxDimension = Math.Max(Math.Max(0, width), Math.Max(0, height));
            if (maxDimension <= 0)
            {
                return 0.5f;
            }
            return Math.Min(0.5f, 1024f / (float)maxDimension);
        }

        private static int ScalePreviewDimension(int value, float scale)
        {
            if (value <= 0)
            {
                return 0;
            }
            if (scale >= 0.9999f)
            {
                return value;
            }
            return Math.Max(1, Mathf.RoundToInt((float)value * scale));
        }

        private static int ScalePreviewCoordinate(int value, float scale)
        {
            if (scale >= 0.9999f)
            {
                return value;
            }
            return Mathf.RoundToInt((float)value * scale);
        }

        private static int ScalePreviewSampleCoordinate(int value, float scale, int sourceLimit)
        {
            if (sourceLimit <= 0)
            {
                return 0;
            }
            if (scale >= 0.9999f)
            {
                return Mathf.Clamp(value, 0, sourceLimit - 1);
            }
            return Mathf.Clamp(Mathf.FloorToInt((float)value / scale), 0, sourceLimit - 1);
        }

        private static int ResolveRenderBitDepth(int sourceBitDepth, bool isPreviewRender)
        {
            if (isPreviewRender || sourceBitDepth != 16)
            {
                return 8;
            }
            // 已删除 License：默认完全授权，16-bit 始终可用。
            return 16;
        }

        private static void CompositeImage(PsdRenderedImage destination, PsdRenderedImage source)
        {
            if (destination == null || source == null || destination.IsEmpty || source.IsEmpty)
            {
                return;
            }
            if (!destination.IsHighBitDepth)
            {
                int offsetX = source.Left - destination.Left;
                int offsetY = source.Top - destination.Top;
                for (int sourceY = 0; sourceY < source.Height; sourceY++)
                {
                    int destinationY = offsetY + sourceY;
                    if (destinationY < 0 || destinationY >= destination.Height)
                    {
                        continue;
                    }
                    for (int sourceX = 0; sourceX < source.Width; sourceX++)
                    {
                        int destinationX = offsetX + sourceX;
                        if (destinationX >= 0 && destinationX < destination.Width)
                        {
                            int pixelOffset = source.GetPixelOffset(sourceX, sourceY);
                            byte sourceAlpha = ReadRenderedSample8(source, pixelOffset + 3);
                            if (sourceAlpha > 0)
                            {
                                destination.CompositePixel(destinationX, destinationY, ReadRenderedSample8(source, pixelOffset), ReadRenderedSample8(source, pixelOffset + 1), ReadRenderedSample8(source, pixelOffset + 2), sourceAlpha);
                            }
                        }
                    }
                }
            }
            else
            {
                CompositeImage16(destination, source);
            }
        }

        private static void CompositeImage16(PsdRenderedImage destination, PsdRenderedImage source)
        {
            int offsetX = source.Left - destination.Left;
            int offsetY = source.Top - destination.Top;
            for (int sourceY = 0; sourceY < source.Height; sourceY++)
            {
                int destinationY = offsetY + sourceY;
                if (destinationY < 0 || destinationY >= destination.Height)
                {
                    continue;
                }
                for (int sourceX = 0; sourceX < source.Width; sourceX++)
                {
                    int destinationX = offsetX + sourceX;
                    if (destinationX >= 0 && destinationX < destination.Width)
                    {
                        int pixelOffset = source.GetPixelOffset(sourceX, sourceY);
                        ushort sourceAlpha = ReadRenderedSample16(source, pixelOffset + 3);
                        if (sourceAlpha > 0)
                        {
                            destination.CompositePixel(destinationX, destinationY, ReadRenderedSample16(source, pixelOffset), ReadRenderedSample16(source, pixelOffset + 1), ReadRenderedSample16(source, pixelOffset + 2), sourceAlpha);
                        }
                    }
                }
            }
        }

        private static byte[] BuildPlacedAlphaMask(PsdRenderedImage source, int destinationLeft, int destinationTop, int destinationWidth, int destinationHeight)
        {
            byte[] placedAlphaMask = new byte[destinationWidth * destinationHeight];
            int offsetX = source.Left - destinationLeft;
            int offsetY = source.Top - destinationTop;
            for (int sourceY = 0; sourceY < source.Height; sourceY++)
            {
                int destinationY = offsetY + sourceY;
                if (destinationY < 0 || destinationY >= destinationHeight)
                {
                    continue;
                }
                for (int sourceX = 0; sourceX < source.Width; sourceX++)
                {
                    int destinationX = offsetX + sourceX;
                    if (destinationX >= 0 && destinationX < destinationWidth)
                    {
                        int pixelOffset = source.GetPixelOffset(sourceX, sourceY);
                        placedAlphaMask[destinationY * destinationWidth + destinationX] = ReadRenderedSample8(source, pixelOffset + 3);
                    }
                }
            }
            return placedAlphaMask;
        }

        private static ushort[] BuildPlacedAlphaMask16(PsdRenderedImage source, int destinationLeft, int destinationTop, int destinationWidth, int destinationHeight)
        {
            ushort[] placedAlphaMask = new ushort[destinationWidth * destinationHeight];
            int offsetX = source.Left - destinationLeft;
            int offsetY = source.Top - destinationTop;
            for (int sourceY = 0; sourceY < source.Height; sourceY++)
            {
                int destinationY = offsetY + sourceY;
                if (destinationY < 0 || destinationY >= destinationHeight)
                {
                    continue;
                }
                for (int sourceX = 0; sourceX < source.Width; sourceX++)
                {
                    int destinationX = offsetX + sourceX;
                    if (destinationX >= 0 && destinationX < destinationWidth)
                    {
                        int pixelOffset = source.GetPixelOffset(sourceX, sourceY);
                        placedAlphaMask[destinationY * destinationWidth + destinationX] = ReadRenderedSample16(source, pixelOffset + 3);
                    }
                }
            }
            return placedAlphaMask;
        }

        private static void ApplyClippingMask(PsdRenderedImage source, byte[] clippingMask, int clippingLeft, int clippingTop, int clippingWidth, int clippingHeight)
        {
            if (source == null || source.IsEmpty || clippingMask == null)
            {
                return;
            }
            int offsetX = source.Left - clippingLeft;
            int offsetY = source.Top - clippingTop;
            for (int sourceY = 0; sourceY < source.Height; sourceY++)
            {
                int clippingY = offsetY + sourceY;
                if (clippingY < 0 || clippingY >= clippingHeight)
                {
                    continue;
                }
                for (int sourceX = 0; sourceX < source.Width; sourceX++)
                {
                    int clippingX = offsetX + sourceX;
                    if (clippingX >= 0 && clippingX < clippingWidth)
                    {
                        byte alphaMask = clippingMask[clippingY * clippingWidth + clippingX];
                        source.MultiplyPixelAlpha(sourceX, sourceY, alphaMask);
                    }
                }
            }
        }

        private static void ApplyClippingMask(PsdRenderedImage source, ushort[] clippingMask, int clippingLeft, int clippingTop, int clippingWidth, int clippingHeight)
        {
            if (source == null || source.IsEmpty || clippingMask == null)
            {
                return;
            }
            int offsetX = source.Left - clippingLeft;
            int offsetY = source.Top - clippingTop;
            for (int sourceY = 0; sourceY < source.Height; sourceY++)
            {
                int clippingY = offsetY + sourceY;
                if (clippingY < 0 || clippingY >= clippingHeight)
                {
                    continue;
                }
                for (int sourceX = 0; sourceX < source.Width; sourceX++)
                {
                    int clippingX = offsetX + sourceX;
                    if (clippingX >= 0 && clippingX < clippingWidth)
                    {
                        ushort alphaMask = clippingMask[clippingY * clippingWidth + clippingX];
                        source.MultiplyPixelAlpha(sourceX, sourceY, alphaMask);
                    }
                }
            }
        }

        private static ushort ReadChannelSample16(Channel channel, int index)
        {
            if (channel != null)
            {
                if (channel.Data16 != null && index >= 0 && index < channel.Data16.Length)
                {
                    return channel.Data16[index];
                }
                if (channel.Data == null || index < 0 || index >= channel.Data.Length)
                {
                    return 0;
                }
                return PsdBinaryUtility.ConvertByteToUInt16(channel.Data[index]);
            }
            return 0;
        }

        private static bool TryReadChannelSample16(Channel channel, int index, out ushort value)
        {
            value = 0;
            if (channel == null)
            {
                return false;
            }
            if (channel.Data16 != null && index >= 0 && index < channel.Data16.Length)
            {
                value = channel.Data16[index];
                return true;
            }
            if (channel.Data != null && index >= 0 && index < channel.Data.Length)
            {
                value = PsdBinaryUtility.ConvertByteToUInt16(channel.Data[index]);
                return true;
            }
            return false;
        }

        private static byte ReadRenderedSample8(PsdRenderedImage image, int index)
        {
            if (image == null)
            {
                return 0;
            }
            if (image.RawRgba32 != null && index >= 0 && index < image.RawRgba32.Length)
            {
                return image.RawRgba32[index];
            }
            if (image.RawRgba64 != null && index >= 0 && index < image.RawRgba64.Length)
            {
                return PsdBinaryUtility.ConvertUInt16ToByte(image.RawRgba64[index]);
            }
            return 0;
        }

        private static ushort ReadRenderedSample16(PsdRenderedImage image, int index)
        {
            if (image != null)
            {
                if (image.RawRgba64 != null && index >= 0 && index < image.RawRgba64.Length)
                {
                    return image.RawRgba64[index];
                }
                if (image.RawRgba32 != null && index >= 0 && index < image.RawRgba32.Length)
                {
                    return PsdBinaryUtility.ConvertByteToUInt16(image.RawRgba32[index]);
                }
                return 0;
            }
            return 0;
        }

        internal static bool IsObfuscationSentinelValid()

        {

            return true;

        }
}
}
