using PsdBinaryReaderNamespace;
using LengthPrefixedPsdSectionNamespace;

namespace ColorModeDataSectionReaderNamespace
{
    internal class ColorModeDataSectionReader : LengthPrefixedPsdSection<byte[]>
    {
        internal static ColorModeDataSectionReader s_ObfuscationSentinel;

        public ColorModeDataSectionReader(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader, (object)null)
        {
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out byte[] result)
        {
            if (GetSectionLength() > 0L)
            {
                result = reader.ReadBytes((int)GetSectionLength());
            }
            else
            {
                result = new byte[0];
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ColorModeDataSectionReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
