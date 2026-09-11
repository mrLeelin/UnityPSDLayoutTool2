using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionInputManifestDocument
    {
        public string version;

        public string treeHash;

        public AiRecognitionInputDocumentInfo document;

        public AiAnalysisConfigInfo config;

        public int nodeCount;

        public string[] nodeShardPaths;

        private static AiRecognitionInputManifestDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionInputManifestDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
