using System;
using System.Collections.Generic;
using System.Reflection;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer.Internal
{

public static class fsTypeCache
{
	private static Dictionary<string, Type> _cachedTypes;

	private static Dictionary<string, Assembly> _assembliesByName;

	private static List<Assembly> _assembliesByIndex;

	static fsTypeCache()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_cachedTypes = new Dictionary<string, Type>();
		lock (typeof(fsTypeCache))
		{
			_assembliesByName = new Dictionary<string, Assembly>();
			_assembliesByIndex = new List<Assembly>();
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				_assembliesByName[assembly.FullName] = assembly;
				_assembliesByIndex.Add(assembly);
			}
			_cachedTypes = new Dictionary<string, Type>();
			AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
		}
	}

	private static void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
	{
		lock (typeof(fsTypeCache))
		{
			_assembliesByName[args.LoadedAssembly.FullName] = args.LoadedAssembly;
			_assembliesByIndex.Add(args.LoadedAssembly);
			_cachedTypes = new Dictionary<string, Type>();
		}
	}

	private static bool TryDirectTypeLookup(string assemblyName, string typeName, out Type type)
	{
		if (assemblyName != null && _assembliesByName.TryGetValue(assemblyName, out var value))
		{
			type = value.GetType(typeName, throwOnError: false);
			return type != null;
		}
		type = null;
		return false;
	}

	private static bool TryIndirectTypeLookup(string typeName, out Type type)
	{
		for (int i = 0; i < _assembliesByIndex.Count; i++)
		{
			Assembly assembly = _assembliesByIndex[i];
			type = assembly.GetType(typeName);
			if (type != null)
			{
				return true;
			}
		}
		for (int i = 0; i < _assembliesByIndex.Count; i++)
		{
			Type[] types = _assembliesByIndex[i].GetTypes();
			foreach (Type type2 in types)
			{
				if (type2.FullName == typeName)
				{
					type = type2.GetType();
					return true;
				}
			}
		}
		type = null;
		return false;
	}

	public static void Reset()
	{
		_cachedTypes = new Dictionary<string, Type>();
	}

	public static Type GetType(string name)
	{
		return GetType(name, null);
	}

	public static Type GetType(string name, string assemblyHint)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		lock (typeof(fsTypeCache))
		{
			if (!_cachedTypes.TryGetValue(name, out var value))
			{
				if (!TryDirectTypeLookup(assemblyHint, name, out value))
				{
					TryIndirectTypeLookup(name, out value);
				}
				_cachedTypes[name] = value;
			}
			return value;
		}
	}
}
}
