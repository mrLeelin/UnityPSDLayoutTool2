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
            prefabAssetPath = string.Empty;

            string outputRootAssetPath;
            if (!TryResolveOutputRoot(psdAssetPath, outputMode, outputFolderName, out outputRootAssetPath))
            {
                return false;
            }

            string psdName = MakeNameSafe(Path.GetFileNameWithoutExtension(NormalizeAssetPath(psdAssetPath)));
            if (string.IsNullOrEmpty(psdName))
            {
                return false;
            }

            if (prefabMode == PsdImporter.PrefabOutputMode.InsideOutputFolder)
            {
                prefabAssetPath = string.Format("{0}/{1}.prefab", outputRootAssetPath, psdName);
                return true;
            }

            string outputParent = NormalizeAssetPath(Path.GetDirectoryName(outputRootAssetPath));
            if (string.IsNullOrEmpty(outputParent))
            {
                outputParent = "Assets";
            }

            prefabAssetPath = string.Format("{0}/{1}.prefab", outputParent.TrimEnd('/'), psdName);
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
            outputRootAssetPath = string.Empty;
            string normalizedPsdPath = NormalizeAssetPath(psdAssetPath);
            if (string.IsNullOrEmpty(normalizedPsdPath) ||
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

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
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
