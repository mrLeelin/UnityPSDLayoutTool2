using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdLinkedLayers;

namespace PsdLinkedLayers
{

internal class EmbeddedLinkedLayerCollection
{
	[CompilerGenerated]
	private EmbeddedLinkedPsdLayer[] _items;

	[SpecialName]
	[CompilerGenerated]
	public EmbeddedLinkedPsdLayer[] GetItems()
	{
		return _items;
	}

	[SpecialName]
	[CompilerGenerated]
	public void SetItems(EmbeddedLinkedPsdLayer[] P_0)
	{
		_items = P_0;
	}

	public EmbeddedLinkedLayerCollection()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
