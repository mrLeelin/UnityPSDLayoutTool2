using System;
using System.Collections.Generic;
using WatermarkMaskNamespace;

namespace WatermarkTextRasterizerNamespace
{
    internal sealed class WatermarkTextRasterizer
    {
        private static readonly Dictionary<char, byte[]> s_LatinGlyphRows = new Dictionary<char, byte[]>
        {
            [' '] = new byte[7],
            ['.'] = new byte[7] { 0, 0, 0, 0, 0, 4, 4 },
            ['?'] = new byte[7] { 14, 17, 2, 4, 4, 0, 4 },
            ['0'] = new byte[7] { 14, 17, 19, 21, 25, 17, 14 },
            ['1'] = new byte[7] { 4, 12, 4, 4, 4, 4, 14 },
            ['2'] = new byte[7] { 14, 17, 1, 2, 4, 8, 31 },
            ['3'] = new byte[7] { 30, 1, 1, 14, 1, 1, 30 },
            ['4'] = new byte[7] { 2, 6, 10, 18, 31, 2, 2 },
            ['5'] = new byte[7] { 31, 16, 16, 30, 1, 1, 30 },
            ['6'] = new byte[7] { 14, 16, 16, 30, 17, 17, 14 },
            ['7'] = new byte[7] { 31, 1, 2, 4, 8, 8, 8 },
            ['8'] = new byte[7] { 14, 17, 17, 14, 17, 17, 14 },
            ['9'] = new byte[7] { 14, 17, 17, 15, 1, 1, 14 },
            ['A'] = new byte[7] { 14, 17, 17, 31, 17, 17, 17 },
            ['a'] = new byte[7] { 0, 0, 14, 1, 15, 17, 15 },
            ['C'] = new byte[7] { 14, 17, 16, 16, 16, 17, 14 },
            ['c'] = new byte[7] { 0, 0, 14, 17, 16, 17, 14 },
            ['D'] = new byte[7] { 30, 17, 17, 17, 17, 17, 30 },
            ['d'] = new byte[7] { 1, 1, 15, 17, 17, 17, 15 },
            ['E'] = new byte[7] { 31, 16, 16, 30, 16, 16, 31 },
            ['e'] = new byte[7] { 0, 0, 14, 17, 31, 16, 14 },
            ['F'] = new byte[7] { 31, 16, 16, 30, 16, 16, 16 },
            ['f'] = new byte[7] { 6, 9, 8, 28, 8, 8, 8 },
            ['G'] = new byte[7] { 14, 17, 16, 23, 17, 17, 14 },
            ['g'] = new byte[7] { 0, 0, 15, 17, 15, 1, 14 },
            ['I'] = new byte[7] { 31, 4, 4, 4, 4, 4, 31 },
            ['i'] = new byte[7] { 4, 0, 12, 4, 4, 4, 14 },
            ['L'] = new byte[7] { 16, 16, 16, 16, 16, 16, 31 },
            ['l'] = new byte[7] { 12, 4, 4, 4, 4, 4, 14 },
            ['N'] = new byte[7] { 17, 25, 21, 19, 17, 17, 17 },
            ['n'] = new byte[7] { 0, 0, 30, 17, 17, 17, 17 },
            ['O'] = new byte[7] { 14, 17, 17, 17, 17, 17, 14 },
            ['o'] = new byte[7] { 0, 0, 14, 17, 17, 17, 14 },
            ['P'] = new byte[7] { 30, 17, 17, 30, 16, 16, 16 },
            ['p'] = new byte[7] { 0, 0, 30, 17, 30, 16, 16 },
            ['R'] = new byte[7] { 30, 17, 17, 30, 20, 18, 17 },
            ['r'] = new byte[7] { 0, 0, 22, 25, 16, 16, 16 },
            ['S'] = new byte[7] { 15, 16, 16, 14, 1, 1, 30 },
            ['s'] = new byte[7] { 0, 0, 15, 16, 14, 1, 30 },
            ['T'] = new byte[7] { 31, 4, 4, 4, 4, 4, 4 },
            ['t'] = new byte[7] { 4, 4, 31, 4, 4, 4, 2 },
            ['U'] = new byte[7] { 17, 17, 17, 17, 17, 17, 14 },
            ['u'] = new byte[7] { 0, 0, 17, 17, 17, 19, 13 }
        };

        private static readonly Dictionary<char, ushort[]> s_CustomCjkGlyphRows = new Dictionary<char, ushort[]>
        {
            ['试'] = new ushort[16]
            {
                0, 48, 12350, 14390, 8190, 2046, 30776, 31736, 14328, 15256,
                14744, 15770, 15835, 16382, 14222, 0
            },
            ['用'] = new ushort[16]
            {
                0, 8190, 16383, 14535, 14535, 16383, 16383, 14535, 14535, 16383,
                16383, 14535, 14535, 12510, 12510, 0
            },
            ['版'] = new ushort[16]
            {
                0, 1538, 14078, 14078, 14016, 16126, 16382, 12518, 12518, 16124,
                16060, 14232, 14268, 30718, 26598, 0
            }
        };

        private static WatermarkTextRasterizer s_ObfuscationSentinel;

        internal static float MeasureTextWidth(object id, float value)
        {
            if (string.IsNullOrEmpty((string)id) || value <= 0f)
            {
                return 0f;
            }
            return Math.Max(1f, MeasureGlyphRunWidth(id) * value);
        }

        internal static float MeasureTextHeight(object value, float value2)
        {
            if (!string.IsNullOrEmpty((string)value) && value2 > 0f)
            {
                return Math.Max(1f, (float)MeasureMaximumGlyphHeight(value) * value2);
            }
            return 0f;
        }

