using System;
using System.Threading;
using AiResultDocumentKindNamespace;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAnalysisRequest
    {
        public string StageName;

        public string StageDisplayName;

        public AiResultDocumentKind ResultDocumentKind;

        public string AnalysisPackageJsonPath;

        public string PromptTemplatePath;

        public AiPromptTag[] PromptTags;

        public string OutputJsonPath;

        public string OutputTempJsonPath;

        public string OutputRawTextPath;

        public string OutputCompletedPath;

        public string StreamOutputPath;

        public string VisibleCliCompletionPath;

        public string WorkingDirectory;

        public string[] ImageInputPaths;

        public bool AllowVisibleCliExecution;

        public bool RequireExplicitOutputJsonFile;

        public bool DisableOutputRecovery;

        public CancellationToken CancellationToken;

        internal static AiAnalysisRequest s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisRequest GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
