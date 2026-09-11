using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAnalysisResult
    {
        public bool Success;

        public string ProviderId;

        public string RawOutput;

        public string PatchJsonPath;

        public string ErrorMessage;

        private static AiAnalysisResult s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisResult GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
