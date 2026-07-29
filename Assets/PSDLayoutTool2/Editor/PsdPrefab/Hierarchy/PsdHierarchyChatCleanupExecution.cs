namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

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
                ValidateRootPlanShape(plan, 2L, true);
                ValidatePlanTarget(plan, context.targetPrefabAssetPath);

                string fingerprint = ReadRequiredString(plan, "snapshotFingerprint");
                if (string.IsNullOrWhiteSpace(context.hierarchySnapshotFingerprint) ||
                    !string.Equals(fingerprint, context.hierarchySnapshotFingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("计划引用的层级快照已经失效，请重新分析当前 Prefab。");
                }

                NormalizeSingleStateVariantExtractions(plan, context);
                ValidateAllExistingNodeReferences(plan, context);
                ValidateRequiredComponentFamilyDecisions(plan, context);
                ResolveExistingNodeReferences(plan, context);
                NormalizeStatefulInstanceMappings(plan, context);
                NormalizeDirectChildVerificationNames(plan);
                WriteRequiredComponentFamilies(plan, context);
                WriteContainmentFindings(plan, context);
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
                return PsdHierarchyNativeCleanupExecutor.Validate(runnerPlanJson);
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
            string planJson)
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
                    PsdHierarchyNativeCleanupExecutor.Apply(runnerPlanJson);
                return nativeResult.success
                    ? PersistCompletedReplayStage(context, runnerPlanJson, nativeResult)
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

                return PersistCompletedReplayStage(context, runnerPlanJson, result);
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
            PsdHierarchyChatCleanupExecutionResult result)
        {
            try
            {
                PsdHierarchyCleanupReplayProfile.Persist(
                    context.sourcePsdAssetPath,
                    context.targetPrefabAssetPath,
                    runnerPlanJson);
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
                return PsdHierarchyNativeCleanupExecutor.Apply(runnerPlanJson);
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
            PsdHierarchyCleanupExecutionBackend selectedBackend = ResolveExecutionBackend();
            return selectedBackend == PsdHierarchyCleanupExecutionBackend.NativeUnity &&
                   PsdHierarchyNativeCleanupExecutor.RequiresUloopRunner(runnerPlanJson)
                ? PsdHierarchyCleanupExecutionBackend.UloopRunner
                : selectedBackend;
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

        private static void ValidateRootPlanShape(JObject plan, long expectedVersion, bool requireSnapshotFingerprint)
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

            ReadRequiredString(plan, "prefabName");
            foreach (string property in RequiredArrayProperties)
            {
                if (!(plan[property] is JArray))
                {
                    throw new InvalidDataException("计划缺少数组字段 " + property + "。");
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

        private static void NormalizeStatefulInstanceMappings(
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
                    if (directChildNames.Any(string.IsNullOrWhiteSpace) ||
                        directChildNames.Distinct(StringComparer.Ordinal).Count() != directChildNames.Length)
                    {
                        throw new InvalidDataException(
                            instanceLabel + ".source has duplicate or empty direct-child names after planned renames.");
                    }

                    int expectedCommonMemberCount = commonMembers.Count;
                    if (directChildNames.Length != expectedCommonMemberCount + expectedStateMemberCount)
                    {
                        throw new InvalidDataException(
                            instanceLabel + " cannot derive Common/State mappings: snapshot has " +
                            directChildNames.Length + " direct children but the contracts require " +
                            expectedCommonMemberCount + " Common plus " + expectedStateMemberCount +
                            " selected-state members.");
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
                            throw new InvalidDataException(
                                instanceLabel + ".commonSourceNames do not leave exactly one member for every " +
                                "selected-state contract entry.");
                        }

                        stateSourceNames = MappingHasSameMembers(stateSourceNames, stateComplement)
                            ? stateSourceNames
                            : stateComplement;
                        stateMappingIsComplete = true;
                    }
                    else if (stateMappingIsComplete)
                    {
                        string[] commonComplement = ComplementDirectChildNames(directChildNames, stateSourceNames);
                        if (commonComplement.Length != expectedCommonMemberCount)
                        {
                            throw new InvalidDataException(
                                instanceLabel + ".stateSourceNames do not leave exactly one member for every " +
                                "Common contract entry.");
                        }

                        commonSourceNames = MappingHasSameMembers(commonSourceNames, commonComplement)
                            ? commonSourceNames
                            : commonComplement;
                        commonMappingIsComplete = true;
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
