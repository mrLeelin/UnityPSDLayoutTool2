using PropertyCollectionNamespace;

namespace PsdStringValueNamespace
{
    internal class PsdStringValue : PropertyCollection
    {
        private static PsdStringValue s_ObfuscationSentinel;

        public PsdStringValue(string text)
        {
            Add("Value", text);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdStringValue GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
