using System;
using PsdProtectionGuards;
using UnityEditor;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[InitializeOnLoad]
internal static class Psd2UIFormOnboarding
{
	private const string PrefKey = "UGF.Psd2UIForm.OnboardingShown";

	private static bool s_Initialized;

	static Psd2UIFormOnboarding()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine(EditorApplication.update, new EditorApplication.CallbackFunction(TryShow));
	}

	private static void TryShow()
	{
		if (!s_Initialized)
		{
			s_Initialized = true;
			EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Remove(EditorApplication.update, new EditorApplication.CallbackFunction(TryShow));
			if (!EditorPrefs.GetBool("UGF.Psd2UIForm.OnboardingShown", defaultValue: false))
			{
				Psd2UIFormOnboardingWindow.ShowWindow();
			}
		}
	}

	internal static void MarkShown()
	{
		EditorPrefs.SetBool("UGF.Psd2UIForm.OnboardingShown", value: true);
	}
}
}
