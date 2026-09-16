using IAiCliProviderNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using CodexCliProviderNamespace;
using ClaudeCodeCliProviderNamespace;
using OpenCodeCliProviderNamespace;

namespace AiCliProviderFactoryNamespace
{
    internal sealed class AiCliProviderFactory
    {
        internal static AiCliProviderFactory s_ObfuscationSentinel;

        internal static IAiCliProvider GetConfiguredProvider(object value)
        {
            AiProviderConfig config = (!((Object)value != (Object)null)) ? new AiProviderConfig() : ((UGUIParser)value).GetAiProviderConfig();
            return CreateProvider(config.provider, config);
        }

        internal static IAiCliProvider CreateProvider(AiProviderKind aiProviderKind)
        {
            return CreateProvider(aiProviderKind, new AiProviderConfig());
        }

        private static IAiCliProvider CreateProvider(AiProviderKind aiProviderKind, AiProviderConfig config)
        {
            AiProviderConnectionSettings connection = (config ?? new AiProviderConfig()).GetConnection(aiProviderKind);
            return aiProviderKind switch
            {
                AiProviderKind.CodexCli => new CodexCliProvider(connection),
                AiProviderKind.ClaudeCodeCli => new ClaudeCodeCliProvider(connection),
                AiProviderKind.OpenCodeCli => new OpenCodeCliProvider(),
                _ => new CodexCliProvider(connection),
            };
        }

        internal static IAiCliProvider CreateProviderById(object id)
        {
            return CreateProviderById(id, new AiProviderConfig());
        }

        internal static IAiCliProvider CreateProviderById(object id, AiProviderConfig config)
        {
            switch (((string)(id ?? string.Empty)).Trim())
            {
            case "claude-code-cli":
            case "claude":
                return CreateProvider(AiProviderKind.ClaudeCodeCli, config);
            default:
                return null;
            case "opencode-cli":
            case "opencode":
                return new OpenCodeCliProvider();
            case "codex-cli":
            case "codex":
                return CreateProvider(AiProviderKind.CodexCli, config);
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiCliProviderFactory GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
