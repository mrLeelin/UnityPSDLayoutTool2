using System;
using PsdLicensing;
using PsdProtectionGuards;
using UnityEditor;
using PsdProtectionRuntime;

namespace PsdLicenseEditor
{

[InitializeOnLoad]
internal static class EditorLicenseOperationScope
{
	private sealed class OperationLease : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (!_disposed)
			{
				_disposed = true;
				EndOperation();
			}
		}

		public OperationLease()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private static readonly object _scopeLock;

	private static IDisposable _licenseScope;

	private static int _activeOperationCount;

	private static bool _releasePending;

	static EditorLicenseOperationScope()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_scopeLock = new object();
		EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine(EditorApplication.update, new EditorApplication.CallbackFunction(ReleaseOnEditorUpdate));
		AssemblyReloadEvents.beforeAssemblyReload += ReleaseOnEditorShutdown;
		EditorApplication.quitting += ReleaseOnEditorShutdown;
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
	}

	internal static IDisposable BeginOperation()
	{
		lock (_scopeLock)
		{
			if (_activeOperationCount == 0 && _licenseScope == null)
			{
				_licenseScope = PsdLicenseService.BeginLicenseOperation();
			}
			_releasePending = false;
			_activeOperationCount++;
		}
		return new OperationLease();
	}

	internal static void ForceRelease()
	{
		ReleaseScope(true);
	}

	private static void EndOperation()
	{
		lock (_scopeLock)
		{
			if (_activeOperationCount > 0)
			{
				_activeOperationCount--;
			}
			if (_activeOperationCount == 0 && _licenseScope != null)
			{
				_releasePending = true;
			}
		}
	}

	private static void ReleaseOnEditorUpdate()
	{
		ReleaseScope(false);
	}

	private static void OnPlayModeStateChanged(PlayModeStateChange P_0)
	{
		if ((uint)P_0 <= 3u)
		{
			ReleaseOnEditorShutdown();
		}
	}

	private static void ReleaseOnEditorShutdown()
	{
		ReleaseScope(true);
	}

	private static void ReleaseScope(bool P_0)
	{
		IDisposable disposable = null;
		lock (_scopeLock)
		{
			if (!P_0 && (_activeOperationCount != 0 || !_releasePending || _licenseScope == null))
			{
				return;
			}
			if (P_0)
			{
				_activeOperationCount = 0;
			}
			disposable = _licenseScope;
			_licenseScope = null;
			_releasePending = false;
		}
		disposable?.Dispose();
	}
}
}
