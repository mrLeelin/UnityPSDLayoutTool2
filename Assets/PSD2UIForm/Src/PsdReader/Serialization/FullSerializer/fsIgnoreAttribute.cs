using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class fsIgnoreAttribute : Attribute
    {
        internal static fsIgnoreAttribute s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsIgnoreAttribute GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
