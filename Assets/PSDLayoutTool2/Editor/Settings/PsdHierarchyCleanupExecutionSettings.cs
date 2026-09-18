namespace PsdLayoutTool2
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Selects the local implementation that applies a reviewed hierarchy plan.
    /// ADR 0001/0002：正式清理只执行 v2，且必须由 Unity 共享核心完成；
    /// Python CLI 只保留只读诊断，不能再作为 Apply 后端。
    /// </summary>
    internal enum PsdHierarchyCleanupExecutionBackend
    {
        NativeUnity = 0,

        /// <summary>
        /// 已废弃。仅为反序列化旧配置保留枚举值；运行时一律视为 NativeUnity。
        /// </summary>
        UnityCliRunner = 1,
    }

    internal readonly struct PsdHierarchyCleanupExecutionSettingsSnapshot
    {
        internal PsdHierarchyCleanupExecutionSettingsSnapshot(PsdHierarchyCleanupExecutionBackend backend)
        {
            this.backend = Normalize(backend);
        }

        internal readonly PsdHierarchyCleanupExecutionBackend backend;

        internal static PsdHierarchyCleanupExecutionBackend Normalize(
            PsdHierarchyCleanupExecutionBackend backend)
        {
            // ADR 0001/0002：唯一正式后端。旧配置的 UnityCliRunner 一律回落并由调用方提示。
            return PsdHierarchyCleanupExecutionBackend.NativeUnity;
        }

        internal bool TryValidate(out string error)
        {
            // 唯一合法正式后端。
            if (backend == PsdHierarchyCleanupExecutionBackend.NativeUnity)
            {
                error = string.Empty;
                return true;
            }

            error = "Prefab 清理执行已固定为 Native Unity（ADR 0001/0002）。CLI Runner 不再作为可选后端。";
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
            if (newBackend == PsdHierarchyCleanupExecutionBackend.UnityCliRunner)
            {
                throw new ArgumentException(
                    "Prefab 清理执行已固定为 Native Unity（ADR 0001/0002）。CLI Runner 不再作为可选后端。",
                    nameof(newBackend));
            }

            var candidate = new PsdHierarchyCleanupExecutionSettingsSnapshot(newBackend);
            if (!candidate.TryValidate(out string error))
            {
                throw new ArgumentException(error, nameof(newBackend));
            }

            if (backend == candidate.backend)
            {
                return false;
            }

            backend = candidate.backend;
            return true;
        }
    }
}
