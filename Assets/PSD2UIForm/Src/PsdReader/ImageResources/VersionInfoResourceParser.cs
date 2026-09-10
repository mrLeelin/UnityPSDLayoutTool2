using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace VersionInfoResourceParserNamespace
{
    [AdditionalLayerInfoParserAttribute("1057", DisplayName = "Version")]
    internal class VersionInfoResourceParser : AdditionalLayerInfoSection
    {
        private static VersionInfoResourceParser s_ObfuscationSentinel;

        public VersionInfoResourceParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection(5);
            value["Version"] = reader.ReadInt32();
            value["HasCompatibilityImage"] = reader.ReadBoolean();
            value["WriterName"] = reader.ReadUnicodeString();
            value["ReaderName"] = reader.ReadUnicodeString();
            value["FileVersion"] = reader.ReadInt32();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static VersionInfoResourceParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
