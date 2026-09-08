using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

[Serializable]
public sealed class PsdTextBevelInfo
{
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

	public PsdTextBevelInfo()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
