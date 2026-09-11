using System.Collections.Generic;
using AdditionalLayerInfoParserAttributeNamespace;
using ExternalLinkedLayerReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using ExternalLinkedLayerNamespace;
using PropertyCollectionNamespace;

namespace ExternalLinkedLayerInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lnkE")]
    internal class ExternalLinkedLayerInfoParser : AdditionalLayerInfoSection
    {
        internal static ExternalLinkedLayerInfoParser s_ObfuscationSentinel;

        public ExternalLinkedLayerInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            List<ExternalLinkedLayer> list = new List<ExternalLinkedLayer>();
            while (reader.Position < GetSectionEndPosition())
            {
                ExternalLinkedLayerReader value2 = new ExternalLinkedLayerReader(reader);
                list.Add(value2.Value);
            }
            value["Items"] = list.ToArray();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ExternalLinkedLayerInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
