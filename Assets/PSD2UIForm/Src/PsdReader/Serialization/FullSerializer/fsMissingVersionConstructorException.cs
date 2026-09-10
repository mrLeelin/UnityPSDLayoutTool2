using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public sealed class fsMissingVersionConstructorException : Exception
    {
        private static fsMissingVersionConstructorException s_ObfuscationSentinel;

        public fsMissingVersionConstructorException(Type versionedType, Type constructorType)
            : base(versionedType?.ToString() + " is missing a constructor for previous model type " + constructorType)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsMissingVersionConstructorException GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
