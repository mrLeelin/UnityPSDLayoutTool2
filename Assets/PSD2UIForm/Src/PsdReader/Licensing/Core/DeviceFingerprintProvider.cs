using UnityEngine;
using LicenseCryptoUtilityNamespace;

namespace DeviceFingerprintProviderNamespace
{
    internal sealed class DeviceFingerprintProvider
    {
        private static DeviceFingerprintProvider s_ObfuscationSentinel;

        internal void GetDeviceFingerprintHash(out string result)
        {
            result = LicenseCryptoUtility.ComputeDeviceIdHash(SystemInfo.deviceUniqueIdentifier);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static DeviceFingerprintProvider GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
