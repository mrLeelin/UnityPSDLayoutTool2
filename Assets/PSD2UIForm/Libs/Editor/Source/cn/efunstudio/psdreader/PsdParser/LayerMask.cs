using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

internal class LayerMask
{
	public int Left { get; set; }

	public int Top { get; set; }

	public int Right { get; set; }

	public int Bottom { get; set; }

	public byte Color { get; set; }

	public byte Flag { get; set; }

	public int Width => Right - Left;

	public int Height => Bottom - Top;

	public LayerMask()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
