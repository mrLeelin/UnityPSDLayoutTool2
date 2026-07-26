namespace PsdLayoutTool2.Editor
{
    using System;
    using System.IO;
    using PhotoshopFile;
    using UnityEngine;

    internal static class PsdHierarchyCompositePreviewWriter
    {
        public static Texture2D BuildTexture(string psdAssetPath)
        {
            if (string.IsNullOrWhiteSpace(psdAssetPath))
            {
                throw new ArgumentException("PSD asset path is required.", "psdAssetPath");
            }
            string fullPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                psdAssetPath.Replace('/', Path.DirectorySeparatorChar)));
            var psd = new PsdFile(fullPath);
            EnsureSupportedMergedImageCompression(psd.ImageCompression);
            return BuildCompositeTexture(psd);
        }

        internal static void EnsureSupportedMergedImageCompression(
            ImageCompression compression)
        {
            if (compression != ImageCompression.Raw &&
                compression != ImageCompression.Rle)
            {
                throw new NotSupportedException(
                    "Merged PSD composite compression is not supported: " +
                    compression + ".");
            }
        }

        private static Texture2D BuildCompositeTexture(PsdFile psd)
        {
            if (psd == null)
            {
                throw new ArgumentNullException("psd");
            }
            if (psd.ColorMode != ColorModes.RGB)
            {
                throw new NotSupportedException(
                    "Composite previews currently require an RGB PSD.");
            }
            if (psd.Depth != 8 && psd.Depth != 16)
            {
                throw new NotSupportedException(
                    "Composite previews currently require 8-bit or 16-bit RGB channels.");
            }

            byte[][] channels = psd.ImageData;
            if (channels == null || channels.Length < 3)
            {
                throw new InvalidDataException(
                    "The PSD has no decoded merged RGB image channels.");
            }

            int bytesPerSample = psd.Depth == 16 ? 2 : 1;
            int pixelCount = checked(psd.Width * psd.Height);
            int requiredChannelLength = checked(pixelCount * bytesPerSample);
            for (int channelIndex = 0; channelIndex < 3; channelIndex++)
            {
                if (channels[channelIndex] == null ||
                    channels[channelIndex].Length < requiredChannelLength)
                {
                    throw new InvalidDataException(
                        "A decoded merged RGB channel is incomplete.");
                }
            }

            bool hasAlpha = channels.Length > 3 &&
                            channels[3] != null &&
                            channels[3].Length >= requiredChannelLength;
            var colors = new Color32[pixelCount];
            for (int sourceY = 0; sourceY < psd.Height; sourceY++)
            {
                int sourceRow = sourceY * psd.Width;
                int textureRow = (psd.Height - 1 - sourceY) * psd.Width;
                for (int x = 0; x < psd.Width; x++)
                {
                    int sourceIndex = (sourceRow + x) * bytesPerSample;
                    colors[textureRow + x] = new Color32(
                        channels[0][sourceIndex],
                        channels[1][sourceIndex],
                        channels[2][sourceIndex],
                        hasAlpha ? channels[3][sourceIndex] : byte.MaxValue);
                }
            }

            Texture2D texture = null;
            bool ownershipTransferred = false;
            try
            {
                texture = new Texture2D(
                    psd.Width, psd.Height, TextureFormat.RGBA32, false);
                texture.SetPixels32(colors);
                texture.Apply(false, false);
                ownershipTransferred = true;
                return texture;
            }
            finally
            {
                if (!ownershipTransferred && texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
