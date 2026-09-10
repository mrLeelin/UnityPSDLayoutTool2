using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiProviderConfig
    {
        public AiProviderKind provider = AiProviderKind.CodexCli;

        public bool showCliWindow = true;

        private static AiProviderConfig s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiProviderConfig GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
