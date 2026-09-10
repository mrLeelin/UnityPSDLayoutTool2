using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;

namespace LayerBlendingRangesReaderNamespace
{
    internal class LayerBlendingRangesReader : PsdSectionReader<LayerBlendingRanges>
    {
        internal static LayerBlendingRangesReader s_ObfuscationSentinel;

        private LayerBlendingRangesReader(PsdBinaryReader value)
            : base(value, true, (object)null)
        {
        }

        public static LayerBlendingRanges ReadBlendingRanges(object psdBinaryReader)
        {
            return new LayerBlendingRangesReader((PsdBinaryReader)psdBinaryReader).Value;
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out LayerBlendingRanges result)
        {
            result = new LayerBlendingRanges();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerBlendingRangesReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
