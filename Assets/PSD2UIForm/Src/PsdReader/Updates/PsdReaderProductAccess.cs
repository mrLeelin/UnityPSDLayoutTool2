using PsdReaderProductAccessImplNamespace;

namespace cn.efunstudio.psdreader
{
    public sealed class PsdReaderProductAccess
    {
        internal static PsdReaderProductAccess s_ObfuscationSentinel;

        public static bool IsAvailable => PsdReaderProductAccessImpl.IsAvailable();

        public static bool NeedsAttention
        {
            get
            {
                if (PsdReaderProductAccessImpl.IsSupported())
                {
                    return !PsdReaderProductAccessImpl.IsAvailable();
                }
                return false;
            }
        }

        public static string GetStatusLabel()
        {
            return PsdReaderProductAccessImpl.GetStatusLabel();
        }

        public static string GetOverviewMessage()
        {
            return PsdReaderProductAccessImpl.GetOverviewMessage();
        }

        public static string GetUserFacingMessage()
        {
            return PsdReaderProductAccessImpl.GetUserFacingMessage();
        }

        public static bool PrimeBuildProtection()
        {
            return PsdReaderProductAccessImpl.PrimeBuildProtection();
        }

        public static void OpenManagementWindow()
        {
            PsdReaderProductAccessImpl.OpenManagementWindow();
        }

        public static PsdReaderProductUpdateInfo CheckForUpdates()
        {
            return PsdReaderProductAccessImpl.CheckForUpdates();
        }

        public static void CheckForUpdatesAndPrompt()
        {
            PsdReaderProductAccessImpl.CheckForUpdatesAndPrompt();
        }

        public static void EnsureDailyUpdateCheck()
        {
            PsdReaderProductAccessImpl.EnsureDailyUpdateCheck();
        }

        public static bool HasPendingUpdateTip()
        {
            return PsdReaderProductAccessImpl.HasPendingUpdateTip();
        }

        public static string GetPendingUpdateTipMessage()
        {
            return PsdReaderProductAccessImpl.GetPendingUpdateTipMessage();
        }

        public static bool TryOpenPendingUpdateDownloadUrl()
        {
            return PsdReaderProductAccessImpl.TryOpenPendingUpdateDownloadUrl();
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
