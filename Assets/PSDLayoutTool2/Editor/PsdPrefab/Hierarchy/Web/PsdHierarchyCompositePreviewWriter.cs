namespace PsdLayoutTool2.Editor
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using PhotoshopFile;
    using UnityEngine;

    internal static class PsdHierarchyCompositePreviewWriter
    {
        private const int MoveFileReplaceExisting = 0x1;
        private const int MoveFileWriteThrough = 0x8;

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

            EnsureDestinationIsRegularOrMissing(outputPath);
            var psd = new PsdFile(Path.GetFullPath(psdAssetPath));
            EnsureSupportedMergedImageCompression(psd.ImageCompression);
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

                WriteAtomically(canonicalDirectory, outputPath, png);
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

            CreateAndVerifyDirectoryPath(canonicalDirectory);
            return canonicalDirectory;
        }

        private static void CreateAndVerifyDirectoryPath(string directory)
        {
            string root = Path.GetPathRoot(directory);
            if (string.IsNullOrEmpty(root))
            {
                throw new IOException("Session directory has no filesystem root.");
            }

            VerifyExistingDirectory(root);
            string current = root;
            string relative = directory.Substring(root.Length);
            string[] components = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string component in components)
            {
                current = Path.Combine(current, component);
                FileAttributes attributes;
                if (TryGetAttributes(current, out attributes))
                {
                    VerifyDirectoryAttributes(current, attributes);
                    continue;
                }

                Directory.CreateDirectory(current);
                VerifyExistingDirectory(current);
            }
        }

        private static void VerifyExistingDirectory(string path)
        {
            FileAttributes attributes;
            if (!TryGetAttributes(path, out attributes))
            {
                throw new IOException("Expected directory does not exist: " + path);
            }
            VerifyDirectoryAttributes(path, attributes);
        }

        private static void VerifyDirectoryAttributes(
            string path,
            FileAttributes attributes)
        {
            if ((attributes & FileAttributes.Directory) == 0)
            {
                throw new IOException("Session path component is not a directory: " + path);
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Session directories must not contain reparse points: " + path);
            }
        }

        private static bool TryGetAttributes(
            string path,
            out FileAttributes attributes)
        {
            try
            {
                attributes = File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                attributes = default(FileAttributes);
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                attributes = default(FileAttributes);
                return false;
            }
        }

        private static void EnsureDestinationIsRegularOrMissing(string outputPath)
        {
            FileAttributes attributes;
            if (!TryGetAttributes(outputPath, out attributes))
            {
                return;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Composite preview destination must not be a reparse point.");
            }
            if ((attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException(
                    "Composite preview destination must be a regular file.");
            }
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

        private static void WriteAtomically(
            string sessionDirectory,
            string outputPath,
            byte[] png)
        {
            CreateAndVerifyDirectoryPath(sessionDirectory);
            EnsureDestinationIsRegularOrMissing(outputPath);
            string temporaryPath = Path.Combine(
                sessionDirectory,
                ".composite-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(png, 0, png.Length);
                    stream.Flush(true);
                }

                FileAttributes temporaryAttributes = File.GetAttributes(temporaryPath);
                if ((temporaryAttributes & FileAttributes.ReparsePoint) != 0 ||
                    (temporaryAttributes & FileAttributes.Directory) != 0)
                {
                    throw new IOException(
                        "Composite preview temporary output is not a regular file.");
                }

                CreateAndVerifyDirectoryPath(sessionDirectory);
                EnsureDestinationIsRegularOrMissing(outputPath);
                MoveTemporaryFile(temporaryPath, outputPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void MoveTemporaryFile(
            string temporaryPath,
            string outputPath)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (!MoveFileEx(
                        temporaryPath,
                        outputPath,
                        MoveFileReplaceExisting | MoveFileWriteThrough))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Could not atomically publish the composite preview.");
                }
                return;
            }

            if (File.Exists(outputPath))
            {
                File.Replace(temporaryPath, outputPath, null);
            }
            else
            {
                File.Move(temporaryPath, outputPath);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(
            string existingFileName,
            string newFileName,
            int flags);

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
