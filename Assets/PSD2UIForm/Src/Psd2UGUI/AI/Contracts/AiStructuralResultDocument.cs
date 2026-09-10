using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiStructuralResultDocument
    {
        public string version;

        public string treeHash;

        public List<AiPatchOperation> operations = new List<AiPatchOperation>();

        internal static AiStructuralResultDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiStructuralResultDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
