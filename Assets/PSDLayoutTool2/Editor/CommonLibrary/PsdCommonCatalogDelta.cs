namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Pure merge rules for Common Asset Catalog changes.
    /// Unity asset loading remains in the catalog; this type only decides
    /// which serialized records survive an asset-database delta.
    /// </summary>
    public static class PsdCommonCatalogDelta
    {
        public static List<PsdCommonCatalogEntryState> Apply(
            IEnumerable<PsdCommonCatalogEntryState> existingEntries,
            IEnumerable<string> replacedOrRemovedPaths,
            IEnumerable<PsdCommonCatalogEntryState> currentEntries)
        {
            var replacedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddPaths(replacedPaths, replacedOrRemovedPaths);

            var result = new List<PsdCommonCatalogEntryState>();
            if (existingEntries != null)
            {
                foreach (PsdCommonCatalogEntryState entry in existingEntries)
                {
                    if (entry != null && !replacedPaths.Contains(entry.AssetPath))
                    {
                        result.Add(entry);
                    }
                }
            }

            if (currentEntries != null)
            {
                foreach (PsdCommonCatalogEntryState entry in currentEntries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    result.RemoveAll(existing => IsSameAsset(existing, entry));
                    result.Add(entry);
                }
            }

            return result;
        }

        private static void AddPaths(ISet<string> paths, IEnumerable<string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (string path in source)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
        }

        private static bool IsSameAsset(PsdCommonCatalogEntryState left, PsdCommonCatalogEntryState right)
        {
            if (left == null || right == null || left.Kind != right.Kind)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(left.Guid) &&
                !string.IsNullOrEmpty(right.Guid) &&
                string.Equals(left.Guid, right.Guid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class PsdCommonCatalogEntryState
    {
        public PsdCommonCatalogEntryState(PsdCommonAssetKind kind, string key, string guid, string assetPath)
        {
            Kind = kind;
            Key = key;
            Guid = guid;
            AssetPath = assetPath;
        }

        public PsdCommonAssetKind Kind { get; private set; }
        public string Key { get; private set; }
        public string Guid { get; private set; }
        public string AssetPath { get; private set; }
    }
}
