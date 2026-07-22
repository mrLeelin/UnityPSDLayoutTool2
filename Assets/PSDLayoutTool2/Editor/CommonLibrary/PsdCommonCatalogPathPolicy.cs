namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Defines which project assets may enter the public Common_* catalog.
    /// Test fixtures can use the same names as production art without becoming
    /// runtime library candidates.
    /// </summary>
    public static class PsdCommonCatalogPathPolicy
    {
        public static bool IsPublicAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string normalizedPath = assetPath.Replace('\\', '/');
            return normalizedPath.IndexOf("/TestData/", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
