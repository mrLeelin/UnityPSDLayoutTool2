using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using cn.efunstudio.psdreader.PsdParser;
using PsdBinaryUtilities;

namespace PsdBinaryUtilities
{

internal static class PsdBinaryDataUtilities
{
	public static byte[] ExpandRunLengthPairs(object P_0)
	{
		List<byte> list = new List<byte>();
		for (int i = 1; i < ((Array)P_0).Length; i += 2)
		{
			for (byte b = ((byte[])P_0)[i - 1]; b > 0; b--)
			{
				list.Add(((byte[])P_0)[i]);
			}
		}
		return list.ToArray();
	}

	public static void DecodePackBits(object P_0, object P_1, int P_2, int P_3)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		byte b = 0;
		int num4 = P_3;
		int num5 = P_2;
		while (num4 > 0 && num5 > 0)
		{
			num3 = ((byte[])P_0)[num++];
			num5--;
			if (num3 == 128)
			{
				continue;
			}
			if (num3 > 128)
			{
				num3 -= 256;
			}
			if (num3 < 0)
			{
				num3 = 1 - num3;
				if (num5 != 0)
				{
					if (num3 <= num4)
					{
						b = ((byte[])P_0)[num];
						while (num3 > 0 && num4 != 0)
						{
							((sbyte[])P_1)[num2++] = (sbyte)b;
							num4--;
							num3--;
						}
						if (num4 > 0)
						{
							num++;
							num5--;
						}
						continue;
					}
					throw new Exception($"Overrun in packbits replicate of {num3 - num4} chars");
				}
				throw new Exception("Input buffer exhausted in replicate");
			}
			num3++;
			while (num3 > 0)
			{
				if (num5 != 0)
				{
					if (num4 != 0)
					{
						((sbyte[])P_1)[num2++] = (sbyte)((byte[])P_0)[num++];
						num4--;
						num5--;
						num3--;
						continue;
					}
					throw new Exception("Output buffer exhausted in copy");
				}
				throw new Exception("Input buffer exhausted in copy");
			}
		}
		if (num4 > 0)
		{
			for (num3 = 0; num3 < num5; num3++)
			{
				((sbyte[])P_1)[num2++] = 0;
			}
		}
	}

	public static BlendMode ParseBlendModeCode(object P_0)
	{
		return ((string)P_0).Trim() switch
		{
			"idiv" => BlendMode.ColorBurn, 
			"div" => BlendMode.ColorDodge, 
			"hue" => BlendMode.Hue, 
			"mul" => BlendMode.Multiply, 
			"smud" => BlendMode.Exclusion, 
			"hMix" => BlendMode.HardMix, 
			"scrn" => BlendMode.Screen, 
			"norm" => BlendMode.Normal, 
			"lddg" => BlendMode.LinearDodge, 
			"dkCl" => BlendMode.DarkerColor, 
			"dark" => BlendMode.Darken, 
			"colr" => BlendMode.Color, 
			"sat" => BlendMode.Saturation, 
			"sLit" => BlendMode.SoftLight, 
			"pLit" => BlendMode.PinLight, 
			"lum" => BlendMode.Luminosity, 
			"vLit" => BlendMode.VividLight, 
			"pass" => BlendMode.PassThrough, 
			"lLit" => BlendMode.LinearLight, 
			"hLit" => BlendMode.HardLight, 
			"diff" => BlendMode.Difference, 
			"fsub" => BlendMode.Subtract, 
			"lbrn" => BlendMode.LinearBurn, 
			"lite" => BlendMode.Lighten, 
			"lgCl" => BlendMode.LighterColor, 
			"over" => BlendMode.Overlay, 
			"diss" => BlendMode.Dissolve, 
			"fdiv" => BlendMode.Divide, 
			_ => BlendMode.Normal, 
		};
	}

	public static UnitType ParseUnitTypeCode(object P_0)
	{
		return P_0 switch
		{
			"#Ang" => UnitType.Angle, 
			"#Prc" => UnitType.Percent, 
			"#Rlt" => UnitType.Distance, 
			"#Rsl" => UnitType.Density, 
			"#Pnt" => UnitType.Points, 
			"#Mlm" => UnitType.Millimeters, 
			"#Pxl" => UnitType.Pixels, 
			"#Nne" => UnitType.None, 
			_ => throw new NotSupportedException(), 
		};
	}

	public static int GetChannelRowByteCount(int P_0, int P_1)
	{
		return P_0 switch
		{
			16 => P_1 * 2, 
			8 => P_1, 
			1 => P_1, 
			_ => throw new NotSupportedException(), 
		};
	}

	public static byte[] ConvertChannelToBytes(object P_0, int P_1, int P_2, int P_3, float P_4 = 1f)
	{
		if (P_0 != null)
		{
			int num = checked(P_1 * P_2);
			switch (P_3)
			{
			default:
				throw new NotSupportedException();
			case 16:
			{
				if (((Array)P_0).Length != num * 2)
				{
					throw new PsdInvalidDataException();
				}
				byte[] array2 = new byte[num];
				int num2 = 0;
				int num3 = 0;
				while (num3 < num)
				{
					byte b = (byte)(((ushort)((((byte[])P_0)[num2] << 8) | ((byte[])P_0)[num2 + 1]) + 128) / 257);
					array2[num3] = ((P_4 >= 0.9999f) ? b : ScaleByteOpacity(b, P_4));
					num3++;
					num2 += 2;
				}
				return array2;
			}
			case 8:
			{
				if (((Array)P_0).Length != num)
				{
					throw new PsdInvalidDataException();
				}
				if (P_4 >= 0.9999f)
				{
					return (byte[])P_0;
				}
				byte[] array = new byte[num];
				for (int i = 0; i < num; i++)
				{
					array[i] = ScaleByteOpacity(((byte[])P_0)[i], P_4);
				}
				return array;
			}
			}
		}
		throw new ArgumentNullException("data");
	}

