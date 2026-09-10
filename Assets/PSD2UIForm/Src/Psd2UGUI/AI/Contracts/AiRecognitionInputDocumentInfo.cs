using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionInputDocumentInfo
    {
        public string previewImagePath;

        public string annotatedPreviewImagePath;

        public string nodePreviewDirectoryPath;

        public string nodeAtlasDirectoryPath;

        public int atlasPageCount;

        public int width;

        public int height;

        private static AiRecognitionInputDocumentInfo s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionInputDocumentInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
