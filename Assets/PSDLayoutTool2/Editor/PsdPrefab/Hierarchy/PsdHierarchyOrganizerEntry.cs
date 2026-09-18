namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Resolves the generated Prefab and starts an interactive AI terminal with its context.
    /// </summary>
    public static class PsdHierarchyOrganizerEntry
    {
        public const string AiButtonLabel = "AI整理";
        public const string CopyPromptButtonLabel = "AI提示词复制";

        /// <summary>
        /// 写入 *.session.json，供 Apply 哨兵监听还原 PSD/Prefab 上下文。
        /// </summary>
        internal static void WriteTerminalSession(
            string sessionId,
            string sourcePsdAssetPath,
            string targetPrefabPath,
            string planFullPath,
            string reviewFullPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("无法解析 Unity 工程根目录。");
            }

            string directory = Path.Combine(projectRoot, "Library", "PsdHierarchyTerminal");
            Directory.CreateDirectory(directory);
            var record = new PsdHierarchyTerminalApplyWatcher.SessionRecord
            {
                version = PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion,
                sessionId = sessionId,
                sourcePsdAssetPath = sourcePsdAssetPath,
                targetPrefabPath = targetPrefabPath,
                planPath = planFullPath,
                reviewPath = reviewFullPath,
            };
            File.WriteAllText(
                Path.Combine(directory, sessionId + ".session.json"),
                Newtonsoft.Json.JsonConvert.SerializeObject(record, Newtonsoft.Json.Formatting.Indented),
                new UTF8Encoding(false));
        }

        /// <summary>Apply 哨兵绝对路径：AI 在人工审核通过后写入此文件触发 Unity 自动应用。</summary>
        internal static string BuildApplySentinelPath(string planFullPath)
        {
            if (string.IsNullOrWhiteSpace(planFullPath))
            {
                throw new ArgumentException("计划路径不能为空。", nameof(planFullPath));
            }

            if (planFullPath.EndsWith(".plan.json", StringComparison.OrdinalIgnoreCase))
            {
                return planFullPath.Substring(0, planFullPath.Length - ".plan.json".Length) + ".apply";
            }

            return planFullPath + ".apply";
        }

        /// <summary>
        /// 终端会话契约：审核、提交、回执与失败状态处理。独立成方法便于回归断言，
        /// 不改变 TryOpenChat 实际写入的提示词正文。
        /// </summary>
        internal static string BuildTerminalSessionContract(
            string planPath,
            string reviewPath,
            string applyPath,
            string applyResultPath)
        {
            return
                "\n\n===== TERMINAL SESSION CONTRACT =====\n" +
                "This is an analysis and plan session. Do not claim that Unity assets were changed.\n" +
                "Write the complete executable JSON plan (and no partial patch) to: " + planPath.Replace('\\', '/') + "\n" +
                "The plan must be version 2 using node:<id> references from the snapshot. Do not write version 1 path plans.\n" +
                "Do not call any Python renderer or CLI runner: their write modes are retired, and Unity applies the reviewed plan itself after .apply.\n" +
                "EXECUTABLE OPERATIONS: wrappers, moves, renames, tightBounds, emptyContainerRemovals, componentExtractions, stateComponentExtractions, variantComponentExtractions, statefulComponentExtractions, textureRenames, spriteAtlasRenames and postGroupingExtractionIntents are executable. " +
                "For componentExtractions use {id, name, assetPath, template: node:<id>, instances: [node:<id>...]}; the template must also appear in instances, every instance must share the template's recursive component structure, and assetPath must be a NEW PascalCase .prefab under Assets/. " +
                "Every requiresExtraction:true snapshot candidate must have exactly one componentFamilyDecisions entry. Its parent and sources must exactly match the candidate; recommendedMode is advisory only; mode must be component|state|variant|stateful and must match the actual extraction list or postGroupingExtractionIntents entry named by extractionId. That extraction's sources must fully cover the candidate. When a mandatory candidate only becomes extractable after the grouping you just planned, Unity revalidates the refreshed candidate before performing that extraction in the second stage. " +
                "For stateComponentExtractions use {id, template: node:<id>, assetPath, defaultState, states: [{id, source: node:<id>, name}]} only for mutually exclusive direct-sibling roots in one visual slot; template must be one of states[].source, and those sources must not be referenced from outside the extracted states. " +
                "For variantComponentExtractions use {id, template: node:<id>, assetPath, commonName, statesName, defaultState, states: [{id, source: node:<id>, name}], instances: [{source: node:<id>, name, state}]} for rows visible at different list positions; every state representative must also appear once in instances, and each instance's structure must match its selected state source. " +
                "For statefulComponentExtractions use {id, template: node:<id>, assetPath, common: {source, members: [{sourceName, name}]}, states: [{id, source, name, members: [...]}], defaultState, instances: [{source, name, state, commonSourceNames, stateSourceNames}]} when repeated items share real content plus a few states; every direct child of an instance source must be mapped exactly once by commonSourceNames + stateSourceNames. " +
                "For textureRenames / spriteAtlasRenames use {from, toName, expectedGuid}: toName has no extension, every Texture toName must start with \"<prefabName>_\", every SpriteAtlas toName must equal prefabName, each from must be a private asset of the target Prefab, and the target must not exist yet. " +
                "containmentResolutions, flatSiblingResolutions, selectedPrefabExtractions and crossParentPrefabExtractions MUST stay empty arrays: Unity refuses a non-empty unsupported array before any write. " +
                "postGroupingExtractionIntents IS executable as an automatic second stage: each entry is {id, mode: component|state|variant|stateful, assetPath, templatePath, commonMembers, states, defaultState, instances: [{path, state, commonSourceNames, stateSourceNames}]}, where templatePath and every instances[].path are POST-grouping hierarchy paths (not node:<id>) resolved against a refreshed snapshot after Unity saves the grouping. mode=component uses empty states and an empty defaultState; every other mode declares states (id/name/sourcePath/members) plus a defaultState id. A mandatory candidate deferred to this stage must reference exactly one same-mode intent, and the rebuilt extraction sources must fully cover the refreshed candidate sources. " +
                "Report containment or flat-sibling suggestions in the review text only; never encode them in the executable JSON.\n" +
                "Every wrappers[].parent, moves[].source, moves[].destination, renames[].target, tightBounds[].target and emptyContainerRemovals[].source must copy an exact " +
                "node:<id> from the snapshot, or reference an earlier wrapper as @wrapperId. Never invent an id and never write a hierarchy path.\n" +
                "Write the human-readable Chinese review to: " + reviewPath.Replace('\\', '/') + "\n" +
                "After every revision, replace both files atomically or rewrite them completely.\n" +
                "Only a later Unity validation triggered by the APPLY sentinel can modify the Prefab. Unity renames .apply to .applying while it works; never write .apply twice for one request.\n" +
                "After the human reviewer explicitly approves in this conversation, write an empty file at: " +
                applyPath.Replace('\\', '/') + "\n" +
                "That .apply file is the only signal Unity needs. Do not write it before human approval.\n" +
                "After writing .apply, poll this result file (about every 2s, up to ~3 minutes): " +
                applyResultPath.Replace('\\', '/') + "\n" +
                "The result JSON has success, status, stage and message; status is one of applied, rejected, partial, uncertain.\n" +
                "- applied: Unity saved the Prefab and verified it. Report that to the human.\n" +
                "- rejected: the plan was refused BEFORE any write, so nothing changed. Do NOT rewrite the approved plan or write another .apply for this request. " +
                "Quote the FULL message to the human and stop. Any corrected plan is a new request that requires a complete new review and explicit human approval before its own .apply is written.\n" +
                "- partial or uncertain: Unity may already have written to the Prefab. Do NOT write another .apply and do NOT claim success. " +
                "Quote the full message, tell the human the on-disk Prefab must be verified, and ask for a new review before any further apply.\n" +
                "Do not claim Unity assets changed until the result file has success=true and status applied.\n";
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
                string applyPath = BuildApplySentinelPath(planPath);
                string applyResultPath = PsdHierarchyTerminalApplyWatcher.BuildResultPath(applyPath);
                WriteTerminalSession(sessionId, sourcePsdAssetPath, targetPrefabPath, planPath, reviewPath);
                string taskPrompt = PsdHierarchyChatClient.BuildPortablePrompt(context) +
                    BuildTerminalSessionContract(planPath, reviewPath, applyPath, applyResultPath);
                File.WriteAllText(promptPath, taskPrompt, new UTF8Encoding(false));
                string initialPrompt = "Read the UTF-8 task file at " + promptPath.Replace('\\', '/') +
                    ". Start the complete PSD hierarchy review now, save the review and full JSON plan to the exact paths specified in that file. After I explicitly approve, write .apply and poll .apply-result.json. If Unity rejects the plan before writing, show me the full error reason and stop; any correction must be presented as a new review request and explicitly approved before another apply. If the result is partial or uncertain, stop and tell me to verify the Prefab instead.";
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

                // 与内置「AI整理」终端共用同一个目录；Apply 哨兵监听会读 session.json 并自动应用。
                string outputDirectory = Path.Combine(context.projectRoot, "Library", "PsdHierarchyTerminal");
                Directory.CreateDirectory(outputDirectory);
                string sessionId = "external-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 6);
                string promptFullPath = Path.Combine(outputDirectory, sessionId + ".prompt.md");
                string planFullPath = Path.Combine(outputDirectory, sessionId + ".plan.json");
                string reviewFullPath = Path.Combine(outputDirectory, sessionId + ".review.md");
                string applyFullPath = BuildApplySentinelPath(planFullPath);
                WriteTerminalSession(sessionId, sourcePsdAssetPath, targetPrefabPath, planFullPath, reviewFullPath);

                string prompt = PsdHierarchyChatClient.BuildExternalSessionPrompt(
                    context,
                    planFullPath,
                    reviewFullPath,
                    applyFullPath);
                // 长提示词同时落盘：剪贴板粘贴失败或需要留档时，可以直接把文件路径交给 CLI。
                File.WriteAllText(promptFullPath, prompt, new UTF8Encoding(false));
                EditorGUIUtility.systemCopyBuffer = prompt;
                externalPrompt = new PsdHierarchyExternalPrompt(
                    prompt,
                    promptFullPath,
                    planFullPath,
                    reviewFullPath,
                    applyFullPath);
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
            string reviewFullPath,
            string applyFullPath)
        {
            this.text = text ?? string.Empty;
            this.promptFullPath = promptFullPath ?? string.Empty;
            this.planFullPath = planFullPath ?? string.Empty;
            this.reviewFullPath = reviewFullPath ?? string.Empty;
            this.applyFullPath = applyFullPath ?? string.Empty;
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
        /// 外部工具必须写入的 v2 计划绝对路径。
        /// </summary>
        public readonly string planFullPath;

        /// <summary>
        /// 外部工具必须写入的中文复核绝对路径。
        /// </summary>
        public readonly string reviewFullPath;

        /// <summary>
        /// 人工审核通过后 AI 必须写入的 Apply 哨兵路径；Unity 会自动应用，无需再点按钮。
        /// </summary>
        public readonly string applyFullPath;
    }
}
