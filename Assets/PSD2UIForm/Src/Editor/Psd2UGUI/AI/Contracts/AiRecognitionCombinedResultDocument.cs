using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionCombinedResultDocument
    {
        public string version;

        public string treeHash;

        public List<AiRecognitionOwnerEntry> owners = new List<AiRecognitionOwnerEntry>();

        public List<AiRecognitionRoleEntry> roles = new List<AiRecognitionRoleEntry>();

        public List<AiRecognitionNodeLabelEntry> nodeLabels = new List<AiRecognitionNodeLabelEntry>();

        private static AiRecognitionCombinedResultDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionCombinedResultDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
