using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAnalysisConfigInfo
    {
        public List<AiAnalysisUiTypeRuleInfo> uiTypeRules = new List<AiAnalysisUiTypeRuleInfo>();

        private static AiAnalysisConfigInfo s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisConfigInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
