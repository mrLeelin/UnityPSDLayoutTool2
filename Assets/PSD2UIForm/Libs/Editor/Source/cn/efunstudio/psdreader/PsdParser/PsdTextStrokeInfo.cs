using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

[Serializable]
public sealed class PsdTextStrokeInfo
{
	public bool Enabled { get; set; }

	public float Size { get; set; }

	public float Opacity { get; set; }

	public PsdColor Color { get; set; }

	public PsdTextStrokePosition Position { get; set; }

	public string BlendModeKey { get; set; }

	public string PaintTypeKey { get; set; }

	public PsdTextStrokeInfo()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
