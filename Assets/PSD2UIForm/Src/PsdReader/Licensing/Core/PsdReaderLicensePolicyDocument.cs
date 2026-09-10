using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderLicensePolicyDocument
    {
        public int LocalCacheTimeoutDays = 2;

        public int SoftMaxMachines = 10;

        internal static PsdReaderLicensePolicyDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderLicensePolicyDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
