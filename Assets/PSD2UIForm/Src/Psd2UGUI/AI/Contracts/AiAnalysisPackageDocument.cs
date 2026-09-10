using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAnalysisPackageDocument
    {
        public string version;

        public string treeHash;

        public AiAnalysisDocumentInfo document;

        public AiAnalysisConfigInfo config;

        public List<AiAnalysisNodeEntry> nodes = new List<AiAnalysisNodeEntry>();

        internal static AiAnalysisPackageDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisPackageDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
