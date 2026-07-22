namespace PsdLayoutTool2
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Runs the PSD name-driven 9-slice conversion before a generated PNG is
    /// written. It has no AssetDatabase dependency so importer orchestration
    /// remains separate from the pixel transform.
    /// </summary>
    public static class PsdNineSliceUnityAutoProcessor
    {
        /// <summary>
        /// Scales the source raster into the same pixel coordinate system as
        /// the generated RectTransform before deriving the Sprite border.
        /// This keeps a physically cropped PNG and its border aligned when a
        /// PSD is fitted into a smaller target Canvas.
        /// </summary>
        public static bool TryProcess(
            Texture2D source,
            PsdNineSliceNameRule rule,
            float horizontalScale,
            float verticalScale,
            out byte[] png,
            out PsdNineSliceBorder border,
            out string reason)
        {
            png = null;
            border = null;
            reason = string.Empty;
            if (source == null || horizontalScale <= 0f || verticalScale <= 0f)
            {
                reason = "The source texture or target Canvas scale is invalid.";
                return false;
            }

            if (Mathf.Approximately(horizontalScale, 1f) && Mathf.Approximately(verticalScale, 1f))
            {
                return TryProcess(source, rule, out png, out border, out reason);
            }

            PsdNineSliceRaster sourceRaster;
            PsdNineSliceRaster sourceResult;
            PsdNineSliceBorder sourceBorder;
            if (!TryBuildSourceConversion(source, rule, out sourceRaster, out sourceResult, out sourceBorder, out reason))
            {
                return false;
            }

            Texture2D scaled = Resize(source, horizontalScale, verticalScale);
            try
            {
                border = sourceBorder.Scale(horizontalScale, verticalScale);
                PsdNineSliceRaster scaledRaster = ToTopLeftRaster(scaled);
                if (!border.IsValidFor(scaledRaster.Width, scaledRaster.Height))
                {
                    reason = "The target Canvas scale leaves no valid 9-slice center.";
                    border = null;
                    return false;
                }

                // The source safety pass may have deliberately kept a baked
                // card/label raster whole. It still needs target-canvas
                // resampling, but must not be cropped after that decision.
                if (sourceResult.Width == sourceRaster.Width && sourceResult.Height == sourceRaster.Height)
                {
                    png = EncodeTopLeftRaster(scaledRaster);
                    return png != null && png.Length > 0;
                }

                PsdNineSliceNameRule scaledRule = new PsdNineSliceNameRule(rule.Mode, border);
                return TryProcess(scaled, scaledRule, out png, out border, out reason);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scaled);
            }
        }

        private static bool TryBuildSourceConversion(
            Texture2D source,
            PsdNineSliceNameRule rule,
            out PsdNineSliceRaster sourceRaster,
            out PsdNineSliceRaster sourceResult,
            out PsdNineSliceBorder sourceBorder,
            out string reason)
        {
            sourceRaster = null;
            sourceResult = null;
            sourceBorder = null;
            reason = string.Empty;
            try
            {
                sourceRaster = ToTopLeftRaster(source);
                return PsdNineSliceAutoProcessor.TryProcessRaster(
                    sourceRaster,
                    rule,
                    out sourceResult,
                    out sourceBorder,
                    out reason);
            }
            catch (Exception exception)
            {
                reason = "Unable to analyze generated PSD pixels: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Infers or applies a border, crops the raster to Unity's minimal
        /// sliced source, and returns PNG bytes plus the matching border.
        /// </summary>
        public static bool TryProcess(
            Texture2D source,
            PsdNineSliceNameRule rule,
            out byte[] png,
            out PsdNineSliceBorder border,
            out string reason)
        {
            png = null;
            border = null;
            reason = string.Empty;
            if (source == null || rule == null)
            {
                reason = "The source texture or nine-slice rule is missing.";
                return false;
            }

            PsdNineSliceRaster raster;
            try
            {
                raster = ToTopLeftRaster(source);
            }
            catch (Exception exception)
            {
                reason = "Unable to read generated PSD pixels: " + exception.Message;
                return false;
            }

            try
            {
                PsdNineSliceRaster cropped;
                if (!PsdNineSliceAutoProcessor.TryProcessRaster(raster, rule, out cropped, out border, out reason))
                {
                    return false;
                }

                png = EncodeTopLeftRaster(cropped);
                if (png == null || png.Length == 0)
                {
                    png = null;
                    border = null;
                    reason = "Unity could not encode the cropped nine-slice texture as PNG.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                reason = "Unable to crop generated nine-slice pixels: " + exception.Message;
                border = null;
                return false;
            }
        }

        /// <summary>
        /// Normalizes Unity's bottom-left texture memory to a top-left raster,
        /// which keeps PSD naming and border order intuitive in pixel analysis.
        /// </summary>
        private static PsdNineSliceRaster ToTopLeftRaster(Texture2D texture)
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

            return new PsdNineSliceRaster(texture.width, texture.height, pixels);
        }

        private static byte[] EncodeTopLeftRaster(PsdNineSliceRaster raster)
        {
            Texture2D texture = new Texture2D(raster.Width, raster.Height, TextureFormat.RGBA32, false);
            try
            {
                Color32[] colors = new Color32[raster.Width * raster.Height];
                for (int y = 0; y < raster.Height; y++)
                {
                    int unityY = raster.Height - 1 - y;
                    for (int x = 0; x < raster.Width; x++)
                    {
                        int sourceOffset = ((y * raster.Width) + x) * 4;
                        colors[(unityY * raster.Width) + x] = new Color32(
                            raster.Pixels[sourceOffset],
                            raster.Pixels[sourceOffset + 1],
                            raster.Pixels[sourceOffset + 2],
                            raster.Pixels[sourceOffset + 3]);
                    }
                }

                texture.SetPixels32(colors);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D Resize(Texture2D source, float horizontalScale, float verticalScale)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * horizontalScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * verticalScale));
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] targetPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Min(source.height - 1, Mathf.FloorToInt(y / verticalScale));
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Min(source.width - 1, Mathf.FloorToInt(x / horizontalScale));
                    targetPixels[(y * width) + x] = sourcePixels[(sourceY * source.width) + sourceX];
                }
            }

            Texture2D resized = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                resized.SetPixels32(targetPixels);
                resized.Apply();
                return resized;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(resized);
                throw;
            }
        }
    }
}
