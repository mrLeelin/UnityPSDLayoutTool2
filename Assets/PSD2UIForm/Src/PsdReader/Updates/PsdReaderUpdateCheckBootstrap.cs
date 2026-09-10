using System;
using PsdReaderUpdateServiceNamespace;
using UnityEditor;

namespace PsdReaderUpdateCheckBootstrapNamespace
{
    [InitializeOnLoad]
    internal sealed class PsdReaderUpdateCheckBootstrap
    {
        internal static PsdReaderUpdateCheckBootstrap s_ObfuscationSentinel;

        static PsdReaderUpdateCheckBootstrap()
        {
            EditorApplication.delayCall = (EditorApplication.CallbackFunction)Delegate.Combine((Delegate)(object)EditorApplication.delayCall, (Delegate)new EditorApplication.CallbackFunction(RunDelayedUpdateCheck));
        }

        private static void RunDelayedUpdateCheck()
        {
            EditorApplication.delayCall = (EditorApplication.CallbackFunction)Delegate.Remove((Delegate)(object)EditorApplication.delayCall, (Delegate)new EditorApplication.CallbackFunction(RunDelayedUpdateCheck));
            PsdReaderUpdateService.CheckForUpdatesDaily();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderUpdateCheckBootstrap GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
