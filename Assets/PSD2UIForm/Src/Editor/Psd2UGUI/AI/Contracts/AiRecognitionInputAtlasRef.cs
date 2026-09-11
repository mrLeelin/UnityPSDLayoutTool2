using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionInputAtlasRef
    {
        public string page;

        public int cell;

        public string label;

        internal static AiRecognitionInputAtlasRef s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionInputAtlasRef GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
