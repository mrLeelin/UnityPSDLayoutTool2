using System;

namespace cn.efunstudio.psdreader.PsdParser
{
    [Serializable]
    public sealed class PsdTextStrokeInfo
    {
        internal static PsdTextStrokeInfo s_ObfuscationSentinel;

        public bool Enabled { get; set; }

        public float Size { get; set; }

        public float Opacity { get; set; }

        public PsdColor Color { get; set; }

        public PsdTextStrokePosition Position { get; set; }

        public string BlendModeKey { get; set; }

        public string PaintTypeKey { get; set; }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextStrokeInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
