using System;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class RectData
    {
        public float x;

        public float y;

        public float w;

        public float h;

        internal static RectData s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static RectData GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
