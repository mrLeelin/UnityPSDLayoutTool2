using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderProductUpdatePayloadDocument
    {
        public int Schema = 1;

        public string VendorCode = string.Empty;

        public string ProductCode = string.Empty;

        public int Revision;

        public string Version = string.Empty;

        public string DownloadUrl = string.Empty;

        public string ReleaseNotes = string.Empty;

        public long PublishedUtcTicks;

        public string PublishedAtUtc = string.Empty;

        internal static PsdReaderProductUpdatePayloadDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderProductUpdatePayloadDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
