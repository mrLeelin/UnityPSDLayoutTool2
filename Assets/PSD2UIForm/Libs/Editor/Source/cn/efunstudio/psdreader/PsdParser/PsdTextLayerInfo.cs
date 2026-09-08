using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

[Serializable]
public sealed class PsdTextLayerInfo
{
	public string Text { get; set; }

	public int FontIndex { get; set; }

	public string FontName { get; set; }

	public float FontSize { get; set; }

	public PsdColor Color { get; set; }

	public bool FauxBold { get; set; }

	public bool FauxItalic { get; set; }

	public bool Underline { get; set; }

	public bool Strikethrough { get; set; }

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
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		FontIndex = -1;
	}
}
}
