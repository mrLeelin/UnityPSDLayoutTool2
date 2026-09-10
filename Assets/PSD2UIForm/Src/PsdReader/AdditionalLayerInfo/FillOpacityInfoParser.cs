using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace FillOpacityInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("iOpa")]
    internal class FillOpacityInfoParser : AdditionalLayerInfoSection
    {
        private static FillOpacityInfoParser s_ObfuscationSentinel;

        public FillOpacityInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            value["Opacity"] = reader.ReadByte();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static FillOpacityInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