        internal static float SampleTextCoverage(object value, float value2, float value3, float value4)
        {
            return SampleTextCoverageWithinBounds(value, value2, value3, value4, MeasureTextHeight(value, value4));
        }

        internal static float SampleTextCoverageWithinBounds(object text, float value, float value2, float value3, float value4)
        {
            if (!string.IsNullOrEmpty((string)text) && !(value3 <= 0f) && !(value < 0f) && !(value2 < 0f) && value2 < value4)
            {
                float num = 0f;
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        float num2 = value + ((float)j + 0.5f) * 0.5f;
                        float num3 = value2 + ((float)i + 0.5f) * 0.5f;
                        if (IsTextPixelSet(text, num2, num3, value3))
                        {
                            num += 0.25f;
                        }
                    }
                }
                return num;
            }
            return 0f;
        }

        private static bool IsTextPixelSet(object value, float value2, float value3, float value4)
        {
            if (!string.IsNullOrEmpty((string)value) && value4 > 0f)
            {
                int num = (int)Math.Floor(value3 / value4);
                if (num < 0)
                {
                    return false;
                }
                int num2 = (int)Math.Floor(value3 / value4);
                int num3 = (int)Math.Floor(value2 / value4);
                if (num3 < 0)
                {
                    return false;
                }
                int num4 = 0;
                int num5 = 0;
                while (true)
                {
                    if (num5 < ((string)value).Length)
                    {
                        GetGlyphMetrics(((string)value)[num5], out var num6, out var num7, out var num8);
                        int num9 = num3 - num4;
                        if (num9 < 0 || num9 >= num6)
                        {
                            num4 += num8;
                            if (num3 < num4)
                            {
                                break;
                            }
                            num5++;
                            continue;
                        }
                        if (num2 >= 0 && num2 < num7)
                        {
                            return IsGlyphPixelSet(((string)value)[num5], num9, num);
                        }
                        return false;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static float MeasureGlyphRunWidth(object value)
        {
            float num = 0f;
            int num2 = 0;
            for (int i = 0; i < ((string)value).Length; i++)
            {
                GetGlyphMetrics(((string)value)[i], out var num3, out var _, out var num5);
                num = Math.Max(num, num2 + num3);
                num2 += num5;
            }
            return num;
        }

        private static int MeasureMaximumGlyphHeight(object value)
        {
            int num = 0;
            for (int i = 0; i < ((string)value).Length; i++)
            {
                GetGlyphMetrics(((string)value)[i], out var _, out var val, out var _);
                num = Math.Max(num, val);
            }
            return num;
        }

        private static void GetGlyphMetrics(char value, out int result, out int result2, out int result3)
        {
            if (!HasCustomCjkGlyph(value))
            {
                result = 5;
                result2 = 7;
                result3 = 6;
            }
            else
            {
                result = 16;
                result2 = 16;
                result3 = 18;
            }
        }

        private static bool IsGlyphPixelSet(char value, int value2, int value3)
        {
            if (HasCustomCjkGlyph(value))
            {
                return IsCustomCjkGlyphPixelSet(value, value2, value3);
            }
            byte[] array = GetLatinGlyphRows(value);
            if (value3 >= 0 && value3 < 7 && value2 >= 0 && value2 < 5)
            {
                return ((array[value3] >> 4 - value2) & 1) != 0;
            }
            return false;
        }

        private static byte[] GetLatinGlyphRows(char value2)
        {
            if (s_LatinGlyphRows.TryGetValue(value2, out var value))
            {
                return value;
            }
            if (s_LatinGlyphRows.TryGetValue(char.ToUpperInvariant(value2), out value))
            {
                return value;
            }
            return s_LatinGlyphRows['?'];
        }

        private static bool HasCustomCjkGlyph(char value)
        {
            return s_CustomCjkGlyphRows.ContainsKey(value);
        }

        private static bool IsCustomCjkGlyphPixelSet(char value2, int value3, int value4)
        {
            if (s_CustomCjkGlyphRows.TryGetValue(value2, out var value) && value != null && value.Length == 16 && value3 >= 0 && value3 < 16 && value4 >= 0 && value4 < 16)
            {
                return ((value[value4] >> 15 - value3) & 1) != 0;
            }
            return false;
        }

        internal static WatermarkMask CreateWatermarkMask(object text, float value, int value2 = 8)
        {
            if (string.IsNullOrEmpty((string)text) || value <= 0f)
            {
                return WatermarkMask.GetEmpty();
            }
            int num = Math.Max(1, value2);
            float num2 = MeasureTextWidth(text, value);
            float num3 = MeasureTextHeight(text, value);
            int num4 = Math.Max(1, (int)Math.Ceiling(num2 * (float)num));
            int num5 = Math.Max(1, (int)Math.Ceiling(num3 * (float)num));
            byte[] array = new byte[num4 * num5];
            float num6 = 1f / (float)num;
            for (int i = 0; i < num5; i++)
            {
                float num7 = ((float)i + 0.5f) * num6;
                for (int j = 0; j < num4; j++)
                {
                    float num8 = ((float)j + 0.5f) * num6;
                    float val = SampleTextCoverageWithinBounds(text, num8, num7, value, num3);
                    array[i * num4 + j] = (byte)Math.Round(Math.Min(1f, Math.Max(0f, val)) * 255f, MidpointRounding.AwayFromZero);
                }
            }
            return new WatermarkMask(num4, num5, num, array);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static WatermarkTextRasterizer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
