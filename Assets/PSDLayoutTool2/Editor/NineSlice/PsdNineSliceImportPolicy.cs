namespace PsdLayoutTool2
{
    using UnityEditor;

    /// <summary>
    /// Defines the source-of-truth policy for generated Sprite borders.
    /// PSD/Figma names opt into automatic nine-slice; untagged layers must not
    /// inherit a stale manual border from an older TextureImporter meta file.
    /// </summary>
    public static class PsdNineSliceImportPolicy
    {
        /// <summary>
        /// Generated borders are expressed in PNG pixels, so Unity must not
        /// resize non-power-of-two textures during import.
        /// </summary>
        public static TextureImporterNPOTScale GeneratedTextureNpotScale
        {
            get { return TextureImporterNPOTScale.None; }
        }

        /// <summary>
        /// Determines whether a generated sprite with no resolved border is an
        /// ordinary PSD layer whose existing Sprite border must be cleared.
        /// </summary>
        public static bool ShouldClearUntaggedBorder(string layerName)
        {
            PsdNineSliceNameRule rule;
            return !PsdNineSliceNameRules.TryParse(layerName, out rule);
        }

        /// <summary>
        /// Determines whether an already imported Sprite carries a usable
        /// border, including a Sprite resolved from the Common Texture catalog.
        /// </summary>
        public static bool HasSpriteBorder(float left, float bottom, float right, float top)
        {
            return left > 0f || bottom > 0f || right > 0f || top > 0f;
        }
    }
}
