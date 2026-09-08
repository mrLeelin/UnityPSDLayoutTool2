namespace PsdLayoutTool2
{
    using System.IO;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    /// <summary>
    /// 提供在 Prefab Stage 的 GameObject 右键菜单中访问局部整理功能的入口。
    /// </summary>
    internal static class PsdHierarchyInspectorMenu
    {
        [MenuItem("GameObject/PSD Layout Tool/局部整理", validate = true)]
        private static bool ValidateOpenLocalRepair()
        {
            // 检查是否在 Prefab Stage
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return false;

            // 检查是否有选中对象
            if (Selection.activeGameObject == null) return false;

            // 检查是否有 PsdPrefabNodeIdentity 组件（在自身或子树中）
            var identity = Selection.activeGameObject.GetComponentInChildren<PsdPrefabNodeIdentity>();
            return identity != null;
        }

        [MenuItem("GameObject/PSD Layout Tool/局部整理", false, 30)]
        private static void OpenLocalRepairFromSelection()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                EditorUtility.DisplayDialog("错误", "当前不在 Prefab 编辑模式中。", "确定");
                return;
            }

            string prefabAssetPath = stage.assetPath;

            // 尝试从 Prefab 根对象找到 PSD 源路径
            GameObject prefabRoot = stage.prefabContentsRoot;
            string psdSourcePath = TryResolvePsdSourcePath(prefabRoot, prefabAssetPath);

            if (string.IsNullOrEmpty(psdSourcePath))
            {
                EditorUtility.DisplayDialog("错误",
                    "无法找到关联的 PSD 源文件。\n请确保该 Prefab 是通过 PSD Layout Tool 生成的。",
                    "确定");
                return;
            }

            if (!PsdHierarchyLocalRepairWindow.TryOpen(psdSourcePath, prefabAssetPath, out string error))
            {
                EditorUtility.DisplayDialog("错误", error, "确定");
            }
        }

        private static string TryResolvePsdSourcePath(GameObject prefabRoot, string prefabAssetPath)
        {
            // 方法 1: 通过 Prefab 路径推断 PSD 路径
            // 假设结构：Assets/XXX/Prefab/xxx.prefab -> Assets/XXX/xxx.psd
            string prefabDir = Path.GetDirectoryName(prefabAssetPath)?.Replace("\\", "/");
            string prefabName = Path.GetFileNameWithoutExtension(prefabAssetPath);

            if (string.IsNullOrEmpty(prefabDir) || string.IsNullOrEmpty(prefabName))
                return string.Empty;

            // 尝试多种可能的路径
            string[] candidatePaths = new[]
            {
                Path.Combine(prefabDir, "..", prefabName + ".psd").Replace("\\", "/"),
                Path.Combine(prefabDir, prefabName + ".psd").Replace("\\", "/"),
                Path.Combine(prefabDir, "..", "..", prefabName + ".psd").Replace("\\", "/"),
            };

            foreach (string candidate in candidatePaths)
            {
                string normalizedPath = candidate.Replace("\\", "/");

                // 尝试相对于 Assets 的路径
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", normalizedPath));
                if (File.Exists(fullPath))
                {
                    return normalizedPath;
                }
            }

            return string.Empty;
        }
    }
}
