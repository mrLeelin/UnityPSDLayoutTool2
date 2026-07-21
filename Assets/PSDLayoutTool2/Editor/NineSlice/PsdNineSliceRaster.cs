namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Immutable RGBA8 pixel input for the PSD nine-slice pipeline.
    /// </summary>
    public sealed class PsdNineSliceRaster
    {
        /// <summary>
        /// Initializes a pixel raster from packed RGBA8 bytes.
        /// </summary>
        public PsdNineSliceRaster(int width, int height, byte[] pixels)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height");
            }

            if (pixels == null || pixels.Length != width * height * 4)
            {
                throw new ArgumentException("Pixels must contain exactly width * height * 4 RGBA8 values.", "pixels");
            }

            Width = width;
            Height = height;
            Pixels = pixels;
        }

        /// <summary>
        /// Gets the pixel width.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the pixel height.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Gets packed RGBA8 pixels in row-major order.
        /// </summary>
        public byte[] Pixels { get; private set; }

        /// <summary>
        /// Gets a color component at the specified pixel.
        /// </summary>
        public byte GetComponent(int x, int y, int component)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || component < 0 || component > 3)
            {
                throw new ArgumentOutOfRangeException();
            }

            return Pixels[((y * Width) + x) * 4 + component];
        }

        /// <summary>
        /// Gets the red component at the specified pixel. Intended for compact tests.
        /// </summary>
        public byte GetRed(int x, int y)
        {
            return GetComponent(x, y, 0);
        }
    }
}
