using PsdBinaryReaderNamespace;
using PsdTypedValueListNamespace;

namespace PsdReferenceValueNamespace
{
    internal class PsdReferenceValue : PsdTypedValueList
    {
        private static PsdReferenceValue s_ObfuscationSentinel;

        public PsdReferenceValue(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReferenceValue GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
