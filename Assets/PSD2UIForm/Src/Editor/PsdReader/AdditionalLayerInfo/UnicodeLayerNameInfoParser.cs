using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace UnicodeLayerNameInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("luni")]
    internal class UnicodeLayerNameInfoParser : AdditionalLayerInfoSection
    {
        private static UnicodeLayerNameInfoParser s_ObfuscationSentinel;

        public UnicodeLayerNameInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            value["Name"] = reader.ReadUnicodeString();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static UnicodeLayerNameInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
