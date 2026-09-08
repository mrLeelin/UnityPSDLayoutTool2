using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

[Serializable]
public sealed class PsdTextGradientStop
{
	public float Location { get; set; }

	public PsdColor Color { get; set; }

	public PsdTextGradientStop()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
