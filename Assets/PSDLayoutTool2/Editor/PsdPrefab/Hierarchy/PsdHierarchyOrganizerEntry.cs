namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Diagnostics;
using System.Text;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Resolves the generated Prefab and starts an interactive AI terminal with its context.
    /// </summary>
    public static class PsdHierarchyOrganizerEntry
    {
        public const string AiButtonLabel = "AI整理";
        public const string ApplyPlanButtonLabel = "应用AI计划";

        public static async void ApplyLatestPlan(string sourcePsdAssetPath)
        {
            if (!TryResolvePrefabAvailability(sourcePsdAssetPath,
                    PsdImporter.OutputMode, PsdImporter.OutputFolderName, PsdImporter.PrefabMode,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out string targetPrefabPath, out string error))
            {
                EditorUtility.DisplayDialog("PSDLayoutTool2", error, "确定"); return;
            }
            if (!PsdHierarchyChatContextBuilder.TryCreate(sourcePsdAssetPath, targetPrefabPath,
                    out PsdHierarchyChatContext context, out error))
            {
                EditorUtility.DisplayDialog("PSDLayoutTool2", error, "确定"); return;
            }
            string directory = Path.Combine(context.projectRoot, "Library", "PsdHierarchyTerminal");
            string planPath = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.plan.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;
            if (string.IsNullOrEmpty(planPath) || !File.Exists(planPath))
            {
                EditorUtility.DisplayDialog("PSDLayoutTool2", "没有找到终端生成的计划文件，请先完成一次 AI 整理。", "确定"); return;
            }
            string planJson = File.ReadAllText(planPath, Encoding.UTF8);
            if (!EditorUtility.DisplayDialog("确认应用 AI 计划",
                    "计划文件：" + Path.GetFileName(planPath) + "\n\nUnity 将重新校验快照、节点引用和执行安全性，然后原地更新 Prefab。是否继续？",
                    "确认应用", "取消")) return;
            PsdHierarchyChatCleanupExecutionResult result =
                await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(context, planJson);
            EditorUtility.DisplayDialog("PSDLayoutTool2", result.success ? "AI 计划已应用并完成 Unity 执行链。" : result.message, "确定");
        }

        public static bool TryResolvePrefabAvailability(
            string psdAssetPath,
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            PsdImporter.PrefabOutputMode prefabMode,
            Func<string, bool> prefabExists,
            out string targetPrefabPath,
            out string explanation)
        {
            targetPrefabPath = string.Empty;
            explanation = string.Empty;
            if (!PsdGeneratedPrefabPathResolver.TryResolve(
                    psdAssetPath,
                    outputMode,
                    outputFolderName,
                    PsdImporter.FixedOutputPath,
                    PsdImporter.PrefabOutputPath,
                    prefabMode,
                    out targetPrefabPath))
            {
                explanation = "Unable to resolve the generated Prefab path.";
                return false;
            }

            if (prefabExists == null) throw new ArgumentNullException(nameof(prefabExists));
            if (prefabExists(targetPrefabPath))
            {
                return true;
            }

            string sourceGuid = AssetDatabase.AssetPathToGUID(NormalizeAssetPath(psdAssetPath));
            if (PsdPrefabTargetBinding.TryResolveMovedTargetPrefabPath(
                    sourceGuid, targetPrefabPath, out string movedPrefabPath))
            {
                targetPrefabPath = movedPrefabPath;
                return true;
            }

            if (PsdHierarchyCleanupReplayProfile.TryResolveMovedTargetPrefabPath(
                    sourceGuid, targetPrefabPath, out movedPrefabPath))
            {
                targetPrefabPath = movedPrefabPath;
                return true;
            }

            if (TryFindUniqueDirectPrefabFallback(targetPrefabPath, prefabExists, out string fallbackPath))
            {
                targetPrefabPath = fallbackPath;
                return true;
            }

            explanation = "Prefab不存在，请先生成Prefab。";
            return false;
        }

        public static bool TryOpenChat(string sourcePsdAssetPath, out string error)
        {
            PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
            string targetPrefabPath;
            string availabilityError;
            if (!TryResolvePrefabAvailability(
                    sourcePsdAssetPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.PrefabMode,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out targetPrefabPath,
                    out availabilityError))
            {
                error = availabilityError;
                return false;
            }

            if (!PsdHierarchyChatContextBuilder.TryCreate(sourcePsdAssetPath, targetPrefabPath,
                    out PsdHierarchyChatContext context, out error)) return false;
            PsdHierarchyAiSettingsSnapshot settings = PsdLayoutProjectSettings.instance.ResolveHierarchyAiSettings();
            if (settings.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                error = "AI整理已改为直接打开 Windows PowerShell，请使用本地 CLI。";
                return false;
            }
            if (!PsdHierarchyAiCliDiscovery.TryGetInstalled(settings.provider, out PsdHierarchyAiCliDescriptor cli))
            {
                error = "全局配置选择的 AI CLI 当前不可用，请重新选择。";
                return false;
            }
            try
            {
                PsdHierarchyChatConnection connection = new PsdHierarchyChatConnection(
                    settings.provider, settings.connectionMode, cli.executablePath,
                    string.Empty, string.Empty, string.Empty);
                if (!connection.TryValidate(out error)) return false;
                // Keep the complete prompt out of Windows command-line length limits and
                // cmd shim quoting. Each terminal owns a separate, persistent prompt file.
                string promptDirectory = Path.Combine(context.projectRoot, "Library", "PsdHierarchyTerminal");
                Directory.CreateDirectory(promptDirectory);
                string sessionId = Guid.NewGuid().ToString("N");
                string promptPath = Path.Combine(promptDirectory, sessionId + ".md");
                string planPath = Path.Combine(promptDirectory, sessionId + ".plan.json");
                string reviewPath = Path.Combine(promptDirectory, sessionId + ".review.md");
                string taskPrompt = PsdHierarchyChatClient.BuildPortablePrompt(context) +
                    "\n\n===== TERMINAL SESSION CONTRACT =====\n" +
                    "This is an analysis and plan session. Do not claim that Unity assets were changed.\n" +
                    "Write the complete executable JSON plan (and no partial patch) to: " + planPath.Replace('\\', '/') + "\n" +
                    "Write the human-readable Chinese review to: " + reviewPath.Replace('\\', '/') + "\n" +
                    "After every revision, replace both files atomically or rewrite them completely.\n" +
                    "Only a later Unity validation and explicit APPLY_PLAN action can modify the Prefab.\n";
                File.WriteAllText(promptPath, taskPrompt, new UTF8Encoding(false));
                string initialPrompt = "Read the UTF-8 task file at " + promptPath.Replace('\\', '/') +
                    ". Start the complete PSD hierarchy review now, save the review and full JSON plan to the exact paths specified in that file, and remain interactive for follow-up revisions.";
                string cliPath = cli.executablePath.Replace("'", "''");
                string command = "Set-Location -LiteralPath '" + context.projectRoot.Replace("'", "''") +
                    "'; & '" + cliPath + "' '" + initialPrompt.Replace("'", "''") + "'";
                string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
                Process.Start(new ProcessStartInfo {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand,
                    WorkingDirectory = context.projectRoot,
                    UseShellExecute = true,
                });
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "打开 PowerShell AI 终端失败：" + exception.Message;
                return false;
            }
        }

        public static bool TryOpenLocalRepair(string sourcePsdAssetPath, out string error)
        {
            PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
            if (!TryResolvePrefabAvailability(
                    sourcePsdAssetPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.PrefabMode,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out string targetPrefabPath,
                    out string availabilityError))
            {
                error = availabilityError;
                return false;
            }

            return PsdHierarchyLocalRepairWindow.TryOpen(
                sourcePsdAssetPath,
                targetPrefabPath,
                out error);
        }

        /// <summary>
        /// 复制 AI 提示词到剪贴板，供外部 AI 使用。
        /// </summary>
        public static bool TryCopyAiPrompt(string sourcePsdAssetPath, out string error)
        {
            PsdImporter.ApplyProjectOutputSettings(PsdLayoutProjectSettings.instance.ResolveOutputSettings());
            string targetPrefabPath;
            string availabilityError;
            if (!TryResolvePrefabAvailability(
                    sourcePsdAssetPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.PrefabMode,
                    path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                    out targetPrefabPath,
                    out availabilityError))
            {
                error = availabilityError;
                return false;
            }

            try
            {
                if (!PsdHierarchyChatContextBuilder.TryCreate(
                        sourcePsdAssetPath,
                        targetPrefabPath,
                        out PsdHierarchyChatContext context,
                        out error))
                {
                    return false;
                }

                string prompt = PsdHierarchyChatClient.BuildPortablePrompt(context);
                EditorGUIUtility.systemCopyBuffer = prompt;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "复制提示词失败：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Supports an already-organized legacy Prefab whose semantic file name differs from
        /// the PSD file name. Only one direct Prefab under the generated Prefab folder is
        /// accepted; nested Common/Component Prefabs and ambiguous roots are rejected.
        /// </summary>
        internal static bool TrySelectUniqueDirectPrefabFallback(
            string configuredPrefabPath,
            IEnumerable<string> prefabCandidates,
            Func<string, bool> prefabExists,
            out string selectedPrefabPath)
        {
            selectedPrefabPath = string.Empty;
            if (prefabCandidates == null || prefabExists == null)
            {
                return false;
            }

            string prefabFolder = GetAssetDirectory(configuredPrefabPath);
            if (string.IsNullOrEmpty(prefabFolder))
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in prefabCandidates)
            {
                string normalizedCandidate = NormalizeAssetPath(candidate);
                if (!normalizedCandidate.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetAssetDirectory(normalizedCandidate), prefabFolder, StringComparison.OrdinalIgnoreCase) ||
                    !prefabExists(normalizedCandidate) ||
                    !seen.Add(normalizedCandidate))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(selectedPrefabPath))
                {
                    selectedPrefabPath = string.Empty;
                    return false;
                }

                selectedPrefabPath = normalizedCandidate;
            }

            return !string.IsNullOrEmpty(selectedPrefabPath);
        }

        private static bool TryFindUniqueDirectPrefabFallback(
            string configuredPrefabPath,
            Func<string, bool> prefabExists,
            out string fallbackPath)
        {
            fallbackPath = string.Empty;
            string prefabFolder = GetAssetDirectory(configuredPrefabPath);
            if (string.IsNullOrEmpty(prefabFolder) || !AssetDatabase.IsValidFolder(prefabFolder))
            {
                return false;
            }

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { prefabFolder });
            var candidates = new List<string>(guids.Length);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                {
                    candidates.Add(assetPath);
                }
            }

            return TrySelectUniqueDirectPrefabFallback(
                configuredPrefabPath,
                candidates,
                prefabExists,
                out fallbackPath);
        }

        private static string GetAssetDirectory(string assetPath)
        {
            string directory = Path.GetDirectoryName(NormalizeAssetPath(assetPath));
            return NormalizeAssetPath(directory);
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty).Replace('\\', '/');
        }
    }
}
