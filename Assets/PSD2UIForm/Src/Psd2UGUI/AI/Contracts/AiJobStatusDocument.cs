using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiJobStatusDocument
    {
        public string jobId;

        public string providerId;

        public string state;

        public string updatedAtUtc;

        public string message;

        public string stage;

        public string detail;

        private static AiJobStatusDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiJobStatusDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
