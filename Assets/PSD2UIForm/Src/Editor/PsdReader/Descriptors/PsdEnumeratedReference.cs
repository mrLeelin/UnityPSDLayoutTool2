using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;

namespace PsdEnumeratedReferenceNamespace
{
    internal class PsdEnumeratedReference : PropertyCollection
    {
        private static PsdEnumeratedReference s_ObfuscationSentinel;

        public PsdEnumeratedReference()
            : base(4)
        {
        }

        public PsdEnumeratedReference(PsdBinaryReader psdBinaryReader)
        {
            Add("Name", psdBinaryReader.ReadUnicodeString());
            Add("ClassID", psdBinaryReader.ReadDescriptorId());
            Add("TypeID", psdBinaryReader.ReadDescriptorId());
            Add("EnumID", psdBinaryReader.ReadDescriptorId());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdEnumeratedReference GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
