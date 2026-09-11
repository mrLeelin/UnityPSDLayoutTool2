using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;

namespace PsdEnumValueNamespace
{
    internal class PsdEnumValue : PropertyCollection
    {
        internal static PsdEnumValue s_ObfuscationSentinel;

        public PsdEnumValue()
            : base(2)
        {
        }

        public PsdEnumValue(PsdBinaryReader psdBinaryReader)
        {
            Add("Type", psdBinaryReader.ReadDescriptorId());
            Add("Value", psdBinaryReader.ReadDescriptorId());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdEnumValue GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
