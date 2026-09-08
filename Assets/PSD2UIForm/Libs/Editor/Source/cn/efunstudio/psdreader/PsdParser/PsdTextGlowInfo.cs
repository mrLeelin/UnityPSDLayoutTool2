using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

[Serializable]
public sealed class PsdTextGlowInfo
{
	public bool Enabled { get; set; }

	public bool Inner { get; set; }

	public float Opacity { get; set; }

	public float Size { get; set; }

	public float Spread { get; set; }

	public PsdColor Color { get; set; }

	public string BlendModeKey { get; set; }

	public PsdTextGlowInfo()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
