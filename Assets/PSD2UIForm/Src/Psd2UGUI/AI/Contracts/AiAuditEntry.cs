using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAuditEntry
    {
        public string targetId;

        public string ownerId;

        public string semanticKind;

        public string currentUIType;

        public string predictedUIType;

        public string verdict;

        public float confidence;

        public string reason;

        internal static AiAuditEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAuditEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
