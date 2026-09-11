using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace ResolutionInfoResourceParserNamespace
{
    [AdditionalLayerInfoParserAttribute("1005", DisplayName = "Resolution")]
    internal class ResolutionInfoResourceParser : AdditionalLayerInfoSection
    {
        private static ResolutionInfoResourceParser s_ObfuscationSentinel;

        public ResolutionInfoResourceParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection(6);
            value["HorizontalRes"] = reader.ReadInt16();
            value["HorizontalResUnit"] = reader.ReadInt32();
            value["WidthUnit"] = reader.ReadInt16();
            value["VerticalRes"] = reader.ReadInt16();
            value["VerticalResUnit"] = reader.ReadInt32();
            value["HeightUnit"] = reader.ReadInt16();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ResolutionInfoResourceParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
