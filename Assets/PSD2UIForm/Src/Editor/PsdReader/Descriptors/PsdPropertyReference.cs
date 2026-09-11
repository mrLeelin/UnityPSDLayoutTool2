using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;

namespace PsdPropertyReferenceNamespace
{
    internal class PsdPropertyReference : PropertyCollection
    {
        internal static PsdPropertyReference s_ObfuscationSentinel;

        public PsdPropertyReference()
            : base(3)
        {
        }

        public PsdPropertyReference(PsdBinaryReader psdBinaryReader)
        {
            Add("Name", psdBinaryReader.ReadUnicodeString());
            Add("ClassID", psdBinaryReader.ReadDescriptorId());
            Add("KeyID", psdBinaryReader.ReadDescriptorId());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdPropertyReference GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
