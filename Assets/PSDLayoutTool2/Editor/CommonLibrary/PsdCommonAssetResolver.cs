namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Exact-key resolver backed by the generated Common Asset Catalog.
    /// </summary>
    public static class PsdCommonAssetResolver
    {
        public static bool TryResolve(
            PsdCommonAssetReference reference,
            out UnityEngine.Object asset,
            out string error)
        {
            asset = null;
            error = string.Empty;
            if (reference == null)
            {
                error = "The Common_* layer rule is missing.";
                return false;
            }

            PsdCommonAssetCatalog catalog = PsdCommonAssetCatalog.Load();
            if (catalog == null)
            {
                error = "Common Asset Catalog is missing. Open Project Settings > PSD Layout Tool > Common Asset Catalog and generate it.";
                return false;
            }

            if (catalog.needsRefresh)
            {
                error = "Common Asset Catalog is out of date. Refresh it in Project Settings before importing Common_* layers.";
                return false;
            }

            List<UnityEngine.Object> matches = FindMatches(catalog, reference);
            if (matches.Count == 0)
            {
                error = "Common " + reference.Kind + " was not found for key '" + reference.Key + "' in the catalog.";
                return false;
            }

            if (matches.Count > 1)
            {
                error = "Common " + reference.Kind + " key '" + reference.Key + "' is ambiguous: " + matches.Count + " assets share that exact name.";
                return false;
            }

            asset = matches[0];
            return true;
        }

        private static List<UnityEngine.Object> FindMatches(PsdCommonAssetCatalog catalog, PsdCommonAssetReference reference)
        {
            var matches = new List<UnityEngine.Object>();
            if (reference.Kind == PsdCommonAssetKind.Prefab)
            {
                foreach (PsdCommonPrefabCatalogEntry entry in catalog.prefabs)
                {
                    if (entry != null && entry.prefab != null &&
                        string.Equals(entry.key, reference.Key, StringComparison.OrdinalIgnoreCase) &&
                        IsPublicEntry(entry.assetPath, entry.prefab))
                    {
                        matches.Add(entry.prefab);
                    }
                }
            }
            else
            {
                foreach (PsdCommonTextureCatalogEntry entry in catalog.textures)
                {
                    if (entry != null && entry.sprite != null &&
                        string.Equals(entry.key, reference.Key, StringComparison.OrdinalIgnoreCase) &&
                        IsPublicEntry(entry.assetPath, entry.sprite))
                    {
                        matches.Add(entry.sprite);
                    }
                }
            }

            return matches;
        }

        private static bool IsPublicEntry(string assetPath, UnityEngine.Object asset)
        {
            string path = string.IsNullOrEmpty(assetPath) ? AssetDatabase.GetAssetPath(asset) : assetPath;
            return PsdCommonCatalogPathPolicy.IsPublicAssetPath(path);
        }
    }
}
