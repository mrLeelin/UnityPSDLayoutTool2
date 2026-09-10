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
            return CreateProvider(((!((Object)value != (Object)null)) ? new AiProviderConfig() : ((UGUIParser)value).GetAiProviderConfig()).provider);
        }

        internal static IAiCliProvider CreateProvider(AiProviderKind aiProviderKind)
        {
            return aiProviderKind switch
            {
                AiProviderKind.CodexCli => new CodexCliProvider(), 
                AiProviderKind.ClaudeCodeCli => new ClaudeCodeCliProvider(), 
                AiProviderKind.OpenCodeCli => new OpenCodeCliProvider(), 
                _ => new CodexCliProvider(), 
            };
        }

        internal static IAiCliProvider CreateProviderById(object id)
        {
            switch (((string)(id ?? string.Empty)).Trim())
            {
            case "claude-code-cli":
            case "claude":
                return new ClaudeCodeCliProvider();
            default:
                return null;
            case "opencode-cli":
            case "opencode":
                return new OpenCodeCliProvider();
            case "codex-cli":
            case "codex":
                return new CodexCliProvider();
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
