using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;

namespace LayerMaskReaderNamespace
{
    internal class LayerMaskReader : PsdSectionReader<LayerMask>
    {
        private static LayerMaskReader s_ObfuscationSentinel;

        private LayerMaskReader(PsdBinaryReader value)
            : base(value, true, (object)null)
        {
        }

        public static LayerMask ReadLayerMask(object psdBinaryReader)
        {
            return new LayerMaskReader((PsdBinaryReader)psdBinaryReader).Value;
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out LayerMask result)
        {
            LayerMask layerMask = new LayerMask();
            layerMask.Top = reader.ReadInt32();
            layerMask.Left = reader.ReadInt32();
            layerMask.Bottom = reader.ReadInt32();
            layerMask.Right = reader.ReadInt32();
            layerMask.Color = reader.ReadByte();
            layerMask.Flag = reader.ReadByte();
            result = layerMask;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerMaskReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
