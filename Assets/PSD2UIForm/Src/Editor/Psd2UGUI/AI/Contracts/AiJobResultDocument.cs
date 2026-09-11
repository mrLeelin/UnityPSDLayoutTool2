using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiJobResultDocument
    {
        public string jobId;

        public string providerId;

        public bool success;

        public string completedAtUtc;

        public long durationMs;

        public string rawOutputFile;

        public string patchFile;

        public string errorFile;

        private static AiJobResultDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiJobResultDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
