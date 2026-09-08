using System;
using PsdProtectionGuards;
using UnityEditor;
using PsdProtectionRuntime;
using PsdLicensing;

namespace PsdLicenseEditor
{

[InitializeOnLoad]
internal static class PsdUpdateStartupCheck
{
	static PsdUpdateStartupCheck()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		EditorApplication.delayCall = (EditorApplication.CallbackFunction)Delegate.Combine(EditorApplication.delayCall, new EditorApplication.CallbackFunction(CheckForUpdatesAfterLoad));
	}

	private static void CheckForUpdatesAfterLoad()
	{
		EditorApplication.delayCall = (EditorApplication.CallbackFunction)Delegate.Remove(EditorApplication.delayCall, new EditorApplication.CallbackFunction(CheckForUpdatesAfterLoad));
		PsdProductUpdateService.CheckForUpdatesAtStartup();
	}
}
}
