using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdReaderProductAccessImplNamespace
{
    internal sealed class PsdReaderProductAccessImpl
    {
        private static PsdReaderProductAccessImpl s_ObfuscationSentinel;

        public static bool IsSupported()
        {
            return true;
        }

        public static bool IsAvailable()
        {
            return true;
        }

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

        public static string GetPreviewProtectionFingerprint(object psdLayer)
        {
            if (psdLayer != null)
            {
                return ((PsdLayer)psdLayer).GetPreviewProtectionFingerprint();
            }
            return string.Empty;
        }

        public static bool ActivateWithOrder(object orderId, out string message)
        {
            message = "默认完全授权，无需激活。";
            return true;
        }

        public static bool RefreshLicense(out string message)
        {
            message = "默认完全授权。";
            return true;
        }

        public static bool HasProjectLicenseFile()
        {
            return false;
        }

        public static string GetProjectLicenseHint()
        {
            return string.Empty;
        }

        public static bool ActivateFromProjectLicenseFile(out string message)
        {
            message = "默认完全授权，无需授权文件。";
            return true;
        }

        public static bool CanExportProjectLicense()
        {
            return false;
        }

        public static bool SaveProjectLicenseFile(int validityDays, out string message)
        {
            message = "默认完全授权，无需导出授权文件。";
            return false;
        }

        public static void ClearLicenseData()
        {
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

        internal static PsdReaderProductAccessImpl GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}