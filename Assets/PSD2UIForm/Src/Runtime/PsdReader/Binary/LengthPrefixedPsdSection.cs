using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;

namespace LengthPrefixedPsdSectionNamespace
{
    internal abstract class LengthPrefixedPsdSection<TValue> : PsdSectionReader<TValue>
    {
        private static object s_ObfuscationSentinel;

        protected LengthPrefixedPsdSection(PsdBinaryReader psdBinaryReader, object value)
            : base(psdBinaryReader, true, value)
        {
        }

        protected LengthPrefixedPsdSection(PsdBinaryReader psdBinaryReader, long value, object value2)
            : base(psdBinaryReader, value, value2)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static object GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
