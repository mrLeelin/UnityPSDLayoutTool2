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
            "Assets/UnityPSDLayoutTool2/.agents/skills/prefab-hierarchy-cleanup/scripts/run_prefab_hierarchy_cleanup.ps1";

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

                ValidateAllExistingNodeReferences(plan, context);
                ResolveExistingNodeReferences(plan, context);
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

            string runnerPath = ToFullPath(context.projectRoot, CleanupRunnerRelativePath);
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

            string runnerPath = ToFullPath(context.projectRoot, CleanupRunnerRelativePath);
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
                return await Task.Run(() => RunCleanup(runnerPath, context.projectRoot, planPath));
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
