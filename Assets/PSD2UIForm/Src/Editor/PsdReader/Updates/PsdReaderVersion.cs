using System;

namespace cn.efunstudio.psdreader
{
    public sealed class PsdReaderVersion
    {
        public const string Current = "3.0.0";

        private static PsdReaderVersion s_ObfuscationSentinel;

        public static Version CurrentValue { get; } = new Version("3.0.0");

        public static int CurrentMajor => CurrentValue.Major;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderVersion GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
