using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class fsForwardAttribute : Attribute
    {
        public string MemberName;

        internal static fsForwardAttribute s_ObfuscationSentinel;

        public fsForwardAttribute(string memberName)
        {
            MemberName = memberName;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsForwardAttribute GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
