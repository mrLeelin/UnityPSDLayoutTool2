using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiPatchOperation
    {
        public string op;

        public string id;

        public string targetId;

        public string parentId;

        public string newParentId;

        public int insertIndex = -1;

        public string name;

        public string uiType;

        public float confidence;

        public string reason;

        private static AiPatchOperation s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchOperation GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
