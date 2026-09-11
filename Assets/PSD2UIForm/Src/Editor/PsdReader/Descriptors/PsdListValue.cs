using PsdBinaryReaderNamespace;
using PsdTypedValueListNamespace;

namespace PsdListValueNamespace
{
    internal class PsdListValue : PsdTypedValueList
    {
        internal static PsdListValue s_ObfuscationSentinel;

        public PsdListValue(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdListValue GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
