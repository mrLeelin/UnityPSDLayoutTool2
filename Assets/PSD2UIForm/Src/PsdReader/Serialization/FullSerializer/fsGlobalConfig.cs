namespace cn.efunstudio.psdreader.FullSerializer
{
    public sealed class fsGlobalConfig
    {
        public static bool IsCaseSensitive = true;

        public static bool AllowInternalExceptions = true;

        public static string InternalFieldPrefix = "$";

        private static fsGlobalConfig s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsGlobalConfig GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
