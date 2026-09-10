using System;

namespace cn.efunstudio.psdreader.PsdParser
{
    [Serializable]
    public sealed class PsdTextLayerInfo
    {
        private static PsdTextLayerInfo s_ObfuscationSentinel;

        public string Text { get; set; }

        public int FontIndex { get; set; }

        public string FontName { get; set; }

        public float FontSize { get; set; }

        public PsdColor Color { get; set; }

        public bool FauxBold { get; set; }

        public bool FauxItalic { get; set; }

        public bool Underline { get; set; }

        public bool Strikethrough { get; set; }

        public bool AllCaps { get; set; }

        public float Tracking { get; set; }

        public float Leading { get; set; }

        public bool AutoLeading { get; set; }

        public PsdTextStrokeInfo Stroke { get; set; }

        public PsdTextShadowInfo Shadow { get; set; }

        public PsdTextGradientInfo Gradient { get; set; }

        public PsdTextShadowInfo InnerShadow { get; set; }

        public PsdTextGlowInfo OuterGlow { get; set; }

        public PsdTextGlowInfo InnerGlow { get; set; }

        public PsdTextBevelInfo Bevel { get; set; }

        public PsdTextLayerInfo()
        {
            FontIndex = -1;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdTextLayerInfo GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
