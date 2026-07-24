namespace PsdLayoutTool2
{
    using UnityEngine;

    /// <summary>
    /// Finds the uniform scale and Z rotation that best maps a public Common
    /// sprite onto the already-rasterized PSD layer visual. The PSD reader only
    /// exposes the baked pixels, therefore this comparison is the authoritative
    /// source for Common_* replacement geometry.
    /// </summary>
    internal static class PsdCommonVisualTransformMatcher
    {
        internal struct Result
        {
            public float RotationDegrees;
            public float Scale;
            public float Error;
        }

        internal static bool TryMatch(
            int sourceWidth, int sourceHeight, Color32[] source,
            int targetWidth, int targetHeight, Color32[] target,
            out Result result)
        {
            result = default(Result);
            if (!IsValid(sourceWidth, sourceHeight, source) || !IsValid(targetWidth, targetHeight, target)) return false;

            float bestError = float.MaxValue;
            float bestAngle = 0f;
            float bestScale = 1f;
            for (float angle = -35f; angle <= 35f; angle += 1f)
            {
                for (float scale = 0.4f; scale <= 0.9f; scale += 0.01f)
                {
                    float error = CalculateAlphaError(sourceWidth, sourceHeight, source, targetWidth, targetHeight, target, angle, scale);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestAngle = angle;
                        bestScale = scale;
                    }
                }
            }

            for (float angle = bestAngle - 1f; angle <= bestAngle + 1f; angle += 0.25f)
            {
                for (float scale = bestScale - 0.02f; scale <= bestScale + 0.02f; scale += 0.005f)
                {
                    float error = CalculateAlphaError(sourceWidth, sourceHeight, source, targetWidth, targetHeight, target, angle, scale);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestAngle = angle;
                        bestScale = scale;
                    }
                }
            }

            result = new Result { RotationDegrees = bestAngle, Scale = bestScale, Error = bestError };
            return bestError <= 0.05f;
        }

        private static bool IsValid(int width, int height, Color32[] pixels)
        {
            return width > 0 && height > 0 && pixels != null && pixels.Length == width * height;
        }

        private static float CalculateAlphaError(
            int sourceWidth, int sourceHeight, Color32[] source,
            int targetWidth, int targetHeight, Color32[] target,
            float angleDegrees, float scale)
        {
            float radians = -angleDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            float sourceCenterX = (sourceWidth - 1) * 0.5f;
            float sourceCenterY = (sourceHeight - 1) * 0.5f;
            float targetCenterX = (targetWidth - 1) * 0.5f;
            float targetCenterY = (targetHeight - 1) * 0.5f;
            float error = 0f;
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    float dx = (x - targetCenterX) / scale;
                    float dy = (y - targetCenterY) / scale;
                    float sourceX = (cosine * dx) - (sine * dy) + sourceCenterX;
                    float sourceY = (sine * dx) + (cosine * dy) + sourceCenterY;
                    float sourceAlpha = SampleAlpha(sourceWidth, sourceHeight, source, sourceX, sourceY);
                    float targetAlpha = target[(y * targetWidth) + x].a / 255f;
                    float difference = sourceAlpha - targetAlpha;
                    error += difference * difference;
                }
            }

            return error / (targetWidth * targetHeight);
        }

        private static float SampleAlpha(int width, int height, Color32[] pixels, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            if (x0 < 0 || y0 < 0 || x0 >= width - 1 || y0 >= height - 1) return 0f;
            float tx = x - x0;
            float ty = y - y0;
            float a = pixels[(y0 * width) + x0].a / 255f;
            float b = pixels[(y0 * width) + x0 + 1].a / 255f;
            float c = pixels[((y0 + 1) * width) + x0].a / 255f;
            float d = pixels[((y0 + 1) * width) + x0 + 1].a / 255f;
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
    }
}
