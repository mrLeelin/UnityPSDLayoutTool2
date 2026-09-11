using System.Collections.Generic;
using AdditionalLayerInfoParserAttributeNamespace;
using InvalidPsdFileExceptionNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace GridAndGuidesResourceParserNamespace
{
    [AdditionalLayerInfoParserAttribute("1032", DisplayName = "GridAndGuides")]
    internal class GridAndGuidesResourceParser : AdditionalLayerInfoSection
    {
        internal static GridAndGuidesResourceParser s_ObfuscationSentinel;

        public GridAndGuidesResourceParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PropertyCollection value = new PropertyCollection();
            if (reader.ReadInt32() != 1)
            {
                throw new InvalidPsdFileException();
            }
            value["HorizontalGrid"] = reader.ReadInt32();
            value["VerticalGrid"] = reader.ReadInt32();
            int num = reader.ReadInt32();
            List<int> list = new List<int>();
            List<int> list2 = new List<int>();
            for (int i = 0; i < num; i++)
            {
                int item = reader.ReadInt32();
                if (reader.ReadByte() != 0)
                {
                    list.Add(item);
                }
                else
                {
                    list2.Add(item);
                }
            }
            value["HorizontalGuides"] = list.ToArray();
            value["VerticalGuides"] = list2.ToArray();
            result = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static GridAndGuidesResourceParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
