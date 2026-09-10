using AdditionalLayerInfoParserAttributeNamespace;
using PlacedLayerDataParserNamespace;
using PsdBinaryReaderNamespace;

namespace SmartObjectLayerDataParserNamespace
{
    [AdditionalLayerInfoParserAttribute("SoLE")]
    internal class SmartObjectLayerDataParser : PlacedLayerDataParser
    {
        internal static SmartObjectLayerDataParser s_ObfuscationSentinel;

        public SmartObjectLayerDataParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static SmartObjectLayerDataParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
