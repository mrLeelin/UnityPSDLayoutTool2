namespace PsdLayoutTool2
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Selects the local implementation that applies a reviewed hierarchy plan.
    /// The native implementation is deliberately the default so standard cleanup
    /// does not require an external CLI tool.
    /// </summary>
    internal enum PsdHierarchyCleanupExecutionBackend
    {
        NativeUnity,
        UloopRunner,
    }

    internal readonly struct PsdHierarchyCleanupExecutionSettingsSnapshot
    {
        internal PsdHierarchyCleanupExecutionSettingsSnapshot(PsdHierarchyCleanupExecutionBackend backend)
        {
            this.backend = backend;
        }

        internal readonly PsdHierarchyCleanupExecutionBackend backend;

        internal bool TryValidate(out string error)
        {
            if (backend == PsdHierarchyCleanupExecutionBackend.NativeUnity ||
                backend == PsdHierarchyCleanupExecutionBackend.UloopRunner)
            {
                error = string.Empty;
                return true;
            }

            error = "Unsupported Prefab cleanup execution backend.";
            return false;
        }
    }

    [Serializable]
    internal sealed class PsdHierarchyCleanupExecutionSettings
    {
        [SerializeField]
        private PsdHierarchyCleanupExecutionBackend backend = PsdHierarchyCleanupExecutionBackend.NativeUnity;

        internal PsdHierarchyCleanupExecutionSettingsSnapshot Resolve()
        {
            return new PsdHierarchyCleanupExecutionSettingsSnapshot(backend);
        }

        internal bool Set(PsdHierarchyCleanupExecutionBackend newBackend)
        {
            var candidate = new PsdHierarchyCleanupExecutionSettingsSnapshot(newBackend);
            if (!candidate.TryValidate(out string error))
            {
                throw new ArgumentException(error, nameof(newBackend));
            }

            if (backend == newBackend)
            {
                return false;
            }

            backend = newBackend;
            return true;
        }
    }
}
