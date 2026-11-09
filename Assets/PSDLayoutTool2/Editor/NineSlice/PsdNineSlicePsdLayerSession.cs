namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using PhotoshopFile;
    using UnityEngine;

    /// <summary>
    /// One visible, pixel-bearing art layer exposed by the PSD nine-slice
    /// editor. Preview textures are decoded lazily by the owning session.
    /// </summary>
    public sealed class PsdNineSlicePsdLayerEntry
    {
        internal PsdNineSlicePsdLayerEntry(Layer layer, string displayName, int depth)
        {
            Layer = layer;
            DisplayName = displayName;
            Depth = depth;
        }

        public Layer Layer { get; private set; }
        public string DisplayName { get; private set; }
        public int Depth { get; private set; }
        public uint LayerId { get { return Layer == null ? 0U : Layer.Id; } }
        public Rect Rect { get { return Layer == null ? default(Rect) : Layer.RenderRect; } }
        public bool IsVisibleRasterLeaf
        {
            get
            {
                return Layer != null && Layer.ClippingBase == null && Layer.Visible && !Layer.IsTextLayer &&
                    Layer.Children != null && Layer.Children.Count == 0 &&
                    !Layer.IsPixelDataIrrelevant && Layer.Rect.width > 0f && Layer.Rect.height > 0f;
            }
        }
    }

    /// <summary>
    /// Loads visible PSD raster layers for the editor window and owns decoded
    /// preview textures. This keeps PSD parsing and native texture lifetime
    /// out of the window UI and prevents preview objects from leaking between
    /// PSD selections.
    /// </summary>
    public sealed class PsdNineSlicePsdLayerSession : IDisposable
    {
        private readonly Dictionary<Layer, Texture2D> previewsByLayer = new Dictionary<Layer, Texture2D>();
        private readonly List<PsdNineSlicePsdLayerEntry> layers = new List<PsdNineSlicePsdLayerEntry>();
        private bool disposed;

        private PsdNineSlicePsdLayerSession(string assetPath, PsdFile psd)
        {
            AssetPath = assetPath;
            Psd = psd;
            AddVisibleRasterLayers(psd == null ? null : psd.Layers, true, 0);
        }

        public string AssetPath { get; private set; }
        public PsdFile Psd { get; private set; }
        public IList<PsdNineSlicePsdLayerEntry> Layers { get { return layers; } }

        public static PsdNineSlicePsdLayerSession Open(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("A PSD asset path is required.", "assetPath");
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            return new PsdNineSlicePsdLayerSession(assetPath, new PsdFile(fullPath));
        }

        public Texture2D GetPreview(PsdNineSlicePsdLayerEntry entry)
        {
            if (disposed || entry == null || entry.Layer == null)
            {
                return null;
            }

            if (previewsByLayer.TryGetValue(entry.Layer, out Texture2D cached))
            {
                return cached;
            }

            Texture2D preview = ImageDecoder.DecodeImage(entry.Layer);
            if (preview == null)
            {
                return null;
            }

            preview.name = "PSD 9-Slice Preview " + entry.DisplayName;
            preview.hideFlags = HideFlags.HideAndDontSave;
            previewsByLayer[entry.Layer] = preview;

            return preview;
        }

        /// <summary>
        /// 从当前磁盘 PSD 按稳定 ID 解码完整图层，不使用窗口缓存或导出的 PNG。
        /// </summary>
        internal static bool TryReadOriginalRaster(string assetPath, uint layerId,
            out PsdNineSliceRaster raster, out string error)
        {
            raster = null;
            error = string.Empty;
            try
            {
                using (var session = Open(assetPath))
                {
                    PsdNineSlicePsdLayerEntry match = null;
                    foreach (var entry in session.Layers)
                    {
                        if (entry.LayerId != layerId) continue;
                        if (match != null) throw new InvalidDataException("PSD 中存在重复的 Photoshop 图层 ID。 ");
                        match = entry;
                    }

                    if (layerId == 0U || match == null)
                        throw new InvalidDataException("源 PSD 中找不到可解码的原始图层 ID：" + layerId);

                    // 新会话的 GetPreview 直接调用完整 ImageDecoder；没有缩放、九宫裁剪或缩略图路径。
                    Texture2D texture = session.GetPreview(match);
                    if (texture == null || texture.width != (int)match.Rect.width || texture.height != (int)match.Rect.height)
                        throw new InvalidDataException("解码像素尺寸与 PSD 原始渲染矩形不一致。");
                    raster = PsdNineSliceTextureProcessor.ReadTextureRaster(texture);
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = "无法读取原始 PSD 图层：" + exception.Message;
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (Texture2D preview in previewsByLayer.Values)
            {
                if (preview != null)
                {
                    UnityEngine.Object.DestroyImmediate(preview);
                }
            }

            previewsByLayer.Clear();
            layers.Clear();
            Psd = null;
        }

        private void AddVisibleRasterLayers(IList<Layer> source, bool ancestorsVisible, int depth)
        {
            if (source == null)
            {
                return;
            }

            foreach (Layer layer in source)
            {
                if (layer == null)
                {
                    continue;
                }

                bool visible = ancestorsVisible && layer.Visible;
                bool isLeaf = layer.Children == null || layer.Children.Count == 0;
                if (visible && isLeaf && layer.ClippingBase == null && !layer.IsTextLayer && !layer.IsPixelDataIrrelevant &&
                    layer.Rect.width > 0f && layer.Rect.height > 0f)
                {
                    string name = string.IsNullOrEmpty(layer.Name) ? "<unnamed layer>" : layer.Name;
                    layers.Add(new PsdNineSlicePsdLayerEntry(layer, name, depth));
                }

                if (layer.Children != null && layer.Children.Count > 0)
                {
                    AddVisibleRasterLayers(layer.Children, visible, depth + 1);
                }
            }
        }
    }
}
