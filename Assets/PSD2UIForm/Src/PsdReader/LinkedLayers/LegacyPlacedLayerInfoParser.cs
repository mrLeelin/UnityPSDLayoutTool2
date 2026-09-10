using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace LegacyPlacedLayerInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("PlLd")]
    internal class LegacyPlacedLayerInfoParser : AdditionalLayerInfoSection
    {
        internal static LegacyPlacedLayerInfoParser s_ObfuscationSentinel;

        public LegacyPlacedLayerInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            reader.ValidateSignature("plcL", "LayerResource PlLd");
            value["Version"] = reader.ReadInt32();
            value["UniqueID"] = reader.ReadPascalString(1);
            value["PageNumbers"] = reader.ReadInt32();
            value["Pages"] = reader.ReadInt32();
            value["AntiAlias"] = reader.ReadInt32();
            value["LayerType"] = reader.ReadInt32();
            value["Transformation"] = reader.ReadDoubles(8);
            value["WarpVersion"] = reader.ReadInt32();
            value["Warp"] = new PsdDescriptorReader(reader);
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LegacyPlacedLayerInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
