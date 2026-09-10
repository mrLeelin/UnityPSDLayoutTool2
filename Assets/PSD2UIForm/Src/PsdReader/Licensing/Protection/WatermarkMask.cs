using System;
using System.Runtime.CompilerServices;

namespace WatermarkMaskNamespace
{
    internal sealed class WatermarkMask
    {
        [CompilerGenerated]
        private static readonly WatermarkMask s_Empty = new WatermarkMask(0, 0, 1, Array.Empty<byte>());

        private readonly int _width;

        private readonly int _height;

        private readonly int _samplesPerUnit;

        private readonly byte[] _coverage;

        private static WatermarkMask s_ObfuscationSentinel;

        [SpecialName]
        [CompilerGenerated]
        internal static WatermarkMask GetEmpty()
        {
            return s_Empty;
        }

        internal WatermarkMask(int value, int value2, int value3, byte[] bytes)
        {
            _width = Math.Max(0, value);
            _height = Math.Max(0, value2);
            _samplesPerUnit = Math.Max(1, value3);
            _coverage = bytes ?? Array.Empty<byte>();
        }

        [SpecialName]
        internal int GetWidth()
        {
            return _width;
        }

        [SpecialName]
        internal int GetHeight()
        {
            return _height;
        }

        [SpecialName]
        internal bool HasCoverage()
        {
            if (_width <= 0 || _height <= 0)
            {
                return false;
            }
            return _coverage.Length != 0;
        }

        internal float SampleCoverage(float value, float value2)
        {
            if (HasCoverage() && !(value < 0f) && value2 >= 0f)
            {
                float num = value * (float)_samplesPerUnit;
                float num2 = value2 * (float)_samplesPerUnit;
                if (!(num < 0f) && !(num2 < 0f) && !(num >= (float)_width) && num2 < (float)_height)
                {
                    int num3 = (int)num;
                    int num4 = (int)num2;
                    int num5 = Math.Min(num3 + 1, _width - 1);
                    int num6 = Math.Min(num4 + 1, _height - 1);
                    float num7 = num - (float)num3;
                    float num8 = num2 - (float)num4;
                    int num9 = num4 * _width;
                    int num10 = num6 * _width;
                    float num11 = (float)(int)_coverage[num9 + num3] * 0.003921569f;
                    float num12 = (float)(int)_coverage[num9 + num5] * 0.003921569f;
                    float num13 = (float)(int)_coverage[num10 + num3] * 0.003921569f;
                    float num14 = (float)(int)_coverage[num10 + num5] * 0.003921569f;
                    float num15 = num11 + (num12 - num11) * num7;
                    float num16 = num13 + (num14 - num13) * num7;
                    return num15 + (num16 - num15) * num8;
                }
                return 0f;
            }
            return 0f;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static WatermarkMask GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
