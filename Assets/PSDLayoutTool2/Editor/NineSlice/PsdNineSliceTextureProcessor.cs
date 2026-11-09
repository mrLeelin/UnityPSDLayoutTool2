namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor adapter between the pure nine-slice core and generated PNG assets.
    /// </summary>
    public static class PsdNineSliceTextureProcessor
    {
        /// <summary>
        /// Reads a generated PNG without changing it and computes a candidate.
        /// </summary>
        public static bool TryAnalyze(string assetPath, out PsdNineSliceInference inference, out string error)
        {
            inference = null;
            PsdNineSliceRaster raster;
            if (!TryReadSourceRaster(assetPath, out raster, out error))
            {
                return false;
            }

            if (!PsdNineSliceAnalyzer.TryInfer(raster, out inference))
            {
                error = "The image does not contain enough visible pixel structure for a safe 9-slice candidate.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Computes a candidate from an in-memory PSD layer preview without
        /// importing or writing an intermediate PNG asset.
        /// </summary>
        public static bool TryAnalyze(Texture2D texture, out PsdNineSliceInference inference, out string error)
        {
            inference = null;
            error = string.Empty;
            if (texture == null)
            {
                error = "The selected PSD layer has no preview texture.";
                return false;
            }

            try
            {
                Color32[] colors = texture.GetPixels32();
                byte[] pixels = new byte[colors.Length * 4];
                for (int y = 0; y < texture.height; y++)
                {
                    int unityY = texture.height - 1 - y;
                    for (int x = 0; x < texture.width; x++)
                    {
                        Color32 color = colors[(unityY * texture.width) + x];
                        int offset = ((y * texture.width) + x) * 4;
                        pixels[offset] = color.r;
                        pixels[offset + 1] = color.g;
                        pixels[offset + 2] = color.b;
                        pixels[offset + 3] = color.a;
                    }
                }

                if (!PsdNineSliceAnalyzer.TryInfer(new PsdNineSliceRaster(texture.width, texture.height, pixels), out inference))
                {
                    error = "The layer does not contain enough visible pixel structure for a safe 9-slice candidate.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "Unable to analyze PSD layer pixels: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 从 PNG 记录的源 PSD 解码完整图层后裁剪，调用方负责导入与失败回滚。
        /// </summary>
        public static bool TryCropAndPersist(
            string assetPath,
            TextureImporter textureImporter,
            uint layerId,
            PsdNineSliceBorder border,
            out string error)
        {
            error = string.Empty;
            if (textureImporter == null)
            {
                error = "TextureImporter was not found.";
                return false;
            }

            string psdPath;
            uint recordedId;
            if (!PsdNineSliceAssetState.TryReadSourcePsdIdentity(textureImporter.userData, out psdPath) ||
                !PsdNineSliceAssetState.TryReadLayerIdentity(textureImporter.userData, out recordedId) ||
                recordedId != layerId || layerId == 0U)
            {
                error = "PNG 缺少匹配的源 PSD 或 Photoshop 图层 ID，不能裁剪旧 PNG。";
                return false;
            }

            PsdNineSliceRaster source;
            if (!PsdNineSlicePsdLayerSession.TryReadOriginalRaster(psdPath, layerId, out source, out error))
            {
                return false;
            }

            return TryCropAndPersist(assetPath, textureImporter, layerId, border, source, out error);
        }

        /// <summary>
        /// 从完整原始像素重建 PNG；像素和配方保持现有自下而上的哈希约定。
        /// 调用方负责失败回滚及导入设置的持久化。
        /// </summary>
        internal static bool TryCropAndPersist(string assetPath, TextureImporter textureImporter,
            uint layerId, PsdNineSliceBorder border, PsdNineSliceRaster source, out string error)
        {
            error = string.Empty;
            if (textureImporter == null || source == null)
            {
                error = "缺少纹理导入器或完整原始图层像素。";
                return false;
            }

            if (border == null || !border.IsValidFor(source.Width, source.Height))
            {
                error = "The selected 9-slice border overlaps or exceeds the source image.";
                return false;
            }

            PsdNineSliceRaster cropped = CropTextureRaster(source, border);
            try
            {
                WriteRaster(assetPath, cropped);
                textureImporter.userData = PsdNineSliceAssetState.Write(
                    textureImporter.userData,
                    layerId,
                    ComputeHash(source.Pixels),
                    ComputeHash(cropped.Pixels),
                    border);
                return true;
            }
            catch (Exception exception)
            {
                error = "Unable to write the cropped 9-slice PNG: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Reapplies a confirmed crop only when the freshly exported PSD layer
        /// still exactly matches the source hash it was confirmed against.
        /// </summary>
        public static bool TryReapplyPersisted(
            string assetPath,
            TextureImporter textureImporter,
            uint expectedLayerId,
            out Vector4 unityBorder,
            out string reason)
        {
            unityBorder = Vector4.zero;
            reason = string.Empty;
            PsdNineSliceAssetState state;
            if (textureImporter == null || !PsdNineSliceAssetState.TryRead(textureImporter.userData, out state))
            {
                return false;
            }

            if (state.LayerId != expectedLayerId)
            {
                reason = "The stored 9-slice decision belongs to a different PSD layer.";
                return false;
            }

            PsdNineSliceRaster source;
            string error;
            if (!TryReadSourceRaster(assetPath, out source, out error))
            {
                reason = error;
                return false;
            }

            string currentHash = ComputeHash(source.Pixels);
            if (string.Equals(currentHash, state.OutputHash, StringComparison.Ordinal))
            {
                unityBorder = ToUnityBorder(state.Border);
                return true;
            }

            if (!string.Equals(currentHash, state.SourceHash, StringComparison.Ordinal))
            {
                reason = "The PSD layer pixels changed; review the new 9-slice candidate before recropping.";
                return false;
            }

            if (!state.Border.IsValidFor(source.Width, source.Height))
            {
                reason = "The saved 9-slice border is no longer valid for the source image.";
                return false;
            }

            try
            {
                WriteRaster(assetPath, CropTextureRaster(source, state.Border));
                unityBorder = ToUnityBorder(state.Border);
                return true;
            }
            catch (Exception exception)
            {
                reason = "Unable to recreate the cropped 9-slice PNG: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Converts author-facing left/top/right/bottom pixels to Unity's
        /// TextureImporter left/bottom/right/top border order.
        /// </summary>
        public static Vector4 ToUnityBorder(PsdNineSliceBorder border)
        {
            return new Vector4(border.Left, border.Bottom, border.Right, border.Top);
        }

        private static PsdNineSliceRaster CropTextureRaster(PsdNineSliceRaster source, PsdNineSliceBorder border)
        {
            // 与自动生成链路一样，在左上原点中裁剪，再恢复 Unity 像素顺序。
            // 不能只交换上下边距，否则两像素中心样本的位置与自动生成链路不同。
            return FlipRows(PsdNineSliceCropper.CropToMinimum(FlipRows(source), border));
        }

        private static PsdNineSliceRaster FlipRows(PsdNineSliceRaster source)
        {
            int rowBytes = source.Width * 4;
            byte[] pixels = new byte[source.Pixels.Length];
            for (int y = 0; y < source.Height; y++)
                Buffer.BlockCopy(source.Pixels, y * rowBytes, pixels, (source.Height - 1 - y) * rowBytes, rowBytes);
            return new PsdNineSliceRaster(source.Width, source.Height, pixels);
        }

        internal static PsdNineSliceRaster ReadTextureRaster(Texture2D texture)
        {
            Color32[] colors = texture.GetPixels32();
            byte[] pixels = new byte[colors.Length * 4];
            for (int index = 0; index < colors.Length; index++)
            {
                int offset = index * 4;
                pixels[offset] = colors[index].r;
                pixels[offset + 1] = colors[index].g;
                pixels[offset + 2] = colors[index].b;
                pixels[offset + 3] = colors[index].a;
            }

            return new PsdNineSliceRaster(texture.width, texture.height, pixels);
        }

        private static bool TryReadSourceRaster(string assetPath, out PsdNineSliceRaster raster, out string error)
        {
            raster = null;
            error = string.Empty;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                error = "Select a PNG asset inside this Unity project.";
                return false;
            }

            string fullPath = GetFullAssetPath(assetPath);
            if (!File.Exists(fullPath))
            {
                error = "The selected PNG file does not exist.";
                return false;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                byte[] encoded = File.ReadAllBytes(fullPath);
                if (!ImageConversion.LoadImage(texture, encoded, false))
                {
                    error = "Unity could not decode the selected PNG.";
                    return false;
                }

                raster = ReadTextureRaster(texture);
                return true;
            }
            catch (Exception exception)
            {
                error = "Unable to read PNG pixels: " + exception.Message;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteRaster(string assetPath, PsdNineSliceRaster raster)
        {
            Texture2D texture = new Texture2D(raster.Width, raster.Height, TextureFormat.RGBA32, false);
            try
            {
                Color32[] colors = new Color32[raster.Width * raster.Height];
                for (int index = 0; index < colors.Length; index++)
                {
                    int offset = index * 4;
                    colors[index] = new Color32(
                        raster.Pixels[offset],
                        raster.Pixels[offset + 1],
                        raster.Pixels[offset + 2],
                        raster.Pixels[offset + 3]);
                }

                texture.SetPixels32(colors);
                File.WriteAllBytes(GetFullAssetPath(assetPath), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string ComputeHash(byte[] source)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(source);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string GetFullAssetPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
