using System;
using PsdReaderLicenseServiceNamespace;
using UnityEditor;

namespace EditorLicenseUsageScopeManagerNamespace
{
    [InitializeOnLoad]
    internal sealed class EditorLicenseUsageScopeManager
    {
        private sealed class UsageLease : IDisposable
        {
            private bool _isDisposed;

            private static UsageLease s_ObfuscationSentinel;

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    ReleaseLease();
                }
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static UsageLease GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static readonly object _syncRoot;

        private static IDisposable _activeUsageScope;

        private static int _leaseCount;

        private static bool _releasePending;

        internal static EditorLicenseUsageScopeManager s_ObfuscationSentinel;

        static EditorLicenseUsageScopeManager()
        {
            _syncRoot = new object();
            EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine((Delegate)(object)EditorApplication.update, (Delegate)new EditorApplication.CallbackFunction(OnEditorUpdate));
            AssemblyReloadEvents.beforeAssemblyReload += new AssemblyReloadEvents.AssemblyReloadCallback(OnEditorShutdown);
            EditorApplication.quitting += OnEditorShutdown;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static IDisposable Acquire()
        {
            lock (_syncRoot)
            {
                if (_leaseCount == 0 && _activeUsageScope == null)
                {
                    _activeUsageScope = PsdReaderLicenseService.BeginUsageScope();
                }
                _releasePending = false;
                _leaseCount++;
            }
            return new UsageLease();
        }

        internal static void ResetUsageScope()
        {
            ReleaseUsageScope(true);
        }

        private static void ReleaseLease()
        {
            lock (_syncRoot)
            {
                if (_leaseCount > 0)
                {
                    _leaseCount--;
                }
                if (_leaseCount == 0 && _activeUsageScope != null)
                {
                    _releasePending = true;
                }
            }
        }

        private static void OnEditorUpdate()
        {
            ReleaseUsageScope(false);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange value)
        {
            if ((int)value <= 3)
            {
                OnEditorShutdown();
            }
        }

        private static void OnEditorShutdown()
        {
            ReleaseUsageScope(true);
        }

        private static void ReleaseUsageScope(bool enabled)
        {
            IDisposable disposable = null;
            lock (_syncRoot)
            {
                if (!enabled && (_leaseCount != 0 || !_releasePending || _activeUsageScope == null))
                {
                    return;
                }
                if (enabled)
                {
                    _leaseCount = 0;
                }
                disposable = _activeUsageScope;
                _activeUsageScope = null;
                _releasePending = false;
            }
            disposable?.Dispose();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EditorLicenseUsageScopeManager GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
