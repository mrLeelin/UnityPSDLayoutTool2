using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace ReferencePointInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("fxrp")]
    internal class ReferencePointInfoParser : AdditionalLayerInfoSection
    {
        private static ReferencePointInfoParser s_ObfuscationSentinel;

        public ReferencePointInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            value["RefernecePoint"] = reader.ReadDoubles(2);
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ReferencePointInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
