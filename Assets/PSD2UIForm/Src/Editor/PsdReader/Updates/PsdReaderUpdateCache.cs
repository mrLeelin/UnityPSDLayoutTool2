using System;
using UnityEngine.Serialization;

namespace PsdReaderUpdateCacheNamespace
{
    [Serializable]
    internal sealed class PsdReaderUpdateCache
    {
        [FormerlySerializedAs("y0vS0UN2s7")]
        public int SchemaVersion = 1;

        [FormerlySerializedAs("b8OS7HDWAC")]
        public long LastCheckUtcTicks;

        [FormerlySerializedAs("LttSsPYkfZ")]
        public string LatestVersion = string.Empty;

        [FormerlySerializedAs("aZdSLUm3KO")]
        public string DownloadUrl = string.Empty;

        [FormerlySerializedAs("p5tSjDMXNy")]
        public string ReleaseNotes = string.Empty;

        internal static PsdReaderUpdateCache s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderUpdateCache GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
