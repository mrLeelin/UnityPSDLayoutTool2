using System;
using LicenseResultCodeNamespace;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderLicenseCacheDocument
    {
        public const int CurrentSchema = 1;

        public int Schema = 1;

        public string LookupId = string.Empty;

        public string LicenseId = string.Empty;

        public byte StatusCode;

        public string StatusText = string.Empty;

        public string Message = string.Empty;

        public string[] Features = Array.Empty<string>();

        public uint FeatureMask;

        public uint CatalogMask;

        public string LastVerifiedUtc = string.Empty;

        public string SupportUntilUtc = string.Empty;

        public string ClockHighWaterUtc = string.Empty;

        public string ClockWatermarkCipher = string.Empty;

        public string ValidationTranscriptHead = string.Empty;

        public int Revision;

        public int LocalCacheTimeoutDays = 2;

        public int CurrentMajorVersion;

        public int MaxMajorVersion;

        public string DeviceFingerprint;

        public int MetaRevision;

        public string ActiveKid;

        public LicenseResultCode LastResultCode;

        public string LicenseAccessKeyCipher;

        public string OrderIdCipher;

        public string GrantSeedCipher;

        public string StateSaltHex;

        public string TelemetryUrl;

        public string TelemetrySiteId;

        public string ActivationSource;

        public string LastRepositoryRequestUtc;

        public string MetaEnvelopeBase64;

        public string LicenseEnvelopeBase64;

        public string OfflineLeaseEnvelopeBase64;

        public string DeviceShardEnvelopeBase64;

        private static PsdReaderLicenseCacheDocument s_ObfuscationSentinel;

        public PsdReaderLicenseCacheDocument()
        {
            CurrentMajorVersion = PsdReaderVersion.CurrentMajor;
            MaxMajorVersion = PsdReaderVersion.CurrentMajor;
            DeviceFingerprint = string.Empty;
            ActiveKid = string.Empty;
            LicenseAccessKeyCipher = string.Empty;
            OrderIdCipher = string.Empty;
            GrantSeedCipher = string.Empty;
            StateSaltHex = string.Empty;
            TelemetryUrl = string.Empty;
            TelemetrySiteId = string.Empty;
            ActivationSource = string.Empty;
            LastRepositoryRequestUtc = string.Empty;
            MetaEnvelopeBase64 = string.Empty;
            LicenseEnvelopeBase64 = string.Empty;
            OfflineLeaseEnvelopeBase64 = string.Empty;
            DeviceShardEnvelopeBase64 = string.Empty;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderLicenseCacheDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
