using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderDeviceShardPayloadDocument
    {
        public const int CurrentSchema = 1;

        public int Schema = 1;

        public string Kind = "DeviceShard";

        public string ProductCode = string.Empty;

        public string Prefix = string.Empty;

        public int Revision;

        public string[] BlockedTargets = Array.Empty<string>();

        public long IssuedUtcTicks;

        internal static PsdReaderDeviceShardPayloadDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderDeviceShardPayloadDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
