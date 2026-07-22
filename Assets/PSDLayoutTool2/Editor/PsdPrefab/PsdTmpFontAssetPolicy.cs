namespace PsdLayoutTool2
{
    using System.Collections.Generic;
    using TMPro;

    /// <summary>
    /// Determines whether a TMP font can render directly or through its fallback chain.
    /// </summary>
    internal static class PsdTmpFontAssetPolicy
    {
        public static bool IsUsable(TMP_FontAsset font)
        {
            return IsUsable(font, new HashSet<TMP_FontAsset>());
        }

        private static bool IsUsable(TMP_FontAsset font, HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font))
            {
                return false;
            }

            if (HasOwnRenderableGlyphs(font))
            {
                return true;
            }

            if (font.fallbackFontAssetTable == null)
            {
                return false;
            }

            foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable)
            {
                if (IsUsable(fallback, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOwnRenderableGlyphs(TMP_FontAsset font)
        {
            if (font.atlasTexture == null || font.material == null)
            {
                return false;
            }

            return (font.characterTable != null && font.characterTable.Count > 0) ||
                (font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.sourceFontFile != null);
        }
    }
}
