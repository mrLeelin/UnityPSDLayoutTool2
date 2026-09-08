namespace Psd2UIForm.Reconstruction
{

internal readonly struct ConstructorGuardToken
{
}

internal static class ConstructorGuards
{
	internal static ConstructorGuardToken Evaluate()
	{
		PsdProtectionRuntime.AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		PsdProtectionGuards.ReactorTrialGuard.CheckTrialPeriodOnce();
		return default;
	}
}
}
