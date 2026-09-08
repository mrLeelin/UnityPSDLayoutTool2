using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderLicensePolicyDocument
{
	public int LocalCacheTimeoutDays;

	public int SoftMaxMachines;

	public PsdReaderLicensePolicyDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		LocalCacheTimeoutDays = 2;
		SoftMaxMachines = 10;
	}
}
}
