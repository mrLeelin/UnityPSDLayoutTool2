using System.Runtime.CompilerServices;
using PsdReaderUpdateServiceNamespace;
using LicenseStatusPresenterNamespace;
using PsdReaderLicenseServiceNamespace;
using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;
using PsdReaderLicenseWindowNamespace;
using LicenseAvailabilityStateNamespace;

namespace PsdReaderProductAccessImplNamespace
{
    internal sealed class PsdReaderProductAccessImpl
    {
        private static PsdReaderProductAccessImpl s_ObfuscationSentinel;

        [SpecialName]
        public static bool IsSupported()
        {
            return true;
        }

        [SpecialName]
        public static bool IsAvailable()
        {
            return LicenseStatusPresenter.GetAvailabilityState(PsdReaderLicenseService.GetLicenseStatus()) == (LicenseAvailabilityState)0;
        }

        public static string GetStatusLabel()
        {
            return LicenseStatusPresenter.GetStatusLabel(PsdReaderLicenseService.GetLicenseStatus());
        }

        public static string GetOverviewMessage()
        {
            return LicenseStatusPresenter.GetActivationHelpText(PsdReaderLicenseService.GetLicenseStatus());
        }

        public static string GetUserFacingMessage()
        {
            return LicenseStatusPresenter.GetStatusMessage(PsdReaderLicenseService.GetLicenseStatus());
        }

        public static bool PrimeBuildProtection()
        {
            return PsdReaderLicenseService.HasMainFeature();
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
            return PsdReaderLicenseService.ActivateWithOrder(orderId, out message);
        }

        public static bool RefreshLicense(out string message)
        {
            return PsdReaderLicenseService.RefreshLicense(out message);
        }

        public static bool HasProjectLicenseFile()
        {
            return PsdReaderLicenseService.HasProjectLicenseFile();
        }

        public static string GetProjectLicenseHint()
        {
            return PsdReaderLicenseService.GetProjectLicenseHint();
        }

        public static bool ActivateFromProjectLicenseFile(out string message)
        {
            return PsdReaderLicenseService.ActivateFromProjectLicenseFile(out message);
        }

        public static bool CanExportProjectLicense()
        {
            return PsdReaderLicenseService.CanExportProjectLicense();
        }

        public static bool SaveProjectLicenseFile(int validityDays, out string message)
        {
            return PsdReaderLicenseService.SaveProjectLicenseFile(validityDays, out message);
        }

        public static void ClearLicenseData()
        {
            PsdReaderLicenseService.ClearLicenseData();
        }

        public static void OpenManagementWindow()
        {
            PsdReaderLicenseWindow.ShowWindow();
        }

        public static PsdReaderProductUpdateInfo CheckForUpdates()
        {
            return PsdReaderUpdateService.CheckForUpdates();
        }

        public static void CheckForUpdatesAndPrompt()
        {
            PsdReaderUpdateService.CheckForUpdatesAndPrompt();
        }

        public static void EnsureDailyUpdateCheck()
        {
            PsdReaderUpdateService.CheckForUpdatesDaily();
        }

        public static bool HasPendingUpdateTip()
        {
            return PsdReaderUpdateService.HasCachedUpdate();
        }

        public static string GetPendingUpdateTipMessage()
        {
            return PsdReaderUpdateService.GetCachedUpdateBadge();
        }

        public static bool TryOpenPendingUpdateDownloadUrl()
        {
            return PsdReaderUpdateService.OpenCachedUpdateDownload();
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
