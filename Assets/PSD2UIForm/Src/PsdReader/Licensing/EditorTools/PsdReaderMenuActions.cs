using PsdReaderLicenseWindowNamespace;

namespace cn.efunstudio.psdreader
{
    public static class PsdReaderMenuActions
    {
        public const string ForceResetMenuPath = "Window/PSDReader/Force Reset";

        public const string LicenseWindowMenuPath = "Tools/Psd2UIForm/Other/LicenseWindow";

        public const string ClearLicenseMenuPath = "Tools/Psd2UIForm/Other/Clear License";

        public const string CheckUpdateMenuPath = "Tools/Psd2UIForm/Check Update";

        public const int LicenseWindowPriority = 9999;

        public const int ClearLicensePriority = 10000;

        public const int CheckUpdatePriority = 10001;

        public static void ForceReset()
        {
            EditorCoroutineRunner.KillAllCoroutines();
        }

        public static void OpenLicenseWindow()
        {
            PsdReaderLicenseWindow.OpenLicenseManager();
        }

        public static void ClearLicense()
        {
            PsdReaderLicenseWindow.ClearLicenseDataWithConfirmation();
        }

        public static void CheckUpdate()
        {
            PsdReaderLicenseWindow.CheckForUpdates();
        }
    }
}
