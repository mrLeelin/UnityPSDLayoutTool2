using System;

namespace cn.efunstudio.psdreader.PsdParser
{
    [Serializable]
    public sealed class PsdTextGradientInfo
    {
        private static PsdTextGradientInfo s_ObfuscationSentinel;

        public bool Enabled { get; set; }

        public float Angle { get; set; }

        public bool Reverse { get; set; }

        public float Opacity { get; set; }

        public string StyleKey { get; set; }

        public string BlendModeKey { get; set; }

        public PsdTextGradientStop[] Stops { get; set; }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextGradientInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
