using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;

namespace PsdOffsetReferenceNamespace
{
    internal class PsdOffsetReference : PropertyCollection
    {
        private static PsdOffsetReference s_ObfuscationSentinel;

        public PsdOffsetReference()
            : base(4)
        {
        }

        public PsdOffsetReference(PsdBinaryReader psdBinaryReader)
        {
            Add("Name", psdBinaryReader.ReadUnicodeString());
            Add("ClassID", psdBinaryReader.ReadDescriptorId());
            Add("Offset", psdBinaryReader.ReadInt32());
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdOffsetReference GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
