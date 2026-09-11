using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiRecognitionOwnerEntry
    {
        public string ownerId;

        public string ownerType;

        public string carrierNodeId;

        public string[] memberNodeIds;

        public float confidence;

        public string reason;

        internal static AiRecognitionOwnerEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiRecognitionOwnerEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
