using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace LayerNameSourceInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lnsr")]
    internal class LayerNameSourceInfoParser : AdditionalLayerInfoSection
    {
        private static LayerNameSourceInfoParser s_ObfuscationSentinel;

        public LayerNameSourceInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            value["Name"] = reader.ReadAsciiString(4);
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerNameSourceInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
