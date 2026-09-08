using System;
using System.Runtime.CompilerServices;

namespace Psd2UIForm.Reconstruction
{
    internal static class RecoveredModuleInitialization
    {
        private static bool initialized;

        [ModuleInitializer]
        internal static void Initialize()
        {
            PsdProtectionRuntime.AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
            InitializeOriginalModuleGuard();
        }

        private static void InitializeOriginalModuleGuard()
        {
            if (!initialized)
            {
                initialized = true;
                if (Math.Sign((DateTime.Now - new DateTime(2026, 5, 28)).Days) >= 14)
                {
                    throw new Exception("This assembly is protected by an unregistered version of Eziriz's \".NET Reactor\"! This assembly won't further work.");
                }
            }
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
