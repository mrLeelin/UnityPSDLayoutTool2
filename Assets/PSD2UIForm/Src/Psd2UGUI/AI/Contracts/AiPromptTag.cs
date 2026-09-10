using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiPromptTag
    {
        public string key;

        public string value;

        private static AiPromptTag s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPromptTag GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
