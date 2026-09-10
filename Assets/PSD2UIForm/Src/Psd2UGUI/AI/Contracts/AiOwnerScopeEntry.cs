using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiOwnerScopeEntry
    {
        public string ownerId;

        public string ownerShortId;

        public string ownerType;

        public RectData rect;

        public string scopeImagePath;

        public string annotatedScopeImagePath;

        public string[] candidateNodeIds;

        private static AiOwnerScopeEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiOwnerScopeEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
