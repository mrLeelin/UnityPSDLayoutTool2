using System;

namespace cn.efunstudio.psdreader.PsdParser
{
    [Serializable]
    public sealed class PsdTextBevelInfo
    {
        private static PsdTextBevelInfo s_ObfuscationSentinel;

        public bool Enabled { get; set; }

        public bool Inner { get; set; }

        public float Depth { get; set; }

        public float Size { get; set; }

        public float Soften { get; set; }

        public bool UseGlobalAngle { get; set; }

        public float Angle { get; set; }

        public float Altitude { get; set; }

        public string StyleKey { get; set; }

        public PsdColor HighlightColor { get; set; }

        public float HighlightOpacity { get; set; }

        public PsdColor ShadowColor { get; set; }

        public float ShadowOpacity { get; set; }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextBevelInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
