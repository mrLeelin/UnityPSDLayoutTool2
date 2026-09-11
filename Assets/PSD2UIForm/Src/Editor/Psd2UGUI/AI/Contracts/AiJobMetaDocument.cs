using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiJobMetaDocument
    {
        public string jobId;

        public string providerId;

        public string createdAtUtc;

        public string projectPath;

        public string psdAssetPath;

        public string converterPath;

        public string analysisPackageVersion;

        public string recognitionCombinedVersion;

        public string mainTypeVersion;

        public string childRelationVersion;

        public string structuralVersion;

        public string patchVersion;

        public string treeHash;

        public string previewHash;

        internal static AiJobMetaDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiJobMetaDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
