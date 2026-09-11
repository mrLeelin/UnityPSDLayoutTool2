using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiAtlasRef
    {
        public string page;

        public int cell;

        public string label;

        public RectData imageRect;

        public RectData labelRect;

        private static AiAtlasRef s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAtlasRef GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
