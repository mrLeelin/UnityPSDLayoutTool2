using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderOfflineLeasePayloadDocument
    {
        public const int CurrentSchema = 1;

        public int Schema = 1;

        public string Kind = "OfflineLease";

        public int Revision;

        public string LookupId = string.Empty;

        public string LicenseId = string.Empty;

        public string VendorCode = string.Empty;

        public string ProductCode = string.Empty;

        public byte StatusCode;

        public string Status = string.Empty;

        public uint FeatureMask;

        public string[] Features = Array.Empty<string>();

        public long SupportUntilUtcTicks;

        public string SupportUntilUtc = string.Empty;

        public int OfflineCacheTimeoutDays = 2;

        public long IssuedUtcTicks;

        public string IssuedAtUtc = string.Empty;

        internal static PsdReaderOfflineLeasePayloadDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderOfflineLeasePayloadDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
