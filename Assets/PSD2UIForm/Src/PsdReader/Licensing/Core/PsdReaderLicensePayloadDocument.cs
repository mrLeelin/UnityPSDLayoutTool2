using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderLicensePayloadDocument
    {
        public const int CurrentSchema = 1;

        public int Schema = 1;

        public int Revision;

        public string LookupId = string.Empty;

        public string LicenseId = string.Empty;

        public string VendorCode = string.Empty;

        public string ProductCode = string.Empty;

        public byte StatusCode;

        public string Status = string.Empty;

        public long IssuedUtcTicks;

        public string IssuedAtUtc = string.Empty;

        public long SupportUntilUtcTicks;

        public string SupportUntilUtc = string.Empty;

        public int MaxMajorVersion;

        public uint FeatureMask;

        public string[] Features = Array.Empty<string>();

        public byte[] GrantSeed = Array.Empty<byte>();

        public PsdReaderLicensePolicyDocument Policy = new PsdReaderLicensePolicyDocument();

        private static PsdReaderLicensePayloadDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderLicensePayloadDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
