using System;
using System.Linq;
using PsdBinaryReaderNamespace;
using PsdBinaryUtilityNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LengthPrefixedPsdSectionNamespace;

namespace ImageDataSectionReaderNamespace
{
    internal class ImageDataSectionReader : LengthPrefixedPsdSection<Channel[]>
    {
        private static ImageDataSectionReader s_ObfuscationSentinel;

        public ImageDataSectionReader(PsdBinaryReader psdBinaryReader, PsdDocument psdDocument)
            : base(psdBinaryReader, (object)psdDocument)
        {
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.GetStreamLength() - reader.Position;
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out Channel[] result)
        {
            result = ReadCompositeImageChannels(reader, context as PsdDocument);
        }

        private static Channel[] ReadCompositeImageChannels(object value, object value2)
        {
            int numberOfChannels = ((PsdDocument)value2).FileHeaderSection.NumberOfChannels;
            int width = ((PsdDocument)value2).Width;
            int height = ((PsdDocument)value2).Height;
            int depth = ((PsdDocument)value2).FileHeaderSection.Depth;
            CompressionType compressionType = (CompressionType)((PsdBinaryReader)value).ReadInt16();
            ChannelType[] array = new ChannelType[4]
            {
                ChannelType.Red,
                ChannelType.Green,
                ChannelType.Blue,
                ChannelType.Alpha
            };
            Channel[] array2 = new Channel[numberOfChannels];
            for (int i = 0; i < array2.Length; i++)
            {
                ChannelType type = ((i < array.Length) ? array[i] : ChannelType.Mask);
                array2[i] = new Channel(type, width, height, 0L);
            }
            switch (compressionType)
            {
            default:
                throw new NotSupportedException();
            case CompressionType.Zip:
            case CompressionType.ZipPrediction:
            {
                int num = PsdBinaryUtility.GetRowByteCount(depth, width);
                int num2 = num * height;
                int num3 = num2 * numberOfChannels;
                byte[] src = PsdBinaryUtility.InflateZipData(((PsdBinaryReader)value).ReadBytes(checked((int)(((PsdBinaryReader)value).GetStreamLength() - ((PsdBinaryReader)value).Position))), num3);
                for (int l = 0; l < array2.Length; l++)
                {
                    byte[] array3 = new byte[num2];
                    Buffer.BlockCopy(src, l * num2, array3, 0, num2);
                    if (compressionType == CompressionType.ZipPrediction)
                    {
                        PsdBinaryUtility.ApplyZipPrediction(array3, num, height, depth);
                    }
                    array2[l].SetData(array3, depth);
                }
                break;
            }
            case CompressionType.Raw:
            case CompressionType.RLE:
            {
                for (int j = 0; j < array2.Length; j++)
                {
                    array2[j].ReadHeader((PsdBinaryReader)value, compressionType);
                }
                for (int k = 0; k < array2.Length; k++)
                {
                    array2[k].Read((PsdBinaryReader)value, depth, compressionType);
                }
                break;
            }
            }
            if (array2.Length == 4)
            {
                for (int m = 0; m < array2[3].Data.Length; m++)
                {
                    float num4 = (float)(int)array2[3].Data[m] / 255f;
                    for (int n = 0; n < 3; n++)
                    {
                        float num5 = (float)(int)array2[n].Data[m] / 255f;
                        float num6 = (num4 + num5 - 1f) * 1f / num4;
                        array2[n].Data[m] = (byte)(num6 * 255f);
                    }
                }
            }
            return array2.OrderBy((Channel item) => item.Type).ToArray();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ImageDataSectionReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
