using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

public static class PsdReaderVersion
{
	public const string Current = "2.0.0";

	public static Version CurrentValue { get; }

	public static int CurrentMajor => CurrentValue.Major;

	static PsdReaderVersion()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		CurrentValue = new Version("2.0.0");
	}
}
}
