using EmbeddedLinkedLayerInfoParserNamespace;
using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;

namespace EmbeddedLinkedLayerInfoLnk2ParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lnk2")]
    internal class EmbeddedLinkedLayerInfoLnk2Parser : EmbeddedLinkedLayerInfoParser
    {
        private static EmbeddedLinkedLayerInfoLnk2Parser s_ObfuscationSentinel;

        public EmbeddedLinkedLayerInfoLnk2Parser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EmbeddedLinkedLayerInfoLnk2Parser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
