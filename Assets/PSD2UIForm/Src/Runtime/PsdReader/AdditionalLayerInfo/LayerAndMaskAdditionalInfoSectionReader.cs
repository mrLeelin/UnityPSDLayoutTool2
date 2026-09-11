using System.Linq;
using PsdPropertiesSectionNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;
using LayerInfoSectionReaderNamespace;
using AdditionalLayerInfoParserRegistryNamespace;

namespace LayerAndMaskAdditionalInfoSectionReaderNamespace
{
    internal class LayerAndMaskAdditionalInfoSectionReader : PsdPropertiesSection
    {
        private static string[] s_LargeLengthKeys = new string[17]
        {
            "LMsk", "Lr16", "Lr32", "Layr", "Mt16", "Mt32", "Mtrn", "Alph", "FMsk", "lnk2",
            "lnk3", "lnkD", "FEid", "FXid", "PxSD", "lnkE", "extd"
        };

        private static LayerAndMaskAdditionalInfoSectionReader s_ObfuscationSentinel;

        public LayerAndMaskAdditionalInfoSectionReader(PsdBinaryReader psdBinaryReader, long value, PsdDocument psdDocument)
            : base(psdBinaryReader, value, psdDocument)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            PsdDocument psdDocument = context as PsdDocument;
            PropertyCollection value = new PropertyCollection();
            while (reader.Position < GetSectionEndPosition())
            {
                reader.ValidateImageResourceSignature(true);
                string text = reader.ReadSignature();
                long num = ReadPaddedBlockLength(reader, text);
                long num2 = reader.Position + num;
                string text2 = AdditionalLayerInfoParserRegistry.GetParserDisplayNameById(text);
                if ((text == "Lr16" || text == "Lr32") && psdDocument != null)
                {
                    PropertyCollection value2 = new PropertyCollection(1);
                    value2["Layers"] = LayerInfoSectionReader.ReadLayers(reader, psdDocument);
                    value[text2] = value2;
                    reader.Position = num2;
                }
                else
                {
                    AdditionalLayerInfoSection value3 = AdditionalLayerInfoParserRegistry.CreateParser(text, reader, num);
                    value[text2] = value3;
                }
            }
            result = value;
        }

        private long ReadPaddedBlockLength(PsdBinaryReader value, string text)
        {
            long num = ((!s_LargeLengthKeys.Contains(text) || value.Version != 2) ? value.ReadInt32() : value.ReadInt64());
            return (num + 3L) & -4L;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerAndMaskAdditionalInfoSectionReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
