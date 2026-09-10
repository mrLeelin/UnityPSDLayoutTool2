using PsdSectionReaderNamespace;
using LayerAdditionalInfoReaderNamespace;
using PsdBinaryReaderNamespace;
using LayerBlendingRangesReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LayerMaskReaderNamespace;

namespace LayerExtraRecordsReaderNamespace
{
    internal class LayerExtraRecordsReader : PsdSectionReader<LayerRecords>
    {
        internal static LayerExtraRecordsReader s_ObfuscationSentinel;

        private LayerExtraRecordsReader(PsdBinaryReader value, LayerRecords value2)
            : base(value, true, (object)value2)
        {
        }

        public static LayerRecords ReadExtraRecords(object psdBinaryReader, object layerRecords)
        {
            return new LayerExtraRecordsReader((PsdBinaryReader)psdBinaryReader, (LayerRecords)layerRecords).Value;
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out LayerRecords result)
        {
            LayerRecords layerRecords = context as LayerRecords;
            LayerMask layerMask = LayerMaskReader.ReadLayerMask(reader);
            LayerBlendingRanges blendingRanges = LayerBlendingRangesReader.ReadBlendingRanges(reader);
            string name = reader.ReadPascalString(4);
            IProperties resources = new LayerAdditionalInfoReader(reader, GetSectionEndPosition() - reader.Position);
            layerRecords.SetExtraRecords(layerMask, blendingRanges, resources, name);
            result = layerRecords;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerExtraRecordsReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
