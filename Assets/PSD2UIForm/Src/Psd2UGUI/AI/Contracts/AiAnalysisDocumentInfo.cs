using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAnalysisDocumentInfo
    {
        public string previewImagePath;

        public string annotatedPreviewImagePath;

        public string nodePreviewDirectoryPath;

        public string nodeAtlasDirectoryPath;

        public List<string> visualInputPaths = new List<string>();

        public int width;

        public int height;

        internal static AiAnalysisDocumentInfo s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisDocumentInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
