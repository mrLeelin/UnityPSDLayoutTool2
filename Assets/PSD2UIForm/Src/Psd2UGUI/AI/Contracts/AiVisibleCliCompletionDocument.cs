using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiVisibleCliCompletionDocument
    {
        public int exitCode;

        public string completedAtUtc;

        public string rawOutputPath;

        public string streamOutputPath;

        private static AiVisibleCliCompletionDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiVisibleCliCompletionDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
