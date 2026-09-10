using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAnalysisUiTypeRuleInfo
    {
        public string uiType;

        public string uiTypeDesc;

        public string[] typeMatches;

        private static AiAnalysisUiTypeRuleInfo s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisUiTypeRuleInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
