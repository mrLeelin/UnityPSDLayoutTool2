using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class fsPropertyAttribute : Attribute
    {
        public string Name;

        public Type Converter;

        internal static fsPropertyAttribute s_ObfuscationSentinel;

        public fsPropertyAttribute()
            : this(string.Empty)
        {
        }

        public fsPropertyAttribute(string name)
        {
            Name = name;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsPropertyAttribute GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
