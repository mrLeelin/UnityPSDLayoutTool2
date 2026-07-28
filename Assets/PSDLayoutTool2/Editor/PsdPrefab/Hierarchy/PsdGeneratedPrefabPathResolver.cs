namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Calculates generated output paths from explicit PSD import settings.
    /// It does not read mutable importer state or search the AssetDatabase.
    /// </summary>
    internal static class PsdGeneratedPrefabPathResolver
    {
        internal const string AtlasFolderName = "Atlas";
        internal const string TextureFolderName = "Texture";
        internal const string PrefabFolderName = "Prefab";

        private static readonly HashSet<char> InvalidGeneratedNameChars = new HashSet<char>(
            Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }));

        private static readonly HashSet<string> ReservedGeneratedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Calculates the exact Prefab asset path selected by the supplied settings.
        /// </summary>
        internal static bool TryResolve(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            PsdImporter.PrefabOutputMode prefabMode,
            out string prefabAssetPath)
        {
            return TryResolve(psdAssetPath, outputMode, outputFolderName, string.Empty, string.Empty, prefabMode, out prefabAssetPath);
        }

        internal static bool TryResolve(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            string fixedOutputPath,
            string prefabOutputPath,
            PsdImporter.PrefabOutputMode prefabMode,
            out string prefabAssetPath)
        {
            prefabAssetPath = string.Empty;

            string outputRootAssetPath;
            if (!TryResolveOutputRoot(psdAssetPath, outputMode, outputFolderName, fixedOutputPath, out outputRootAssetPath))
            {
                return false;
            }

            string psdName = MakeNameSafe(Path.GetFileNameWithoutExtension(NormalizeAssetPath(psdAssetPath)));
            if (string.IsNullOrEmpty(psdName))
            {
                return false;
            }

            string prefabFolder;
            if (!TryResolveContentFolder(outputRootAssetPath, PrefabFolderName, prefabOutputPath, out prefabFolder))
            {
                return false;
            }

            prefabAssetPath = prefabFolder + "/" + psdName + ".prefab";
            return true;
        }

        /// <summary>
        /// Calculates the three fixed content folders created under one PSD output root.
        /// </summary>
        internal static bool TryResolveContentFolders(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            out string atlasFolderAssetPath,
            out string textureFolderAssetPath,
            out string prefabFolderAssetPath)
        {
            return TryResolveContentFolders(
                psdAssetPath,
                outputMode,
                outputFolderName,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                out atlasFolderAssetPath,
                out textureFolderAssetPath,
                out prefabFolderAssetPath);
        }

        internal static bool TryResolveContentFolders(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            string fixedOutputPath,
            string atlasOutputPath,
            string textureOutputPath,
            string prefabOutputPath,
            out string atlasFolderAssetPath,
            out string textureFolderAssetPath,
            out string prefabFolderAssetPath)
        {
            atlasFolderAssetPath = string.Empty;
            textureFolderAssetPath = string.Empty;
            prefabFolderAssetPath = string.Empty;

            string outputRootAssetPath;
            if (!TryResolveOutputRoot(psdAssetPath, outputMode, outputFolderName, fixedOutputPath, out outputRootAssetPath))
            {
                return false;
            }

            if (!TryResolveContentFolder(outputRootAssetPath, AtlasFolderName, atlasOutputPath, out atlasFolderAssetPath) ||
                !TryResolveContentFolder(outputRootAssetPath, TextureFolderName, textureOutputPath, out textureFolderAssetPath) ||
                !TryResolveContentFolder(outputRootAssetPath, PrefabFolderName, prefabOutputPath, out prefabFolderAssetPath))
            {
                atlasFolderAssetPath = string.Empty;
                textureFolderAssetPath = string.Empty;
                prefabFolderAssetPath = string.Empty;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Calculates the generated output folder selected by the supplied settings.
        /// </summary>
        internal static bool TryResolveOutputRoot(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            out string outputRootAssetPath)
        {
            return TryResolveOutputRoot(psdAssetPath, outputMode, outputFolderName, string.Empty, out outputRootAssetPath);
        }

        internal static bool TryResolveOutputRoot(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            string fixedOutputPath,
            out string outputRootAssetPath)
        {
            outputRootAssetPath = string.Empty;
            string normalizedPsdPath = NormalizeAssetPath(psdAssetPath);
            if (string.IsNullOrEmpty(normalizedPsdPath) ||
                ContainsTraversalSegment(normalizedPsdPath) ||
                (!normalizedPsdPath.Equals("Assets", StringComparison.Ordinal) &&
                 !normalizedPsdPath.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                return false;
            }

            string psdName = MakeNameSafe(Path.GetFileNameWithoutExtension(normalizedPsdPath));
            if (string.IsNullOrEmpty(psdName))
            {
                return false;
            }

            string assetDirectory = NormalizeAssetPath(Path.GetDirectoryName(normalizedPsdPath));
            if (string.IsNullOrEmpty(assetDirectory))
            {
                assetDirectory = "Assets";
            }

            string basePath = outputMode == PsdImporter.OutputDirectoryMode.AssetsRoot ? "Assets" : assetDirectory;
            string folderName = MakeNameSafe(string.IsNullOrEmpty(outputFolderName) ? psdName : outputFolderName);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = psdName;
            }

            outputRootAssetPath = string.Format("{0}/{1}", basePath.TrimEnd('/'), folderName);
            return true;
        }

        private static bool TryResolveContentFolder(
            string outputRootAssetPath,
            string defaultFolderName,
            string configuredPath,
            out string folderPath)
        {
            folderPath = string.Empty;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                folderPath = outputRootAssetPath + "/" + defaultFolderName;
                return true;
            }

            string normalizedPath;
            if (!TryNormalizeAssetsFolderPath(configuredPath, out normalizedPath))
            {
                return false;
            }

            folderPath = normalizedPath;
            return true;
        }

        private static bool TryNormalizeAssetsFolderPath(string path, out string normalizedPath)
        {
            normalizedPath = NormalizeAssetPath(path).TrimEnd('/');
            if (string.IsNullOrEmpty(normalizedPath) ||
                ContainsTraversalSegment(normalizedPath) ||
                (!normalizedPath.Equals("Assets", StringComparison.Ordinal) &&
                 !normalizedPath.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                normalizedPath = string.Empty;
                return false;
            }

            string[] segments = normalizedPath.Split('/');
            for (int index = 1; index < segments.Length; index++)
            {
                string safeSegment = MakeNameSafe(segments[index]);
                if (string.IsNullOrEmpty(safeSegment) ||
                    !string.Equals(segments[index], safeSegment, StringComparison.Ordinal))
                {
                    normalizedPath = string.Empty;
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
        }

        private static bool ContainsTraversalSegment(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            foreach (string segment in segments)
            {
                if (segment.Equals(".", StringComparison.Ordinal) ||
                    segment.Equals("..", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string MakeNameSafe(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string trimmedName = name.Trim();
            StringBuilder builder = new StringBuilder(trimmedName.Length);
            foreach (char currentChar in trimmedName)
            {
                builder.Append(InvalidGeneratedNameChars.Contains(currentChar) || char.IsControl(currentChar) ? '_' : currentChar);
            }

            string sanitized = builder.ToString().Trim().TrimEnd('.');
            while (sanitized.EndsWith(" ", StringComparison.Ordinal))
            {
                sanitized = sanitized.Substring(0, sanitized.Length - 1);
            }

            if (ReservedGeneratedNames.Contains(sanitized))
            {
                sanitized += "_";
            }

            return sanitized;
        }
    }
}