	public static ushort[] ConvertChannelToUInt16(object P_0, int P_1, int P_2, int P_3, float P_4 = 1f)
	{
		if (P_0 != null)
		{
			int num = checked(P_1 * P_2);
			switch (P_3)
			{
			default:
				throw new NotSupportedException();
			case 16:
			{
				if (((Array)P_0).Length != num * 2)
				{
					throw new PsdInvalidDataException();
				}
				ushort[] array2 = new ushort[num];
				int num2 = 0;
				int num3 = 0;
				while (num3 < num)
				{
					ushort num4 = (ushort)((((byte[])P_0)[num2] << 8) | ((byte[])P_0)[num2 + 1]);
					array2[num3] = ((P_4 >= 0.9999f) ? num4 : ScaleUInt16Opacity(num4, P_4));
					num3++;
					num2 += 2;
				}
				return array2;
			}
			case 8:
			{
				if (((Array)P_0).Length != num)
				{
					throw new PsdInvalidDataException();
				}
				ushort[] array = new ushort[num];
				for (int i = 0; i < num; i++)
				{
					byte b = ((P_4 >= 0.9999f) ? ((byte[])P_0)[i] : ScaleByteOpacity(((byte[])P_0)[i], P_4));
					array[i] = ConvertByteToUInt16(b);
				}
				return array;
			}
			}
		}
		throw new ArgumentNullException("data");
	}

	public static byte[] DecompressZipData(object P_0, int P_1)
	{
		if (P_0 == null)
		{
			throw new ArgumentNullException("packedData");
		}
		if (P_1 < 0)
		{
			throw new ArgumentOutOfRangeException("expectedLength");
		}
		byte[] array = TryInflateRange(P_0, 0, ((Array)P_0).Length, P_1);
		if (array != null)
		{
			return array;
		}
		if (HasZlibHeader(P_0) && ((Array)P_0).Length > 6)
		{
			array = TryInflateRange(P_0, 2, ((Array)P_0).Length - 6, P_1);
			if (array != null)
			{
				return array;
			}
		}
		throw new PsdInvalidDataException();
	}

	public static void UndoHorizontalPrediction(object P_0, int P_1, int P_2, int P_3)
	{
		if (P_0 == null)
		{
			throw new ArgumentNullException("data");
		}
		if (P_1 <= 0 || P_2 <= 0)
		{
			return;
		}
		switch (P_3)
		{
		default:
			throw new NotSupportedException();
		case 16:
		{
			for (int k = 0; k < P_2; k++)
			{
				int num2 = k * P_1;
				for (int l = 2; l < P_1; l += 2)
				{
					ushort num3 = (ushort)((((byte[])P_0)[num2 + l - 2] << 8) | ((byte[])P_0)[num2 + l - 1]);
					ushort num4 = (ushort)((ushort)((((byte[])P_0)[num2 + l] << 8) | ((byte[])P_0)[num2 + l + 1]) + num3);
					((sbyte[])P_0)[num2 + l] = (sbyte)(byte)(num4 >> 8);
					((sbyte[])P_0)[num2 + l + 1] = (sbyte)(byte)num4;
				}
			}
			break;
		}
		case 8:
		{
			for (int i = 0; i < P_2; i++)
			{
				int num = i * P_1;
				for (int j = 1; j < P_1; j++)
				{
					((sbyte[])P_0)[num + j] = (sbyte)(byte)(((byte[])P_0)[num + j] + ((byte[])P_0)[num + j - 1]);
				}
			}
			break;
		}
		}
	}

	private static byte ScaleByteOpacity(byte P_0, float P_1)
	{
		P_1 = Math.Min(1f, Math.Max(0f, P_1));
		return (byte)Math.Round((float)(int)P_0 * P_1, MidpointRounding.AwayFromZero);
	}

	public static ushort ScaleUInt16Opacity(ushort P_0, float P_1)
	{
		P_1 = Math.Min(1f, Math.Max(0f, P_1));
		return (ushort)Math.Round((float)(int)P_0 * P_1, MidpointRounding.AwayFromZero);
	}

	public static byte ConvertUInt16ToByte(ushort P_0)
	{
		return (byte)((P_0 + 128) / 257);
	}

	public static ushort ConvertByteToUInt16(byte P_0)
	{
		return (ushort)(P_0 * 257);
	}

	private static byte[] TryInflateRange(object P_0, int P_1, int P_2, int P_3)
	{
		try
		{
			using MemoryStream stream = new MemoryStream((byte[])P_0, P_1, P_2, writable: false);
			using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
			using MemoryStream memoryStream = ((P_3 > 0) ? new MemoryStream(P_3) : new MemoryStream());
			deflateStream.CopyTo(memoryStream);
			byte[] array = memoryStream.ToArray();
			if (P_3 > 0 && array.Length != P_3)
			{
				return null;
			}
			return array;
		}
		catch (InvalidDataException)
		{
			return null;
		}
		catch (IOException)
		{
			return null;
		}
	}

	private static bool HasZlibHeader(object P_0)
	{
		if (P_0 != null && ((Array)P_0).Length >= 2)
		{
			int num = ((byte[])P_0)[0];
			int num2 = ((byte[])P_0)[1];
			if ((num & 0xF) != 8)
			{
				return false;
			}
			return ((num << 8) | num2) % 31 == 0;
		}
		return false;
	}
}
}
