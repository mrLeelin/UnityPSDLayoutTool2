using UnityEngine;

namespace ReadOnlyFieldAttributeNamespace
{
    internal sealed class ReadOnlyFieldAttribute : PropertyAttribute
    {
        private static ReadOnlyFieldAttribute s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ReadOnlyFieldAttribute GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
