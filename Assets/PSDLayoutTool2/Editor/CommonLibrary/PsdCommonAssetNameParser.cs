namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// 解析项目配置的通用 Prefab 和通用 Texture 名称。
    /// 这些名称属于明确的导入契约，因此不允许模糊匹配。
    /// </summary>
    public static class PsdCommonAssetNameParser
    {
        public static bool TryParse(string layerName, out PsdCommonAssetReference reference)
        {
            return TryParse(layerName, GetNaming(), out reference);
        }

        internal static bool TryParse(
            string layerName,
            PsdCommonAssetNamingSnapshot naming,
            out PsdCommonAssetReference reference)
        {
            reference = null;
            if (string.IsNullOrEmpty(layerName))
            {
                return false;
            }

            if (TryParsePrefix(layerName, naming.prefabPrefix, PsdCommonAssetKind.Prefab, out reference))
            {
                return true;
            }

            return TryParsePrefix(layerName, naming.texturePrefix, PsdCommonAssetKind.Texture, out reference);
        }

        /// <summary>
        /// 刷新映射表时，从公共 Prefab 资源名称中读取资源键。
        /// </summary>
        public static bool TryParsePrefabAssetKey(string assetName, out string key)
        {
            return TryParsePrefabAssetKey(assetName, GetNaming(), out key);
        }

        internal static bool TryParsePrefabAssetKey(
            string assetName,
            PsdCommonAssetNamingSnapshot naming,
            out string key)
        {
            return TryParseAssetKey(assetName, naming.prefabPrefix, out key);
        }

        /// <summary>
        /// 刷新映射表时，从公共纹理资源名称中读取资源键。
        /// </summary>
        public static bool TryParseTextureAssetKey(string assetName, out string key)
        {
            return TryParseTextureAssetKey(assetName, GetNaming(), out key);
        }

        internal static bool TryParseTextureAssetKey(
            string assetName,
            PsdCommonAssetNamingSnapshot naming,
            out string key)
        {
            return TryParseAssetKey(assetName, naming.texturePrefix, out key);
        }

        private static PsdCommonAssetNamingSnapshot GetNaming()
        {
            return PsdLayoutProjectSettings.instance.ResolveCommonAssetNaming();
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
