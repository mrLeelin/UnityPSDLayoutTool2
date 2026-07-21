namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Pixel border in author-facing left, top, right, bottom order.
    /// </summary>
    public sealed class PsdNineSliceBorder
    {
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
        /// Validates that the source still has a non-empty stretchable center.
        /// </summary>
        public bool IsValidFor(int width, int height)
        {
            return Left >= 0 && Top >= 0 && Right >= 0 && Bottom >= 0 &&
                Left + Right < width && Top + Bottom < height;
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
