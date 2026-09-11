using PsdPropertiesSectionNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;
using AdditionalLayerInfoParserRegistryNamespace;

namespace ImageResourcesSectionReaderNamespace
{
    internal class ImageResourcesSectionReader : PsdPropertiesSection
    {
        private static ImageResourcesSectionReader s_ObfuscationSentinel;

        public ImageResourcesSectionReader(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader, null)
        {
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            while (reader.Position < GetSectionEndPosition())
            {
                reader.ValidateImageResourceSignature();
                string text = reader.ReadInt16().ToString();
                reader.ReadPascalString(2);
                long num = reader.ReadInt32();
                num += num % 2L;
                AdditionalLayerInfoSection value2 = AdditionalLayerInfoParserRegistry.CreateParser(text, reader, num);
                string text2 = AdditionalLayerInfoParserRegistry.GetParserDisplayNameById(text);
                value[text2] = value2;
            }
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ImageResourcesSectionReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
