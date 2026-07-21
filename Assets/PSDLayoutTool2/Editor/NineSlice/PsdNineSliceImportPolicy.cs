namespace PsdLayoutTool2
{
    /// <summary>
    /// Defines the source-of-truth policy for generated Sprite borders.
    /// PSD/Figma names opt into automatic nine-slice; untagged layers must not
    /// inherit a stale manual border from an older TextureImporter meta file.
    /// </summary>
    public static class PsdNineSliceImportPolicy
    {
        /// <summary>
        /// Determines whether a generated sprite with no resolved border is an
        /// ordinary PSD layer whose existing Sprite border must be cleared.
        /// </summary>
        public static bool ShouldClearUntaggedBorder(string layerName)
        {
            PsdNineSliceNameRule rule;
            return !PsdNineSliceNameRules.TryParse(layerName, out rule);
        }
    }
}
