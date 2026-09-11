using System;

namespace cn.efunstudio.psdreader.PsdParser
{
    [Serializable]
    public sealed class PsdTextGlowInfo
    {
        private static PsdTextGlowInfo s_ObfuscationSentinel;

        public bool Enabled { get; set; }

        public bool Inner { get; set; }

        public float Opacity { get; set; }

        public float Size { get; set; }

        public float Spread { get; set; }

        public PsdColor Color { get; set; }

        public string BlendModeKey { get; set; }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextGlowInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
