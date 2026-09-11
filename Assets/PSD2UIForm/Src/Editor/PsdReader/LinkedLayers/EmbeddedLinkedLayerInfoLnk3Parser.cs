using EmbeddedLinkedLayerInfoParserNamespace;
using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;

namespace EmbeddedLinkedLayerInfoLnk3ParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lnk3")]
    internal class EmbeddedLinkedLayerInfoLnk3Parser : EmbeddedLinkedLayerInfoParser
    {
        private static EmbeddedLinkedLayerInfoLnk3Parser s_ObfuscationSentinel;

        public EmbeddedLinkedLayerInfoLnk3Parser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EmbeddedLinkedLayerInfoLnk3Parser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
