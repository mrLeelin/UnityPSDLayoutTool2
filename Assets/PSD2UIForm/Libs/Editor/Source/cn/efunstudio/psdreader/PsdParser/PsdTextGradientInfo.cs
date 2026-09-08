using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

[Serializable]
public sealed class PsdTextGradientInfo
{
	public bool Enabled { get; set; }

	public float Angle { get; set; }

	public bool Reverse { get; set; }

	public float Opacity { get; set; }

	public string StyleKey { get; set; }

	public string BlendModeKey { get; set; }

	public PsdTextGradientStop[] Stops { get; set; }

	public PsdTextGradientInfo()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
