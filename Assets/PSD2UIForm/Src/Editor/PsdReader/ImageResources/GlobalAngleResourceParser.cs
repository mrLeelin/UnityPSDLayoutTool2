using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace GlobalAngleResourceParserNamespace
{
    [AdditionalLayerInfoParserAttribute("1037", DisplayName = "GlobalAngle")]
    internal class GlobalAngleResourceParser : AdditionalLayerInfoSection
    {
        internal static GlobalAngleResourceParser s_ObfuscationSentinel;

        public GlobalAngleResourceParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection(1);
            value["Angle"] = reader.ReadInt32();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static GlobalAngleResourceParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
