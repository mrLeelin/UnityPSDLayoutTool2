using System;

namespace InvalidPsdFileExceptionNamespace
{
    internal class InvalidPsdFileException : Exception
    {
        private static InvalidPsdFileException s_ObfuscationSentinel;

        public InvalidPsdFileException()
            : base("Invalid PSD file")
        {
        }

        public InvalidPsdFileException(string text)
            : base(text)
        {
        }

        public InvalidPsdFileException(string text, params object[] args)
            : base(string.Format(text, args))
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static InvalidPsdFileException GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
