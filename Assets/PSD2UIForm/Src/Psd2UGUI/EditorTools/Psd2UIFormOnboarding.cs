using System;
using UnityEditor;

namespace UGF.EditorTools.Psd2UGUI
{
    [InitializeOnLoad]
    internal sealed class Psd2UIFormOnboarding
    {
        private const string PrefKey = "UGF.Psd2UIForm.OnboardingShown";

        private static bool s_Initialized;

        internal static Psd2UIFormOnboarding s_ObfuscationSentinel;

        static Psd2UIFormOnboarding()
        {
            EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine((Delegate)(object)EditorApplication.update, (Delegate)new EditorApplication.CallbackFunction(TryShow));
        }

        private static void TryShow()
        {
            if (!s_Initialized)
            {
                s_Initialized = true;
                EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Remove((Delegate)(object)EditorApplication.update, (Delegate)new EditorApplication.CallbackFunction(TryShow));
                if (!EditorPrefs.GetBool("UGF.Psd2UIForm.OnboardingShown", false))
                {
                    Psd2UIFormOnboardingWindow.ShowWindow();
                }
            }
        }

        internal static void MarkShown()
        {
            EditorPrefs.SetBool("UGF.Psd2UIForm.OnboardingShown", true);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Psd2UIFormOnboarding GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
