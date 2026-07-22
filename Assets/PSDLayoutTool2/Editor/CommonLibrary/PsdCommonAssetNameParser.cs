namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Parses explicit Common_Prefab_ and Common_Texture_ PSD layer names.
    /// No fuzzy matching is allowed because these names are import contracts.
    /// </summary>
    public static class PsdCommonAssetNameParser
    {
        private const string PrefabPrefix = "Common_Prefab_";
        private const string TexturePrefix = "Common_Texture_";

        public static bool TryParse(string layerName, out PsdCommonAssetReference reference)
        {
            reference = null;
            if (string.IsNullOrEmpty(layerName))
            {
                return false;
            }

            if (TryParsePrefix(layerName, PrefabPrefix, PsdCommonAssetKind.Prefab, out reference))
            {
                return true;
            }

            return TryParsePrefix(layerName, TexturePrefix, PsdCommonAssetKind.Texture, out reference);
        }

        /// <summary>
        /// Reads the key from a public prefab asset name during catalog refresh.
        /// </summary>
        public static bool TryParsePrefabAssetKey(string assetName, out string key)
        {
            return TryParseAssetKey(assetName, PrefabPrefix, out key);
        }

        /// <summary>
        /// Reads the key from a public texture asset name during catalog refresh.
        /// </summary>
        public static bool TryParseTextureAssetKey(string assetName, out string key)
        {
            return TryParseAssetKey(assetName, TexturePrefix, out key);
        }

        private static bool TryParsePrefix(
            string layerName,
            string prefix,
            PsdCommonAssetKind kind,
            out PsdCommonAssetReference reference)
        {
            reference = null;
            if (!layerName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string key = layerName.Substring(prefix.Length).Trim();
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            reference = new PsdCommonAssetReference(kind, key);
            return true;
        }

        private static bool TryParseAssetKey(string assetName, string prefix, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrEmpty(assetName) || !assetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            key = assetName.Substring(prefix.Length).Trim();
            return !string.IsNullOrEmpty(key);
        }
    }
}
