using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PsdProtectionRuntime;
using PsdProtectionGuards;

[CompilerGenerated]
internal sealed class _003C_003Ef__AnonymousType0<TNode, TName>
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly TNode _node;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly TName _name;

	private static object gtfBcdZwfx5XkY2ajWJ5;

	public TNode Node => _node;

	public TName Name => _name;

	[DebuggerHidden]
	public _003C_003Ef__AnonymousType0(TNode Node, TName Name)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_node = Node;
		_name = Name;
	}

	[DebuggerHidden]
	public override bool Equals(object value)
	{
		global::_003C_003Ef__AnonymousType0<TNode, TName> anon = value as global::_003C_003Ef__AnonymousType0<TNode, TName>;
		if (this != anon)
		{
			if (anon != null && EqualityComparer<TNode>.Default.Equals(_node, anon._node))
			{
				return EqualityComparer<TName>.Default.Equals(_name, anon._name);
			}
			return false;
		}
		return true;
	}

	[DebuggerHidden]
	public override int GetHashCode()
	{
		return (1466110645 + EqualityComparer<TNode>.Default.GetHashCode(_node)) * -1521134295 + EqualityComparer<TName>.Default.GetHashCode(_name);
	}

	[DebuggerHidden]
	public override string ToString()
	{
		object[] array = new object[2];
		TNode val = _node;
		array[0] = ((val != null) ? val.ToString() : null);
		TName val2 = _name;
		array[1] = ((val2 == null) ? null : val2.ToString());
		return string.Format(null, "{{ Node = {0}, Name = {1} }}", array);
	}

	internal static bool y6qAybZwcwRhY42SeS5X()
	{
		return gtfBcdZwfx5XkY2ajWJ5 == null;
	}

	internal static object lLgAd1Zwgl8dRflw1MYt()
	{
		return gtfBcdZwfx5XkY2ajWJ5;
	}
}
