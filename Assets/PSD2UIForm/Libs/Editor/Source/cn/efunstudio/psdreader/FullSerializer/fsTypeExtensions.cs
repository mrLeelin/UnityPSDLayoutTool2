using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public static class fsTypeExtensions
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass2_0
	{
		public bool includeNamespace;

		public _003C_003Ec__DisplayClass2_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal string _003CCSharpName_003Eb__0(Type t)
		{
			return t.CSharpName(includeNamespace);
		}
	}

	public static string CSharpName(this Type type)
	{
		return type.CSharpName(includeNamespace: false);
	}

	public static string CSharpName(this Type type, bool includeNamespace, bool ensureSafeDeclarationName)
	{
		string text = type.CSharpName(includeNamespace);
		if (ensureSafeDeclarationName)
		{
			text = text.Replace('>', '_').Replace('<', '_').Replace('.', '_')
				.Replace(',', '_');
		}
		return text;
	}

	public static string CSharpName(this Type type, bool includeNamespace)
	{
		_003C_003Ec__DisplayClass2_0 CS_0024_003C_003E8__locals3 = new _003C_003Ec__DisplayClass2_0();
		CS_0024_003C_003E8__locals3.includeNamespace = includeNamespace;
		if (type == typeof(void))
		{
			return "void";
		}
		if (type == typeof(int))
		{
			return "int";
		}
		if (!(type == typeof(float)))
		{
			if (type == typeof(bool))
			{
				return "bool";
			}
			if (type == typeof(double))
			{
				return "double";
			}
			if (type == typeof(string))
			{
				return "string";
			}
			if (type.IsGenericParameter)
			{
				return type.ToString();
			}
			string text = "";
			IEnumerable<Type> source = type.GetGenericArguments();
			if (type.IsNested)
			{
				text = text + type.DeclaringType.CSharpName() + ".";
				if (type.DeclaringType.GetGenericArguments().Length != 0)
				{
					source = source.Skip(type.DeclaringType.GetGenericArguments().Length);
				}
			}
			if (!source.Any())
			{
				text += type.Name;
			}
			else
			{
				int num = type.Name.IndexOf('`');
				if (num > 0)
				{
					text += type.Name.Substring(0, num);
				}
				text = text + "<" + string.Join(",", source.Select((Type t) => t.CSharpName(CS_0024_003C_003E8__locals3.includeNamespace)).ToArray()) + ">";
			}
			if (CS_0024_003C_003E8__locals3.includeNamespace && type.Namespace != null)
			{
				text = type.Namespace + "." + text;
			}
			return text;
		}
		return "float";
	}

	public static bool IsInterface(this Type type)
	{
		return type.IsInterface;
	}

	public static bool IsAbstract(this Type type)
	{
		return type.IsAbstract;
	}

	public static bool IsGenericType(this Type type)
	{
		return type.IsGenericType;
	}
}
}
