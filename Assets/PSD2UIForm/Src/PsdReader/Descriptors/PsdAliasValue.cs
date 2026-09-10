using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;

namespace PsdAliasValueNamespace
{
    internal class PsdAliasValue : PropertyCollection
    {
        internal static PsdAliasValue s_ObfuscationSentinel;

        public PsdAliasValue(PsdBinaryReader psdBinaryReader)
        {
            int num = psdBinaryReader.ReadInt32();
            Add("Alias", psdBinaryReader.ReadAsciiString(num));
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdAliasValue GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
