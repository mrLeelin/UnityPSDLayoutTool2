namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Pixel border in author-facing left, top, right, bottom order.
    /// </summary>
    public sealed class PsdNineSliceBorder
    {
        /// <summary>
        /// Smallest center sample preserved in an exported nine-slice PNG.
        /// This is intentionally two pixels to match the established Figma
        /// export crop contract.
        /// </summary>
        public const int MinimumStretchCenterPixels = 2;

        /// <summary>
        /// Initializes a nine-slice border.
        /// </summary>
        public PsdNineSliceBorder(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; private set; }
        public int Top { get; private set; }
        public int Right { get; private set; }
        public int Bottom { get; private set; }

        /// <summary>
        /// Validates that the source can retain the minimum stretchable center.
        /// </summary>
        public bool IsValidFor(int width, int height)
        {
            return Left >= 0 && Top >= 0 && Right >= 0 && Bottom >= 0 &&
                HasMinimumStretchCenter(width, height, MinimumStretchCenterPixels);
        }

        public bool HasMinimumStretchCenter(int width, int height, int minimumCenterPixels)
        {
            return minimumCenterPixels > 0 &&
                Left + Right + minimumCenterPixels <= width &&
                Top + Bottom + minimumCenterPixels <= height;
        }

        /// <summary>
        /// Scales the border into the same coordinate system as a target Canvas.
        /// </summary>
        public PsdNineSliceBorder Scale(float horizontalScale, float verticalScale)
        {
            if (horizontalScale < 0f || verticalScale < 0f)
            {
                throw new ArgumentOutOfRangeException("Nine-slice border scale cannot be negative.");
            }

            return new PsdNineSliceBorder(
                RoundPixels(Left * horizontalScale),
                RoundPixels(Top * verticalScale),
                RoundPixels(Right * horizontalScale),
                RoundPixels(Bottom * verticalScale));
        }

        private static int RoundPixels(float value)
        {
            return (int)Math.Floor(value + 0.5f);
        }
    }

    /// <summary>
    /// Confidence returned by the conservative pixel inference service.
    /// </summary>
    public enum PsdNineSliceConfidence
    {
        None,
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Candidate computed from the same raster Unity will import as a Sprite.
    /// </summary>
    public sealed class PsdNineSliceInference
    {
        public PsdNineSliceInference(PsdNineSliceBorder border, PsdNineSliceConfidence confidence, string method)
        {
            if (border == null)
            {
                throw new ArgumentNullException("border");
            }

            Border = border;
            Confidence = confidence;
            Method = method ?? string.Empty;
        }

        public PsdNineSliceBorder Border { get; private set; }
        public PsdNineSliceConfidence Confidence { get; private set; }
        public string Method { get; private set; }
    }
}
