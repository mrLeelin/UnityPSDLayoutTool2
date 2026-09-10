using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public sealed class fsDuplicateVersionNameException : Exception
    {
        internal static fsDuplicateVersionNameException s_ObfuscationSentinel;

        public fsDuplicateVersionNameException(Type typeA, Type typeB, string version)
            : base(typeA?.ToString() + " and " + typeB?.ToString() + " have the same version string (" + version + "); please change one of them.")
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsDuplicateVersionNameException GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
