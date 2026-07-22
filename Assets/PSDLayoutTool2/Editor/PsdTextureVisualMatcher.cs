namespace PsdLayoutTool2
{
    using UnityEngine;

    /// <summary>
    /// Compares decoded PNG pixels using their visible result. RGB values in
    /// fully transparent pixels are ignored, and small export/rendering
    /// differences are tolerated without treating different artwork as equal.
    /// </summary>
    public static class PsdTextureVisualMatcher
    {
        private const int SampleSize = 24;
        private const double MaximumMeanAlphaError = 0.01d;
        private const double MaximumMeanPremultipliedColorError = 0.02d;
        private const double MaximumAspectRatioError = 0.03d;

        public static bool AreEquivalent(
            int firstWidth,
            int firstHeight,
            Color32[] firstPixels,
            int secondWidth,
            int secondHeight,
            Color32[] secondPixels)
        {
            if (firstWidth <= 0 || firstHeight <= 0 || secondWidth <= 0 || secondHeight <= 0 ||
                firstPixels == null || secondPixels == null ||
                firstPixels.Length != firstWidth * firstHeight ||
                secondPixels.Length != secondWidth * secondHeight)
            {
                return false;
            }

            RectInt firstBounds;
            RectInt secondBounds;
            bool firstVisible = TryGetVisibleBounds(firstWidth, firstHeight, firstPixels, out firstBounds);
            bool secondVisible = TryGetVisibleBounds(secondWidth, secondHeight, secondPixels, out secondBounds);
            if (!firstVisible || !secondVisible)
            {
                return firstVisible == secondVisible;
            }

            double firstAspect = firstBounds.width / (double)firstBounds.height;
            double secondAspect = secondBounds.width / (double)secondBounds.height;
            if (System.Math.Abs(firstAspect - secondAspect) / System.Math.Max(firstAspect, secondAspect) > MaximumAspectRatioError)
            {
                return false;
            }

            double alphaError = 0d;
            double colorError = 0d;
            for (int y = 0; y < SampleSize; y++)
            {
                for (int x = 0; x < SampleSize; x++)
                {
                    Color32 first = Sample(firstWidth, firstPixels, firstBounds, x, y);
                    Color32 second = Sample(secondWidth, secondPixels, secondBounds, x, y);
                    double firstAlpha = first.a / 255d;
                    double secondAlpha = second.a / 255d;
                    alphaError += System.Math.Abs(firstAlpha - secondAlpha);
                    colorError += System.Math.Abs((first.r / 255d * firstAlpha) - (second.r / 255d * secondAlpha));
                    colorError += System.Math.Abs((first.g / 255d * firstAlpha) - (second.g / 255d * secondAlpha));
                    colorError += System.Math.Abs((first.b / 255d * firstAlpha) - (second.b / 255d * secondAlpha));
                }
            }

            double pixelCount = SampleSize * SampleSize;
            return alphaError / pixelCount <= MaximumMeanAlphaError &&
                colorError / (pixelCount * 3d) <= MaximumMeanPremultipliedColorError;
        }

        private static bool TryGetVisibleBounds(int width, int height, Color32[] pixels, out RectInt bounds)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[(y * width) + x].a == 0)
                    {
                        continue;
                    }

                    minX = System.Math.Min(minX, x);
                    minY = System.Math.Min(minY, y);
                    maxX = System.Math.Max(maxX, x);
                    maxY = System.Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                bounds = new RectInt();
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static Color32 Sample(int sourceWidth, Color32[] pixels, RectInt bounds, int sampleX, int sampleY)
        {
            int x = bounds.xMin + System.Math.Min(
                bounds.width - 1,
                (int)(((sampleX + 0.5d) / SampleSize) * bounds.width));
            int y = bounds.yMin + System.Math.Min(
                bounds.height - 1,
                (int)(((sampleY + 0.5d) / SampleSize) * bounds.height));
            return pixels[(y * sourceWidth) + x];
        }
    }
}
