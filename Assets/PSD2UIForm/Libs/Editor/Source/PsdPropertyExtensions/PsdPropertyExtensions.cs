using System;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdPropertyUtilities
{

internal static class PsdPropertyExtensions
{
	internal static bool ContainsPropertyPath(this object P_0, object P_1, params string[] properties)
	{
		return ((IProperties)P_0).Contains(JoinPropertyPath(P_1, properties));
	}

	internal static ht6w5JFCLilVB3LvZ1L GetPropertyValue<ht6w5JFCLilVB3LvZ1L>(this object P_0, object P_1, params string[] properties)
	{
		return (ht6w5JFCLilVB3LvZ1L)((IProperties)P_0)[JoinPropertyPath(P_1, properties)];
	}

	internal static Guid GetGuidValue(this object P_0, object P_1, params string[] properties)
	{
		return new Guid(P_0.GetStringValue(P_1, properties));
	}

	internal static string GetStringValue(this object P_0, object P_1, params string[] properties)
	{
		return P_0.GetPropertyValue<string>(P_1, properties);
	}

	internal static byte GetByteValue(this object P_0, object P_1, params string[] properties)
	{
		return P_0.GetPropertyValue<byte>(P_1, properties);
	}

	internal static int GetInt32Value(this object P_0, object P_1, params string[] properties)
	{
		return P_0.GetPropertyValue<int>(P_1, properties);
	}

	internal static float GetSingleValue(this object P_0, object P_1, params string[] properties)
	{
		return P_0.GetPropertyValue<float>(P_1, properties);
	}

	internal static double GetDoubleValue(this object P_0, object P_1, params string[] properties)
	{
		return P_0.GetPropertyValue<double>(P_1, properties);
	}

	internal static bool GetBooleanValue(this object P_0, object P_1, params string[] properties)
	{
		return P_0.GetPropertyValue<bool>(P_1, properties);
	}

	internal static bool TryReadPropertyValue<SMRncOFiOYyK1duANG8>(this object P_0, ref SMRncOFiOYyK1duANG8 P_1, object P_2, params string[] properties)
	{
		string text = JoinPropertyPath(P_2, properties);
		if (!((IProperties)P_0).Contains(text))
		{
			return false;
		}
		P_1 = P_0.GetPropertyValue<SMRncOFiOYyK1duANG8>(text, Array.Empty<string>());
		return true;
	}

	private static string JoinPropertyPath(object P_0, params string[] properties)
	{
		if (properties.Length == 0)
		{
			return (string)P_0;
		}
		return (string)P_0 + "." + string.Join(".", properties);
	}
}
}
