namespace cn.efunstudio.psdreader
{
    public sealed class PsdReaderProductAccess
    {
        internal static PsdReaderProductAccess s_ObfuscationSentinel;

        public static bool IsAvailable => true;

        public static bool NeedsAttention => false;

        public static string GetStatusLabel()
        {
            return "完整版";
        }

        public static string GetOverviewMessage()
        {
            return "默认完全授权，无需激活。";
        }

        public static string GetUserFacingMessage()
        {
            return "默认完全授权，无需激活。";
        }

        public static bool PrimeBuildProtection()
        {
            return true;
        }

        public static void OpenManagementWindow()
        {
            UnityEngine.Debug.Log("Psd2UIForm：默认完全授权，无授权窗口。");
        }

        public static PsdReaderProductUpdateInfo CheckForUpdates()
        {
            return null;
        }

        public static void CheckForUpdatesAndPrompt()
        {
        }

        public static void EnsureDailyUpdateCheck()
        {
        }

        public static bool HasPendingUpdateTip()
        {
            return false;
        }

        public static string GetPendingUpdateTipMessage()
        {
            return string.Empty;
        }

        public static bool TryOpenPendingUpdateDownloadUrl()
        {
            return false;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderProductAccess GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}