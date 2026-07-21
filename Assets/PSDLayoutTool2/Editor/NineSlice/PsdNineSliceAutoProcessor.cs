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
    }
}
