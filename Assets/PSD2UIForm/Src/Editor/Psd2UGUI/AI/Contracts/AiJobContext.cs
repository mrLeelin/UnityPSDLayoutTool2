using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiJobContext
    {
        public string JobId;

        public string ProviderId;

        public string JobDirectory;

        public string RequestDirectory;

        public string ResponseDirectory;

        public string MetaPath;

        public string AnalysisPackagePath;

        public string RecognitionCombinedPath;

        public string RecognitionCombinedTempPath;

        public string MainTypePath;

        public string MainTypeTempPath;

        public string ChildRelationPath;

        public string ChildRelationTempPath;

        public string StructuralPath;

        public string StructuralTempPath;

        public string OwnerScopeManifestPath;

        public string RecognitionInputManifestPath;

        public string RecognitionNodeShardDirectory;

        public string PromptPath;

        public string StatusPath;

        public string ResultPath;

        public string PatchPath;

        public string PatchTempPath;

        public string RawOutputPath;

        public string CompletedPath;

        public string StreamOutputPath;

        public string VisibleCliCompletionPath;

        public string ErrorPath;

        public string DebugLogPath;

        public string DebugConsoleCloseSignalPath;

        internal static AiJobContext s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiJobContext GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
