using System;

namespace cn.efunstudio.psdreader.PsdParser
{
    [Serializable]
    public sealed class PsdTextShadowInfo
    {
        internal static PsdTextShadowInfo s_ObfuscationSentinel;

        public bool Enabled { get; set; }

        public bool UseGlobalAngle { get; set; }

        public float Opacity { get; set; }

        public float Angle { get; set; }

        public float Distance { get; set; }

        public float Spread { get; set; }

        public float Blur { get; set; }

        public PsdColor Color { get; set; }

        public string BlendModeKey { get; set; }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextShadowInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
