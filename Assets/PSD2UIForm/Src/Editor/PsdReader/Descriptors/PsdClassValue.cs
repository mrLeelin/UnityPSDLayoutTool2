using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;

namespace PsdClassValueNamespace
{
    internal class PsdClassValue : PropertyCollection
    {
        internal static PsdClassValue s_ObfuscationSentinel;

        public PsdClassValue()
            : base(2)
        {
        }

        public PsdClassValue(PsdBinaryReader psdBinaryReader)
        {
            Add("Name", psdBinaryReader.ReadUnicodeString());
            Add("ClassID", psdBinaryReader.ReadDescriptorId());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdClassValue GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
