using System;
using System.Collections.Generic;
using System.Reflection;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdReflection
{

internal static class PsdReflectionUtilities
{
	internal static class Assembly
	{
		private static readonly System.Reflection.Assembly[] s_Assemblies;

		private static readonly Dictionary<string, Type> s_CachedTypes;

		static Assembly()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			s_Assemblies = null;
			s_CachedTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
			s_Assemblies = AppDomain.CurrentDomain.GetAssemblies();
		}

		internal static System.Reflection.Assembly[] GetAssemblies()
		{
			return s_Assemblies;
		}

		internal static Type[] GetTypes()
		{
			List<Type> list = new List<Type>();
			System.Reflection.Assembly[] array = s_Assemblies;
			foreach (System.Reflection.Assembly assembly in array)
			{
				list.AddRange(assembly.GetTypes());
			}
			return list.ToArray();
		}

		internal static void GetTypes(List<Type> results)
		{
			if (results == null)
			{
				throw new Exception("Results is invalid.");
			}
			results.Clear();
			System.Reflection.Assembly[] array = s_Assemblies;
			foreach (System.Reflection.Assembly assembly in array)
			{
				results.AddRange(assembly.GetTypes());
			}
		}

		internal static Type GetType(string typeName)
		{
			if (!string.IsNullOrEmpty(typeName))
			{
				Type value = null;
				if (s_CachedTypes.TryGetValue(typeName, out value))
				{
					return value;
				}
				value = Type.GetType(typeName);
				if (value != null)
				{
					s_CachedTypes.Add(typeName, value);
					return value;
				}
				System.Reflection.Assembly[] array = s_Assemblies;
				int num = 0;
				while (true)
				{
					if (num < array.Length)
					{
						System.Reflection.Assembly assembly = array[num];
						value = Type.GetType($"{typeName}, {assembly.FullName}");
						if (value != null)
						{
							break;
						}
						num++;
						continue;
					}
					return null;
				}
				s_CachedTypes.Add(typeName, value);
				return value;
			}
			throw new Exception("Type name is invalid.");
		}
	}
}
}
