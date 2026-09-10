using GlobalLayerMaskInfoReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LayerAndMaskSectionDataNamespace;
using LayerAndMaskAdditionalInfoSectionReaderNamespace;
using PropertyCollectionNamespace;
using LayerInfoSectionReaderNamespace;
using LengthPrefixedPsdSectionNamespace;

namespace LayerAndMaskSectionReaderNamespace
{
    internal class LayerAndMaskSectionReader : LengthPrefixedPsdSection<LayerAndMaskSectionData>
    {
        internal static LayerAndMaskSectionReader s_ObfuscationSentinel;

        public LayerAndMaskSectionReader(PsdBinaryReader psdBinaryReader, PsdDocument psdDocument)
            : base(psdBinaryReader, (object)psdDocument)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out LayerAndMaskSectionData result)
        {
            PsdDocument psdDocument = context as PsdDocument;
            LayerInfoSectionReader value = new LayerInfoSectionReader(reader, psdDocument);
            if (reader.Position + 4L < GetSectionEndPosition())
            {
                GlobalLayerMaskInfoReader value2 = new GlobalLayerMaskInfoReader(reader);
                LayerAndMaskAdditionalInfoSectionReader value3 = new LayerAndMaskAdditionalInfoSectionReader(reader, GetSectionEndPosition() - reader.Position, psdDocument);
                result = new LayerAndMaskSectionData(value, value2, value3);
            }
            else
            {
                result = new LayerAndMaskSectionData(value, null, new PropertyCollection());
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerAndMaskSectionReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
