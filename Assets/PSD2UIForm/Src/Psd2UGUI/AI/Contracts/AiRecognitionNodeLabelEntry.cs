using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionNodeLabelEntry
    {
        public string nodeId;

        public string currentUIType;

        public string labelType;

        public float confidence;

        public string reason;

        private static AiRecognitionNodeLabelEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionNodeLabelEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
