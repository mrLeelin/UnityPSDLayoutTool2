using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiProviderConfig
    {
        public AiProviderKind provider = AiProviderKind.CodexCli;

        public bool showCliWindow = true;

        public AiProviderConnectionSettings codexConnection = new AiProviderConnectionSettings();

        public AiProviderConnectionSettings claudeConnection = new AiProviderConnectionSettings();

        internal AiProviderConnectionSettings GetConnection(AiProviderKind kind)
        {
            if (kind == AiProviderKind.ClaudeCodeCli)
            {
                return claudeConnection ?? (claudeConnection = new AiProviderConnectionSettings());
            }
            return codexConnection ?? (codexConnection = new AiProviderConnectionSettings());
        }

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
