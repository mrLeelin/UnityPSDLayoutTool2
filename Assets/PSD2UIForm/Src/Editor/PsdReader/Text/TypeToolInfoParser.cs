using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace TypeToolInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("TySh")]
    internal class TypeToolInfoParser : AdditionalLayerInfoSection
    {
        internal static TypeToolInfoParser s_ObfuscationSentinel;

        public TypeToolInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection(7);
            value["Version"] = reader.ReadInt16();
            value["Transforms"] = reader.ReadDoubles(6);
            value["TextVersion"] = reader.ReadInt16();
            value["Text"] = new PsdDescriptorReader(reader);
            value["WarpVersion"] = reader.ReadInt16();
            value["Warp"] = new PsdDescriptorReader(reader);
            value["Bounds"] = reader.ReadDoubles(2);
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static TypeToolInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
