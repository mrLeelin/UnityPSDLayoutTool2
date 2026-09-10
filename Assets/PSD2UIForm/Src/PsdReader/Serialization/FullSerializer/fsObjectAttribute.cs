using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class fsObjectAttribute : Attribute
    {
        public Type[] PreviousModels;

        public string VersionString;

        public fsMemberSerialization MemberSerialization = fsMemberSerialization.Default;

        public Type Converter;

        public Type Processor;

        internal static fsObjectAttribute s_ObfuscationSentinel;

        public fsObjectAttribute()
        {
        }

        public fsObjectAttribute(string versionString, params Type[] previousModels)
        {
            VersionString = versionString;
            PreviousModels = previousModels;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsObjectAttribute GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
