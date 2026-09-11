using System.Collections.Generic;
using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace MetadataSettingInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("shmd")]
    internal class MetadataSettingInfoParser : AdditionalLayerInfoSection
    {
        private static MetadataSettingInfoParser s_ObfuscationSentinel;

        public MetadataSettingInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            int num = reader.ReadInt32();
            List<PsdDescriptorReader> list = new List<PsdDescriptorReader>();
            for (int i = 0; i < num; i++)
            {
                reader.ReadAsciiString(4);
                reader.ReadAsciiString(4);
                reader.ReadByte();
                reader.ReadBytes(3);
                int num2 = reader.ReadInt32();
                long num3 = reader.Position;
                PsdDescriptorReader item = new PsdDescriptorReader(reader);
                list.Add(item);
                reader.Position = num3 + num2;
            }
            value["Items"] = list;
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static MetadataSettingInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
