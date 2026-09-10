using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderProjectLicenseBundleDocument
    {
        public const int CurrentSchema = 2;

        public int Schema = 2;

        public string VendorCode = string.Empty;

        public string ProductCode = string.Empty;

        public string LookupId = string.Empty;

        public string LicenseId = string.Empty;

        public string MetaEnvelopeBase64 = string.Empty;

        public string LicenseEnvelopeBase64 = string.Empty;

        public string OfflineLeaseEnvelopeBase64 = string.Empty;

        public string WrappedLicenseAccessKey = string.Empty;

        public string WrappedOrderIdCipher = string.Empty;

        public long ExportIssuedUtcTicks;

        public string ExportIssuedUtc = string.Empty;

        public long ExportExpiresUtcTicks;

        public string ExportExpiresUtc = string.Empty;

        public string BundleSeal = string.Empty;

        private static PsdReaderProjectLicenseBundleDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderProjectLicenseBundleDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
