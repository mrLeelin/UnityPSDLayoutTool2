namespace PsdLayoutTool2
{
    using UnityEditor;

    /// <summary>
    /// Invalidates only when a configured common-library root changes.
    /// </summary>
    public sealed class PsdCommonAssetLibraryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            PsdCommonAssetLibrarySettings settings = PsdCommonAssetLibrarySettings.Load();
            if (settings == null)
            {
                return;
            }

            if (ContainsConfiguredPath(settings, importedAssets) ||
                ContainsConfiguredPath(settings, deletedAssets) ||
                ContainsConfiguredPath(settings, movedAssets) ||
                ContainsConfiguredPath(settings, movedFromAssetPaths))
            {
                PsdCommonAssetResolver.Invalidate();
            }
        }

        private static bool ContainsConfiguredPath(PsdCommonAssetLibrarySettings settings, string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            foreach (string path in paths)
            {
                if (settings.IsPathUnderConfiguredRoot(path))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
