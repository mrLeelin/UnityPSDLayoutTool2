namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Prevents name-driven 9-slice export from destroying baked artwork.
    /// A PSD layer may be tagged as a nine-slice while it still contains text
    /// or icons. Reconstructing it at the original size exposes that mistake:
    /// a real stretchable background stays visually equivalent, while baked
    /// content has a large pixel error and must retain its source PNG.
    /// </summary>
    internal static class PsdNineSliceCropSafety
    {
        private const float MaximumMeanChannelDifference = 4.0f;

        public static bool IsSafeToCrop(
            PsdNineSliceRaster source,
            PsdNineSliceRaster cropped,
            PsdNineSliceBorder border,
            out float meanChannelDifference)
        {
            meanChannelDifference = float.MaxValue;
            if (source == null || cropped == null || border == null ||
                !border.IsValidFor(source.Width, source.Height))
            {
                return false;
            }

            double totalDifference = 0.0;
            int componentCount = 0;
            for (int y = 0; y < source.Height; y++)
            {
                int croppedY = MapToCroppedCoordinate(y, source.Height, cropped.Height, border.Top, border.Bottom);
                for (int x = 0; x < source.Width; x++)
                {
                    int croppedX = MapToCroppedCoordinate(x, source.Width, cropped.Width, border.Left, border.Right);
                    for (int component = 0; component < 4; component++)
                    {
                        totalDifference += Math.Abs(source.GetComponent(x, y, component) - cropped.GetComponent(croppedX, croppedY, component));
                        componentCount++;
                    }
                }
            }

            meanChannelDifference = componentCount > 0 ? (float)(totalDifference / componentCount) : float.MaxValue;
            return meanChannelDifference <= MaximumMeanChannelDifference;
        }

        private static int MapToCroppedCoordinate(int sourceCoordinate, int sourceSize, int croppedSize, int leading, int trailing)
        {
            if (leading <= 0 && trailing <= 0)
            {
                return sourceCoordinate;
            }

            if (sourceCoordinate < leading)
            {
                return sourceCoordinate;
            }

            if (sourceCoordinate >= sourceSize - trailing)
            {
                return croppedSize - (sourceSize - sourceCoordinate);
            }

            int sourceCenterSize = sourceSize - leading - trailing;
            return leading + ((sourceCoordinate - leading) * PsdNineSliceBorder.MinimumStretchCenterPixels / sourceCenterSize);
        }
    }
}
