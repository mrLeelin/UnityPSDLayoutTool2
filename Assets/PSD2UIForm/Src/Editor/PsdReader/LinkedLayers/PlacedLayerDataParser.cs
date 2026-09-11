using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using AdditionalLayerInfoSectionNamespace;

namespace PlacedLayerDataParserNamespace
{
    [AdditionalLayerInfoParserAttribute("SoLd")]
    internal class PlacedLayerDataParser : AdditionalLayerInfoSection
    {
        private static PlacedLayerDataParser s_ObfuscationSentinel;

        public PlacedLayerDataParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            reader.ValidateSignature("soLD", "SoLd ID");
            int num = reader.ReadInt32();
            PsdDescriptorReader value = new PsdDescriptorReader(reader, true);
            value["ResourceVersion"] = num;
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PlacedLayerDataParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
