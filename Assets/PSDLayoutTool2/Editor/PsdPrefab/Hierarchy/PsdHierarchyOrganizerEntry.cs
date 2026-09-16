namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
        public const string CopyPromptButtonLabel = "AI提示词复制";

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
            // 未选择 AI 模型时直接说清楚该去哪儿选，而不是抛一句「CLI 不可用」让人猜。
            if (!settings.isConfigured)
            {
                error = "尚未选择 AI 模型。\n\n请先打开「全局配置」，在「AI 层级整理」里选择一个本机已安装的 CLI，再回来点「AI整理」。";
                return false;
            }

            if (settings.connectionMode != PsdHierarchyAiConnectionMode.LocalCli)
            {
                error = "AI整理已改为直接打开 Windows PowerShell，请把全局配置里的 API 地址留空以使用本地 CLI。";
                return false;
            }

            if (!PsdHierarchyAiCliDiscovery.TryGetInstalled(settings.provider, out PsdHierarchyAiCliDescriptor cli))
            {
                error = "全局配置选择的 " + PsdHierarchyChatClient.GetProviderDisplayName(settings.provider) +
                    " CLI 当前不可用。\n\n请先安装该 CLI 并重启 Unity，或打开全局配置改选其它模型。";
                return false;
            }

            try
            {
                PsdHierarchyChatConnection connection = new PsdHierarchyChatConnection(
                    settings.provider, settings.connectionMode, cli.executablePath,
                    string.Empty, settings.customModel, string.Empty, settings.ResolveReasoningEffort());
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
                // 模型与思考程度都留空时不生成任何参数，直接使用 CLI 自身的配置。
                string optionArguments = PsdHierarchyChatClient.BuildModelAndEffortArguments(
                    connection,
                    value => "'" + value.Replace("'", "''") + "'");
                string command = "Set-Location -LiteralPath '" + context.projectRoot.Replace("'", "''") +
                    "'; & '" + cliPath + "'" + optionArguments + " '" + initialPrompt.Replace("'", "''") + "'";
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
        /// 构建一份可交给任意外部 CLI / 桌面 AI 执行的提示词并复制到剪贴板。
        /// 不依赖 Unity 侧打开的 AI 终端会话，也不要求先配置本机 AI 模型；
        /// 提示词里引用的每个路径都是绝对路径，工具从哪个工作目录启动都能读到。
        /// </summary>
        public static bool TryCopyAiPrompt(
            string sourcePsdAssetPath,
            out PsdHierarchyExternalPrompt externalPrompt,
            out string error)
        {
            externalPrompt = null;
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

                // 与内置「AI整理」终端共用同一个目录，「应用AI计划」按写入时间取最新的一份计划。
                string outputDirectory = Path.Combine(context.projectRoot, "Library", "PsdHierarchyTerminal");
                Directory.CreateDirectory(outputDirectory);
                string sessionId = "external-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 6);
                string promptFullPath = Path.Combine(outputDirectory, sessionId + ".prompt.md");
                string planFullPath = Path.Combine(outputDirectory, sessionId + ".plan.json");
                string reviewFullPath = Path.Combine(outputDirectory, sessionId + ".review.md");

                string prompt = PsdHierarchyChatClient.BuildExternalSessionPrompt(
                    context,
                    planFullPath,
                    reviewFullPath);
                // 长提示词同时落盘：剪贴板粘贴失败或需要留档时，可以直接把文件路径交给 CLI。
                File.WriteAllText(promptFullPath, prompt, new UTF8Encoding(false));
                EditorGUIUtility.systemCopyBuffer = prompt;
                externalPrompt = new PsdHierarchyExternalPrompt(
                    prompt,
                    promptFullPath,
                    planFullPath,
                    reviewFullPath);
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

    /// <summary>
    /// 一次「AI提示词复制」的产物：提示词正文，以及它引用/要求的全部绝对路径。
    /// </summary>
    public sealed class PsdHierarchyExternalPrompt
    {
        internal PsdHierarchyExternalPrompt(
            string text,
            string promptFullPath,
            string planFullPath,
            string reviewFullPath)
        {
            this.text = text ?? string.Empty;
            this.promptFullPath = promptFullPath ?? string.Empty;
            this.planFullPath = planFullPath ?? string.Empty;
            this.reviewFullPath = reviewFullPath ?? string.Empty;
        }

        /// <summary>
        /// 提示词正文，已写入剪贴板。
        /// </summary>
        public readonly string text;

        /// <summary>
        /// 提示词落盘的绝对路径，可直接把该文件交给 CLI 读取。
        /// </summary>
        public readonly string promptFullPath;

        /// <summary>
        /// 外部工具必须写入的 v2 计划绝对路径，「应用AI计划」会读取它。
        /// </summary>
        public readonly string planFullPath;

        /// <summary>
        /// 外部工具必须写入的中文复核绝对路径。
        /// </summary>
        public readonly string reviewFullPath;
    }
}
