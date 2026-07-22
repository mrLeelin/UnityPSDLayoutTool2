namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Builds the minimum RGBA source needed by Unity's Sliced Image renderer.
    /// </summary>
    public static class PsdNineSliceCropper
    {
        private const int StretchSampleSize = PsdNineSliceBorder.MinimumStretchCenterPixels;

        /// <summary>
        /// Keeps protected corners and a two-pixel sample of every stretchable
        /// area. The returned raster is suitable for PNG encoding and Sprite
        /// border assignment using the same border values.
        /// </summary>
        public static PsdNineSliceRaster CropToMinimum(PsdNineSliceRaster source, PsdNineSliceBorder border)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (border == null || !border.IsValidFor(source.Width, source.Height))
            {
                throw new ArgumentException("The nine-slice border is not valid for the source raster.", "border");
            }

            AxisSegments horizontal = AxisSegments.Create(source.Width, border.Left, border.Right);
            AxisSegments vertical = AxisSegments.Create(source.Height, border.Top, border.Bottom);
            int targetWidth = horizontal.TargetSize;
            int targetHeight = vertical.TargetSize;
            byte[] targetPixels = new byte[targetWidth * targetHeight * 4];

            for (int row = 0; row < vertical.Count; row++)
            {
                for (int column = 0; column < horizontal.Count; column++)
                {
                    CopyRectangle(
                        source,
                        targetPixels,
                        targetWidth,
                        horizontal.SourceStarts[column],
                        vertical.SourceStarts[row],
                        horizontal.TargetStarts[column],
                        vertical.TargetStarts[row],
                        horizontal.Lengths[column],
                        vertical.Lengths[row]);
                }
            }

            return new PsdNineSliceRaster(targetWidth, targetHeight, targetPixels);
        }

        private static void CopyRectangle(
            PsdNineSliceRaster source,
            byte[] target,
            int targetWidth,
            int sourceX,
            int sourceY,
            int targetX,
            int targetY,
            int width,
            int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = (((sourceY + y) * source.Width) + sourceX + x) * 4;
                    int targetOffset = (((targetY + y) * targetWidth) + targetX + x) * 4;
                    target[targetOffset] = source.Pixels[sourceOffset];
                    target[targetOffset + 1] = source.Pixels[sourceOffset + 1];
                    target[targetOffset + 2] = source.Pixels[sourceOffset + 2];
                    target[targetOffset + 3] = source.Pixels[sourceOffset + 3];
                }
            }
        }

        /// <summary>
        /// Represents either a cropped three-slice axis or an unchanged
        /// non-stretch axis. This keeps h3/v3 source dimensions intact.
        /// </summary>
        private sealed class AxisSegments
        {
            private AxisSegments(int[] sourceStarts, int[] targetStarts, int[] lengths, int targetSize)
            {
                SourceStarts = sourceStarts;
                TargetStarts = targetStarts;
                Lengths = lengths;
                TargetSize = targetSize;
            }

            public int[] SourceStarts { get; private set; }
            public int[] TargetStarts { get; private set; }
            public int[] Lengths { get; private set; }
            public int TargetSize { get; private set; }
            public int Count { get { return Lengths.Length; } }

            public static AxisSegments Create(int sourceSize, int leading, int trailing)
            {
                if (leading <= 0 && trailing <= 0)
                {
                    return new AxisSegments(new[] { 0 }, new[] { 0 }, new[] { sourceSize }, sourceSize);
                }

                return new AxisSegments(
                    new[] { 0, leading, sourceSize - trailing },
                    new[] { 0, leading, leading + StretchSampleSize },
                    new[] { leading, StretchSampleSize, trailing },
                    leading + StretchSampleSize + trailing);
            }
        }
    }
}
