using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using InvalidPsdFileExceptionNamespace;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdBinaryUtilityNamespace
{
    internal sealed class PsdBinaryUtility
    {
        internal static PsdBinaryUtility s_ObfuscationSentinel;

        public static byte[] ExpandRunLengthPairs(object value)
        {
            List<byte> list = new List<byte>();
            for (int i = 1; i < ((Array)value).Length; i += 2)
            {
                for (byte b = ((byte[])value)[i - 1]; b > 0; b--)
                {
                    list.Add(((byte[])value)[i]);
                }
            }
            return list.ToArray();
        }

        public static void DecodePackBits(object value, object values, int value2, int value3)
        {
            int num = 0;
            int num2 = 0;
            int num3 = value3;
            int num4 = value2;
            while (num3 > 0 && num4 > 0)
            {
                int num5 = ((byte[])value)[num++];
                num4--;
                if (num5 == 128)
                {
                    continue;
                }
                if (num5 > 128)
                {
                    num5 -= 256;
                }
                if (num5 >= 0)
                {
                    num5++;
                    while (num5 > 0)
                    {
                        if (num4 != 0)
                        {
                            if (num3 != 0)
                            {
                                ((sbyte[])values)[num2++] = (sbyte)((byte[])value)[num++];
                                num3--;
                                num4--;
                                num5--;
                                continue;
                            }
                            throw new Exception("Output buffer exhausted in copy");
                        }
                        throw new Exception("Input buffer exhausted in copy");
                    }
                    continue;
                }
                num5 = 1 - num5;
                if (num4 != 0)
                {
                    if (num5 <= num3)
                    {
                        byte b = ((byte[])value)[num];
                        while (num5 > 0 && num3 != 0)
                        {
                            ((sbyte[])values)[num2++] = (sbyte)b;
                            num3--;
                            num5--;
                        }
                        if (num3 > 0)
                        {
                            num++;
                            num4--;
                        }
                        continue;
                    }
                    throw new Exception($"Overrun in packbits replicate of {num5 - num3} chars");
                }
                throw new Exception("Input buffer exhausted in replicate");
            }
            if (num3 > 0)
            {
                for (int num5 = 0; num5 < num4; num5++)
                {
                    ((sbyte[])values)[num2++] = 0;
                }
            }
        }

        public static BlendMode ParseBlendMode(object text)
        {
            return ((string)text).Trim() switch
            {
                "pLit" => BlendMode.PinLight, 
                "lum" => BlendMode.Luminosity, 
                "vLit" => BlendMode.VividLight, 
                "hLit" => BlendMode.HardLight, 
                "diff" => BlendMode.Difference, 
                "pass" => BlendMode.PassThrough, 
                "lLit" => BlendMode.LinearLight, 
                "diss" => BlendMode.Dissolve, 
                "fdiv" => BlendMode.Divide, 
                "lgCl" => BlendMode.LighterColor, 
                "over" => BlendMode.Overlay, 
                "fsub" => BlendMode.Subtract, 
                "lbrn" => BlendMode.LinearBurn, 
                "lite" => BlendMode.Lighten, 
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
                "sat" => BlendMode.Saturation, 
                "sLit" => BlendMode.SoftLight, 
                "dark" => BlendMode.Darken, 
                "colr" => BlendMode.Color, 
                _ => BlendMode.Normal, 
            };
        }

        public static UnitType ParseUnitType(object value)
        {
            return value switch
            {
                "#Rlt" => UnitType.Distance, 
                "#Rsl" => UnitType.Density, 
                "#Ang" => UnitType.Angle, 
                "#Prc" => UnitType.Percent, 
                "#Pxl" => UnitType.Pixels, 
                "#Nne" => UnitType.None, 
                "#Pnt" => UnitType.Points, 
                "#Mlm" => UnitType.Millimeters, 
                _ => throw new NotSupportedException(), 
            };
        }

        public static int GetRowByteCount(int count, int count2)
        {
            return count switch
            {
                16 => count2 * 2, 
                8 => count2, 
                1 => count2, 
                _ => throw new NotSupportedException(), 
            };
        }

        public static byte[] ConvertChannelDataTo8Bit(object value, int value2, int value3, int value4, float value5 = 1f)
        {
            if (value != null)
            {
                int num = checked(value2 * value3);
                switch (value4)
                {
                default:
                    throw new NotSupportedException();
                case 16:
                {
                    if (((Array)value).Length != num * 2)
                    {
                        throw new InvalidPsdFileException();
                    }
                    byte[] array2 = new byte[num];
                    int num2 = 0;
                    int num3 = 0;
                    while (num3 < num)
                    {
                        byte b = (byte)(((ushort)((((byte[])value)[num2] << 8) | ((byte[])value)[num2 + 1]) + 128) / 257);
                        array2[num3] = ((value5 >= 0.9999f) ? b : ScaleByteByOpacity(b, value5));
                        num3++;
                        num2 += 2;
                    }
                    return array2;
                }
                case 8:
                {
                    if (((Array)value).Length != num)
                    {
                        throw new InvalidPsdFileException();
                    }
                    if (value5 >= 0.9999f)
                    {
                        return (byte[])value;
                    }
                    byte[] array = new byte[num];
                    for (int i = 0; i < num; i++)
                    {
                        array[i] = ScaleByteByOpacity(((byte[])value)[i], value5);
                    }
                    return array;
                }
                }
            }
            throw new ArgumentNullException("data");
        }

        public static ushort[] ConvertChannelDataTo16Bit(object value, int value2, int value3, int value4, float value5 = 1f)
        {
            if (value == null)
            {
                throw new ArgumentNullException("data");
            }
            int num = checked(value2 * value3);
            switch (value4)
            {
            default:
                throw new NotSupportedException();
            case 16:
            {
                if (((Array)value).Length != num * 2)
                {
                    throw new InvalidPsdFileException();
                }
                ushort[] array2 = new ushort[num];
                int num2 = 0;
                int num3 = 0;
                while (num3 < num)
                {
                    ushort num4 = (ushort)((((byte[])value)[num2] << 8) | ((byte[])value)[num2 + 1]);
                    array2[num3] = ((value5 >= 0.9999f) ? num4 : ScaleUInt16ByOpacity(num4, value5));
                    num3++;
                    num2 += 2;
                }
                return array2;
            }
            case 8:
            {
                if (((Array)value).Length != num)
                {
                    throw new InvalidPsdFileException();
                }
                ushort[] array = new ushort[num];
                for (int i = 0; i < num; i++)
                {
                    byte b = ((value5 >= 0.9999f) ? ((byte[])value)[i] : ScaleByteByOpacity(((byte[])value)[i], value5));
                    array[i] = ConvertByteToUInt16(b);
                }
                return array;
            }
            }
        }

        public static byte[] InflateZipData(object array2, int value)
        {
            if (array2 == null)
            {
                throw new ArgumentNullException("packedData");
            }
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("expectedLength");
            }
            byte[] array = TryInflateData(array2, 0, ((Array)array2).Length, value);
            if (array != null)
            {
                return array;
            }
            if (HasZlibHeader(array2) && ((Array)array2).Length > 6)
            {
                array = TryInflateData(array2, 2, ((Array)array2).Length - 6, value);
                if (array != null)
                {
                    return array;
                }
            }
            throw new InvalidPsdFileException();
        }

        public static void ApplyZipPrediction(object value, int value2, int value3, int value4)
        {
            if (value == null)
            {
                throw new ArgumentNullException("data");
            }
            if (value2 <= 0 || value3 <= 0)
            {
                return;
            }
            switch (value4)
            {
            default:
                throw new NotSupportedException();
            case 16:
            {
                for (int k = 0; k < value3; k++)
                {
                    int num2 = k * value2;
                    for (int l = 2; l < value2; l += 2)
                    {
                        ushort num3 = (ushort)((((byte[])value)[num2 + l - 2] << 8) | ((byte[])value)[num2 + l - 1]);
                        ushort num4 = (ushort)((ushort)((((byte[])value)[num2 + l] << 8) | ((byte[])value)[num2 + l + 1]) + num3);
                        ((sbyte[])value)[num2 + l] = (sbyte)(byte)(num4 >> 8);
                        ((sbyte[])value)[num2 + l + 1] = (sbyte)(byte)num4;
                    }
                }
                break;
            }
            case 8:
            {
                for (int i = 0; i < value3; i++)
                {
                    int num = i * value2;
                    for (int j = 1; j < value2; j++)
                    {
                        ((sbyte[])value)[num + j] = (sbyte)(byte)(((byte[])value)[num + j] + ((byte[])value)[num + j - 1]);
                    }
                }
                break;
            }
            }
        }

        private static byte ScaleByteByOpacity(byte value, float value2)
        {
            value2 = Math.Min(1f, Math.Max(0f, value2));
            return (byte)Math.Round((float)(int)value * value2, MidpointRounding.AwayFromZero);
        }

        public static ushort ScaleUInt16ByOpacity(ushort value, float value2)
        {
            value2 = Math.Min(1f, Math.Max(0f, value2));
            return (ushort)Math.Round((float)(int)value * value2, MidpointRounding.AwayFromZero);
        }

        public static byte ConvertUInt16ToByte(ushort value)
        {
            return (byte)((value + 128) / 257);
        }

        public static ushort ConvertByteToUInt16(byte value)
        {
            return (ushort)(value * 257);
        }

        private static byte[] TryInflateData(object value, int value2, int value3, int value4)
        {
            try
            {
                using MemoryStream stream = new MemoryStream((byte[])value, value2, value3, writable: false);
                using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
                using MemoryStream memoryStream = ((value4 <= 0) ? new MemoryStream() : new MemoryStream(value4));
                deflateStream.CopyTo(memoryStream);
                byte[] array = memoryStream.ToArray();
                if (value4 > 0 && array.Length != value4)
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

        private static bool HasZlibHeader(object value)
        {
            if (value != null && ((Array)value).Length >= 2)
            {
                int num = ((byte[])value)[0];
                int num2 = ((byte[])value)[1];
                if ((num & 0xF) != 8)
                {
                    return false;
                }
                return ((num << 8) | num2) % 31 == 0;
            }
            return false;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdBinaryUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
