namespace UGF.EditorTools.Psd2UGUI.NineSlice
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor adapter between the pure nine-slice core and generated PNG assets.
    /// </summary>
    public static class Psd2UiNineSliceTextureProcessor
    {
        /// <summary>
        /// Reads a generated PNG without changing it and computes a candidate.
        /// </summary>
        public static bool TryAnalyze(string assetPath, out Psd2UiNineSliceInference inference, out string error)
        {
            inference = null;
            Psd2UiNineSliceRaster raster;
            if (!TryReadSourceRaster(assetPath, out raster, out error))
            {
                return false;
            }

            if (!Psd2UiNineSliceAnalyzer.TryInfer(raster, out inference))
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
        public static bool TryAnalyze(Texture2D texture, out Psd2UiNineSliceInference inference, out string error)
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

                if (!Psd2UiNineSliceAnalyzer.TryInfer(new Psd2UiNineSliceRaster(texture.width, texture.height, pixels), out inference))
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

        // DISABLED: TryCropAndPersist and TryReapplyPersisted require Psd2UiNineSliceAssetState
        // which was removed (part of the visual editor). These functions are optional and not
        // used by the core nine-slice functionality.

        /*
        /// <summary>
        /// Crops a confirmed source PNG and stores its reusable recipe in the
        /// TextureImporter's userData. The caller is responsible for reimporting.
        /// </summary>
        public static bool TryCropAndPersist(
            string assetPath,
            TextureImporter textureImporter,
            uint layerId,
            Psd2UiNineSliceBorder border,
            out string error)
        {
            error = "TryCropAndPersist is disabled (requires Psd2UiNineSliceAssetState)";
            return false;
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
            reason = "TryReapplyPersisted is disabled (requires Psd2UiNineSliceAssetState)";
            return false;
        }
        */

        /// <summary>
        /// Converts author-facing left/top/right/bottom pixels to Unity's
        /// TextureImporter left/bottom/right/top border order.
        /// </summary>
        public static Vector4 ToUnityBorder(Psd2UiNineSliceBorder border)
        {
            return new Vector4(border.Left, border.Bottom, border.Right, border.Top);
        }

        private static bool TryReadSourceRaster(string assetPath, out Psd2UiNineSliceRaster raster, out string error)
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

                raster = new Psd2UiNineSliceRaster(texture.width, texture.height, pixels);
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

        private static void WriteRaster(string assetPath, Psd2UiNineSliceRaster raster)
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
