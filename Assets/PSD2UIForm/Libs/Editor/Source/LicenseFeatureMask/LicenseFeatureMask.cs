using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal static class LicenseFeatureMask
{
	private static readonly string[] _featureNames;

	internal static uint EncodeFeatures(object P_0)
	{
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			uint num = 0u;
			for (int i = 0; i < ((Array)P_0).Length; i++)
			{
				int num2 = Array.IndexOf(_featureNames, (string)((object[])P_0)[i]);
				if (num2 >= 0)
				{
					num |= (uint)(1 << num2);
				}
			}
			return num;
		}
		return 0u;
	}

	internal static string[] DecodeFeatures(uint P_0)
	{
		if (P_0 != 0)
		{
			List<string> list = new List<string>(_featureNames.Length);
			for (int i = 0; i < _featureNames.Length; i++)
			{
				if ((P_0 & (uint)(1 << i)) != 0)
				{
					list.Add(_featureNames[i]);
				}
			}
			return list.ToArray();
		}
		return Array.Empty<string>();
	}

	internal static bool ContainsFeature(uint P_0, object P_1)
	{
		int num = Array.IndexOf(_featureNames, (string)(P_1 ?? string.Empty));
		if (num < 0)
		{
			return false;
		}
		return (P_0 & (uint)(1 << num)) != 0;
	}

	static LicenseFeatureMask()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_featureNames = new string[1] { "Main" };
	}
}
}
