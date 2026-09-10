using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderProductMetaPayloadDocument
    {
        public const int CurrentSchema = 1;

        public int Schema = 1;

        public string VendorCode = string.Empty;

        public string ProductCode = string.Empty;

        public string DisplayName = string.Empty;

        public int CurrentMajorVersion;

        public uint FeatureCatalogMask;

        public string[] Features;

        public int LocalCacheTimeoutDays;

        public int SoftMaxMachines;

        public int Revision;

        public string ActiveKid;

        public string ActiveLeafPublicKeyPem;

        public long PublishedUtcTicks;

        public string PublishedAtUtc;

        public string[] RepositoryBaseUrls;

        public string TelemetryUrl;

        public string TelemetrySiteId;

        public byte[] StateSalt;

        internal static PsdReaderProductMetaPayloadDocument s_ObfuscationSentinel;

        public PsdReaderProductMetaPayloadDocument()
        {
            CurrentMajorVersion = PsdReaderVersion.CurrentMajor;
            Features = Array.Empty<string>();
            LocalCacheTimeoutDays = 2;
            SoftMaxMachines = 10;
            ActiveKid = string.Empty;
            ActiveLeafPublicKeyPem = string.Empty;
            PublishedAtUtc = string.Empty;
            RepositoryBaseUrls = Array.Empty<string>();
            TelemetryUrl = string.Empty;
            TelemetrySiteId = string.Empty;
            StateSalt = Array.Empty<byte>();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderProductMetaPayloadDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
