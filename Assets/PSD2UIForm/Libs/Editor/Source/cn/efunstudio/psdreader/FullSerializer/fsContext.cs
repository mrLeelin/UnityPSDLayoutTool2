using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public sealed class fsContext
{
	private readonly Dictionary<Type, object> _contextObjects;

	public void Reset()
	{
		_contextObjects.Clear();
	}

	public void Set<T>(T obj)
	{
		_contextObjects[typeof(T)] = obj;
	}

	public bool Has<T>()
	{
		return _contextObjects.ContainsKey(typeof(T));
	}

	public T Get<T>()
	{
		if (!_contextObjects.TryGetValue(typeof(T), out var value))
		{
			throw new InvalidOperationException("There is no context object of type " + typeof(T));
		}
		return (T)value;
	}

	public fsContext()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_contextObjects = new Dictionary<Type, object>();
	}
}
}
