namespace PhotoshopFile
{
    /// <summary>
    /// Text capitalization modes authored in Photoshop EngineData.
    /// </summary>
    public enum PsdTextCapitalization
    {
        Normal = 0,
        SmallCaps = 1,
        AllCaps = 2
    }

    /// <summary>
    /// Converts Photoshop's FontCaps integer into a stable text presentation mode.
    /// </summary>
    public static class PsdTextCapitalizationResolver
    {
        public static PsdTextCapitalization FromPhotoshopFontCaps(int value)
        {
            if (value == (int)PsdTextCapitalization.SmallCaps)
            {
                return PsdTextCapitalization.SmallCaps;
            }

            if (value == (int)PsdTextCapitalization.AllCaps)
            {
                return PsdTextCapitalization.AllCaps;
            }

            return PsdTextCapitalization.Normal;
        }
    }
}
