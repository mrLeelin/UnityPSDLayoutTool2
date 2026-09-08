using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public struct fsOption<T>
{
	private bool _hasValue;

	private T _value;

	public static fsOption<T> Empty;

	public bool HasValue => _hasValue;

	public bool IsEmpty => !_hasValue;

	public T Value
	{
		get
		{
			if (IsEmpty)
			{
				throw new InvalidOperationException("fsOption is empty");
			}
			return _value;
		}
	}

	public fsOption(T value)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_hasValue = true;
		_value = value;
	}

	internal static bool zN2XcKZX7PFcRwVt5Sow()
	{
		return true;
	}

	internal static object C6OcGpZXvr9VC3acdNf5()
	{
		return null;
	}
}
public static class fsOption
{
	public static fsOption<T> Just<T>(T value)
	{
		return new fsOption<T>(value);
	}
}
}
