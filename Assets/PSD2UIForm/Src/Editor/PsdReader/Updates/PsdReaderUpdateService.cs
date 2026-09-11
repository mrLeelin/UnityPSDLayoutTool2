using cn.efunstudio.psdreader;

namespace PsdReaderUpdateServiceNamespace
{
    internal sealed class PsdReaderUpdateService
    {
        internal static PsdReaderUpdateService s_ObfuscationSentinel;

        public static PsdReaderProductUpdateInfo CheckForUpdates()
        {
            return null;
        }

        public static void CheckForUpdatesAndPrompt()
        {
        }

        public static void CheckForUpdatesDaily()
        {
        }

        public static bool HasCachedUpdate()
        {
            return false;
        }

        public static string GetCachedUpdateBadge()
        {
            return string.Empty;
        }

        public static bool OpenCachedUpdateDownload()
        {
            return false;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderUpdateService GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}