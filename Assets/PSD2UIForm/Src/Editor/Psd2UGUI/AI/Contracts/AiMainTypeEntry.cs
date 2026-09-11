using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiMainTypeEntry
    {
        public string targetId;

        public string currentUIType;

        public string predictedUIType;

        public float confidence;

        public string reason;

        private static AiMainTypeEntry s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiMainTypeEntry GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
