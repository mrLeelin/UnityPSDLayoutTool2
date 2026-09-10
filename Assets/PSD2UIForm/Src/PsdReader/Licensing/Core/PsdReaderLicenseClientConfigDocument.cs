using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderLicenseClientConfigDocument
    {
        public string VendorCode = string.Empty;

        public string ProductCode = string.Empty;

        public string[] RepositoryBaseUrls = Array.Empty<string>();

        public string MatomoUrl = string.Empty;

        public string MatomoSiteId = string.Empty;

        public int RequestTimeoutSeconds = 60;

        public int CurrentMajorVersion;

        private static PsdReaderLicenseClientConfigDocument s_ObfuscationSentinel;

        public PsdReaderLicenseClientConfigDocument()
        {
            CurrentMajorVersion = PsdReaderVersion.CurrentMajor;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderLicenseClientConfigDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
