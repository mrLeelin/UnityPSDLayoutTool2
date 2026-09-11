using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;

namespace LayerRecordReaderNamespace
{
    internal class LayerRecordReader : PsdSectionReader<LayerRecords>
    {
        private static LayerRecordReader s_ObfuscationSentinel;

        private LayerRecordReader(PsdBinaryReader value)
            : base(value, false, (object)null)
        {
        }

        public static LayerRecords ReadLayerRecords(object psdBinaryReader)
        {
            return new LayerRecordReader((PsdBinaryReader)psdBinaryReader).Value;
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out LayerRecords result)
        {
            LayerRecords layerRecords = new LayerRecords();
            layerRecords.Top = reader.ReadInt32();
            layerRecords.Left = reader.ReadInt32();
            layerRecords.Bottom = reader.ReadInt32();
            layerRecords.Right = reader.ReadInt32();
            layerRecords.ValidateSize();
            int num = (layerRecords.ChannelCount = reader.ReadUInt16());
            for (int i = 0; i < num; i++)
            {
                layerRecords.Channels[i].Type = reader.ReadChannelType();
                layerRecords.Channels[i].Size = reader.ReadVersionedLength();
                layerRecords.Channels[i].Width = layerRecords.Width;
                layerRecords.Channels[i].Height = layerRecords.Height;
            }
            reader.ValidateImageResourceSignature();
            layerRecords.BlendMode = reader.ReadBlendMode();
            layerRecords.Opacity = reader.ReadByte();
            layerRecords.Clipping = reader.ReadBoolean();
            layerRecords.Flags = reader.ReadLayerFlags();
            layerRecords.Filter = reader.ReadByte();
            result = layerRecords;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerRecordReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
