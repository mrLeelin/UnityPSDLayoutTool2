using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiOwnerScopeManifestDocument
    {
        public string version;

        public string treeHash;

        public List<AiOwnerScopeEntry> scopes = new List<AiOwnerScopeEntry>();

        private static AiOwnerScopeManifestDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiOwnerScopeManifestDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
