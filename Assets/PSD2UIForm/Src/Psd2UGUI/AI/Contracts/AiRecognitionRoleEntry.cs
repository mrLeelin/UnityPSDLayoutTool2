using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionRoleEntry
    {
        public string ownerId;

        public string roleType;

        public string carrierNodeId;

        public string[] memberNodeIds;

        public float confidence;

        public string reason;

        private static AiRecognitionRoleEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionRoleEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
