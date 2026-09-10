using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using AdditionalLayerInfoSectionNamespace;

namespace SolidColorAdjustmentInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("SoCo")]
    internal class SolidColorAdjustmentInfoParser : AdditionalLayerInfoSection
    {
        internal static SolidColorAdjustmentInfoParser s_ObfuscationSentinel;

        public SolidColorAdjustmentInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            int num = reader.ReadInt32();
            PsdDescriptorReader value = new PsdDescriptorReader(reader, false);
            value["Version"] = num;
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static SolidColorAdjustmentInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
