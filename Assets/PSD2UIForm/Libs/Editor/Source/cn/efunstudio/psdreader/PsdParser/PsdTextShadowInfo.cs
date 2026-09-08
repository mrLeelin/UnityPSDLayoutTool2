using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

[Serializable]
public sealed class PsdTextShadowInfo
{
	public bool Enabled { get; set; }

	public bool UseGlobalAngle { get; set; }

	public float Opacity { get; set; }

	public float Angle { get; set; }

	public float Distance { get; set; }

	public float Spread { get; set; }

	public float Blur { get; set; }

	public PsdColor Color { get; set; }

	public string BlendModeKey { get; set; }

	public PsdTextShadowInfo()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
