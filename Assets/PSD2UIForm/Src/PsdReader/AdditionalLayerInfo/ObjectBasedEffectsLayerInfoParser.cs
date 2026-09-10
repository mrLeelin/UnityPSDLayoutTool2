using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using AdditionalLayerInfoSectionNamespace;

namespace ObjectBasedEffectsLayerInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lfx2")]
    internal class ObjectBasedEffectsLayerInfoParser : AdditionalLayerInfoSection
    {
        internal static ObjectBasedEffectsLayerInfoParser s_ObfuscationSentinel;

        public ObjectBasedEffectsLayerInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            int num = reader.ReadInt32();
            PsdDescriptorReader value = new PsdDescriptorReader(reader, true);
            value["ResourceVersion"] = num;
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ObjectBasedEffectsLayerInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
