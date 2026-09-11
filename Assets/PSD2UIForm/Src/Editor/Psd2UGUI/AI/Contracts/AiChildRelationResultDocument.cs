using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiChildRelationResultDocument
    {
        public string version;

        public string treeHash;

        public List<AiChildRelationEntry> relations = new List<AiChildRelationEntry>();

        private static AiChildRelationResultDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiChildRelationResultDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
