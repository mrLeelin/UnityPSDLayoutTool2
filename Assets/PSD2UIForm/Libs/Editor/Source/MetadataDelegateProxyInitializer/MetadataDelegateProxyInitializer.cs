using System;
using System.Reflection;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdProtectionRuntime
{

internal class MetadataDelegateProxyInitializer
{
	internal delegate void ObjectActionProxy(object o);

	internal static Module _manifestModule;

	internal static MetadataDelegateProxyInitializer _initializationSentinel;

	internal static void InitializeProxyDelegates(int typemdt)
	{
		FieldInfo[] fields = default(FieldInfo[]);
		int num2 = default(int);
		FieldInfo fieldInfo = default(FieldInfo);
		MethodInfo method = default(MethodInfo);
		while (true)
		{
			Type type = _manifestModule.ResolveType(33554432 + typemdt);
			int num = 2;
			if (IsInitializationSentinelUnset())
			{
				goto IL_006b;
			}
			goto IL_0093;
			IL_0093:
			switch (num)
			{
			case 8:
				break;
			case 7:
				goto IL_000d;
			case 5:
				goto IL_0038;
			case 6:
				goto IL_004e;
			default:
				goto IL_0061;
			case 3:
				goto IL_006b;
			case 1:
				goto IL_0081;
			case 4:
				continue;
			case 2:
				return;
			}
			goto IL_0006;
			IL_006b:
			fields = type.GetFields();
			num = 0;
			if (IsInitializationSentinelUnset())
			{
				goto IL_0081;
			}
			goto IL_0093;
			IL_0081:
			num2 = 0;
			num = 0;
			if (IsInitializationSentinelUnset())
			{
				goto IL_0061;
			}
			goto IL_0093;
			IL_0006:
			fieldInfo = fields[num2];
			goto IL_000d;
			IL_000d:
			method = (MethodInfo)_manifestModule.ResolveMethod(fieldInfo.MetadataToken + 100663296);
			num = 5;
			if (GetInitializationSentinel() == null)
			{
				goto IL_0038;
			}
			goto IL_0093;
			IL_0038:
			fieldInfo.SetValue(null, (MulticastDelegate)Delegate.CreateDelegate(type, method));
			goto IL_004e;
			IL_004e:
			num2++;
			num = 7;
			if (GetInitializationSentinel() == null)
			{
				goto IL_0061;
			}
			goto IL_0093;
			IL_0061:
			if (num2 >= fields.Length)
			{
				break;
			}
			goto IL_0006;
		}
	}

	public MetadataDelegateProxyInitializer()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}

	static MetadataDelegateProxyInitializer()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_manifestModule = typeof(MetadataDelegateProxyInitializer).Assembly.ManifestModule;
	}

	internal static bool IsInitializationSentinelUnset()
	{
		return _initializationSentinel == null;
	}

	internal static MetadataDelegateProxyInitializer GetInitializationSentinel()
	{
		return _initializationSentinel;
	}
}
}
