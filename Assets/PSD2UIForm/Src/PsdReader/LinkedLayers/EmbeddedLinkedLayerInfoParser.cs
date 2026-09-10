using System.Collections.Generic;
using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using EmbeddedLinkedLayerReaderNamespace;
using EmbeddedLinkedLayerNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace EmbeddedLinkedLayerInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lnkD")]
    internal class EmbeddedLinkedLayerInfoParser : AdditionalLayerInfoSection
    {
        private static EmbeddedLinkedLayerInfoParser s_ObfuscationSentinel;

        public EmbeddedLinkedLayerInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            List<EmbeddedLinkedLayer> list = new List<EmbeddedLinkedLayer>();
            while (reader.Position < GetSectionEndPosition())
            {
                EmbeddedLinkedLayerReader value2 = new EmbeddedLinkedLayerReader(reader);
                list.Add(value2.Value);
            }
            value["Items"] = list.ToArray();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EmbeddedLinkedLayerInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
