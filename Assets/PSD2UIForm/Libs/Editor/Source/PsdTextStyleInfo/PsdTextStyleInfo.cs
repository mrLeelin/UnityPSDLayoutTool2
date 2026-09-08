using TMPro;
using UnityEngine;
using PsdTextStyles;

namespace PsdTextStyles
{

internal struct PsdTextStyleInfo
{
	internal enum TMPOutlineMode
	{
		Center,
		Inside,
		Outside
	}

	public string TextContent;

	public int FontSize;

	public bool AutoLineSpacing;

	public float LineSpacing;

	public float CharacterSpacing;

	public Color TextColor;

	public FontStyle FontStyle;

	public FontStyles TmpFontStyle;

	public string FontName;

	public bool OutlineEnabled;

	public Color OutlineColor;

	public float UguiOutlineSize;

	public float TmpOutlineSize;

	public TMPOutlineMode OutlineMode;

	public bool ShadowEnabled;

	public bool InnerShadow;

	public Color ShadowColor;

	public Vector2 ShadowOffset;

	public float ShadowSpread;

	public float ShadowSoftness;

	public bool GlowEnabled;

	public bool InnerGlow;

	public Color GlowColor;

	public float GlowSize;

	public float GlowSpread;

	public float GlowOffset;

	public float GlowPower;

	public bool BevelEnabled;

	public bool InnerBevel;

	public float BevelSize;

	public float BevelDepth;

	public float BevelSoften;

	public float BevelAngle;

	public float BevelAltitude;

	public Color BevelHighlightColor;

	public float BevelHighlightOpacity;

	public Color BevelShadowColor;

	public float BevelShadowOpacity;

	public bool GradientEnabled;

	public float GradientAngle;

	public bool GradientReverse;

	public string GradientStyleKey;

	public string GradientBlendModeKey;

	public PsdUiGradientStop[] GradientStops;
}
}
