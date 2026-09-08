using PsdLicensing;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal sealed class LicenseDeviceIdentifierProvider
{
	internal void GetDeviceIdHash(out string P_0)
	{
		P_0 = LicenseCryptography.HashDeviceIdentifier(SystemInfo.deviceUniqueIdentifier);
	}

	public LicenseDeviceIdentifierProvider()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
