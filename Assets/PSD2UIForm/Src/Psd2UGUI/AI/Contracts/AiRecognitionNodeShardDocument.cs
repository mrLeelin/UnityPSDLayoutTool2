using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionNodeShardDocument
    {
        public string version;

        public string treeHash;

        public int shardIndex;

        public int totalShards;

        public List<AiRecognitionInputNodeEntry> nodes = new List<AiRecognitionInputNodeEntry>();

        internal static AiRecognitionNodeShardDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionNodeShardDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
