using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiProviderCapabilities
    {
        public bool SupportsImages;

        public bool SupportsStrictJson;

        public bool UsesVisibleCliExecution;

        internal static AiProviderCapabilities s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiProviderCapabilities GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
