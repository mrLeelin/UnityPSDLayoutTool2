using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;

namespace GlobalLayerMaskInfoReaderNamespace
{
    internal class GlobalLayerMaskInfoReader : PsdSectionReader<object>
    {
        internal static GlobalLayerMaskInfoReader s_ObfuscationSentinel;

        public GlobalLayerMaskInfoReader(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader, true, (object)null)
        {
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out object result)
        {
            result = new object();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static GlobalLayerMaskInfoReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
