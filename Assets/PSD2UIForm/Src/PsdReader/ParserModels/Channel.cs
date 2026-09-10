using System;
using InvalidPsdFileExceptionNamespace;
using PsdBinaryReaderNamespace;
using PsdBinaryUtilityNamespace;

namespace cn.efunstudio.psdreader.PsdParser
{
    internal sealed class Channel
    {
        private byte[] data;

        private ushort[] data16;

        private ChannelType type;

        private int height;

        private int width;

        private int[] rlePackLengths;

        private float opacity = 1f;

        private long size;

        internal static Channel s_ObfuscationSentinel;

        internal byte[] Data => data;

        internal ushort[] Data16 => data16;

        internal ChannelType Type
        {
            get
            {
                return type;
            }
            set
            {
                type = value;
            }
        }

        internal int Width
        {
            get
            {
                return width;
            }
            set
            {
                width = value;
            }
        }

        internal int Height
        {
            get
            {
                return height;
            }
            set
            {
                height = value;
            }
        }

        internal float Opacity
        {
            get
            {
                return opacity;
            }
            set
            {
                opacity = value;
            }
        }

        internal long Size
        {
            get
            {
                return size;
            }
            set
            {
                size = value;
            }
        }

        internal Channel(ChannelType type, int width, int height, long size)
        {
            this.type = type;
            this.width = width;
            this.height = height;
            this.size = size;
        }

        internal Channel()
        {
        }

        internal void ReadHeader(PsdBinaryReader reader, CompressionType compressionType)
        {
            if (compressionType != CompressionType.RLE)
            {
                return;
            }
            rlePackLengths = new int[height];
            if (reader.Version == 1)
            {
                for (int i = 0; i < height; i++)
                {
                    rlePackLengths[i] = reader.ReadInt16();
                }
            }
            else
            {
                for (int j = 0; j < height; j++)
                {
                    rlePackLengths[j] = reader.ReadInt32();
                }
            }
        }

        internal void Read(PsdBinaryReader reader, int bpp, CompressionType compressionType)
        {
            Read(reader, bpp, compressionType, 0);
        }

        internal void Read(PsdBinaryReader reader, int bpp, CompressionType compressionType, int packedDataLength)
        {
            switch (compressionType)
            {
            default:
                throw new NotSupportedException();
            case CompressionType.Raw:
                ReadData(reader, bpp, compressionType, null);
                break;
            case CompressionType.RLE:
                ReadData(reader, bpp, compressionType, rlePackLengths);
                break;
            case CompressionType.Zip:
                ReadZipData(reader, bpp, packedDataLength, applyPrediction: false);
                break;
            case CompressionType.ZipPrediction:
                ReadZipData(reader, bpp, packedDataLength, applyPrediction: true);
                break;
            }
        }

        internal void SetData(byte[] data, int bps)
        {
            this.data = PsdBinaryUtility.ConvertChannelDataTo8Bit(data, width, height, bps, opacity);
            data16 = ((bps == 16) ? PsdBinaryUtility.ConvertChannelDataTo16Bit(data, width, height, bps, opacity) : null);
        }

        private void ReadData(PsdBinaryReader reader, int bps, CompressionType compressionType, int[] rlePackLengths)
        {
            int num = PsdBinaryUtility.GetRowByteCount(bps, width);
            byte[] array = new byte[num * height];
            switch (compressionType)
            {
            case CompressionType.RLE:
            {
                for (int i = 0; i < height; i++)
                {
                    byte[] array2 = new byte[rlePackLengths[i]];
                    byte[] array3 = new byte[num];
                    reader.Read(array2, 0, rlePackLengths[i]);
                    DecodeRLE(array2, array3, rlePackLengths[i], num);
                    for (int j = 0; j < num; j++)
                    {
                        array[i * num + j] = array3[j];
                    }
                }
                break;
            }
            case CompressionType.Raw:
                reader.Read(array, 0, array.Length);
                break;
            }
            data = PsdBinaryUtility.ConvertChannelDataTo8Bit(array, width, height, bps, opacity);
            data16 = ((bps == 16) ? PsdBinaryUtility.ConvertChannelDataTo16Bit(array, width, height, bps, opacity) : null);
        }

        private void ReadZipData(PsdBinaryReader reader, int bps, int packedDataLength, bool applyPrediction)
        {
            if (packedDataLength < 0)
            {
                throw new InvalidPsdFileException();
            }
            int num = PsdBinaryUtility.GetRowByteCount(bps, width);
            int num2 = num * height;
            byte[] array = PsdBinaryUtility.InflateZipData(reader.ReadBytes(packedDataLength), num2);
            if (applyPrediction)
            {
                PsdBinaryUtility.ApplyZipPrediction(array, num, height, bps);
            }
            data = PsdBinaryUtility.ConvertChannelDataTo8Bit(array, width, height, bps, opacity);
            data16 = ((bps == 16) ? PsdBinaryUtility.ConvertChannelDataTo16Bit(array, width, height, bps, opacity) : null);
        }

        private static void DecodeRLE(byte[] src, byte[] dst, int packedLength, int unpackedLength)
        {
            int num = 0;
            int num2 = 0;
            int num3 = unpackedLength;
            int num4 = packedLength;
            while (num3 > 0 && num4 > 0)
            {
                int num5 = src[num++];
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
                                dst[num2++] = src[num++];
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
                        byte b = src[num];
                        while (num5 > 0 && num3 != 0)
                        {
                            dst[num2++] = b;
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
                    dst[num2++] = 0;
                }
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Channel GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
