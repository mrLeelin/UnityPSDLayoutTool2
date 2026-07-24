namespace PsdLayoutTool2.Editor
{
    using System;
    using System.IO;
    using PhotoshopFile;
    using UnityEngine;

    internal static class PsdHierarchyCompositePreviewWriter
    {
        public static string Write(string psdAssetPath, string sessionDirectory)
        {
            if (string.IsNullOrWhiteSpace(psdAssetPath))
            {
                throw new ArgumentException("PSD asset path is required.", "psdAssetPath");
            }

            string canonicalDirectory = CanonicalizeSessionDirectory(sessionDirectory);
            string outputPath = Path.GetFullPath(
                Path.Combine(canonicalDirectory, "composite.png"));
            if (!string.Equals(
                    Path.GetDirectoryName(outputPath),
                    canonicalDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Composite preview output escaped the supplied session directory.");
            }

            var psd = new PsdFile(Path.GetFullPath(psdAssetPath));
            Texture2D texture = null;
            try
            {
                texture = BuildCompositeTexture(psd);
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Unity could not encode the PSD composite preview.");
                }

                File.WriteAllBytes(outputPath, png);
                return outputPath;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        private static string CanonicalizeSessionDirectory(string sessionDirectory)
        {
            if (string.IsNullOrWhiteSpace(sessionDirectory))
            {
                throw new ArgumentException(
                    "Session directory is required.", "sessionDirectory");
            }

            string canonicalDirectory = NormalizeDirectory(sessionDirectory);
            string assetsDirectory = NormalizeDirectory(Application.dataPath);
            if (string.Equals(canonicalDirectory, assetsDirectory, StringComparison.OrdinalIgnoreCase) ||
                canonicalDirectory.StartsWith(
                    assetsDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Composite previews must not be written under Assets.");
            }

            Directory.CreateDirectory(canonicalDirectory);
            return canonicalDirectory;
        }

        private static string NormalizeDirectory(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                ? root
                : fullPath.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

            var texture = new Texture2D(
                psd.Width, psd.Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(colors);
            texture.Apply(false, false);
            return texture;
        }
    }
}
