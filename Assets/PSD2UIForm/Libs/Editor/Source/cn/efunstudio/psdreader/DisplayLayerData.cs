using System.Collections.Generic;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

internal class DisplayLayerData
{
	public int[] indexId;

	public bool isVisible;

	public bool isGroup;

	public bool isOpen;

	public bool isLinked;

	public int[] linkId;

	public List<DisplayLayerData> Childs;

	public DisplayLayerData()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Childs = new List<DisplayLayerData>();
	}
}
}
