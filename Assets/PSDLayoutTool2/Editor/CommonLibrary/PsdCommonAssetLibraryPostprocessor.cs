namespace PsdLayoutTool2
{
    using UnityEditor;

    /// <summary>
    /// Keeps the generated catalog synchronized with only the affected assets.
    /// </summary>
    public sealed class PsdCommonAssetLibraryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            PsdCommonAssetCatalog.ApplyAssetChanges(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}
