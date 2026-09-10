using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiChildRelationEntry
    {
        public string ownerId;

        public string targetId;

        public string roleType;

        public string[] memberNodeIds;

        public float confidence;

        public string reason;

        internal static AiChildRelationEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiChildRelationEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
