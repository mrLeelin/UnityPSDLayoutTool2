using System;

namespace cn.efunstudio.psdreader.PsdParser
{
    [Serializable]
    public sealed class PsdTextGradientStop
    {
        internal static PsdTextGradientStop s_ObfuscationSentinel;

        public float Location { get; set; }

        public PsdColor Color { get; set; }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextGradientStop GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
