using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdPreviewProtection;

namespace PsdPreviewProtection
{

internal static class PreviewWatermarkBitmapFont
{
	private static readonly Dictionary<char, byte[]> _latinGlyphRows;

	private static readonly Dictionary<char, ushort[]> _chineseGlyphRows;

	internal static float lxL3jFuJQ5(object P_0, float P_1)
	{
		if (!string.IsNullOrEmpty((string)P_0) && P_1 > 0f)
		{
			return Math.Max(1f, MeasureUnscaledTextWidth(P_0) * P_1);
		}
		return 0f;
	}

	internal static float DXm3YbEbAW(object P_0, float P_1)
	{
		if (!string.IsNullOrEmpty((string)P_0) && P_1 > 0f)
		{
			return Math.Max(1f, (float)MeasureUnscaledTextHeight(P_0) * P_1);
		}
		return 0f;
	}

	internal static float uKg3tRUdtF(object P_0, float P_1, float P_2, float P_3)
	{
		return DIU34EdWTy(P_0, P_1, P_2, P_3, DXm3YbEbAW(P_0, P_3));
	}

	internal static float DIU34EdWTy(object P_0, float P_1, float P_2, float P_3, float P_4)
	{
		if (!string.IsNullOrEmpty((string)P_0) && !(P_3 <= 0f) && !(P_1 < 0f) && !(P_2 < 0f) && P_2 < P_4)
		{
			float num = 0f;
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < 2; j++)
				{
					float num2 = P_1 + ((float)j + 0.5f) * 0.5f;
					float num3 = P_2 + ((float)i + 0.5f) * 0.5f;
					if (aWB3fm7Umn(P_0, num2, num3, P_3))
					{
						num += 0.25f;
					}
				}
			}
			return num;
		}
		return 0f;
	}

	private static bool aWB3fm7Umn(object P_0, float P_1, float P_2, float P_3)
	{
		if (!string.IsNullOrEmpty((string)P_0) && P_3 > 0f)
		{
			int num = (int)Math.Floor(P_2 / P_3);
			if (num < 0)
			{
				return false;
			}
			int num2 = (int)Math.Floor(P_2 / P_3);
			int num3 = (int)Math.Floor(P_1 / P_3);
			if (num3 < 0)
			{
				return false;
			}
			int num4 = 0;
			int num5 = 0;
			while (true)
			{
				if (num5 < ((string)P_0).Length)
				{
					GetGlyphMetrics(((string)P_0)[num5], out var num6, out var num7, out var num8);
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
						return IsGlyphPixelSet(((string)P_0)[num5], num9, num);
					}
					return false;
				}
				return false;
			}
			return false;
		}
		return false;
	}

	private static float MeasureUnscaledTextWidth(object P_0)
	{
		float num = 0f;
		int num2 = 0;
		for (int i = 0; i < ((string)P_0).Length; i++)
		{
			GetGlyphMetrics(((string)P_0)[i], out var num3, out var _, out var num5);
			num = Math.Max(num, num2 + num3);
			num2 += num5;
		}
		return num;
	}

	private static int MeasureUnscaledTextHeight(object P_0)
	{
		int num = 0;
		for (int i = 0; i < ((string)P_0).Length; i++)
		{
			GetGlyphMetrics(((string)P_0)[i], out var _, out var val, out var _);
			num = Math.Max(num, val);
		}
		return num;
	}

	private static void GetGlyphMetrics(char P_0, out int P_1, out int P_2, out int P_3)
	{
		if (!HasChineseGlyph(P_0))
		{
			P_1 = 5;
			P_2 = 7;
			P_3 = 6;
		}
		else
		{
			P_1 = 16;
			P_2 = 16;
			P_3 = 18;
		}
	}

	private static bool IsGlyphPixelSet(char P_0, int P_1, int P_2)
	{
		if (!HasChineseGlyph(P_0))
		{
			byte[] array = GetLatinGlyphRows(P_0);
			if (P_2 >= 0 && P_2 < 7 && P_1 >= 0 && P_1 < 5)
			{
				return ((array[P_2] >> 4 - P_1) & 1) != 0;
			}
			return false;
		}
		return IsChineseGlyphPixelSet(P_0, P_1, P_2);
	}

	private static byte[] GetLatinGlyphRows(char P_0)
	{
		if (!_latinGlyphRows.TryGetValue(P_0, out var value))
		{
			if (_latinGlyphRows.TryGetValue(char.ToUpperInvariant(P_0), out value))
			{
				return value;
			}
			return _latinGlyphRows['?'];
		}
		return value;
	}

	private static bool HasChineseGlyph(char P_0)
	{
		return _chineseGlyphRows.ContainsKey(P_0);
	}

	private static bool IsChineseGlyphPixelSet(char P_0, int P_1, int P_2)
	{
		if (_chineseGlyphRows.TryGetValue(P_0, out var value) && value != null && value.Length == 16 && P_1 >= 0 && P_1 < 16 && P_2 >= 0 && P_2 < 16)
		{
			return ((value[P_2] >> 15 - P_1) & 1) != 0;
		}
		return false;
	}

	internal static WatermarkCoverageBitmap RasterizeCoverageBitmap(object P_0, float P_1, int P_2 = 8)
	{
		if (!string.IsNullOrEmpty((string)P_0) && P_1 > 0f)
		{
			int num = Math.Max(1, P_2);
			float num2 = lxL3jFuJQ5(P_0, P_1);
			float num3 = DXm3YbEbAW(P_0, P_1);
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
					float val = DIU34EdWTy(P_0, num8, num7, P_1, num3);
					array[i * num4 + j] = (byte)Math.Round(Math.Min(1f, Math.Max(0f, val)) * 255f, MidpointRounding.AwayFromZero);
				}
			}
			return new WatermarkCoverageBitmap(num4, num5, num, array);
		}
		return WatermarkCoverageBitmap.GetEmpty();
	}

	static PreviewWatermarkBitmapFont()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_latinGlyphRows = new Dictionary<char, byte[]>
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
		_chineseGlyphRows = new Dictionary<char, ushort[]>
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
	}
}
}
