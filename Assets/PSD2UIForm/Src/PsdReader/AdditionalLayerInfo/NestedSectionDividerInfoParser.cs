using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace NestedSectionDividerInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lsdk")]
    internal class NestedSectionDividerInfoParser : AdditionalLayerInfoSection
    {
        internal static NestedSectionDividerInfoParser s_ObfuscationSentinel;

        public NestedSectionDividerInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            value["SectionType"] = (SectionType)reader.ReadInt32();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static NestedSectionDividerInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
