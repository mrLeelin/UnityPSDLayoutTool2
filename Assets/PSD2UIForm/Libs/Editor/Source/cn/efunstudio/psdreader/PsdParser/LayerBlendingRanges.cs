using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

internal class LayerBlendingRanges
{
	public LayerBlendingRanges()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
