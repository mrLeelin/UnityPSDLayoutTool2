using PsdPropertiesSectionNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;
using AdditionalLayerInfoParserRegistryNamespace;

namespace LayerAdditionalInfoReaderNamespace
{
    internal class LayerAdditionalInfoReader : PsdPropertiesSection
    {
        private static LayerAdditionalInfoReader s_ObfuscationSentinel;

        public LayerAdditionalInfoReader(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value, null)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            while (reader.Position < GetSectionEndPosition())
            {
                reader.ValidateImageResourceSignature();
                string text = reader.ReadSignature();
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

        internal static LayerAdditionalInfoReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
