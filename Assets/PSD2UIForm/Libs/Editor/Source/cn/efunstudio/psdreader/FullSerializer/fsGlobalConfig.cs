using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public static class fsGlobalConfig
{
	public static bool IsCaseSensitive;

	public static bool AllowInternalExceptions;

	public static string InternalFieldPrefix;

	static fsGlobalConfig()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		IsCaseSensitive = true;
		AllowInternalExceptions = true;
		InternalFieldPrefix = "$";
	}
}
}
