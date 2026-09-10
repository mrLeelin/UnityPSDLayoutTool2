using TMPro;
using UnityEngine;
using TextGradientColorStopNamespace;

namespace PsdTextStyleInfoNamespace
{
    internal struct PsdTextStyleInfo
    {
        internal enum TMPOutlineMode
        {
            Center,
            Inside,
            Outside
        }

        public string Text;

        public int FontSize;

        public bool IsLineSpacingAuto;

        public float LineSpacing;

        public float CharacterSpacing;

        public Color Color;

        public FontStyle LegacyFontStyle;

        public FontStyles TMPFontStyle;

        public string FontName;

        public bool HasOutline;

        public Color OutlineColor;

        public float LegacyOutlineSize;

        public float TMPOutlineSize;

        public TMPOutlineMode OutlineMode;

        public bool HasShadow;

        public bool IsInnerShadow;

        public Color ShadowColor;

        public Vector2 ShadowOffset;

        public float ShadowSpread;

        public float ShadowSoftness;

        public bool HasGlow;

        public bool IsInnerGlow;

        public Color GlowColor;

        public float GlowSize;

        public float GlowSpread;

        public float GlowOffset;

        public float GlowPower;

        public bool HasBevel;

        public bool IsInnerBevel;

        public float BevelSize;

        public float BevelDepth;

        public float BevelSoftness;

        public float BevelAngle;

        public float BevelAltitude;

        public Color BevelHighlightColor;

        public float BevelHighlightOpacity;

        public Color BevelShadowColor;

        public float BevelShadowOpacity;

        public bool HasGradient;

        public float GradientAngle;

        public bool IsGradientReversed;

        public string GradientStyleKey;

        public string GradientBlendModeKey;

        public TextGradientColorStop[] GradientStops;
    }
}
