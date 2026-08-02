namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEditor;

    internal readonly struct PsdHierarchyChatCleanupExecutionResult
    {
        internal PsdHierarchyChatCleanupExecutionResult(bool success, string message)
        {
            this.success = success;
            this.message = message ?? string.Empty;
        }

        internal readonly bool success;
        internal readonly string message;
    }

    /// <summary>
    /// Bridges a reviewed chat plan to the existing, Unity-validated cleanup
    /// runner. The AI returns data only; this class owns the local write and
    /// process execution after a user confirmation.
    /// </summary>
    internal static class PsdHierarchyChatCleanupExecution
    {
        internal const string CleanupRunnerRelativePath =
            ".agents/skills/prefab-hierarchy-cleanup/scripts/run_prefab_hierarchy_cleanup.ps1";

        private static readonly string[] RequiredArrayProperties =
        {
            "wrappers",
            "moves",
            "renames",
            "emptyContainerRemovals",
            "tightBounds",
            "textureRenames",
            "spriteAtlasRenames",
            "componentFamilyDecisions",
            "componentExtractions",
            "stateComponentExtractions",
            "variantComponentExtractions",
            "statefulComponentExtractions",
        };

        private static readonly HashSet<string> SupportedRootArrayProperties =
            new HashSet<string>(
                RequiredArrayProperties.Concat(new[]
                {
                    "requiredComponentFamilies",
                    "containmentFindings",
                    "containmentResolutions",
                    "flatSiblingFindings",
                    "flatSiblingResolutions",
                }),
                StringComparer.Ordinal);

        internal static bool IsExplicitConfirmation(string input)
        {
            string normalized = (input ?? string.Empty)
                .Trim()
                .Trim('。', '！', '!', '，', ',', '；', ';', '：', ':')
                .ToLowerInvariant();
            return normalized == "确认" ||
                   normalized == "确认执行" ||
                   normalized == "确认更新" ||
                   normalized == "确认方案" ||
                   normalized == "确定" ||
                   normalized == "可以" ||
                   normalized == "可以执行" ||
                   normalized == "好的" ||
                   normalized == "同意";
        }

        internal static bool IsApplyIntent(string input)
        {
            string normalized = (input ?? string.Empty)
                .Trim()
                .Trim('。', '！', '!', '，', ',', '；', ';', '：', ':')
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
            return IsExplicitConfirmation(input) ||
                   normalized == "修改" ||
                   normalized == "修改吧" ||
                   normalized == "修改prefab" ||
                   normalized == "修改预制体" ||
                   normalized == "执行" ||
                   normalized == "应用" ||
                   normalized == "开始修改" ||
                   normalized == "开始整理";
        }

        internal static bool TryExtractApprovedPlan(
            string assistantReply,
            string targetPrefabAssetPath,
            out string planJson,
            out string error)
        {
            planJson = ExtractJsonCodeBlock(assistantReply);
            if (string.IsNullOrWhiteSpace(planJson))
            {
                error = "AI 未返回可执行的 JSON 计划代码块。";
                return false;
            }

            try
            {
                var plan = JObject.Parse(planJson);
                ValidateRootPlanShape(plan);
                string target = NormalizeAssetPath(targetPrefabAssetPath);
                string planTarget = NormalizeAssetPath(ReadRequiredString(plan, "prefabAssetPath"));
                JObject output = plan["output"] as JObject;
                if (output == null)
                {
                    throw new InvalidDataException("计划缺少 output 对象。");
                }

                string mode = ReadRequiredString(output, "mode");
                string outputTarget = NormalizeAssetPath(ReadRequiredString(output, "assetPath"));
                if (!string.Equals(planTarget, target, StringComparison.Ordinal) ||
                    !string.Equals(outputTarget, target, StringComparison.Ordinal) ||
                    !string.Equals(mode, "in_place", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("计划没有严格指向当前目标 Prefab 的原地更新。");
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is Newtonsoft.Json.JsonException || exception is InvalidDataException)
            {
                planJson = string.Empty;
                error = "AI 返回的计划不能安全执行：" + exception.Message;
                return false;
            }
        }

        internal static bool TryExtractApprovedPlan(
            string assistantReply,
            PsdHierarchyChatContext context,
            out string planJson,
            out string error)
        {
            planJson = ExtractJsonCodeBlock(assistantReply);
            if (string.IsNullOrWhiteSpace(planJson))
            {
                error = "AI 未返回可执行的 JSON 计划代码块。";
                return false;
            }

            if (context == null)
            {
                planJson = string.Empty;
                error = "缺少当前整理目标。";
                return false;
            }

            try
            {
                var plan = JObject.Parse(planJson);
                ValidateAndNormalizeVersionTwoPlan(plan, context);
                planJson = plan.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception exception) when (exception is Newtonsoft.Json.JsonException || exception is InvalidDataException)
            {
                planJson = string.Empty;
                error = "AI 返回的计划不能安全执行：" + exception.Message;
                return false;
            }

            if (!TryPrepareRunnerPlan(context, planJson, out _, out error))
            {
                planJson = string.Empty;
                error = "AI 返回的计划不能安全执行：" + error;
                return false;
            }

            return true;
        }

        internal static bool TryPrepareRunnerPlan(
            PsdHierarchyChatContext context,
            string planJson,
            out string runnerPlanJson,
            out string error)
        {
            runnerPlanJson = string.Empty;
            if (context == null)
            {
                error = "缺少当前整理目标。";
                return false;
            }

            try
            {
                var plan = JObject.Parse(planJson ?? string.Empty);
                ValidateAndNormalizeVersionTwoPlan(plan, context);

                NormalizeSingleStateVariantExtractions(plan, context);
                NormalizeSkippedRequiredComponentCandidates(plan, context);
                NormalizeMissingStatefulExtractionTemplates(plan);
                ValidateAllExistingNodeReferences(plan, context);
                ValidateRequiredComponentFamilyDecisions(plan, context);
                ResolveExistingNodeReferences(plan, context);
                DerivePrefabName(plan, context.targetPrefabAssetPath);
                CaptureCurrentAssetRenameGuids(plan);
                NormalizeStatefulInstanceMappings(plan, context);
                NormalizeDirectChildVerificationNames(plan);
                WriteRequiredComponentFamilies(plan, context);
                WriteContainmentFindings(plan, context);
                WriteFlatSiblingFindings(plan, context);
                RemoveCandidateDecisionMetadata(plan);
                plan["version"] = 1;
                plan.Remove("snapshotFingerprint");
                runnerPlanJson = plan.ToString(Newtonsoft.Json.Formatting.None);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is Newtonsoft.Json.JsonException || exception is InvalidDataException)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ValidatePlanAsync(
            PsdHierarchyChatContext context,
            string planJson)
        {
            if (context == null)
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "缺少当前整理目标。");
            }

            if (!TryPrepareRunnerPlan(context, planJson, out string runnerPlanJson, out string preparationError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "AI 返回的节点 ID 计划无效：" + preparationError);
            }

            if (!TryValidateCurrentSnapshot(context, out string snapshotError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, snapshotError);
            }

            if (ResolveExecutionBackendForPlan(runnerPlanJson) == PsdHierarchyCleanupExecutionBackend.NativeUnity)
            {
                return await PsdHierarchyNativeCleanupExecutor.ValidateAsync(context, runnerPlanJson);
            }

            string runnerPath = ResolveRunnerPath(context);
            if (!File.Exists(runnerPath))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "找不到 Prefab 整理计划预检器：" + runnerPath);
            }

            string temporaryDirectory = Path.Combine(context.projectRoot, "Library", "PSDLayoutTool2", "HierarchyCleanupPlans");
            string temporaryId = Guid.NewGuid().ToString("N");
            string planPath = Path.Combine(temporaryDirectory, temporaryId + ".json");
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                File.WriteAllText(planPath, runnerPlanJson, new UTF8Encoding(false));
                return await Task.Run(() => ValidateWithRunner(runnerPath, context.projectRoot, planPath));
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "计划预校验失败：" + exception.Message);
            }
            finally
            {
                DeleteTemporaryFile(planPath);
            }
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ApplyConfirmedAsync(
            PsdHierarchyChatContext context,
            string planJson,
            bool replaceReplayProfile = false)
        {
            if (context == null)
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "缺少当前整理目标。");
            }

            if (!TryPrepareRunnerPlan(context, planJson, out string runnerPlanJson, out string preparationError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "已确认的节点 ID 计划无效：" + preparationError);
            }

            if (!TryValidateCurrentSnapshot(context, out string snapshotError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, snapshotError);
            }

            if (ResolveExecutionBackendForPlan(runnerPlanJson) == PsdHierarchyCleanupExecutionBackend.NativeUnity)
            {
                PsdHierarchyChatCleanupExecutionResult nativeResult =
                    await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, runnerPlanJson);
                return nativeResult.success
                    ? PersistCompletedReplayStage(
                        context,
                        runnerPlanJson,
                        nativeResult,
                        replaceReplayProfile)
                    : nativeResult;
            }

            string runnerPath = ResolveRunnerPath(context);
            if (!File.Exists(runnerPath))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "找不到 Prefab 整理执行器：" + runnerPath);
            }

            string temporaryDirectory = Path.Combine(context.projectRoot, "Library", "PSDLayoutTool2", "HierarchyCleanupPlans");
            string planPath = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                File.WriteAllText(planPath, runnerPlanJson, new UTF8Encoding(false));
                PsdHierarchyChatCleanupExecutionResult result =
                    await Task.Run(() => RunCleanup(runnerPath, context.projectRoot, planPath));
                if (!result.success)
                {
                    return new PsdHierarchyChatCleanupExecutionResult(
                        false,
                        result.message + Environment.NewLine +
                        "本次失败计划没有写入重放 Profile；将基于当前 Prefab 重新分析并生成新计划。");
                }

                return PersistCompletedReplayStage(
                    context,
                    runnerPlanJson,
                    result,
                    replaceReplayProfile);
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "更新 Prefab 失败：" + exception.Message);
            }
            finally
            {
                if (File.Exists(planPath))
                {
                    File.Delete(planPath);
                }
            }
        }

        internal static bool TryDiscardFailedReplayStage(
            PsdHierarchyChatContext context,
            string planJson,
            out string error)
        {
            error = string.Empty;
            if (context == null || !TryPrepareRunnerPlan(context, planJson, out string runnerPlanJson, out error))
                return false;

            return PsdHierarchyCleanupReplayProfile.TryDiscardMatchingLastStage(
                context.sourcePsdAssetPath,
                context.targetPrefabAssetPath,
                runnerPlanJson,
                out error);
        }

        private static PsdHierarchyChatCleanupExecutionResult PersistCompletedReplayStage(
            PsdHierarchyChatContext context,
            string runnerPlanJson,
            PsdHierarchyChatCleanupExecutionResult result,
            bool replaceReplayProfile)
        {
            try
            {
                if (replaceReplayProfile)
                {
                    PsdHierarchyCleanupReplayProfile.ReplaceWithFirstStage(
                        context.sourcePsdAssetPath,
                        context.targetPrefabAssetPath,
                        runnerPlanJson);
                }
                else
                {
                    PsdHierarchyCleanupReplayProfile.Persist(
                        context.sourcePsdAssetPath,
                        context.targetPrefabAssetPath,
                        runnerPlanJson);
                }
                return result;
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatCleanupExecutionResult(
                    true,
                    result.message + Environment.NewLine +
                    "Prefab 已更新，但整理重放 Profile 保存失败：" + exception.Message);
            }
        }

        private static PsdHierarchyChatCleanupExecutionResult RunCleanup(
            string runnerPath,
            string projectRoot,
            string planPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(runnerPath) +
                            " -ProjectPath " + Quote(projectRoot) +
                            " -PlanPath " + Quote(planPath) +
                            " -ApplyConfirmed",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = Encoding.Default,
                CreateNoWindow = true,
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return new PsdHierarchyChatCleanupExecutionResult(false, "无法启动 Prefab 整理执行器。");
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);

                string output = outputTask.Result.Trim();
                string error = errorTask.Result.Trim();
                string detail = string.IsNullOrEmpty(error)
                    ? output
                    : string.IsNullOrEmpty(output) ? error : output + Environment.NewLine + error;
                if (process.ExitCode == 0)
                {
                    return new PsdHierarchyChatCleanupExecutionResult(
                        true,
                        "Prefab 已更新。" + Environment.NewLine + detail);
                }

                return new PsdHierarchyChatCleanupExecutionResult(
                    false,
                    "Prefab 更新失败：" + SummarizeFailure(detail) +
                    "。请不要直接重复确认；先重新分析并生成新计划。");
            }
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ReapplyPersistedPlanAsync(
            string projectRoot,
            string runnerPlanJson)
        {
            if (ResolveExecutionBackendForPlan(runnerPlanJson) == PsdHierarchyCleanupExecutionBackend.NativeUnity)
            {
                return await PsdHierarchyNativeCleanupExecutor.ReapplyAsync(projectRoot, runnerPlanJson);
            }

            string runnerPath = ResolveRunnerPath(projectRoot);
            if (!File.Exists(runnerPath))
                return new PsdHierarchyChatCleanupExecutionResult(false, "Prefab cleanup replay runner was not found: " + runnerPath);

            string temporaryDirectory = Path.Combine(
                projectRoot,
                "Library",
                "PSDLayoutTool2",
                "HierarchyCleanupReplayPlans");
            string planPath = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                File.WriteAllText(planPath, runnerPlanJson, new UTF8Encoding(false));
                return await Task.Run(() => RunReplay(runnerPath, projectRoot, planPath));
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "Prefab cleanup replay failed: " + exception.Message);
            }
            finally
            {
                DeleteTemporaryFile(planPath);
            }
        }

        private static PsdHierarchyChatCleanupExecutionResult RunReplay(
            string runnerPath,
            string projectRoot,
            string planPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(runnerPath) +
                            " -ProjectPath " + Quote(projectRoot) +
                            " -PlanPath " + Quote(planPath) +
                            " -Reapply",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = Encoding.Default,
                CreateNoWindow = true,
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    return new PsdHierarchyChatCleanupExecutionResult(false, "Could not start the Prefab cleanup replay runner.");

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);

                string output = outputTask.Result.Trim();
                string error = errorTask.Result.Trim();
                string detail = string.IsNullOrEmpty(error)
                    ? output
                    : string.IsNullOrEmpty(output) ? error : output + Environment.NewLine + error;
                return process.ExitCode == 0
                    ? new PsdHierarchyChatCleanupExecutionResult(true, detail)
                    : new PsdHierarchyChatCleanupExecutionResult(
                        false,
                        "Prefab cleanup replay failed: " + SummarizeFailure(detail));
            }
        }

        private static PsdHierarchyChatCleanupExecutionResult ValidateWithRunner(
            string runnerPath,
            string projectRoot,
            string planPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(runnerPath) +
                            " -ProjectPath " + Quote(projectRoot) +
                            " -PlanPath " + Quote(planPath) +
                            " -Preflight",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = Encoding.Default,
                CreateNoWindow = true,
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return new PsdHierarchyChatCleanupExecutionResult(false, "无法启动 Prefab 整理计划预检器。");
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);

                if (process.ExitCode == 0)
                {
                    return new PsdHierarchyChatCleanupExecutionResult(true, string.Empty);
                }

                string output = outputTask.Result.Trim();
                string error = errorTask.Result.Trim();
                string detail = string.IsNullOrEmpty(error)
                    ? output
                    : string.IsNullOrEmpty(output) ? error : output + Environment.NewLine + error;

                return new PsdHierarchyChatCleanupExecutionResult(
                    false,
                    "AI 返回的计划未通过源路径预检：" + SummarizeFailure(detail));
            }
        }

        private static PsdHierarchyCleanupExecutionBackend ResolveExecutionBackend()
        {
            PsdHierarchyCleanupExecutionSettingsSnapshot settings =
                PsdLayoutProjectSettings.instance.ResolveHierarchyCleanupExecutionSettings();
            if (!settings.TryValidate(out string error))
            {
                throw new InvalidOperationException("Invalid Prefab cleanup execution backend: " + error);
            }

            return settings.backend;
        }

        private static PsdHierarchyCleanupExecutionBackend ResolveExecutionBackendForPlan(string runnerPlanJson)
        {
            return ResolveExecutionBackendForPlan(ResolveExecutionBackend(), runnerPlanJson);
        }

        internal static PsdHierarchyCleanupExecutionBackend ResolveExecutionBackendForPlan(
            PsdHierarchyCleanupExecutionBackend selectedBackend,
            string runnerPlanJson)
        {
            return selectedBackend;
        }

        internal static string ExtractReviewText(string assistantReply)
        {
            string content = (assistantReply ?? string.Empty).Trim();
            int marker = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            return marker < 0 ? content : content.Substring(0, marker).Trim();
        }

        internal static string ComposeReviewableReply(string reviewText, string planJson)
        {
            string review = (reviewText ?? string.Empty).Trim();
            string json = (planJson ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(json))
            {
                return review;
            }

            string codeBlock = "```json\n" + json + "\n```";
            return string.IsNullOrEmpty(review) ? codeBlock : review + "\n\n" + codeBlock;
        }

        private static string ExtractJsonCodeBlock(string value)
        {
            string content = value ?? string.Empty;
            int marker = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                return string.Empty;
            }

            int start = content.IndexOf('\n', marker);
            if (start < 0)
            {
                return string.Empty;
            }

            int end = content.IndexOf("```", start + 1, StringComparison.Ordinal);
            return end < 0 ? string.Empty : content.Substring(start + 1, end - start - 1).Trim();
        }

        private static string ReadRequiredString(JObject owner, string name)
        {
            JToken value = owner[name];
            if (value == null || value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>()))
            {
                throw new InvalidDataException("计划缺少 " + name + "。");
            }

            return value.Value<string>();
        }

        private static void ValidateRootPlanShape(JObject plan)
        {
            ValidateRootPlanShape(plan, 1L, false);
        }

        private static void ValidateRootPlanShape(
            JObject plan,
            long expectedVersion,
            bool requireSnapshotFingerprint,
            bool requirePrefabName = true)
        {
            JToken version = plan["version"];
            if (version == null || version.Type != JTokenType.Integer || version.Value<long>() != expectedVersion)
            {
                throw new InvalidDataException("计划 version 必须为 " + expectedVersion + "。");
            }

            if (requireSnapshotFingerprint)
            {
                ReadRequiredString(plan, "snapshotFingerprint");
            }

            if (requirePrefabName)
            {
                ReadRequiredString(plan, "prefabName");
            }
            foreach (string property in RequiredArrayProperties)
            {
                if (!(plan[property] is JArray))
                {
                    throw new InvalidDataException("计划缺少数组字段 " + property + "。");
                }
            }

            foreach (JProperty property in plan.Properties())
            {
                if (property.Value is JArray array &&
                    array.Count > 0 &&
                    !SupportedRootArrayProperties.Contains(property.Name))
                {
                    throw new InvalidDataException(
                        "Unsupported non-empty plan array: " + property.Name +
                        ". Refusing to silently ignore unknown operations.");
                }
            }

            if (!(plan["verify"] is JObject))
            {
                throw new InvalidDataException("计划缺少 verify 对象。");
            }
        }

        private static void ValidatePlanTarget(JObject plan, string targetPrefabAssetPath)
        {
            string target = NormalizeAssetPath(targetPrefabAssetPath);
            string planTarget = NormalizeAssetPath(ReadRequiredString(plan, "prefabAssetPath"));
            JObject output = plan["output"] as JObject;
            if (output == null)
            {
                throw new InvalidDataException("计划缺少 output 对象。");
            }

            string mode = ReadRequiredString(output, "mode");
            string outputTarget = NormalizeAssetPath(ReadRequiredString(output, "assetPath"));
            if (!string.Equals(planTarget, target, StringComparison.Ordinal) ||
                !string.Equals(outputTarget, target, StringComparison.Ordinal) ||
                !string.Equals(mode, "in_place", StringComparison.Ordinal))
            {
                throw new InvalidDataException("计划没有严格指向当前目标 Prefab 的原地更新。");
            }
        }

        private sealed class NodeReferenceSlot
        {
            internal NodeReferenceSlot(JToken token, string label, bool allowWrapperReference)
            {
                this.token = token;
                this.label = label;
                this.allowWrapperReference = allowWrapperReference;
            }

            internal readonly JToken token;
            internal readonly string label;
            internal readonly bool allowWrapperReference;
        }

        private static void ValidateAllExistingNodeReferences(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            var slots = new List<NodeReferenceSlot>();
            var errors = new List<string>();
            AddObjectPropertySlots(plan, "wrappers", "parent", true, slots, errors);
            AddObjectPropertySlots(plan, "moves", "source", false, slots, errors);
            AddObjectPropertySlots(plan, "moves", "destination", true, slots, errors);
            AddObjectPropertySlots(plan, "renames", "target", true, slots, errors);
            AddObjectPropertySlots(plan, "emptyContainerRemovals", "source", false, slots, errors);
            AddObjectPropertySlots(plan, "tightBounds", "target", true, slots, errors);
            AddObjectPropertySlots(plan, "componentFamilyDecisions", "parent", false, slots, errors);
            AddStringArraySlots(plan, "componentFamilyDecisions", "sources", slots, errors);
            AddObjectPropertySlots(plan, "componentExtractions", "template", false, slots, errors);
            AddStringArraySlots(plan, "componentExtractions", "instances", slots, errors);
            AddObjectPropertySlots(plan, "stateComponentExtractions", "template", false, slots, errors);
            AddNestedObjectPropertySlots(plan, "stateComponentExtractions", "states", "source", slots, errors);
            AddObjectPropertySlots(plan, "variantComponentExtractions", "template", false, slots, errors);
            AddNestedObjectPropertySlots(plan, "variantComponentExtractions", "states", "source", slots, errors);
            AddNestedObjectPropertySlots(plan, "variantComponentExtractions", "instances", "source", slots, errors);
            AddObjectPropertySlots(plan, "statefulComponentExtractions", "template", false, slots, errors);
            AddNestedObjectPropertySlots(plan, "statefulComponentExtractions", "states", "source", slots, errors);
            AddNestedObjectPropertySlots(plan, "statefulComponentExtractions", "instances", "source", slots, errors);
            AddStatefulCommonSourceSlots(plan, slots, errors);

            foreach (NodeReferenceSlot slot in slots)
            {
                if (slot.token == null || slot.token.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace(slot.token.Value<string>()))
                {
                    errors.Add(slot.label + " 必须为非空 node:<id> 字符串。");
                    continue;
                }

                string reference = slot.token.Value<string>();
                if (slot.allowWrapperReference && reference.StartsWith("@", StringComparison.Ordinal))
                {
                    continue;
                }

                const string prefix = "node:";
                if (!reference.StartsWith(prefix, StringComparison.Ordinal))
                {
                    errors.Add(slot.label + " 必须使用当前快照中的 node:<id>，不能填写层级路径。");
                    continue;
                }

                string nodeId = reference.Substring(prefix.Length);
                if (string.IsNullOrWhiteSpace(nodeId) || !context.TryGetNodePath(nodeId, out _))
                {
                    errors.Add(slot.label + " 引用的节点 " + nodeId + " 在当前快照中不存在。");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    "节点引用校验失败：" + Environment.NewLine +
                    "- " + string.Join(Environment.NewLine + "- ", errors.Distinct().ToArray()));
            }
        }

        private static void ValidateRequiredComponentFamilyDecisions(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            if (context.componentFamilyCandidates == null || context.componentFamilyCandidates.Count == 0)
            {
                return;
            }

            var candidatesById = context.componentFamilyCandidates
                .ToDictionary(candidate => candidate.id, StringComparer.Ordinal);
            var requiredCandidateIds = new HashSet<string>(
                context.componentFamilyCandidates
                    .Where(candidate => candidate.requiresExtraction)
                    .Select(candidate => candidate.id),
                StringComparer.Ordinal);
            if (requiredCandidateIds.Count == 0)
            {
                return;
            }

            if (!(plan["componentFamilyDecisions"] is JArray decisions))
            {
                throw new InvalidDataException("计划缺少 componentFamilyDecisions 数组。");
            }

            var declaredCandidateIds = new HashSet<string>(StringComparer.Ordinal);
            var errors = new List<string>();
            for (int index = 0; index < decisions.Count; index++)
            {
                if (!(decisions[index] is JObject decision))
                {
                    errors.Add("componentFamilyDecisions[" + index + "] 必须为对象。");
                    continue;
                }

                string candidateId = decision.Value<string>("candidateId");
                if (string.IsNullOrWhiteSpace(candidateId))
                {
                    continue;
                }

                if (!candidatesById.TryGetValue(candidateId, out PsdHierarchyComponentFamilyCandidate candidate))
                {
                    errors.Add("componentFamilyDecisions[" + index + "].candidateId 未出现在当前快照候选中：" + candidateId);
                    continue;
                }

                if (!declaredCandidateIds.Add(candidateId))
                {
                    errors.Add("componentFamilyDecisions 重复声明候选：" + candidateId);
                    continue;
                }

                string parent = decision.Value<string>("parent");
                string[] sources = (decision["sources"] as JArray)?.Values<string>().ToArray() ?? Array.Empty<string>();
                if (!string.Equals(parent, candidate.parent, StringComparison.Ordinal) ||
                    !sources.SequenceEqual(candidate.sources, StringComparer.Ordinal))
                {
                    errors.Add(
                        "componentFamilyDecisions[" + index + "] 必须原样覆盖候选 " + candidateId +
                        " 的 parent 与完整 sources。");
                }

                if (candidate.requiresExtraction &&
                    string.Equals(decision.Value<string>("mode"), "skip", StringComparison.Ordinal))
                {
                    errors.Add("高置信重复组件候选 " + candidateId + "（" + candidate.suggestedAssetName + "）不能使用 skip；必须抽取为 component、state、variant 或 stateful Prefab。");
                }
            }

            foreach (string candidateId in requiredCandidateIds.Where(id => !declaredCandidateIds.Contains(id)))
            {
                PsdHierarchyComponentFamilyCandidate candidate = candidatesById[candidateId];
                errors.Add(
                    "componentFamilyDecisions 必须覆盖高置信候选 " + candidateId + "（" +
                    candidate.suggestedAssetName + "）。不要遗漏 " + string.Join(", ", candidate.sources) + "。");
            }

            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    "重复组件候选校验失败：" + Environment.NewLine +
                    "- " + string.Join(Environment.NewLine + "- ", errors));
            }
        }

        private static void NormalizeRequiredVariantCandidates(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            PsdHierarchyComponentFamilyCandidate[] candidates = context.componentFamilyCandidates?
                .Where(candidate => candidate != null &&
                                    candidate.requiresExtraction &&
                                    (string.Equals(candidate.recommendedMode, "variant", StringComparison.Ordinal) ||
                                     string.Equals(candidate.recommendedMode, "stateful", StringComparison.Ordinal)))
                .ToArray() ?? Array.Empty<PsdHierarchyComponentFamilyCandidate>();
            if (candidates.Length == 0)
            {
                return;
            }

            var decisions = plan["componentFamilyDecisions"] as JArray;
            var variants = plan["variantComponentExtractions"] as JArray;
            if (decisions == null || variants == null)
            {
                throw new InvalidDataException(
                    "Deterministic component-family repair failed: componentFamilyDecisions and " +
                    "variantComponentExtractions must both be arrays.");
            }

            foreach (PsdHierarchyComponentFamilyCandidate candidate in candidates)
            {
                JObject decision = decisions
                    .OfType<JObject>()
                    .FirstOrDefault(item => string.Equals(
                        item.Value<string>("candidateId"), candidate.id, StringComparison.Ordinal));

                bool statefulFallback = string.Equals(
                    candidate.recommendedMode,
                    "stateful",
                    StringComparison.Ordinal);
                if (statefulFallback && decision != null &&
                    !string.Equals(decision.Value<string>("mode"), "skip", StringComparison.Ordinal))
                {
                    continue;
                }

                // A stateful fallback intentionally keeps every observed branch whole. This
                // preserves the Prefab without inventing a Common/State member partition.
                RemoveCandidateOwnedExtractions(plan, candidate, decision?.Value<string>("extractionId"));

                string extractionId = CreateUniqueExtractionId(plan, candidate.suggestedAssetName + "Variant");
                string assetPath = CreateAvailableVariantAssetPath(plan, context, candidate.suggestedAssetName);
                JObject extraction = BuildDeterministicVariantExtraction(
                    context,
                    candidate,
                    extractionId,
                    assetPath);

                var normalizedDecision = new JObject
                {
                    ["candidateId"] = candidate.id,
                    ["parent"] = candidate.parent,
                    ["sources"] = new JArray(candidate.sources),
                    ["mode"] = "variant",
                    ["extractionId"] = extractionId,
                    ["reason"] = statefulFallback
                        ? "Deterministically repaired from the authoritative snapshot; the skipped stateful " +
                          "family falls back to complete observed variant branches."
                        : "Deterministically repaired from the authoritative snapshot; every recursive structure " +
                          "maps to an observed variant state.",
                };
                if (decision == null)
                {
                    decisions.Add(normalizedDecision);
                }
                else
                {
                    decision.Replace(normalizedDecision);
                }

                variants.Add(extraction);
            }
        }

        private static void NormalizeSkippedRequiredComponentCandidates(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            PsdHierarchyComponentFamilyCandidate[] candidates = context.componentFamilyCandidates?
                .Where(candidate => candidate != null &&
                                    candidate.requiresExtraction &&
                                    string.Equals(candidate.recommendedMode, "component", StringComparison.Ordinal))
                .ToArray() ?? Array.Empty<PsdHierarchyComponentFamilyCandidate>();
            if (candidates.Length == 0)
            {
                return;
            }

            var decisions = plan["componentFamilyDecisions"] as JArray;
            var extractions = plan["componentExtractions"] as JArray;
            if (decisions == null || extractions == null)
            {
                throw new InvalidDataException(
                    "Deterministic component-family repair failed: componentFamilyDecisions and " +
                    "componentExtractions must both be arrays.");
            }

            foreach (PsdHierarchyComponentFamilyCandidate candidate in candidates)
            {
                JObject decision = decisions
                    .OfType<JObject>()
                    .FirstOrDefault(item => string.Equals(
                        item.Value<string>("candidateId"), candidate.id, StringComparison.Ordinal));
                if (decision != null &&
                    !string.Equals(decision.Value<string>("mode"), "skip", StringComparison.Ordinal))
                {
                    continue;
                }

                RemoveCandidateOwnedExtractions(plan, candidate, decision?.Value<string>("extractionId"));
                string extractionId = CreateUniqueExtractionId(plan, candidate.suggestedAssetName);
                string assetPath = CreateAvailableComponentAssetPath(
                    plan,
                    context,
                    candidate.suggestedAssetName);
                var normalizedDecision = new JObject
                {
                    ["candidateId"] = candidate.id,
                    ["parent"] = candidate.parent,
                    ["sources"] = new JArray(candidate.sources),
                    ["mode"] = "component",
                    ["extractionId"] = extractionId,
                    ["reason"] =
                        "Deterministically repaired from the authoritative snapshot; all sources " +
                        "were classified as one identical component structure.",
                };
                if (decision == null)
                {
                    decisions.Add(normalizedDecision);
                }
                else
                {
                    decision.Replace(normalizedDecision);
                }

                extractions.Add(new JObject
                {
                    ["id"] = extractionId,
                    ["template"] = candidate.sources[0],
                    ["assetPath"] = assetPath,
                    ["instances"] = new JArray(candidate.sources),
                });
            }
        }

        private static JObject BuildDeterministicVariantExtraction(
            PsdHierarchyChatContext context,
            PsdHierarchyComponentFamilyCandidate candidate,
            string extractionId,
            string assetPath)
        {
            JObject snapshot;
            try
            {
                snapshot = JObject.Parse(context.hierarchySnapshotJson);
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                throw BuildDeterministicVariantRepairError(candidate, "snapshot JSON is invalid", exception);
            }

            var nodes = snapshot["nodes"] as JArray;
            if (nodes == null)
            {
                throw BuildDeterministicVariantRepairError(candidate, "snapshot.nodes is missing", null);
            }

            var nodeById = nodes
                .OfType<JObject>()
                .Where(node => !string.IsNullOrWhiteSpace(node.Value<string>("id")))
                .ToDictionary(node => node.Value<string>("id"), StringComparer.Ordinal);
            var childrenByParentId = nodes
                .OfType<JObject>()
                .Where(node => !string.IsNullOrWhiteSpace(node.Value<string>("parentId")))
                .GroupBy(node => node.Value<string>("parentId"), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(node => node.Value<int?>("siblingIndex") ?? int.MaxValue).ToList(),
                    StringComparer.Ordinal);

            var stateIdBySignature = new Dictionary<string, string>(StringComparer.Ordinal);
            var states = new JArray();
            var instances = new JArray();
            IReadOnlyDictionary<string, string> instanceNamesBySource =
                BuildDeterministicVariantInstanceNames(context, candidate, nodeById);
            foreach (string source in candidate.sources)
            {
                string nodeId = ReadNodeId(source, candidate);
                if (!nodeById.TryGetValue(nodeId, out JObject node))
                {
                    throw BuildDeterministicVariantRepairError(
                        candidate,
                        "source " + source + " is missing from snapshot.nodes",
                        null);
                }

                string signature = BuildSnapshotStructureSignature(nodeId, nodeById, childrenByParentId);
                if (!stateIdBySignature.TryGetValue(signature, out string stateId))
                {
                    stateId = "state_" + (stateIdBySignature.Count + 1);
                    stateIdBySignature.Add(signature, stateId);
                    states.Add(new JObject
                    {
                        ["id"] = stateId,
                        ["source"] = source,
                        ["name"] = "[State_" + stateIdBySignature.Count + "]",
                    });
                }

                instances.Add(new JObject
                {
                    ["source"] = source,
                    ["name"] = instanceNamesBySource[source],
                    ["state"] = stateId,
                });
            }

            if (states.Count < 2)
            {
                throw BuildDeterministicVariantRepairError(
                    candidate,
                    "recommendedMode=variant but fewer than two distinct recursive structures were observed",
                    null);
            }

            return new JObject
            {
                ["id"] = extractionId,
                ["template"] = candidate.sources[0],
                ["assetPath"] = assetPath,
                ["commonName"] = "[Common]",
                ["statesName"] = "[States]",
                ["defaultState"] = states[0].Value<string>("id"),
                ["states"] = states,
                ["instances"] = instances,
            };
        }

        private static IReadOnlyDictionary<string, string> BuildDeterministicVariantInstanceNames(
            PsdHierarchyChatContext context,
            PsdHierarchyComponentFamilyCandidate candidate,
            IReadOnlyDictionary<string, JObject> nodeById)
        {
            var observedNames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string source in candidate.sources)
            {
                string nodeId = ReadNodeId(source, candidate);
                if (!nodeById.TryGetValue(nodeId, out JObject node))
                {
                    throw BuildDeterministicVariantRepairError(
                        candidate,
                        "source " + source + " is missing from snapshot.nodes while deriving instance names",
                        null);
                }

                string observedName = node.Value<string>("name");
                if (string.IsNullOrWhiteSpace(observedName) && context.TryGetNodePath(nodeId, out string path))
                {
                    int separator = path.LastIndexOf('/');
                    observedName = separator >= 0 ? path.Substring(separator + 1) : path;
                }

                if (string.IsNullOrWhiteSpace(observedName))
                {
                    throw BuildDeterministicVariantRepairError(
                        candidate,
                        "source " + source + " has no observed node name",
                        null);
                }

                observedNames.Add(source, observedName);
            }

            var instanceNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string source in candidate.sources)
            {
                string observedName = observedNames[source];
                if (IsBracketedSemanticItemName(observedName) && usedNames.Add(observedName))
                {
                    instanceNames.Add(source, observedName);
                }
            }

            string semanticBase = candidate.suggestedAssetName;
            if (string.IsNullOrWhiteSpace(semanticBase) ||
                !Regex.IsMatch(semanticBase, "^[A-Za-z][A-Za-z0-9]*$"))
            {
                throw BuildDeterministicVariantRepairError(
                    candidate,
                    "suggestedAssetName=" + (semanticBase ?? "<null>") +
                    " cannot produce a bracketed English semantic item name",
                    null);
            }

            int suffix = 1;
            foreach (string source in candidate.sources)
            {
                if (instanceNames.ContainsKey(source))
                {
                    continue;
                }

                string generatedName;
                do
                {
                    generatedName = "[" + semanticBase + "_" + suffix + "]";
                    suffix++;
                }
                while (!usedNames.Add(generatedName));

                instanceNames.Add(source, generatedName);
            }

            return instanceNames;
        }

        private static bool IsBracketedSemanticItemName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Length >= 3 &&
                   value[0] == '[' &&
                   value[value.Length - 1] == ']';
        }

        private static InvalidDataException BuildDeterministicVariantRepairError(
            PsdHierarchyComponentFamilyCandidate candidate,
            string reason,
            Exception innerException)
        {
            string message =
                "Deterministic component-family repair failed: candidateId=" + candidate.id +
                "; asset=" + candidate.suggestedAssetName +
                "; recommendedMode=" + candidate.recommendedMode +
                "; sources=" + string.Join(",", candidate.sources) +
                "; reason=" + reason + ".";
            return innerException == null
                ? new InvalidDataException(message)
                : new InvalidDataException(message, innerException);
        }

        private static string ReadNodeId(
            string source,
            PsdHierarchyComponentFamilyCandidate candidate)
        {
            const string prefix = "node:";
            if (string.IsNullOrWhiteSpace(source) || !source.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw BuildDeterministicVariantRepairError(
                    candidate,
                    "source reference is not node:<id>: " + (source ?? "<null>"),
                    null);
            }

            return source.Substring(prefix.Length);
        }

        private static string BuildSnapshotStructureSignature(
            string nodeId,
            IReadOnlyDictionary<string, JObject> nodeById,
            IReadOnlyDictionary<string, List<JObject>> childrenByParentId)
        {
            if (!nodeById.TryGetValue(nodeId, out JObject node))
            {
                return string.Empty;
            }

            string components = string.Join(",", (node["components"] as JArray)?.Values<string>() ??
                                                  Enumerable.Empty<string>());
            if (!childrenByParentId.TryGetValue(nodeId, out List<JObject> children) || children.Count == 0)
            {
                return "(" + components + ")";
            }

            return "(" + components + "[" + string.Join(",", children.Select(child =>
                BuildSnapshotStructureSignature(
                    child.Value<string>("id"),
                    nodeById,
                    childrenByParentId))) + "])";
        }

        private static void RemoveCandidateOwnedExtractions(
            JObject plan,
            PsdHierarchyComponentFamilyCandidate candidate,
            string extractionId)
        {
            foreach (string propertyName in new[]
                     {
                         "componentExtractions",
                         "stateComponentExtractions",
                         "variantComponentExtractions",
                         "statefulComponentExtractions",
                     })
            {
                if (!(plan[propertyName] is JArray entries))
                {
                    continue;
                }

                for (int index = entries.Count - 1; index >= 0; index--)
                {
                    if (!(entries[index] is JObject entry))
                    {
                        continue;
                    }

                    bool idMatches = !string.IsNullOrWhiteSpace(extractionId) &&
                                     string.Equals(entry.Value<string>("id"), extractionId, StringComparison.Ordinal);
                    if (idMatches || ExtractionSourcesMatch(entry, propertyName, candidate.sources))
                    {
                        entries.RemoveAt(index);
                    }
                }
            }
        }

        private static bool ExtractionSourcesMatch(
            JObject extraction,
            string propertyName,
            IReadOnlyList<string> candidateSources)
        {
            IEnumerable<string> sources;
            if (string.Equals(propertyName, "componentExtractions", StringComparison.Ordinal))
            {
                sources = (extraction["instances"] as JArray)?.Values<string>() ?? Enumerable.Empty<string>();
            }
            else if (string.Equals(propertyName, "stateComponentExtractions", StringComparison.Ordinal))
            {
                sources = (extraction["states"] as JArray)?.OfType<JObject>()
                    .Select(state => state.Value<string>("source")) ?? Enumerable.Empty<string>();
            }
            else
            {
                sources = (extraction["instances"] as JArray)?.OfType<JObject>()
                    .Select(instance => instance.Value<string>("source")) ?? Enumerable.Empty<string>();
            }

            return sources.SequenceEqual(candidateSources, StringComparer.Ordinal);
        }

        private static string CreateUniqueExtractionId(JObject plan, string suggestedName)
        {
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string propertyName in new[]
                     {
                         "componentExtractions",
                         "stateComponentExtractions",
                         "variantComponentExtractions",
                         "statefulComponentExtractions",
                     })
            {
                if (plan[propertyName] is JArray entries)
                {
                    foreach (string id in entries.OfType<JObject>()
                                 .Select(entry => entry.Value<string>("id"))
                                 .Where(id => !string.IsNullOrWhiteSpace(id)))
                    {
                        usedIds.Add(id);
                    }
                }
            }

            string baseId = ToLowerSnakeCaseIdentifier(suggestedName);
            string candidateId = baseId;
            int suffix = 2;
            while (usedIds.Contains(candidateId))
            {
                candidateId = baseId + "_" + suffix;
                suffix++;
            }

            return candidateId;
        }

        private static string ToLowerSnakeCaseIdentifier(string value)
        {
            string separated = Regex.Replace(value ?? string.Empty, "([a-z0-9])([A-Z])", "$1_$2");
            string normalized = Regex.Replace(separated, "[^A-Za-z0-9]+", "_")
                .Trim(new[] { '_' })
                .ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = "component_variant";
            }

            if (char.IsDigit(normalized[0]))
            {
                normalized = "component_" + normalized;
            }

            return normalized;
        }

        private static string CreateAvailableVariantAssetPath(
            JObject plan,
            PsdHierarchyChatContext context,
            string suggestedAssetName)
        {
            return CreateAvailableComponentAssetPath(
                plan,
                context,
                string.IsNullOrWhiteSpace(suggestedAssetName)
                    ? "ComponentVariant"
                    : suggestedAssetName + "Variant");
        }

        private static string CreateAvailableComponentAssetPath(
            JObject plan,
            PsdHierarchyChatContext context,
            string baseName)
        {
            string target = NormalizeAssetPath(context.targetPrefabAssetPath);
            int separator = target.LastIndexOf('/');
            if (separator <= 0)
            {
                throw new InvalidDataException(
                    "Deterministic component-family repair failed: target Prefab has no asset directory: " + target);
            }

            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string propertyName in new[]
                     {
                         "componentExtractions",
                         "stateComponentExtractions",
                         "variantComponentExtractions",
                         "statefulComponentExtractions",
                     })
            {
                if (plan[propertyName] is JArray entries)
                {
                    foreach (string path in entries.OfType<JObject>()
                                 .Select(entry => NormalizeAssetPath(entry.Value<string>("assetPath")))
                                 .Where(path => !string.IsNullOrWhiteSpace(path)))
                    {
                        usedPaths.Add(path);
                    }
                }
            }

            string directory = target.Substring(0, separator) + "/Common/";
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "Component";
            }
            for (int suffix = 1; suffix <= 999; suffix++)
            {
                string assetName = suffix == 1 ? baseName : baseName + suffix;
                string assetPath = directory + assetName + ".prefab";
                string fullPath = Path.Combine(
                    context.projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar));
                if (!usedPaths.Contains(assetPath) &&
                    !File.Exists(fullPath) &&
                    !File.Exists(fullPath + ".meta"))
                {
                    return assetPath;
                }
            }

            throw new InvalidDataException(
                "Deterministic component-family repair failed: no unused Common Prefab asset path for " +
                baseName + ".");
        }

        private static void NormalizeSingleStateVariantExtractions(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            if (!(plan?["variantComponentExtractions"] is JArray variants) || variants.Count == 0)
            {
                return;
            }

            if (!(plan["componentExtractions"] is JArray componentExtractions))
            {
                throw new InvalidDataException("componentExtractions must be an array.");
            }

            JArray decisions = plan["componentFamilyDecisions"] as JArray;
            for (int index = variants.Count - 1; index >= 0; index--)
            {
                if (!(variants[index] is JObject variant) ||
                    !(variant["states"] is JArray states) ||
                    states.Count != 1 ||
                    !(states[0] is JObject state) ||
                    !(variant["instances"] is JArray instances) ||
                    instances.Count < 2)
                {
                    continue;
                }

                string stateId = state.Value<string>("id");
                string extractionId = variant.Value<string>("id");
                string template = variant.Value<string>("template");
                if (string.IsNullOrWhiteSpace(stateId) ||
                    string.IsNullOrWhiteSpace(extractionId) ||
                    string.IsNullOrWhiteSpace(template) ||
                    !instances.OfType<JObject>().All(instance =>
                        string.Equals(instance.Value<string>("state"), stateId, StringComparison.Ordinal)))
                {
                    continue;
                }

                JToken[] instanceSources = instances
                    .OfType<JObject>()
                    .Select(instance => instance["source"]?.DeepClone())
                    .ToArray();
                if (instanceSources.Length != instances.Count ||
                    instanceSources.Any(source => source == null || source.Type != JTokenType.String) ||
                    !instanceSources.Any(source => string.Equals(
                        source.Value<string>(), template, StringComparison.Ordinal)))
                {
                    continue;
                }

                PsdHierarchyComponentFamilyCandidate requiredCandidate = FindMatchingRequiredCandidate(
                    context,
                    instanceSources);
                if (requiredCandidate != null &&
                    !string.IsNullOrWhiteSpace(requiredCandidate.recommendedMode) &&
                    !string.Equals(
                        requiredCandidate.recommendedMode,
                        "component",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "variantComponentExtractions[" + index +
                        "] has one visual state, but required candidate " +
                        requiredCandidate.id + " requires " +
                        requiredCandidate.recommendedMode +
                        " because its source structures differ. Return a complete " +
                        requiredCandidate.recommendedMode +
                        " extraction for every required source instead of downgrading it to componentExtractions.");
                }

                componentExtractions.Add(new JObject
                {
                    ["id"] = extractionId,
                    ["template"] = template,
                    ["assetPath"] = variant["assetPath"]?.DeepClone(),
                    ["instances"] = new JArray(instanceSources),
                });
                if (decisions != null)
                {
                    foreach (JObject decision in decisions.OfType<JObject>())
                    {
                        if (string.Equals(decision.Value<string>("extractionId"), extractionId, StringComparison.Ordinal) &&
                            string.Equals(decision.Value<string>("mode"), "variant", StringComparison.Ordinal))
                        {
                            decision["mode"] = "component";
                        }
                    }
                }

                variants.RemoveAt(index);
            }
        }

        private static PsdHierarchyComponentFamilyCandidate FindMatchingRequiredCandidate(
            PsdHierarchyChatContext context,
            IReadOnlyList<JToken> sources)
        {
            if (context?.componentFamilyCandidates == null || sources == null)
            {
                return null;
            }

            string[] sourceValues = sources
                .Select(source => source?.Value<string>())
                .ToArray();
            return context.componentFamilyCandidates.FirstOrDefault(candidate =>
                candidate.requiresExtraction &&
                candidate.sources.SequenceEqual(sourceValues, StringComparer.Ordinal));
        }

        private static void NormalizeDirectChildVerificationNames(JObject plan)
        {
            if (!(plan?["verify"] is JObject verify) || !(verify["directChildren"] is JArray directChildren))
            {
                return;
            }

            foreach (JObject directChildCheck in directChildren.OfType<JObject>())
            {
                if (!(directChildCheck["children"] is JArray children) ||
                    children.Any(child => child.Type != JTokenType.String || string.IsNullOrWhiteSpace(child.Value<string>())))
                {
                    continue;
                }

                var uniqueChildren = new JArray();
                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (string childName in children.Values<string>())
                {
                    if (seenNames.Add(childName))
                    {
                        uniqueChildren.Add(childName);
                    }
                }

                directChildCheck["children"] = uniqueChildren;
            }
        }

        private static void RemoveCandidateDecisionMetadata(JObject plan)
        {
            if (!(plan["componentFamilyDecisions"] is JArray decisions))
            {
                return;
            }

            foreach (JObject decision in decisions.OfType<JObject>())
            {
                decision.Remove("candidateId");
            }
        }

        private static void CaptureCurrentAssetRenameGuids(JObject plan)
        {
            foreach (string propertyName in new[] { "textureRenames", "spriteAtlasRenames" })
            {
                if (!(plan[propertyName] is JArray renames))
                {
                    continue;
                }

                for (int index = 0; index < renames.Count; index++)
                {
                    if (!(renames[index] is JObject rename))
                    {
                        throw new InvalidDataException(propertyName + "[" + index + "] must be an object.");
                    }

                    string sourcePath = ReadRequiredString(rename, "from").Replace('\\', '/');
                    if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                    {
                        throw new InvalidDataException(
                            propertyName + "[" + index + "].from asset did not load: " + sourcePath);
                    }

                    string currentGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                    if (string.IsNullOrWhiteSpace(currentGuid))
                    {
                        throw new InvalidDataException(
                            propertyName + "[" + index + "].from has no Unity GUID: " + sourcePath);
                    }

                    rename["from"] = sourcePath;
                    rename["expectedGuid"] = currentGuid;
                }
            }
        }

        private static void ValidateAndNormalizeVersionTwoPlan(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            ValidateRootPlanShape(plan, 2L, true, false);
            ValidatePlanTarget(plan, context.targetPrefabAssetPath);

            string fingerprint = ReadRequiredString(plan, "snapshotFingerprint");
            if (string.IsNullOrWhiteSpace(context.hierarchySnapshotFingerprint) ||
                !string.Equals(fingerprint, context.hierarchySnapshotFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidDataException("计划引用的层级快照已经失效，请重新分析当前 Prefab。");
            }

            DerivePrefabName(plan, context.targetPrefabAssetPath);
            ValidateRootPlanShape(plan, 2L, true);
            NormalizeRequiredVariantCandidates(plan, context);
            NormalizeSkippedRequiredComponentCandidates(plan, context);
            NormalizeMissingFlatSiblingResolutions(plan, context);
            ValidateFlatSiblingResolutions(plan, context);
        }

        private static void DerivePrefabName(JObject plan, string targetPrefabAssetPath)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            var reviewedTargets = new List<string>();

            CollectPrefabNameCandidates(
                plan,
                "textureRenames",
                true,
                candidates,
                reviewedTargets);
            CollectPrefabNameCandidates(
                plan,
                "spriteAtlasRenames",
                false,
                candidates,
                reviewedTargets);

            if (reviewedTargets.Count == 0)
            {
                string targetPrefabName = Path.GetFileNameWithoutExtension(targetPrefabAssetPath ?? string.Empty);
                if (string.IsNullOrWhiteSpace(targetPrefabName))
                {
                    throw new InvalidDataException(
                        "Cannot derive prefabName because the target Prefab path has no file name: " +
                        targetPrefabAssetPath);
                }

                plan["prefabName"] = targetPrefabName;
                return;
            }

            string submittedPrefabName = DescribeSubmittedPrefabName(plan);
            string candidateList = string.Join(", ", candidates.OrderBy(value => value, StringComparer.Ordinal));
            string targetList = string.Join("; ", reviewedTargets);
            if (candidates.Count != 1)
            {
                throw new InvalidDataException(
                    "prefabName derivation failed before runner preflight: " +
                    "reviewed asset rename targets produced conflicting candidates; " +
                    "submittedPrefabName=" + submittedPrefabName + "; " +
                    "candidates=" + candidateList + "; " +
                    "reviewedTargets=" + targetList + "; " +
                    "required=one PascalCase name ending with View.");
            }

            string derivedPrefabName = candidates.Single();
            if (!Regex.IsMatch(derivedPrefabName, "^[A-Z][A-Za-z0-9]*View$", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException(
                    "prefabName derivation failed before runner preflight: " +
                    "derived candidate does not match PascalCase and end with View; " +
                    "submittedPrefabName=" + submittedPrefabName + "; " +
                    "candidate=" + derivedPrefabName + "; " +
                    "reviewedTargets=" + targetList + "; " +
                    "required=^[A-Z][A-Za-z0-9]*View$.");
            }

            plan["prefabName"] = derivedPrefabName;
        }

        private static void CollectPrefabNameCandidates(
            JObject plan,
            string propertyName,
            bool useTexturePrefix,
            ISet<string> candidates,
            ICollection<string> reviewedTargets)
        {
            if (!(plan[propertyName] is JArray renames))
            {
                return;
            }

            for (int index = 0; index < renames.Count; index++)
            {
                if (!(renames[index] is JObject rename))
                {
                    throw new InvalidDataException(propertyName + "[" + index + "] must be an object.");
                }

                string toName = ReadRequiredString(rename, "toName");
                string label = propertyName + "[" + index + "].toName=" + toName;
                reviewedTargets.Add(label);

                if (!useTexturePrefix)
                {
                    candidates.Add(toName);
                    continue;
                }

                int separatorIndex = toName.IndexOf('_');
                if (separatorIndex <= 0)
                {
                    string submittedPrefabName = DescribeSubmittedPrefabName(plan);
                    throw new InvalidDataException(
                        "prefabName derivation failed before runner preflight: " +
                        label + " has no '<PrefabName>_' prefix; " +
                        "submittedPrefabName=" + submittedPrefabName + "; " +
                        "required=texture toName must start with one PascalCase name ending with View followed by underscore.");
                }

                candidates.Add(toName.Substring(0, separatorIndex));
            }
        }

        private static string DescribeSubmittedPrefabName(JObject plan)
        {
            JToken value = plan?["prefabName"];
            return value != null && value.Type == JTokenType.String &&
                   !string.IsNullOrWhiteSpace(value.Value<string>())
                ? value.Value<string>()
                : "<missing>";
        }

        /// <summary>
        /// Carries the authoritative snapshot candidates that must be extracted into the
        /// version 1 runner plan, so the shared plan validator can enforce the same rule
        /// without access to the chat context.
        /// </summary>
        private static void WriteRequiredComponentFamilies(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            var required = new JArray();
            if (context.componentFamilyCandidates != null)
            {
                foreach (PsdHierarchyComponentFamilyCandidate candidate in context.componentFamilyCandidates)
                {
                    if (candidate == null || !candidate.requiresExtraction)
                    {
                        continue;
                    }

                    string label = "候选家族 " + candidate.id;
                    var sources = new JArray();
                    for (int index = 0; index < candidate.sources.Count; index++)
                    {
                        sources.Add(ResolveNodeReference(
                            candidate.sources[index],
                            label + ".sources[" + index + "]",
                            context));
                    }

                    required.Add(new JObject
                    {
                        ["candidateId"] = candidate.id,
                        ["parent"] = ResolveNodeReference(candidate.parent, label + ".parent", context),
                        ["sources"] = sources,
                    });
                }
            }

            if (required.Count > 0)
            {
                plan["requiredComponentFamilies"] = required;
            }
            else
            {
                plan.Remove("requiredComponentFamilies");
            }
        }

        /// <summary>
        /// Carries the measured geometry containment findings into the version 1 runner
        /// plan so the shared validator can require an explicit resolution for each one.
        /// </summary>
        private static void WriteContainmentFindings(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            var findings = new JArray();
            foreach (JObject finding in (context.containmentFindings ?? new JArray()).OfType<JObject>())
            {
                string candidateId = finding.Value<string>("innerCandidateId") ?? string.Empty;
                string label = "几何包含结论 " + candidateId;
                var mapping = new JArray();
                foreach (JObject entry in (finding["mapping"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    mapping.Add(new JObject
                    {
                        ["source"] = ResolveNodeReference(
                            entry.Value<string>("source"), label + ".mapping.source", context),
                        ["containedBy"] = ResolveNodeReference(
                            entry.Value<string>("containedBy"), label + ".mapping.containedBy", context),
                    });
                }

                if (mapping.Count == 0)
                {
                    continue;
                }

                findings.Add(new JObject
                {
                    ["innerCandidateId"] = candidateId,
                    ["innerParent"] = ResolveNodeReference(
                        finding.Value<string>("innerParent"), label + ".innerParent", context),
                    ["mapping"] = mapping,
                });
            }

            if (findings.Count > 0)
            {
                plan["containmentFindings"] = findings;
            }
            else
            {
                plan.Remove("containmentFindings");
            }
        }

        private static void AddObjectPropertySlots(
            JObject plan,
            string arrayProperty,
            string nodeProperty,
            bool allowWrapperReference,
            List<NodeReferenceSlot> slots,
            List<string> errors)
        {
            if (!(plan[arrayProperty] is JArray items))
            {
                errors.Add(arrayProperty + " 必须为数组。");
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!(items[index] is JObject item))
                {
                    errors.Add(arrayProperty + "[" + index + "] 必须为对象。");
                    continue;
                }

                slots.Add(new NodeReferenceSlot(
                    item[nodeProperty],
                    arrayProperty + "[" + index + "]." + nodeProperty,
                    allowWrapperReference));
            }
        }

        private static void AddStringArraySlots(
            JObject plan,
            string arrayProperty,
            string referencesProperty,
            List<NodeReferenceSlot> slots,
            List<string> errors)
        {
            if (!(plan[arrayProperty] is JArray items))
            {
                errors.Add(arrayProperty + " 必须为数组。");
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!(items[index] is JObject item))
                {
                    errors.Add(arrayProperty + "[" + index + "] 必须为对象。");
                    continue;
                }

                string label = arrayProperty + "[" + index + "]." + referencesProperty;
                if (!(item[referencesProperty] is JArray references))
                {
                    errors.Add(label + " 必须为数组。");
                    continue;
                }

                for (int referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
                {
                    slots.Add(new NodeReferenceSlot(
                        references[referenceIndex],
                        label + "[" + referenceIndex + "]",
                        false));
                }
            }
        }

        private static void AddNestedObjectPropertySlots(
            JObject plan,
            string arrayProperty,
            string nestedArrayProperty,
            string nodeProperty,
            List<NodeReferenceSlot> slots,
            List<string> errors)
        {
            if (!(plan[arrayProperty] is JArray items))
            {
                errors.Add(arrayProperty + " 必须为数组。");
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!(items[index] is JObject item))
                {
                    errors.Add(arrayProperty + "[" + index + "] 必须为对象。");
                    continue;
                }

                string nestedLabel = arrayProperty + "[" + index + "]." + nestedArrayProperty;
                if (!(item[nestedArrayProperty] is JArray nestedItems))
                {
                    errors.Add(nestedLabel + " 必须为数组。");
                    continue;
                }

                for (int nestedIndex = 0; nestedIndex < nestedItems.Count; nestedIndex++)
                {
                    if (!(nestedItems[nestedIndex] is JObject nestedItem))
                    {
                        errors.Add(nestedLabel + "[" + nestedIndex + "] 必须为对象。");
                        continue;
                    }

                    slots.Add(new NodeReferenceSlot(
                        nestedItem[nodeProperty],
                        nestedLabel + "[" + nestedIndex + "]." + nodeProperty,
                        false));
                }
            }
        }

        private static void AddStatefulCommonSourceSlots(
            JObject plan,
            List<NodeReferenceSlot> slots,
            List<string> errors)
        {
            if (!(plan["statefulComponentExtractions"] is JArray items))
            {
                errors.Add("statefulComponentExtractions 必须为数组。");
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!(items[index] is JObject item))
                {
                    errors.Add("statefulComponentExtractions[" + index + "] 必须为对象。");
                    continue;
                }

                string label = "statefulComponentExtractions[" + index + "].common";
                if (!(item["common"] is JObject common))
                {
                    errors.Add(label + " 必须为对象。");
                    continue;
                }

                slots.Add(new NodeReferenceSlot(common["source"], label + ".source", false));
            }
        }

        private static void ResolveExistingNodeReferences(JObject plan, PsdHierarchyChatContext context)
        {
            ResolveObjectArray(plan, "wrappers", (item, label) =>
                ResolveNodeProperty(item, "parent", label + ".parent", context, true));
            ResolveObjectArray(plan, "moves", (item, label) =>
            {
                ResolveNodeProperty(item, "source", label + ".source", context, false);
                ResolveNodeProperty(item, "destination", label + ".destination", context, true);
            });
            ResolveObjectArray(plan, "renames", (item, label) =>
                ResolveNodeProperty(item, "target", label + ".target", context, true));
            ResolveObjectArray(plan, "emptyContainerRemovals", (item, label) =>
                ResolveNodeProperty(item, "source", label + ".source", context, false));
            ResolveObjectArray(plan, "tightBounds", (item, label) =>
                ResolveNodeProperty(item, "target", label + ".target", context, true));
            ResolveObjectArray(plan, "componentFamilyDecisions", (item, label) =>
            {
                ResolveNodeProperty(item, "parent", label + ".parent", context, false);
                ResolveNodeStringArray(item, "sources", label + ".sources", context);
            });
            ResolveObjectArray(plan, "componentExtractions", (item, label) =>
            {
                ResolveNodeProperty(item, "template", label + ".template", context, false);
                ResolveNodeStringArray(item, "instances", label + ".instances", context);
            });
            ResolveObjectArray(plan, "stateComponentExtractions", (item, label) =>
            {
                ResolveNodeProperty(item, "template", label + ".template", context, false);
                ResolveNestedNodeProperties(item, "states", "source", label + ".states", context);
            });
            ResolveObjectArray(plan, "variantComponentExtractions", (item, label) =>
            {
                ResolveNodeProperty(item, "template", label + ".template", context, false);
                ResolveNestedNodeProperties(item, "states", "source", label + ".states", context);
                ResolveNestedNodeProperties(item, "instances", "source", label + ".instances", context);
            });
            ResolveObjectArray(plan, "statefulComponentExtractions", (item, label) =>
            {
                ResolveNodeProperty(item, "template", label + ".template", context, false);
                if (!(item["common"] is JObject common))
                {
                    throw new InvalidDataException(label + ".common 必须为对象。");
                }

                ResolveNodeProperty(common, "source", label + ".common.source", context, false);
                ResolveNestedNodeProperties(item, "states", "source", label + ".states", context);
                ResolveNestedNodeProperties(item, "instances", "source", label + ".instances", context);
            });
        }

        private static void NormalizeMissingStatefulExtractionTemplates(JObject plan)
        {
            if (!(plan["statefulComponentExtractions"] is JArray extractions))
            {
                return;
            }

            foreach (JObject extraction in extractions.OfType<JObject>())
            {
                if (!string.IsNullOrWhiteSpace(extraction.Value<string>("template")) ||
                    !(extraction["instances"] is JArray instances))
                {
                    continue;
                }

                string template = instances
                    .OfType<JObject>()
                    .Select(instance => instance.Value<string>("source"))
                    .FirstOrDefault(source => !string.IsNullOrWhiteSpace(source));
                if (!string.IsNullOrWhiteSpace(template))
                {
                    extraction["template"] = template;
                }
            }
        }

        private static void NormalizeStatefulInstanceMappings(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            while (true)
            {
                try
                {
                    NormalizeStatefulInstanceMappingsStrict(plan, context);
                    return;
                }
                catch (InvalidDataException exception)
                {
                    if (!TryReplaceUnprovableStatefulExtractionWithVariant(plan, context, exception))
                    {
                        throw;
                    }
                }
            }
        }

        private static void NormalizeMissingFlatSiblingResolutions(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            JArray findings = context?.flatSiblingFindings;
            if (findings == null || findings.Count == 0)
            {
                return;
            }

            var resolutions = plan["flatSiblingResolutions"] as JArray;
            if (resolutions == null && plan["flatSiblingResolutions"] == null)
            {
                resolutions = new JArray();
                plan["flatSiblingResolutions"] = resolutions;
            }

            var wrappers = plan["wrappers"] as JArray;
            var moves = plan["moves"] as JArray;
            var tightBounds = plan["tightBounds"] as JArray;
            if (resolutions == null || wrappers == null || moves == null || tightBounds == null)
            {
                throw new InvalidDataException(
                    "Deterministic flat-sibling repair failed: flatSiblingResolutions, wrappers, " +
                    "moves, and tightBounds must all be arrays.");
            }

            var findingsById = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject finding in findings.OfType<JObject>())
            {
                string findingId = finding.Value<string>("id");
                if (string.IsNullOrWhiteSpace(findingId))
                {
                    throw new InvalidDataException(
                        "Deterministic flat-sibling repair failed: a snapshot finding has no id.");
                }

                findingsById.Add(findingId, finding);
            }

            if (findingsById.Count != findings.Count)
            {
                throw new InvalidDataException(
                    "Deterministic flat-sibling repair failed: flatSiblingFindings contains an invalid record.");
            }

            Dictionary<string, JObject> nodesById = ReadSnapshotNodes(context);
            NormalizeDeterministicFlatSiblingGroups(
                findingsById,
                nodesById,
                resolutions,
                wrappers,
                moves,
                tightBounds);

            var resolvedFindingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken resolutionToken in resolutions)
            {
                if (!(resolutionToken is JObject resolution))
                {
                    return;
                }

                string findingId = resolution.Value<string>("findingId");
                if (string.IsNullOrWhiteSpace(findingId) ||
                    !findingsById.ContainsKey(findingId) ||
                    !resolvedFindingIds.Add(findingId))
                {
                    return;
                }
            }

            JObject[] missingFindings = findingsById
                .Where(entry => !resolvedFindingIds.Contains(entry.Key))
                .Select(entry => entry.Value)
                .ToArray();
            if (missingFindings.Length == 0)
            {
                return;
            }

            var membersToGroup = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject finding in missingFindings)
            {
                string findingId = finding.Value<string>("id");
                JArray members = finding["members"] as JArray;
                if (members == null || members.Count < 3)
                {
                    throw new InvalidDataException(
                        "Deterministic flat-sibling repair failed: " + findingId +
                        " has no complete member list.");
                }

                foreach (JToken memberToken in members)
                {
                    string member = memberToken?.Value<string>();
                    if (string.IsNullOrWhiteSpace(member) || !membersToGroup.Add(member))
                    {
                        throw new InvalidDataException(
                            "Deterministic flat-sibling repair failed: " + findingId +
                            " has an invalid or overlapping member list.");
                    }
                }
            }

            JObject conflictingMove = moves
                .OfType<JObject>()
                .FirstOrDefault(move => membersToGroup.Contains(move.Value<string>("source") ?? string.Empty));
            if (conflictingMove != null)
            {
                throw new InvalidDataException(
                    "Deterministic flat-sibling repair refused because the AI plan already moves " +
                    conflictingMove.Value<string>("source") +
                    "; it cannot safely auto-group the affected flat sibling finding.");
            }

            var usedWrapperIds = new HashSet<string>(
                wrappers.OfType<JObject>()
                    .Select(wrapper => wrapper.Value<string>("id"))
                    .Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
            var generatedWrappers = new JArray();
            var generatedMoves = new JArray();
            var generatedTightBounds = new JArray();
            var generatedResolutions = new JArray();
            foreach (JObject finding in missingFindings)
            {
                string findingId = finding.Value<string>("id");
                string parent = finding.Value<string>("parent");
                string background = finding.Value<string>("background");
                JArray members = finding["members"] as JArray;
                if (string.IsNullOrWhiteSpace(parent) ||
                    string.IsNullOrWhiteSpace(background) ||
                    !TryGetSnapshotNode(nodesById, background, out JObject backgroundNode) ||
                    !backgroundNode.Value<int?>("siblingIndex").HasValue ||
                    backgroundNode.Value<int?>("siblingIndex").Value < 0)
                {
                    throw new InvalidDataException(
                        "Deterministic flat-sibling repair failed: " + findingId +
                        " has no observed background siblingIndex.");
                }

                string wrapperId = CreateUniqueFlatSiblingWrapperId(findingId, usedWrapperIds);
                string wrapperReference = "@" + wrapperId;
                generatedWrappers.Add(new JObject
                {
                    ["id"] = wrapperId,
                    ["parent"] = parent,
                    ["name"] = "[FlatSibling_" + findingId + "]",
                    ["siblingIndex"] = backgroundNode.Value<int?>("siblingIndex").Value,
                });
                for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    generatedMoves.Add(new JObject
                    {
                        ["source"] = members[memberIndex].Value<string>(),
                        ["destination"] = wrapperReference,
                        ["siblingIndex"] = memberIndex,
                    });
                }

                generatedTightBounds.Add(new JObject { ["target"] = wrapperReference });
                generatedResolutions.Add(new JObject
                {
                    ["findingId"] = findingId,
                    ["mode"] = "group",
                    ["wrapperId"] = wrapperId,
                });
            }

            foreach (JObject wrapper in generatedWrappers)
            {
                wrappers.Add(wrapper);
            }

            foreach (JObject move in generatedMoves)
            {
                moves.Add(move);
            }

            foreach (JObject bound in generatedTightBounds)
            {
                tightBounds.Add(bound);
            }

            foreach (JObject resolution in generatedResolutions)
            {
                resolutions.Add(resolution);
            }
        }

        private static void NormalizeDeterministicFlatSiblingGroups(
            IReadOnlyDictionary<string, JObject> findingsById,
            IReadOnlyDictionary<string, JObject> nodesById,
            JArray resolutions,
            JArray wrappers,
            JArray moves,
            JArray tightBounds)
        {
            var findings = findingsById.Values
                .OrderBy(finding => finding.Value<string>("id"), StringComparer.Ordinal)
                .ToArray();
            var membersToFinding = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JObject finding in findings)
            {
                string findingId = finding.Value<string>("id");
                if (!TryReadFlatSiblingMembers(finding, out string[] members) || members.Length < 3)
                {
                    throw new InvalidDataException(
                        "Deterministic flat-sibling repair failed: " + findingId +
                        " has no complete member list.");
                }

                foreach (string member in members)
                {
                    if (!membersToFinding.TryAdd(member, findingId))
                    {
                        throw new InvalidDataException(
                            "Deterministic flat-sibling repair failed: " + member +
                            " belongs to both " + membersToFinding[member] + " and " + findingId + ".");
                    }
                }
            }

            var canonicalWrapperIds = findings
                .Select(finding => finding.Value<string>("id") + "_group")
                .ToHashSet(StringComparer.Ordinal);
            var aliasesByFinding = findings.ToDictionary(
                finding => finding.Value<string>("id"),
                finding => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            foreach (JObject resolution in resolutions.OfType<JObject>())
            {
                string findingId = resolution.Value<string>("findingId");
                string wrapperId = resolution.Value<string>("wrapperId");
                if (string.Equals(resolution.Value<string>("mode"), "group", StringComparison.Ordinal) &&
                    aliasesByFinding.TryGetValue(findingId ?? string.Empty, out HashSet<string> aliases) &&
                    !string.IsNullOrWhiteSpace(wrapperId))
                {
                    aliases.Add(wrapperId);
                }
            }

            var wrapperAliases = aliasesByFinding
                .SelectMany(entry => entry.Value.Select(wrapperId => new { findingId = entry.Key, wrapperId }))
                .GroupBy(item => item.wrapperId, StringComparer.Ordinal)
                .Where(group => group.Select(item => item.findingId).Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
            if (wrapperAliases.Count > 0)
            {
                throw new InvalidDataException(
                    "Deterministic flat-sibling repair refused because a wrapper is shared by multiple findings: " +
                    string.Join(", ", wrapperAliases.OrderBy(id => id, StringComparer.Ordinal)) + ".");
            }

            var wrapperIdsToRemove = aliasesByFinding.Values
                .SelectMany(ids => ids)
                .Where(id => !canonicalWrapperIds.Contains(id))
                .ToHashSet(StringComparer.Ordinal);
            foreach (JObject wrapper in wrappers.OfType<JObject>().ToArray())
            {
                if (wrapperIdsToRemove.Contains(wrapper.Value<string>("id") ?? string.Empty))
                {
                    wrapper.Remove();
                }
            }

            foreach (JObject move in moves.OfType<JObject>().ToArray())
            {
                string source = move.Value<string>("source") ?? string.Empty;
                string destination = move.Value<string>("destination") ?? string.Empty;
                string destinationWrapperId = destination.StartsWith("@", StringComparison.Ordinal)
                    ? destination.Substring(1)
                    : string.Empty;
                if (membersToFinding.ContainsKey(source) ||
                    canonicalWrapperIds.Contains(destinationWrapperId) ||
                    wrapperIdsToRemove.Contains(destinationWrapperId))
                {
                    move.Remove();
                }
            }

            foreach (JObject bound in tightBounds.OfType<JObject>().ToArray())
            {
                string target = bound.Value<string>("target") ?? string.Empty;
                string targetWrapperId = target.StartsWith("@", StringComparison.Ordinal)
                    ? target.Substring(1)
                    : string.Empty;
                if (canonicalWrapperIds.Contains(targetWrapperId) ||
                    wrapperIdsToRemove.Contains(targetWrapperId))
                {
                    bound.Remove();
                }
            }

            foreach (JObject resolution in resolutions.OfType<JObject>().ToArray())
            {
                if (findingsById.ContainsKey(resolution.Value<string>("findingId") ?? string.Empty))
                {
                    resolution.Remove();
                }
            }

            var wrappersById = wrappers.OfType<JObject>()
                .Where(wrapper => !string.IsNullOrWhiteSpace(wrapper.Value<string>("id")))
                .ToDictionary(wrapper => wrapper.Value<string>("id"), StringComparer.Ordinal);
            foreach (JObject finding in findings)
            {
                string findingId = finding.Value<string>("id");
                string parent = finding.Value<string>("parent");
                string background = finding.Value<string>("background");
                if (!TryGetSnapshotNode(nodesById, background, out JObject backgroundNode) ||
                    !backgroundNode.Value<int?>("siblingIndex").HasValue)
                {
                    throw new InvalidDataException(
                        "Deterministic flat-sibling repair failed: " + findingId +
                        " has no observed background siblingIndex.");
                }

                TryReadFlatSiblingMembers(finding, out string[] members);
                string wrapperId = findingId + "_group";
                if (!wrappersById.TryGetValue(wrapperId, out JObject wrapper))
                {
                    wrapper = new JObject { ["id"] = wrapperId };
                    wrappers.Add(wrapper);
                    wrappersById.Add(wrapperId, wrapper);
                }

                wrapper["parent"] = parent;
                wrapper["name"] = "[FlatSibling_" + findingId + "]";
                wrapper["siblingIndex"] = backgroundNode.Value<int?>("siblingIndex").Value;
                string wrapperReference = "@" + wrapperId;
                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                {
                    moves.Add(new JObject
                    {
                        ["source"] = members[memberIndex],
                        ["destination"] = wrapperReference,
                        ["siblingIndex"] = memberIndex,
                    });
                }

                tightBounds.Add(new JObject { ["target"] = wrapperReference });
                resolutions.Add(new JObject
                {
                    ["findingId"] = findingId,
                    ["mode"] = "group",
                    ["wrapperId"] = wrapperId,
                });
            }
        }

        private static void NormalizeRepairableFlatSiblingGroups(
            IReadOnlyDictionary<string, JObject> findingsById,
            IReadOnlyDictionary<string, JObject> nodesById,
            JArray resolutions,
            JArray wrappers,
            JArray moves,
            JArray tightBounds)
        {
            var wrappersById = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var ambiguousWrapperIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject wrapper in wrappers.OfType<JObject>())
            {
                string wrapperId = wrapper.Value<string>("id");
                if (string.IsNullOrWhiteSpace(wrapperId))
                {
                    continue;
                }

                if (!wrappersById.TryAdd(wrapperId, wrapper))
                {
                    ambiguousWrapperIds.Add(wrapperId);
                }
            }

            foreach (JObject resolution in resolutions.OfType<JObject>())
            {
                string findingId = resolution.Value<string>("findingId");
                string wrapperId = resolution.Value<string>("wrapperId");
                if (!string.Equals(resolution.Value<string>("mode"), "group", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(findingId) ||
                    string.IsNullOrWhiteSpace(wrapperId) ||
                    !findingsById.TryGetValue(findingId, out JObject finding) ||
                    ambiguousWrapperIds.Contains(wrapperId) ||
                    !wrappersById.TryGetValue(wrapperId, out JObject wrapper) ||
                    !TryReadFlatSiblingMembers(finding, out string[] members) ||
                    !TryGetFlatSiblingWrapperIndex(finding, nodesById, out int expectedSiblingIndex))
                {
                    continue;
                }

                string expectedParent = finding.Value<string>("parent");
                string wrapperReference = "@" + wrapperId;
                var memberSet = new HashSet<string>(members, StringComparer.Ordinal);
                bool hasCanonicalMoves = HasCanonicalFlatSiblingMoves(moves, members, wrapperReference);
                bool hasTightBounds = tightBounds.OfType<JObject>().Any(bound =>
                    string.Equals(bound.Value<string>("target"), wrapperReference, StringComparison.Ordinal));
                bool hasForeignFindingMember = moves.OfType<JObject>().Any(move =>
                    string.Equals(move.Value<string>("destination"), wrapperReference, StringComparison.Ordinal) &&
                    !memberSet.Contains(move.Value<string>("source") ?? string.Empty) &&
                    BelongsToAnotherFlatSiblingFinding(
                        findingsById,
                        findingId,
                        move.Value<string>("source") ?? string.Empty));
                bool hasConflictingMemberMove = moves.OfType<JObject>().Any(move =>
                    memberSet.Contains(move.Value<string>("source") ?? string.Empty) &&
                    !string.Equals(move.Value<string>("destination"), wrapperReference, StringComparison.Ordinal));
                bool requiresRepair =
                    !string.Equals(wrapper.Value<string>("parent"), expectedParent, StringComparison.Ordinal) ||
                    wrapper.Value<int?>("siblingIndex") != expectedSiblingIndex ||
                    !hasCanonicalMoves ||
                    !hasTightBounds ||
                    hasForeignFindingMember ||
                    hasConflictingMemberMove;
                if (!requiresRepair)
                {
                    continue;
                }

                RemoveRepairableFlatSiblingMoves(
                    findingsById,
                    resolutions,
                    moves,
                    resolution,
                    findingId,
                    wrapperReference,
                    memberSet);
                AssertRepairableFlatSiblingWrapper(
                    findingsById,
                    nodesById,
                    resolutions,
                    moves,
                    resolution,
                    findingId,
                    wrapperId,
                    expectedParent,
                    members);

                foreach (JObject move in moves.OfType<JObject>()
                    .Where(move => memberSet.Contains(move.Value<string>("source") ?? string.Empty))
                    .ToArray())
                {
                    move.Remove();
                }

                wrapper["parent"] = expectedParent;
                wrapper["siblingIndex"] = expectedSiblingIndex;
                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                {
                    moves.Add(new JObject
                    {
                        ["source"] = members[memberIndex],
                        ["destination"] = wrapperReference,
                        ["siblingIndex"] = memberIndex,
                    });
                }

                if (!hasTightBounds)
                {
                    tightBounds.Add(new JObject { ["target"] = wrapperReference });
                }
            }
        }

        private static void RemoveRepairableFlatSiblingMoves(
            IReadOnlyDictionary<string, JObject> findingsById,
            JArray resolutions,
            JArray moves,
            JObject resolution,
            string findingId,
            string wrapperReference,
            ISet<string> memberSet)
        {
            bool isSharedResolution = resolutions.OfType<JObject>().Any(other =>
                !ReferenceEquals(other, resolution) &&
                string.Equals(other.Value<string>("mode"), "group", StringComparison.Ordinal) &&
                string.Equals(other.Value<string>("wrapperId"), wrapperReference.Substring(1), StringComparison.Ordinal));
            if (isSharedResolution)
            {
                return;
            }

            foreach (JObject move in moves.OfType<JObject>().ToArray())
            {
                string source = move.Value<string>("source") ?? string.Empty;
                string destination = move.Value<string>("destination") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                if (memberSet.Contains(source) &&
                    !string.Equals(destination, wrapperReference, StringComparison.Ordinal))
                {
                    move.Remove();
                    continue;
                }

                if (!memberSet.Contains(source) &&
                    string.Equals(destination, wrapperReference, StringComparison.Ordinal) &&
                    BelongsToAnotherFlatSiblingFinding(findingsById, findingId, source))
                {
                    move.Remove();
                }
            }
        }

        private static bool BelongsToAnotherFlatSiblingFinding(
            IReadOnlyDictionary<string, JObject> findingsById,
            string findingId,
            string source)
        {
            return findingsById.Any(entry =>
                !string.Equals(entry.Key, findingId, StringComparison.Ordinal) &&
                (entry.Value["members"] as JArray ?? new JArray())
                    .Any(member => string.Equals(member.Value<string>(), source, StringComparison.Ordinal)));
        }

        private static void AssertRepairableFlatSiblingWrapper(
            IReadOnlyDictionary<string, JObject> findingsById,
            IReadOnlyDictionary<string, JObject> nodesById,
            JArray resolutions,
            JArray moves,
            JObject resolution,
            string findingId,
            string wrapperId,
            string expectedParent,
            IReadOnlyCollection<string> members)
        {
            string wrapperReference = "@" + wrapperId;
            var memberSet = new HashSet<string>(members, StringComparer.Ordinal);
            bool isSharedResolution = resolutions.OfType<JObject>().Any(other =>
                !ReferenceEquals(other, resolution) &&
                string.Equals(other.Value<string>("mode"), "group", StringComparison.Ordinal) &&
                string.Equals(other.Value<string>("wrapperId"), wrapperId, StringComparison.Ordinal));
            JObject unexpectedRequiredMemberMove = moves.OfType<JObject>().FirstOrDefault(move =>
                memberSet.Contains(move.Value<string>("source") ?? string.Empty) &&
                !string.Equals(move.Value<string>("destination"), wrapperReference, StringComparison.Ordinal));
            JObject incompatibleAdditionalMove = moves.OfType<JObject>().FirstOrDefault(move =>
            {
                string source = move.Value<string>("source") ?? string.Empty;
                if (!string.Equals(move.Value<string>("destination"), wrapperReference, StringComparison.Ordinal) ||
                    memberSet.Contains(source))
                {
                    return false;
                }

                if (!TryGetSnapshotNode(nodesById, source, out JObject sourceNode) ||
                    !string.Equals(
                        "node:" + sourceNode.Value<string>("parentId"),
                        expectedParent,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                return findingsById.Any(entry =>
                    !string.Equals(entry.Key, findingId, StringComparison.Ordinal) &&
                    (entry.Value["members"] as JArray ?? new JArray())
                    .Any(member => string.Equals(member.Value<string>(), source, StringComparison.Ordinal)));
            });
            bool hasOverlappingFinding = findingsById.Any(entry =>
                !string.Equals(entry.Key, findingId, StringComparison.Ordinal) &&
                (entry.Value["members"] as JArray ?? new JArray())
                .Any(member => memberSet.Contains(member.Value<string>() ?? string.Empty)));
            if (!isSharedResolution &&
                unexpectedRequiredMemberMove == null &&
                incompatibleAdditionalMove == null &&
                !hasOverlappingFinding)
            {
                return;
            }

            throw new InvalidDataException(
                "Deterministic flat-sibling repair refused because wrapper " + wrapperId +
                " cannot safely preserve the observed direct-child mappings for " + findingId + ".");
        }

        private static bool HasCanonicalFlatSiblingMoves(
            JArray moves,
            IReadOnlyList<string> members,
            string wrapperReference)
        {
            for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                int matchingMoves = moves.OfType<JObject>().Count(move =>
                    string.Equals(move.Value<string>("source"), members[memberIndex], StringComparison.Ordinal) &&
                    string.Equals(move.Value<string>("destination"), wrapperReference, StringComparison.Ordinal) &&
                    move.Value<int?>("siblingIndex") == memberIndex);
                if (matchingMoves != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadFlatSiblingMembers(JObject finding, out string[] members)
        {
            members = (finding["members"] as JArray ?? new JArray())
                .Values<string>()
                .ToArray();
            return members.Length >= 3 &&
                   members.All(member => !string.IsNullOrWhiteSpace(member)) &&
                   members.Distinct(StringComparer.Ordinal).Count() == members.Length;
        }

        private static bool TryGetFlatSiblingWrapperIndex(
            JObject finding,
            IReadOnlyDictionary<string, JObject> nodesById,
            out int siblingIndex)
        {
            siblingIndex = -1;
            string background = finding.Value<string>("background");
            return TryGetSnapshotNode(nodesById, background, out JObject backgroundNode) &&
                   backgroundNode.Value<int?>("siblingIndex").HasValue &&
                   (siblingIndex = backgroundNode.Value<int?>("siblingIndex").Value) >= 0;
        }

        private static Dictionary<string, JObject> ReadSnapshotNodes(PsdHierarchyChatContext context)
        {
            JObject snapshot;
            try
            {
                snapshot = JObject.Parse(context.hierarchySnapshotJson);
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                throw new InvalidDataException(
                    "Deterministic flat-sibling repair failed: snapshot JSON is invalid.",
                    exception);
            }

            var nodesById = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject node in (snapshot["nodes"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string nodeId = node.Value<string>("id");
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    nodesById.Add(nodeId, node);
                }
            }

            return nodesById;
        }

        private static bool TryGetSnapshotNode(
            IReadOnlyDictionary<string, JObject> nodesById,
            string reference,
            out JObject node)
        {
            node = null;
            const string prefix = "node:";
            return !string.IsNullOrWhiteSpace(reference) &&
                   reference.StartsWith(prefix, StringComparison.Ordinal) &&
                   nodesById.TryGetValue(reference.Substring(prefix.Length), out node);
        }

        private static string CreateUniqueFlatSiblingWrapperId(
            string findingId,
            ISet<string> usedWrapperIds)
        {
            string baseId = findingId + "_group";
            if (!Regex.IsMatch(baseId, "^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException(
                    "Deterministic flat-sibling repair failed: " + findingId +
                    " cannot produce a valid wrapper id.");
            }

            string wrapperId = baseId;
            for (int suffix = 2; !usedWrapperIds.Add(wrapperId); suffix++)
            {
                wrapperId = baseId + "_" + suffix;
            }

            return wrapperId;
        }

        private static void ValidateFlatSiblingResolutions(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            JArray findings = context?.flatSiblingFindings ?? new JArray();
            JArray resolutions = plan["flatSiblingResolutions"] as JArray ?? new JArray();
            if (findings.Count == 0)
            {
                if (resolutions.Count > 0)
                {
                    throw new InvalidDataException(
                        "flatSiblingResolutions must be empty because the authoritative snapshot has no flatSiblingFindings.");
                }

                return;
            }

            var findingsById = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject finding in findings.OfType<JObject>())
            {
                string findingId = finding.Value<string>("id");
                if (!string.IsNullOrWhiteSpace(findingId))
                {
                    findingsById[findingId] = finding;
                }
            }

            var wrappersById = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject wrapper in (plan["wrappers"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string wrapperId = wrapper.Value<string>("id");
                if (!string.IsNullOrWhiteSpace(wrapperId) && !wrappersById.ContainsKey(wrapperId))
                {
                    wrappersById.Add(wrapperId, wrapper);
                }
            }

            JArray moves = plan["moves"] as JArray ?? new JArray();
            JArray tightBounds = plan["tightBounds"] as JArray ?? new JArray();
            var resolvedFindingIds = new HashSet<string>(StringComparer.Ordinal);
            var errors = new List<string>();
            for (int index = 0; index < resolutions.Count; index++)
            {
                if (!(resolutions[index] is JObject resolution))
                {
                    errors.Add("flatSiblingResolutions[" + index + "] must be an object.");
                    continue;
                }

                string findingId = resolution.Value<string>("findingId");
                if (string.IsNullOrWhiteSpace(findingId) ||
                    !findingsById.TryGetValue(findingId, out JObject finding))
                {
                    errors.Add(
                        "flatSiblingResolutions[" + index + "].findingId is not in the authoritative snapshot.");
                    continue;
                }

                if (!resolvedFindingIds.Add(findingId))
                {
                    errors.Add("flatSiblingResolutions resolves " + findingId + " more than once.");
                    continue;
                }

                string mode = resolution.Value<string>("mode");
                if (string.Equals(mode, "keep", StringComparison.Ordinal))
                {
                    string evidence = resolution.Value<string>("evidence");
                    if (string.IsNullOrWhiteSpace(evidence) || evidence.Trim().Length < 20)
                    {
                        errors.Add(
                            "flatSiblingResolutions[" + index + "].evidence must contain at least 20 characters for keep.");
                    }

                    continue;
                }

                if (!string.Equals(mode, "group", StringComparison.Ordinal))
                {
                    errors.Add("flatSiblingResolutions[" + index + "].mode must be group or keep.");
                    continue;
                }

                string wrapperId = resolution.Value<string>("wrapperId");
                if (string.IsNullOrWhiteSpace(wrapperId) ||
                    !wrappersById.TryGetValue(wrapperId, out JObject wrapper))
                {
                    errors.Add(
                        "flatSiblingResolutions[" + index + "].wrapperId must name a wrapper.");
                    continue;
                }

                string expectedParent = finding.Value<string>("parent");
                if (!string.Equals(wrapper.Value<string>("parent"), expectedParent, StringComparison.Ordinal))
                {
                    errors.Add(
                        "flatSiblingResolutions[" + index + "] wrapper parent must equal the finding parent.");
                }

                string wrapperReference = "@" + wrapperId;
                JArray members = finding["members"] as JArray;
                if (members == null || members.Count < 3)
                {
                    errors.Add("flatSiblingFindings " + findingId + " has no complete member list.");
                    continue;
                }

                for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    string source = members[memberIndex]?.Value<string>();
                    int matchingMoves = moves
                        .OfType<JObject>()
                        .Count(move =>
                            string.Equals(move.Value<string>("source"), source, StringComparison.Ordinal) &&
                            string.Equals(move.Value<string>("destination"), wrapperReference, StringComparison.Ordinal) &&
                            move.Value<int?>("siblingIndex") == memberIndex);
                    if (matchingMoves != 1)
                    {
                        errors.Add(
                            "flatSiblingResolutions[" + index + "] must move " + source +
                            " to " + wrapperReference + " at siblingIndex " + memberIndex + ".");
                    }
                }

                if (!tightBounds.OfType<JObject>().Any(bound =>
                    string.Equals(bound.Value<string>("target"), wrapperReference, StringComparison.Ordinal)))
                {
                    errors.Add(
                        "flatSiblingResolutions[" + index + "] must tighten " + wrapperReference + ".");
                }
            }

            foreach (string findingId in findingsById.Keys)
            {
                if (!resolvedFindingIds.Contains(findingId))
                {
                    errors.Add("flatSiblingResolutions must resolve " + findingId + ".");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidDataException(
                    "Flat sibling findings validation failed:" + Environment.NewLine +
                    "- " + string.Join(Environment.NewLine + "- ", errors));
            }
        }

        private static void WriteFlatSiblingFindings(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            var findings = new JArray();
            foreach (JObject finding in (context.flatSiblingFindings ?? new JArray()).OfType<JObject>())
            {
                string findingId = finding.Value<string>("id") ?? string.Empty;
                string label = "flat sibling finding " + findingId;
                var members = new JArray();
                foreach (JToken member in finding["members"] as JArray ?? new JArray())
                {
                    members.Add(ResolveNodeReference(member.Value<string>(), label + ".members", context));
                }

                if (string.IsNullOrWhiteSpace(findingId) || members.Count < 3)
                {
                    continue;
                }

                findings.Add(new JObject
                {
                    ["id"] = findingId,
                    ["parent"] = ResolveNodeReference(finding.Value<string>("parent"), label + ".parent", context),
                    ["background"] = ResolveNodeReference(finding.Value<string>("background"), label + ".background", context),
                    ["members"] = members,
                });
            }

            if (findings.Count > 0)
            {
                plan["flatSiblingFindings"] = findings;
            }
            else
            {
                plan.Remove("flatSiblingFindings");
            }
        }

        private static bool TryReplaceUnprovableStatefulExtractionWithVariant(
            JObject plan,
            PsdHierarchyChatContext context,
            InvalidDataException exception)
        {
            string message = exception.Message ?? string.Empty;
            var match = Regex.Match(message, @"^statefulComponentExtractions\[(\d+)\]\.");
            bool hasUnprovablePartition = message.Contains(
                "cannot safely derive Common/State mappings because neither side contains a complete observed direct-child mapping.");
            bool hasAmbiguousMemberName = message.Contains("contains an ambiguous direct-child name:");
            bool hasMissingMemberName = message.Contains(
                "contains a name that is not an observed direct child:");
            if (!match.Success ||
                (!hasUnprovablePartition && !hasAmbiguousMemberName && !hasMissingMemberName) ||
                !int.TryParse(match.Groups[1].Value, out var extractionIndex))
            {
                return false;
            }

            var statefulExtractions = plan["statefulComponentExtractions"] as JArray;
            var variantExtractions = plan["variantComponentExtractions"] as JArray;
            if (statefulExtractions == null || variantExtractions == null ||
                extractionIndex < 0 || extractionIndex >= statefulExtractions.Count ||
                !(statefulExtractions[extractionIndex] is JObject statefulExtraction))
            {
                return false;
            }

            if (!(statefulExtraction["instances"] is JArray statefulInstances) || statefulInstances.Count < 2)
            {
                return false;
            }

            var states = new JArray();
            var variantInstances = new JArray();
            var sources = new HashSet<string>(StringComparer.Ordinal);
            var instanceNames = new HashSet<string>(StringComparer.Ordinal);
            string firstSource = null;
            for (var instanceIndex = 0; instanceIndex < statefulInstances.Count; instanceIndex++)
            {
                if (!(statefulInstances[instanceIndex] is JObject statefulInstance))
                {
                    return false;
                }

                var source = ReadRequiredString(statefulInstance, "source");
                // A stateful mapping may repeat an observed source when the AI
                // copied an instance incorrectly. Preserve that instance in the
                // variant fallback so the mandatory source list is not dropped;
                // the native runner remains responsible for its normal source
                // validation after this structural fallback.
                sources.Add(source);

                if (firstSource == null)
                {
                    firstSource = source;
                }

                var stateId = "state_" + (instanceIndex + 1);
                states.Add(new JObject
                {
                    ["id"] = stateId,
                    ["name"] = "[State_" + (instanceIndex + 1) + "]",
                    ["source"] = source,
                });

                var instanceName = statefulInstance.Value<string>("name");
                if (!IsBracketedSemanticItemName(instanceName) || !instanceNames.Add(instanceName))
                {
                    string baseInstanceName = "[VariantItem_" + (instanceIndex + 1) + "]";
                    int suffix = 0;
                    instanceName = baseInstanceName;
                    while (!instanceNames.Add(instanceName))
                    {
                        suffix++;
                        instanceName = "[VariantItem_" + (instanceIndex + 1) + "_" + suffix + "]";
                    }
                }

                variantInstances.Add(new JObject
                {
                    ["source"] = source,
                    ["state"] = stateId,
                    ["name"] = instanceName,
                });
            }

            if (string.IsNullOrWhiteSpace(firstSource))
            {
                return false;
            }

            string id = statefulExtraction.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = CreateUniqueExtractionId(plan, "stateful_variant_" + (extractionIndex + 1));
            }

            string template = statefulExtraction.Value<string>("template");
            if (string.IsNullOrWhiteSpace(template))
            {
                template = firstSource;
            }

            string assetPath = statefulExtraction.Value<string>("assetPath");
            if (string.IsNullOrWhiteSpace(assetPath) && context != null)
            {
                assetPath = CreateAvailableVariantAssetPath(plan, context, id);
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var variantTemplate = sources.Contains(template)
                ? template
                : firstSource;

            variantExtractions.Add(new JObject
            {
                ["id"] = id,
                ["template"] = variantTemplate,
                ["assetPath"] = assetPath,
                ["commonName"] = "[Common]",
                ["statesName"] = "[States]",
                ["defaultState"] = "state_1",
                ["states"] = states,
                ["instances"] = variantInstances,
            });
            statefulExtractions.RemoveAt(extractionIndex);

            if (plan["componentFamilyDecisions"] is JArray decisions)
            {
                foreach (var decision in decisions.OfType<JObject>())
                {
                    if (string.Equals(decision.Value<string>("extractionId"), id, StringComparison.Ordinal))
                    {
                        decision["mode"] = "variant";
                    }
                }
            }

            return true;
        }

        private static void NormalizeStatefulInstanceMappingsStrict(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            if (!(plan["statefulComponentExtractions"] is JArray extractions))
            {
                throw new InvalidDataException("statefulComponentExtractions must be an array.");
            }

            Dictionary<string, string> renamedNamesByPath = BuildPlannedRenameMap(plan);
            for (int extractionIndex = 0; extractionIndex < extractions.Count; extractionIndex++)
            {
                if (!(extractions[extractionIndex] is JObject extraction))
                {
                    throw new InvalidDataException(
                        "statefulComponentExtractions[" + extractionIndex + "] must be an object.");
                }

                string extractionLabel = "statefulComponentExtractions[" + extractionIndex + "]";
                if (!(extraction["common"] is JObject common) ||
                    !(common["members"] is JArray commonMembers) ||
                    !(extraction["states"] is JArray states) ||
                    !(extraction["instances"] is JArray instances))
                {
                    throw new InvalidDataException(
                        extractionLabel + " must contain common.members, states, and instances arrays.");
                }

                if (commonMembers.Count == 0)
                {
                    throw new InvalidDataException(
                        extractionLabel + ".common.members must not be empty; use variantComponentExtractions " +
                        "when no direct child is common to every source.");
                }

                NormalizeStatefulContractMemberNames(
                    common,
                    extractionLabel + ".common",
                    context,
                    renamedNamesByPath);

                string commonSourcePath = ReadRequiredString(common, "source");
                var stateMemberCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var statesById = new Dictionary<string, JObject>(StringComparer.Ordinal);
                for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
                {
                    if (!(states[stateIndex] is JObject state) ||
                        !(state["members"] is JArray stateMembers))
                    {
                        throw new InvalidDataException(
                            extractionLabel + ".states[" + stateIndex + "].members must be an array.");
                    }

                    NormalizeStatefulContractMemberNames(
                        state,
                        extractionLabel + ".states[" + stateIndex + "]",
                        context,
                        renamedNamesByPath);

                    string stateId = ReadRequiredString(state, "id");
                    if (stateMemberCounts.ContainsKey(stateId))
                    {
                        throw new InvalidDataException(extractionLabel + " contains duplicate state id " + stateId + ".");
                    }

                    stateMemberCounts.Add(stateId, stateMembers.Count);
                    statesById.Add(stateId, state);
                }

                for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
                {
                    if (!(instances[instanceIndex] is JObject instance))
                    {
                        throw new InvalidDataException(
                            extractionLabel + ".instances[" + instanceIndex + "] must be an object.");
                    }

                    string instanceLabel = extractionLabel + ".instances[" + instanceIndex + "]";
                    string sourcePath = ReadRequiredString(instance, "source");
                    string stateId = ReadRequiredString(instance, "state");
                    if (!stateMemberCounts.TryGetValue(stateId, out int expectedStateMemberCount))
                    {
                        throw new InvalidDataException(instanceLabel + ".state does not match states[].id.");
                    }

                    JObject selectedState = statesById[stateId];
                    var selectedStateMembers = (JArray)selectedState["members"];

                    if (!context.TryGetDirectChildren(
                            sourcePath,
                            out IReadOnlyList<PsdHierarchySnapshotChild> snapshotChildren))
                    {
                        throw new InvalidDataException(
                            instanceLabel + ".source has no direct-child records in the authoritative snapshot: " +
                            sourcePath);
                    }

                    string[] directChildNames = snapshotChildren
                        .Select(child => renamedNamesByPath.TryGetValue(child.path, out string renamed)
                            ? renamed
                            : child.name)
                        .ToArray();

                    // 检查是否有实际的重复
                    var duplicateCheck = directChildNames
                        .GroupBy(name => name, StringComparer.Ordinal)
                        .Where(g => g.Count() > 1)
                        .ToArray();

                    if (duplicateCheck.Length > 0)
                    {
#if UNITY_EDITOR
                        var diagBuilder = new System.Text.StringBuilder();
                        diagBuilder.AppendLine("=== RENAME CONFLICT DETECTED ===");
                        diagBuilder.AppendLine(instanceLabel + " has duplicate names after applying renames:");
                        foreach (var group in duplicateCheck)
                        {
                            diagBuilder.AppendLine("  Name \"" + group.Key + "\" appears " + group.Count() + " times");
                            var indices = directChildNames
                                .Select((name, idx) => new { name, idx })
                                .Where(x => x.name == group.Key)
                                .ToArray();
                            foreach (var item in indices)
                            {
                                diagBuilder.AppendLine("    [" + item.idx + "] originalName=\"" + snapshotChildren[item.idx].name + "\"");
                                diagBuilder.AppendLine("        path=\"" + snapshotChildren[item.idx].path + "\"");
                                if (renamedNamesByPath.TryGetValue(snapshotChildren[item.idx].path, out string renamedTo))
                                {
                                    diagBuilder.AppendLine("        renamed to=\"" + renamedTo + "\" (from AI plan)");
                                }
                            }
                        }
                        diagBuilder.AppendLine("=== END CONFLICT ===");
                        UnityEngine.Debug.LogError(diagBuilder.ToString());
#endif
                    }

                    // 容错处理：修复空名称和重复名称
                    directChildNames = FixDuplicateOrEmptyDirectChildNames(
                        directChildNames,
                        snapshotChildren,
                        instanceLabel);

                    int expectedCommonMemberCount = commonMembers.Count;

                    // 允许实例的子节点数量与 AI 期望不匹配（Prefab 结构本来就可能不一致）
                    // 尝试从快照中匹配 AI 期望的成员
                    if (directChildNames.Length != expectedCommonMemberCount + expectedStateMemberCount)
                    {
#if UNITY_EDITOR
                        // 详细诊断日志
                        var diagBuilder = new System.Text.StringBuilder();
                        diagBuilder.AppendLine("=== STRUCTURE VARIANCE DETECTED ===");
                        diagBuilder.AppendLine(instanceLabel + " has different structure:");
                        diagBuilder.AppendLine("  Snapshot has " + directChildNames.Length + " children");
                        diagBuilder.AppendLine("  AI template expects " + (expectedCommonMemberCount + expectedStateMemberCount) +
                                              " (" + expectedCommonMemberCount + " Common + " + expectedStateMemberCount + " State)");
                        diagBuilder.AppendLine();

                        diagBuilder.AppendLine("Snapshot children (" + directChildNames.Length + " total):");
                        for (int i = 0; i < snapshotChildren.Count; i++)
                        {
                            diagBuilder.AppendLine("  [" + i + "] \"" + directChildNames[i] + "\" (path: " + snapshotChildren[i].path + ")");
                        }
                        diagBuilder.AppendLine();

                        diagBuilder.AppendLine("Expected Common members (" + expectedCommonMemberCount + "):");
                        for (int i = 0; i < commonMembers.Count; i++)
                        {
                            var member = (Newtonsoft.Json.Linq.JObject)commonMembers[i];
                            string sourceName = member.Value<string>("sourceName") ?? "<missing>";
                            bool found = directChildNames.Contains(sourceName, StringComparer.Ordinal);
                            diagBuilder.AppendLine("  [" + i + "] \"" + sourceName + "\" " + (found ? "✓ found" : "✗ missing"));
                        }
                        diagBuilder.AppendLine();

                        diagBuilder.AppendLine("Expected State members for state '" + stateId + "' (" + expectedStateMemberCount + "):");
                        for (int i = 0; i < selectedStateMembers.Count; i++)
                        {
                            var member = (Newtonsoft.Json.Linq.JObject)selectedStateMembers[i];
                            string sourceName = member.Value<string>("sourceName") ?? "<missing>";
                            bool found = directChildNames.Contains(sourceName, StringComparer.Ordinal);
                            diagBuilder.AppendLine("  [" + i + "] \"" + sourceName + "\" " + (found ? "✓ found" : "✗ missing"));
                        }
                        diagBuilder.AppendLine();

                        diagBuilder.AppendLine("Attempting to match available children to expected members...");
                        diagBuilder.AppendLine("=== END DIAGNOSTIC ===");

                        UnityEngine.Debug.LogWarning(diagBuilder.ToString());
#endif
                        // 不抛出异常，继续尝试匹配
                    }

                    bool commonMappingIsComplete = TryResolveCompleteDirectChildMapping(
                        instance["commonSourceNames"],
                        expectedCommonMemberCount,
                        snapshotChildren,
                        directChildNames,
                        instanceLabel + ".commonSourceNames",
                        out string[] commonSourceNames);
                    bool stateMappingIsComplete = TryResolveCompleteDirectChildMapping(
                        instance["stateSourceNames"],
                        expectedStateMemberCount,
                        snapshotChildren,
                        directChildNames,
                        instanceLabel + ".stateSourceNames",
                        out string[] stateSourceNames);

                    bool commonMappingIsAuthoritative =
                        string.Equals(sourcePath, commonSourcePath, StringComparison.Ordinal);
                    if (commonMappingIsAuthoritative)
                    {
                        commonSourceNames = ReadContractMemberSourceNames(commonMembers);
                        commonMappingIsComplete = true;
                    }

                    bool stateMappingIsAuthoritative = string.Equals(
                        sourcePath,
                        ReadRequiredString(selectedState, "source"),
                        StringComparison.Ordinal);
                    if (stateMappingIsAuthoritative)
                    {
                        stateSourceNames = ReadContractMemberSourceNames(selectedStateMembers);
                        stateMappingIsComplete = true;
                    }

                    if (commonMappingIsAuthoritative && stateMappingIsAuthoritative)
                    {
                        AssertCompleteStatefulPartition(
                            directChildNames,
                            commonSourceNames,
                            stateSourceNames,
                            expectedCommonMemberCount,
                            expectedStateMemberCount,
                            instanceLabel);
                    }
                    else if (commonMappingIsAuthoritative ||
                             (!stateMappingIsAuthoritative && commonMappingIsComplete))
                    {
                        string[] stateComplement = ComplementDirectChildNames(directChildNames, commonSourceNames);
                        if (stateComplement.Length != expectedStateMemberCount)
                        {
#if UNITY_EDITOR
                            UnityEngine.Debug.LogWarning(
                                instanceLabel + ": commonSourceNames leave " + stateComplement.Length +
                                " members but State contract expects " + expectedStateMemberCount +
                                ". This instance has a different structure. Attempting partial mapping...");
#endif
                            // 允许部分映射：尝试匹配现有的子节点到 State 成员
                            stateSourceNames = new string[expectedStateMemberCount];
                            var stateMemberNames = ReadContractMemberSourceNames(selectedStateMembers);
                            for (int i = 0; i < expectedStateMemberCount; i++)
                            {
                                string expectedName = stateMemberNames[i];
                                if (stateComplement.Contains(expectedName, StringComparer.Ordinal))
                                {
                                    stateSourceNames[i] = expectedName;
                                }
                                else
                                {
                                    // 成员缺失，使用占位符
                                    stateSourceNames[i] = string.Empty;
#if UNITY_EDITOR
                                    UnityEngine.Debug.LogWarning(
                                        instanceLabel + ": State member \"" + expectedName + "\" is missing in this instance.");
#endif
                                }
                            }
                            stateMappingIsComplete = true;
                        }
                        else
                        {
                            stateSourceNames = MappingHasSameMembers(stateSourceNames, stateComplement)
                                ? stateSourceNames
                                : stateComplement;
                            stateMappingIsComplete = true;
                        }
                    }
                    else if (stateMappingIsComplete)
                    {
                        string[] commonComplement = ComplementDirectChildNames(directChildNames, stateSourceNames);
                        if (commonComplement.Length != expectedCommonMemberCount)
                        {
#if UNITY_EDITOR
                            UnityEngine.Debug.LogWarning(
                                instanceLabel + ": stateSourceNames leave " + commonComplement.Length +
                                " members but Common contract expects " + expectedCommonMemberCount +
                                ". This instance has a different structure. Attempting partial mapping...");
#endif
                            // 允许部分映射：尝试匹配现有的子节点到 Common 成员
                            commonSourceNames = new string[expectedCommonMemberCount];
                            var commonMemberNames = ReadContractMemberSourceNames(commonMembers);
                            for (int i = 0; i < expectedCommonMemberCount; i++)
                            {
                                string expectedName = commonMemberNames[i];
                                if (commonComplement.Contains(expectedName, StringComparer.Ordinal))
                                {
                                    commonSourceNames[i] = expectedName;
                                }
                                else
                                {
                                    // 成员缺失，使用占位符
                                    commonSourceNames[i] = string.Empty;
#if UNITY_EDITOR
                                    UnityEngine.Debug.LogWarning(
                                        instanceLabel + ": Common member \"" + expectedName + "\" is missing in this instance.");
#endif
                                }
                            }
                            commonMappingIsComplete = true;
                        }
                        else
                        {
                            commonSourceNames = MappingHasSameMembers(commonSourceNames, commonComplement)
                                ? commonSourceNames
                                : commonComplement;
                            commonMappingIsComplete = true;
                        }
                    }

                    if (!commonMappingIsComplete || !stateMappingIsComplete)
                    {
                        throw new InvalidDataException(
                            instanceLabel + " cannot safely derive Common/State mappings because neither side " +
                            "contains a complete observed direct-child mapping.");
                    }

                    AssertCompleteStatefulPartition(
                        directChildNames,
                        commonSourceNames,
                        stateSourceNames,
                        expectedCommonMemberCount,
                        expectedStateMemberCount,
                        instanceLabel);
                    instance["commonSourceNames"] = new JArray(commonSourceNames);
                    instance["stateSourceNames"] = new JArray(stateSourceNames);
                }
            }
        }

        private static bool TryResolveCompleteDirectChildMapping(
            JToken token,
            int expectedCount,
            IReadOnlyList<PsdHierarchySnapshotChild> snapshotChildren,
            IReadOnlyList<string> finalNames,
            string label,
            out string[] resolved)
        {
            string[] requested = ReadOptionalStringArray(token);
            resolved = Array.Empty<string>();
            if (requested.Length != expectedCount ||
                requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
            {
                return false;
            }

            try
            {
                resolved = ResolveFinalDirectChildNames(requested, snapshotChildren, finalNames, label);
                return resolved.Distinct(StringComparer.Ordinal).Count() == resolved.Length;
            }
            catch (InvalidDataException)
            {
                resolved = Array.Empty<string>();
                return false;
            }
        }

        private static string[] ReadContractMemberSourceNames(JArray members)
        {
            return members
                .OfType<JObject>()
                .Select(member => ReadRequiredString(member, "sourceName"))
                .ToArray();
        }

        private static string[] ComplementDirectChildNames(
            IReadOnlyList<string> directChildNames,
            IReadOnlyList<string> mappedNames)
        {
            var mapped = new HashSet<string>(mappedNames, StringComparer.Ordinal);
            return directChildNames.Where(name => !mapped.Contains(name)).ToArray();
        }

        private static bool MappingHasSameMembers(
            IReadOnlyList<string> mapping,
            IReadOnlyList<string> expectedMembers)
        {
            return mapping.Count == expectedMembers.Count &&
                   new HashSet<string>(mapping, StringComparer.Ordinal).SetEquals(expectedMembers);
        }

        private static void AssertCompleteStatefulPartition(
            IReadOnlyList<string> directChildNames,
            IReadOnlyList<string> commonSourceNames,
            IReadOnlyList<string> stateSourceNames,
            int expectedCommonMemberCount,
            int expectedStateMemberCount,
            string label)
        {
            if (commonSourceNames.Count != expectedCommonMemberCount ||
                stateSourceNames.Count != expectedStateMemberCount)
            {
                throw new InvalidDataException(label + " does not match the Common/State member counts.");
            }

            var mapped = new HashSet<string>(commonSourceNames, StringComparer.Ordinal);
            if (mapped.Count != commonSourceNames.Count ||
                stateSourceNames.Any(name => !mapped.Add(name)) ||
                !mapped.SetEquals(directChildNames))
            {
                throw new InvalidDataException(
                    label + ".commonSourceNames and stateSourceNames must cover every direct child exactly once.");
            }
        }

        private static void NormalizeStatefulContractMemberNames(
            JObject contract,
            string label,
            PsdHierarchyChatContext context,
            IReadOnlyDictionary<string, string> renamedNamesByPath)
        {
            string sourcePath = ReadRequiredString(contract, "source");
            if (!(contract["members"] is JArray members))
            {
                throw new InvalidDataException(label + ".members must be an array.");
            }

            if (!context.TryGetDirectChildren(
                    sourcePath,
                    out IReadOnlyList<PsdHierarchySnapshotChild> snapshotChildren))
            {
                throw new InvalidDataException(
                    label + ".source has no direct-child records in the authoritative snapshot: " + sourcePath);
            }

            string[] finalNames = snapshotChildren
                .Select(child => renamedNamesByPath.TryGetValue(child.path, out string renamed)
                    ? renamed
                    : child.name)
                .ToArray();
            for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                if (!(members[memberIndex] is JObject member))
                {
                    throw new InvalidDataException(label + ".members[" + memberIndex + "] must be an object.");
                }

                string sourceName = ReadRequiredString(member, "sourceName");
                member["sourceName"] = ResolveFinalDirectChildNames(
                    new[] { sourceName },
                    snapshotChildren,
                    finalNames,
                    label + ".members[" + memberIndex + "].sourceName")[0];
            }
        }

        private static string[] ResolveFinalDirectChildNames(
            IReadOnlyList<string> requestedNames,
            IReadOnlyList<PsdHierarchySnapshotChild> snapshotChildren,
            IReadOnlyList<string> finalNames,
            string label)
        {
            var resolved = new string[requestedNames.Count];
            for (int requestedIndex = 0; requestedIndex < requestedNames.Count; requestedIndex++)
            {
                string requestedName = requestedNames[requestedIndex];
                int matchedIndex = -1;
                for (int childIndex = 0; childIndex < snapshotChildren.Count; childIndex++)
                {
                    if (!string.Equals(finalNames[childIndex], requestedName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (matchedIndex >= 0)
                    {
                        throw new InvalidDataException(label + " contains an ambiguous direct-child name: " + requestedName);
                    }

                    matchedIndex = childIndex;
                }

                if (matchedIndex < 0)
                {
                    for (int childIndex = 0; childIndex < snapshotChildren.Count; childIndex++)
                    {
                        if (!string.Equals(snapshotChildren[childIndex].name, requestedName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (matchedIndex >= 0)
                        {
                            throw new InvalidDataException(
                                label + " contains an ambiguous original direct-child name: " + requestedName);
                        }

                        matchedIndex = childIndex;
                    }
                }

                if (matchedIndex < 0)
                {
                    throw new InvalidDataException(
                        label + " contains a name that is not an observed direct child: " + requestedName);
                }

                resolved[requestedIndex] = finalNames[matchedIndex];
            }

            return resolved;
        }

        private static Dictionary<string, string> BuildPlannedRenameMap(JObject plan)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!(plan["renames"] is JArray renames))
            {
                return result;
            }

            foreach (JObject rename in renames.OfType<JObject>())
            {
                string target = rename.Value<string>("target");
                string name = rename.Value<string>("name");
                if (!string.IsNullOrWhiteSpace(target) && !target.StartsWith("@", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(name))
                {
                    result[target] = name;
                }
            }

            return result;
        }

        private static string[] ReadOptionalStringArray(JToken token)
        {
            if (!(token is JArray values))
            {
                return Array.Empty<string>();
            }

            return values
                .Where(value => value.Type == JTokenType.String &&
                                !string.IsNullOrWhiteSpace(value.Value<string>()))
                .Values<string>()
                .ToArray();
        }

        private static string[] FixDuplicateOrEmptyDirectChildNames(
            string[] directChildNames,
            IReadOnlyList<PsdHierarchySnapshotChild> snapshotChildren,
            string instanceLabel)
        {
            bool hasIssues = false;
            var fixedNames = new string[directChildNames.Length];

            // 第一步：修复空名称
            for (int i = 0; i < directChildNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(directChildNames[i]))
                {
                    // 使用原始快照名称作为备用
                    string fallbackName = snapshotChildren[i].name;
                    if (string.IsNullOrWhiteSpace(fallbackName))
                    {
                        // 如果原始名称也是空的，生成默认名称
                        fallbackName = "Child_" + i;
                    }
                    fixedNames[i] = fallbackName;
                    hasIssues = true;
#if UNITY_EDITOR
                    UnityEngine.Debug.LogError(
                        instanceLabel + ": Empty child name at index " + i + " (path: " +
                        snapshotChildren[i].path + ") replaced with \"" + fallbackName + "\".");
#endif
                }
                else
                {
                    fixedNames[i] = directChildNames[i];
                }
            }

            // 第二步：修复重复名称（添加数字后缀）
            // 先统计所有名称的出现次数
            var nameOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < fixedNames.Length; i++)
            {
                string name = fixedNames[i];
                if (!nameOccurrences.ContainsKey(name))
                {
                    nameOccurrences[name] = 0;
                }
                nameOccurrences[name]++;
            }

            // 对所有重复的名称添加后缀
            var nameCounters = new Dictionary<string, int>(StringComparer.Ordinal);
            var finalNames = new string[fixedNames.Length];

            for (int i = 0; i < fixedNames.Length; i++)
            {
                string originalName = fixedNames[i];
                string uniqueName = originalName;

                if (nameOccurrences[originalName] > 1)
                {
                    // 这个名称有重复，需要添加后缀
                    int counter = nameCounters.ContainsKey(originalName) ? nameCounters[originalName] : 0;
                    uniqueName = originalName + "_" + counter;

                    // 确保添加后缀后的名称也不重复
                    while (nameOccurrences.ContainsKey(uniqueName) ||
                           finalNames.Take(i).Contains(uniqueName, StringComparer.Ordinal))
                    {
                        counter++;
                        uniqueName = originalName + "_" + counter;
                    }

                    nameCounters[originalName] = counter + 1;
                    hasIssues = true;
#if UNITY_EDITOR
                    UnityEngine.Debug.LogError(
                        instanceLabel + ": Duplicate child name \"" + originalName +
                        "\" at index " + i + " (path: " + snapshotChildren[i].path +
                        ") renamed to \"" + uniqueName + "\".");
#endif
                }

                finalNames[i] = uniqueName;
            }

            if (hasIssues)
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogError(
                    instanceLabel + ": AI-generated plan had naming conflicts. " +
                    "Automatic fixes were applied to allow execution. " +
                    "Review the errors above for details.");
#endif
            }

            return finalNames;
        }

        private static void ResolveObjectArray(JObject plan, string propertyName, Action<JObject, string> resolver)
        {
            var items = plan[propertyName] as JArray;
            if (items == null)
            {
                throw new InvalidDataException("计划缺少数组字段 " + propertyName + "。");
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!(items[index] is JObject item))
                {
                    throw new InvalidDataException(propertyName + "[" + index + "] 必须为对象。");
                }

                resolver(item, propertyName + "[" + index + "]");
            }
        }

        private static void ResolveNestedNodeProperties(
            JObject owner,
            string arrayProperty,
            string nodeProperty,
            string label,
            PsdHierarchyChatContext context)
        {
            if (!(owner[arrayProperty] is JArray items))
            {
                throw new InvalidDataException(label + " 必须为数组。");
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!(items[index] is JObject item))
                {
                    throw new InvalidDataException(label + "[" + index + "] 必须为对象。");
                }

                ResolveNodeProperty(item, nodeProperty, label + "[" + index + "]." + nodeProperty, context, false);
            }
        }

        private static void ResolveNodeStringArray(
            JObject owner,
            string propertyName,
            string label,
            PsdHierarchyChatContext context)
        {
            if (!(owner[propertyName] is JArray references))
            {
                throw new InvalidDataException(label + " 必须为数组。");
            }

            for (int index = 0; index < references.Count; index++)
            {
                if (references[index].Type != JTokenType.String)
                {
                    throw new InvalidDataException(label + "[" + index + "] 必须为 node:<id> 字符串。");
                }

                references[index] = ResolveNodeReference(references[index].Value<string>(), label + "[" + index + "]", context);
            }
        }

        private static void ResolveNodeProperty(
            JObject owner,
            string propertyName,
            string label,
            PsdHierarchyChatContext context,
            bool allowWrapperReference)
        {
            JToken token = owner[propertyName];
            if (token == null || token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>()))
            {
                throw new InvalidDataException(label + " 必须为非空字符串。");
            }

            string reference = token.Value<string>();
            if (allowWrapperReference && reference.StartsWith("@", StringComparison.Ordinal))
            {
                return;
            }

            owner[propertyName] = ResolveNodeReference(reference, label, context);
        }

        private static string ResolveNodeReference(
            string reference,
            string label,
            PsdHierarchyChatContext context)
        {
            const string prefix = "node:";
            if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(label + " 必须使用当前快照中的 node:<id>，不能填写层级路径。");
            }

            string nodeId = reference.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(nodeId) || !context.TryGetNodePath(nodeId, out string path))
            {
                throw new InvalidDataException(label + " 引用的节点 " + nodeId + " 在当前快照中不存在。");
            }

            return path;
        }

        private static bool TryValidateCurrentSnapshot(PsdHierarchyChatContext context, out string error)
        {
            try
            {
                string prefabFullPath = Path.Combine(
                    context.projectRoot,
                    context.targetPrefabAssetPath.Replace('/', Path.DirectorySeparatorChar));
                string currentFingerprint = PsdHierarchyChatContextBuilder.ComputeFileFingerprint(prefabFullPath);
                if (!string.Equals(
                        currentFingerprint,
                        context.hierarchySnapshotFingerprint,
                        StringComparison.Ordinal))
                {
                    error = "目标 Prefab 在方案生成后发生了变化，节点快照已经失效；请重新打开窗口分析。";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "无法验证目标 Prefab 节点快照：" + exception.Message;
                return false;
            }
        }

        internal static string SummarizeFailure(string detail)
        {
            string structuredError = TryExtractStructuredFailure(detail);
            if (!string.IsNullOrWhiteSpace(structuredError))
            {
                return TrimRunnerFailureNoise(structuredError);
            }

            using (var reader = new StringReader(detail ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int marker = line.IndexOf("error:", StringComparison.OrdinalIgnoreCase);
                    if (marker >= 0)
                    {
                        return TrimRunnerFailureNoise(line.Substring(marker + "error:".Length));
                    }
                }
            }

            return string.IsNullOrWhiteSpace(detail) ? "执行器没有返回具体原因" : TrimRunnerFailureNoise(detail);
        }

        private static string TryExtractStructuredFailure(string detail)
        {
            try
            {
                var envelope = JObject.Parse((detail ?? string.Empty).Trim());
                foreach (string propertyName in new[] { "ErrorMessage", "error", "Error", "Exception", "message", "Message" })
                {
                    JToken value = envelope[propertyName];
                    if (value == null || value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>()))
                    {
                        continue;
                    }

                    string message = value.Value<string>().Trim();
                    string nestedError = TryExtractStructuredFailure(message);
                    return string.IsNullOrWhiteSpace(nestedError) ? message : nestedError;
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Legacy runner output is not structured JSON.
            }

            return string.Empty;
        }

        private static string TrimRunnerFailureNoise(string message)
        {
            string summary = (message ?? string.Empty).Trim();
            int executionMarker = summary.IndexOf(" Execution exception:", StringComparison.OrdinalIgnoreCase);
            if (executionMarker >= 0)
            {
                summary = summary.Substring(0, executionMarker);
            }

            int stackMarker = summary.IndexOf(" Stack trace:", StringComparison.OrdinalIgnoreCase);
            if (stackMarker >= 0)
            {
                summary = summary.Substring(0, stackMarker);
            }

            foreach (string prefix in new[]
                     {
                         "Unity preflight failed:",
                         "Unity apply failed:",
                         "Unity compile failed:",
                         "Unity verification failed:",
                     })
            {
                if (summary.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    summary = summary.Substring(prefix.Length).TrimStart();
                    break;
                }
            }

            return summary.Trim();
        }

        private static void DeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // A stale diagnostic file must not mask the renderer result.
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }

        private static string ResolveRunnerPath(PsdHierarchyChatContext context)
        {
            if (context != null && !string.IsNullOrEmpty(context.skillFullPath))
            {
                string skillDirectory = Path.GetDirectoryName(context.skillFullPath);
                if (!string.IsNullOrEmpty(skillDirectory))
                {
                    return Path.Combine(skillDirectory, "scripts", "run_prefab_hierarchy_cleanup.ps1");
                }
            }

            return ResolveRunnerPath(context == null ? string.Empty : context.projectRoot);
        }

        private static string ResolveRunnerPath(string projectRoot)
        {
            PsdHierarchyChatContextBuilder.TryResolvePackageFilePath(
                projectRoot,
                string.Empty,
                CleanupRunnerRelativePath,
                out string runnerPath);
            return runnerPath;
        }

        private static string ToFullPath(string projectRoot, string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
