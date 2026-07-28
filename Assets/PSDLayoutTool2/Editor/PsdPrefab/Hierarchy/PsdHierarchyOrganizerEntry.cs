namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Resolves the exact generated Prefab for a PSD and opens the in-editor
    /// AI chat only when that Prefab exists.
    /// </summary>
    public static class PsdHierarchyOrganizerEntry
    {
        public const string AiButtonLabel = "AI整理";

        public static bool TryResolvePrefabAvailability(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            PsdImporter.PrefabOutputMode prefabMode,
            Func<string, bool> prefabExists,
            out string targetPrefabPath,
            out string explanation)
        {
            targetPrefabPath = string.Empty;
            explanation = string.Empty;
            if (!PsdGeneratedPrefabPathResolver.TryResolve(
                    psdAssetPath,
                    outputMode,
                    outputFolderName,
                    PsdImporter.FixedOutputPath,
                    PsdImporter.PrefabOutputPath,
                    prefabMode,
                    out targetPrefabPath))
            {
                explanation = "Unable to resolve the generated Prefab path.";
                return false;
            }

            if (prefabExists == null) throw new ArgumentNullException(nameof(prefabExists));
            if (prefabExists(targetPrefabPath))
            {
                return true;
            }

            if (TryFindUniqueDirectPrefabFallback(targetPrefabPath, prefabExists, out string fallbackPath))
            {
                targetPrefabPath = fallbackPath;
                return true;
            }

            explanation = "Prefab不存在，请先生成Prefab。";
            return false;
        }

        public static bool TryOpenChat(string sourcePsdAssetPath, out string error)
        {
            PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
            string targetPrefabPath;
            string availabilityError;
            if (!TryResolvePrefabAvailability(
                    sourcePsdAssetPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.PrefabMode,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out targetPrefabPath,
                    out availabilityError))
            {
                error = availabilityError;
                return false;
            }

            return PsdHierarchyChatWindow.TryOpen(
                sourcePsdAssetPath,
                targetPrefabPath,
                out error);
        }

        /// <summary>
        /// Supports an already-organized legacy Prefab whose semantic file name differs from
        /// the PSD file name. Only one direct Prefab under the generated Prefab folder is
        /// accepted; nested Common/Component Prefabs and ambiguous roots are rejected.
        /// </summary>
        internal static bool TrySelectUniqueDirectPrefabFallback(
            string configuredPrefabPath,
            IEnumerable<string> prefabCandidates,
            Func<string, bool> prefabExists,
            out string selectedPrefabPath)
        {
            selectedPrefabPath = string.Empty;
            if (prefabCandidates == null || prefabExists == null)
            {
                return false;
            }

            string prefabFolder = GetAssetDirectory(configuredPrefabPath);
            if (string.IsNullOrEmpty(prefabFolder))
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in prefabCandidates)
            {
                string normalizedCandidate = NormalizeAssetPath(candidate);
                if (!normalizedCandidate.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetAssetDirectory(normalizedCandidate), prefabFolder, StringComparison.OrdinalIgnoreCase) ||
                    !prefabExists(normalizedCandidate) ||
                    !seen.Add(normalizedCandidate))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(selectedPrefabPath))
                {
                    selectedPrefabPath = string.Empty;
                    return false;
                }

                selectedPrefabPath = normalizedCandidate;
            }

            return !string.IsNullOrEmpty(selectedPrefabPath);
        }

        private static bool TryFindUniqueDirectPrefabFallback(
            string configuredPrefabPath,
            Func<string, bool> prefabExists,
            out string fallbackPath)
        {
            fallbackPath = string.Empty;
            string prefabFolder = GetAssetDirectory(configuredPrefabPath);
            if (string.IsNullOrEmpty(prefabFolder) || !AssetDatabase.IsValidFolder(prefabFolder))
            {
                return false;
            }

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { prefabFolder });
            var candidates = new List<string>(guids.Length);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                {
                    candidates.Add(assetPath);
                }
            }

            return TrySelectUniqueDirectPrefabFallback(
                configuredPrefabPath,
                candidates,
                prefabExists,
                out fallbackPath);
        }

        private static string GetAssetDirectory(string assetPath)
        {
            string directory = Path.GetDirectoryName(NormalizeAssetPath(assetPath));
            return NormalizeAssetPath(directory);
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty).Replace('\\', '/');
        }
    }
}
