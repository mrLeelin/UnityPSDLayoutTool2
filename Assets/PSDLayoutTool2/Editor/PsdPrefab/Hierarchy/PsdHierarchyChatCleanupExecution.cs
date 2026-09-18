namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEditor;

    internal enum PsdHierarchyCleanupExecutionState
    {
        Rejected,
        Success,
        Partial,
        Uncertain,
    }

    internal readonly struct PsdHierarchyChatCleanupExecutionResult
    {
        internal PsdHierarchyChatCleanupExecutionResult(bool success, string message)
            : this(
                success ? PsdHierarchyCleanupExecutionState.Success : PsdHierarchyCleanupExecutionState.Rejected,
                string.Empty,
                message)
        {
        }

        internal PsdHierarchyChatCleanupExecutionResult(
            PsdHierarchyCleanupExecutionState state,
            string stage,
            string message)
        {
            this.success = state == PsdHierarchyCleanupExecutionState.Success;
            this.state = state;
            this.stage = stage ?? string.Empty;
            this.message = message ?? string.Empty;
        }

        internal readonly bool success;
        internal readonly PsdHierarchyCleanupExecutionState state;
        internal readonly string stage;
        internal readonly string message;
    }

    internal readonly struct PsdHierarchyLocalRepairAnalysisResult
    {
        internal PsdHierarchyLocalRepairAnalysisResult(
            bool success,
            string planJson,
            string review,
            string error,

            PsdHierarchyVisualAnalysisResult visualAnalysis = null)
        {
            this.success = success;
            this.planJson = planJson ?? string.Empty;
            this.review = review ?? string.Empty;
            this.error = error ?? string.Empty;
            this.visualAnalysis = visualAnalysis;
        }

        internal readonly bool success;
        internal readonly string planJson;
        internal readonly string review;
        internal readonly string error;
        internal readonly PsdHierarchyVisualAnalysisResult visualAnalysis;
    }

    /// <summary>
        /// Owns the v2 cleanup-plan boundary. The AI returns data only; this
        /// class validates and executes the same v2 document after confirmation.
    /// </summary>
    internal static class PsdHierarchyChatCleanupExecution
    {
        private const string ReusableItemFallbackName = "ReusableItem";
        private static readonly Regex PlanFailureCandidateIdRegex = new Regex(
            @"(?:candidateId|candidate)=(?<id>[A-Za-z0-9_.-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PlanFailureNodeIdRegex = new Regex(
            @"\bnode:(?<id>[A-Za-z0-9_.-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static PsdHierarchyPlanIssue ClassifyPlanFailure(
            PsdHierarchyPlanIssueCategory category,
            string error)
        {
            string technicalDetails = error ?? string.Empty;
            string[] candidateIds = PlanFailureCandidateIdRegex.Matches(technicalDetails)
                .Cast<Match>()
                .Select(match => match.Groups["id"].Value)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string[] nodeIds = PlanFailureNodeIdRegex.Matches(technicalDetails)
                .Cast<Match>()
                .Select(match => match.Groups["id"].Value)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return PsdHierarchyPlanWorkspace.CreateIssue(
                category,
                category == PsdHierarchyPlanIssueCategory.Infrastructure
                    ? PsdHierarchyPlanIssueSeverity.Blocked
                    : PsdHierarchyPlanIssueSeverity.NeedsDecision,
                GetPlanFailureSummary(category),
                technicalDetails,
                category == PsdHierarchyPlanIssueCategory.Infrastructure
                    ? "请重试或刷新快照。"
                    : "保持原结构，或重新分析此项。",
                candidateIds,
                nodeIds);
        }

        internal static PsdHierarchyPlanWorkspace CreateReadyWorkspace(
            PsdHierarchyChatContext context,
            string reviewText,
            string rawAssistantReply,
            string validatedPlanJson)
        {
            return PsdHierarchyPlanWorkspace.CreateReady(
                context?.hierarchySnapshotFingerprint,
                reviewText,
                rawAssistantReply,
                validatedPlanJson);
        }

        internal static PsdHierarchyPlanWorkspace CreateIssueWorkspace(
            PsdHierarchyChatContext context,
            string reviewText,
            string rawAssistantReply,
            PsdHierarchyPlanIssueCategory category,
            string error,
            string quarantinedPlanJson = "")
        {
            PsdHierarchyPlanIssue issue = ClassifyPlanFailure(category, error);
            if (category == PsdHierarchyPlanIssueCategory.Infrastructure)
            {
                return PsdHierarchyPlanWorkspace.CreateInfrastructureBlocked(
                    context?.hierarchySnapshotFingerprint,
                    reviewText,
                    rawAssistantReply,
                    issue.technicalDetails,
                    issue.recommendedResolution,
                    quarantinedPlanJson);
            }

            return PsdHierarchyPlanWorkspace.CreateBlockedPlan(
                context?.hierarchySnapshotFingerprint,
                reviewText,
                rawAssistantReply,
                category,
                issue.summary,
                issue.technicalDetails,
                issue.recommendedResolution,
                issue.candidateIds,
                issue.affectedNodeIds,
                quarantinedPlanJson);
        }

        private static string GetPlanFailureSummary(PsdHierarchyPlanIssueCategory category)
        {
            switch (category)
            {
                case PsdHierarchyPlanIssueCategory.PlanExtraction:
                    return "AI 返回内容缺少可读取的计划。";
                case PsdHierarchyPlanIssueCategory.PlanPreparation:
                    return "计划包含相互冲突或无法确定的结构操作。";
                case PsdHierarchyPlanIssueCategory.RunnerPreflight:
                    return "计划未通过执行前校验。";
                case PsdHierarchyPlanIssueCategory.Infrastructure:
                    return "分析或校验环境当前不可用。";
                default:
                    return "计划需要进一步处理。";
            }
        }

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
            "containmentResolutions",
            "flatSiblingResolutions",
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
                    "selectedPrefabExtractions",
                    "crossParentPrefabExtractions",
                    "postGroupingExtractionIntents",
                }),
                StringComparer.Ordinal);

        private static readonly HashSet<string> SupportedRootProperties =
            new HashSet<string>(
                SupportedRootArrayProperties.Concat(new[]
                {
                    "version",
                    "snapshotFingerprint",
                    "prefabAssetPath",
                    "output",
                    "prefabName",
                    "verify",
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
                   normalized == "同意" ||
                   normalized == "满意" ||
                   normalized == "满意了";
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

        internal static bool TryBuildSelectedPrefabExtractionPlan(
            PsdHierarchyChatContext context,
            PsdHierarchyLocalRepairScope scope,
            string componentName,
            out string planJson,
            out string review,
            out string error)
        {
            planJson = string.Empty;
            review = string.Empty;
            error = string.Empty;
            if (context == null || scope == null)
            {
                error = "请先使用当前 Hierarchy 选择锁定要抽取的节点。";
                return false;
            }

            if (!scope.TryCreateSelectedPrefabExtraction(context, componentName, out PsdHierarchySelectedPrefabExtraction extraction, out error))
            {
                return false;
            }

            string extractionId = ToLowerSnakeCaseIdentifier(extraction.componentName);
            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabAssetPath"] = context.targetPrefabAssetPath,
                ["output"] = new JObject
                {
                    ["mode"] = "in_place",
                    ["assetPath"] = context.targetPrefabAssetPath,
                },
                ["prefabName"] = Path.GetFileNameWithoutExtension(context.targetPrefabAssetPath),
                ["wrappers"] = new JArray(),
                ["moves"] = new JArray(),
                ["renames"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["textureRenames"] = new JArray(),
                ["spriteAtlasRenames"] = new JArray(),
                ["componentFamilyDecisions"] = new JArray(),
                ["componentExtractions"] = new JArray(),
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
                ["flatSiblingResolutions"] = new JArray(),
                ["selectedPrefabExtractions"] = new JArray(new JObject
                {
                    ["id"] = extractionId,
                    ["name"] = extraction.componentName,
                    ["assetPath"] = extraction.assetPath,
                    ["parent"] = "node:" + extraction.parentNodeId,
                    ["sources"] = new JArray(extraction.sourceNodeIds.Select(id => "node:" + id)),
                }),
                ["verify"] = new JObject(),
            };
            planJson = plan.ToString(Newtonsoft.Json.Formatting.None);
            review = "将当前选择的 " + extraction.sourceNodeIds.Length + " 个同级节点抽取为 Nested Prefab `" +
                     extraction.componentName + "`，保存到 `" + extraction.assetPath +
                     "`；外层 Prefab 只会以该 Nested Prefab 实例替换选区。";
            return true;
        }

        /// <summary>
        /// 构建局部整理方案（包含视觉相似度分析）
        /// </summary>
        internal static async Task<PsdHierarchyLocalRepairAnalysisResult> TryBuildLocalPrefabOrganizationPlanWithVisualAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyLocalRepairScope scope,
            string componentName)
        {
            // 先尝试构建基础方案（纯逻辑）
            if (!TryBuildLocalPrefabOrganizationPlan(
                    context,
                    scope,
                    componentName,
                    out string planJson,
                    out string review,
                    out string error))
            {
                return new PsdHierarchyLocalRepairAnalysisResult(false, string.Empty, string.Empty, error, null);
            }

            // 检查是否是跨父级组件化（需要视觉分析）
            if (!scope.TryCreateCrossParentPrefabExtraction(
                    context,
                    componentName,
                    out PsdHierarchyCrossParentPrefabExtraction extraction,
                    out string _))
            {
                // 不是跨父级模式，返回基础方案
                return new PsdHierarchyLocalRepairAnalysisResult(true, planJson, review, string.Empty, null);
            }

            // 执行视觉分析
            PsdHierarchyVisualAnalysisResult visualAnalysis = null;
            try
            {
                // TODO: 调用 AI 进行视觉分析
                // 暂时返回模拟数据用于测试
                visualAnalysis = await PerformVisualAnalysisAsync(context, extraction);
            }
            catch (Exception exception)
            {
                // 视觉分析失败不影响基础方案
                UnityEngine.Debug.LogWarning("视觉分析失败: " + exception.Message);
            }

            return new PsdHierarchyLocalRepairAnalysisResult(true, planJson, review, string.Empty, visualAnalysis);
        }

        private static async Task<PsdHierarchyVisualAnalysisResult> PerformVisualAnalysisAsync(
            PsdHierarchyChatContext context,
            PsdHierarchyCrossParentPrefabExtraction extraction)
        {
            // TODO: 实现真实的视觉分析
            // 1. 生成缩略图
            // 2. 调用 AI API 分析
            // 3. 解析评分结果

            await Task.Delay(100); // 模拟异步操作

            // 返回模拟数据
            var scores = new List<PsdHierarchyVisualScore>();
            foreach (var instance in extraction.instances)
            {
                // 模板组给 100 分
                bool isTemplate = instance.sourceNodeIds.SequenceEqual(extraction.templateSourceNodeIds);
                int score = isTemplate ? 100 : 85;
                string reason = isTemplate ? "模板组" : "视觉高度相似";

                foreach (string nodeId in instance.sourceNodeIds)
                {
                    scores.Add(new PsdHierarchyVisualScore(nodeId, score, reason));
                }
            }

            return new PsdHierarchyVisualAnalysisResult(
                extraction.templateSourceNodeIds,
                scores,
                "mock_analysis_v1");
        }

        internal static bool TryBuildLocalPrefabOrganizationPlan(
            PsdHierarchyChatContext context,
            PsdHierarchyLocalRepairScope scope,
            string componentName,
            out string planJson,
            out string review,
            out string error)
        {
            if (TryBuildSelectedPrefabExtractionPlan(
                    context,
                    scope,
                    componentName,
                    out planJson,
                    out review,
                    out error))
            {
                return true;
            }

            string crossParentError = string.Empty;
            if (context == null || scope == null ||
                !scope.TryCreateCrossParentPrefabExtraction(
                    context,
                    componentName,
                    out PsdHierarchyCrossParentPrefabExtraction extraction,
                    out crossParentError))
            {
                error = string.IsNullOrWhiteSpace(crossParentError) ? error : crossParentError;
                planJson = string.Empty;
                review = string.Empty;
                return false;
            }

            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabAssetPath"] = context.targetPrefabAssetPath,
                ["output"] = new JObject
                {
                    ["mode"] = "in_place",
                    ["assetPath"] = context.targetPrefabAssetPath,
                },
                ["prefabName"] = Path.GetFileNameWithoutExtension(context.targetPrefabAssetPath),
                ["wrappers"] = new JArray(),
                ["moves"] = new JArray(),
                ["renames"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["textureRenames"] = new JArray(),
                ["spriteAtlasRenames"] = new JArray(),
                ["componentFamilyDecisions"] = new JArray(),
                ["componentExtractions"] = new JArray(),
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
                ["flatSiblingResolutions"] = new JArray(),
                ["crossParentPrefabExtractions"] = new JArray(new JObject
                {
                    ["id"] = ToLowerSnakeCaseIdentifier(extraction.componentName),
                    ["name"] = extraction.componentName,
                    ["assetPath"] = extraction.assetPath,
                    ["root"] = "node:" + extraction.rootNodeId,
                    ["templateSources"] = new JArray(
                        extraction.templateSourceNodeIds.Select(id => "node:" + id)),
                    ["instances"] = new JArray(extraction.instances.Select(instance => new JObject
                    {
                        ["sequence"] = instance.sequence,
                        ["sources"] = new JArray(instance.sourceNodeIds.Select(id => "node:" + id)),
                    })),
                    ["unmatched"] = new JArray(extraction.unmatchedNodeIds.Select(id => "node:" + id)),
                }),
                ["verify"] = new JObject(),
            };

            planJson = plan.ToString(Newtonsoft.Json.Formatting.None);
            review = "Cross-parent local organization will create `" + extraction.componentName +
                     "`, replace " + extraction.instances.Length + " complete groups, and leave " +
                     extraction.unmatchedNodeIds.Length + " unmatched nodes unchanged.";
            error = string.Empty;
            return true;
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

            if (!TryPrepareExecutionPlan(
                    context,
                    planJson,
                    out _,
                    out string preparationError))
            {
                planJson = string.Empty;
                error = "AI 返回的计划不能安全执行：" + preparationError;
                return false;
            }

            // Keep the reviewed JSON text itself. Formal preparation validates
            // it again before execution and must never replace it with a
            // derived operation set.
            error = string.Empty;
            return true;
        }

        internal static bool TryPrepareExecutionPlan(
            PsdHierarchyChatContext context,
            string planJson,
            out string executionPlanJson,
            out string error)
        {
            executionPlanJson = string.Empty;
            if (context == null)
            {
                error = "缺少当前整理目标。";
                return false;
            }

            try
            {
                var plan = JObject.Parse(planJson ?? string.Empty);
                ValidateRootPlanShape(plan, 2L, true, false);
                ValidatePlanTarget(plan, context.targetPrefabAssetPath);

                string fingerprint = ReadRequiredString(plan, "snapshotFingerprint");
                if (string.IsNullOrWhiteSpace(context.hierarchySnapshotFingerprint) ||
                    !string.Equals(fingerprint, context.hierarchySnapshotFingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("计划引用的层级快照已经失效，请重新分析当前 Prefab。");
                }

                PsdHierarchyPostGroupingExtraction.ValidateIntents(plan);
                ValidateAllExistingNodeReferences(plan, context);
                if (context.localRepairScope != null)
                {
                    context.localRepairScope.ValidatePlan(plan);
                }
                else if (HasLocalPrefabExtractions(plan))
                {
                    throw new InvalidDataException("selectedPrefabExtractions 只能由已锁定选区的局部修复生成。");
                }

                // Formal execution consumes the reviewed v2 document itself.
                // Preparation validates it but must not expand or rewrite the
                // operation set that the user confirmed.
                executionPlanJson = plan.ToString(Newtonsoft.Json.Formatting.None);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException ||
                exception is InvalidDataException ||
                exception is InvalidOperationException)
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
                return new PsdHierarchyChatCleanupExecutionResult(PsdHierarchyCleanupExecutionState.Rejected, "context", "缺少当前整理目标。");
            }

            if (!TryPrepareExecutionPlan(context, planJson, out string executionPlanJson, out string preparationError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(
                    PsdHierarchyCleanupExecutionState.Rejected,
                    "prepare",
                    "AI 返回的节点 ID 计划无效：" + preparationError);
            }

            if (!TryValidateCurrentSnapshot(context, out string snapshotError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(
                    PsdHierarchyCleanupExecutionState.Rejected,
                    "snapshot",
                    snapshotError);
            }

            return await PsdHierarchyNativeCleanupExecutor.ValidateAsync(context, executionPlanJson);
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ApplyConfirmedAsync(
            PsdHierarchyChatContext context,
            string planJson,
            bool replaceReplayProfile = false)
        {
            if (context == null)
            {
                return new PsdHierarchyChatCleanupExecutionResult(PsdHierarchyCleanupExecutionState.Rejected, "context", "缺少当前整理目标。");
            }

            if (!TryPrepareExecutionPlan(context, planJson, out string executionPlanJson, out string preparationError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(
                    PsdHierarchyCleanupExecutionState.Rejected,
                    "prepare",
                    "已确认的节点 ID 计划无效：" + preparationError);
            }

            if (!TryValidateCurrentSnapshot(context, out string snapshotError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(
                    PsdHierarchyCleanupExecutionState.Rejected,
                    "snapshot",
                    snapshotError);
            }

            PsdHierarchyChatCleanupExecutionResult nativeResult =
                await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, executionPlanJson);
            if (!nativeResult.success)
            {
                return nativeResult;
            }

            // v2 成功后写入重放 Profile，供增量更新阶段复用。
            return PersistCompletedReplayStage(
                context,
                executionPlanJson,
                nativeResult,
                replaceReplayProfile);
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ReapplyPersistedPlanAsync(
            string projectRoot,
            string planJson)
        {
            // ADR 0002：不再走 Python CLI。v2 重放需要新的权威快照，无法仅凭落盘计划自动执行。
            await Task.Yield();
            return await PsdHierarchyNativeCleanupExecutor.ReapplyAsync(projectRoot, planJson);
        }

        /// <summary>
        /// 重放无法证明节点对应关系时的稳定失败标识：既用于执行结果，也用于让重放协调器把
        /// 这次失败判定为永久失败（标记需要重新分析），而不是反复重试。
        /// 触发条件：v1 路径计划、缺少节点绑定证据、或重新生成结果上找不到唯一对应节点。
        /// </summary>
        internal const string ReplayRequiresFreshAnalysisMessage =
            "整理重放需要基于当前生成结果的权威快照重新分析并确认新计划：该记录缺少可证明的节点对应关系（v1 路径计划或绑定证据不足），无法自动重放。" +
            "原有业务 Prefab 未被覆盖。";

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
                // 保存阶段同时记录被引用节点的身份证据：v2 快照的节点 ID 是位置编号，
                // 只有证据才能让 PSD 更新后的重放证明对应关系（见 PsdHierarchyReplayBinding）。
                string bindingJson = string.Empty;
                if (!PsdHierarchyReplayBinding.TryBuildForPlan(
                        JObject.Parse(runnerPlanJson),
                        context.hierarchySnapshotJson,
                        out bindingJson,
                        out string bindingError))
                {
                    return new PsdHierarchyChatCleanupExecutionResult(
                        PsdHierarchyCleanupExecutionState.Partial,
                        "replay-binding",
                        "Prefab 已更新，但整理重放节点绑定记录失败，PSD 更新后需要重新分析：" + bindingError);
                }

                if (replaceReplayProfile)
                {
                    PsdHierarchyCleanupReplayProfile.ReplaceWithFirstStage(
                        context.sourcePsdAssetPath,
                        context.targetPrefabAssetPath,
                        runnerPlanJson,
                        bindingJson);
                }
                else
                {
                    PsdHierarchyCleanupReplayProfile.Persist(
                        context.sourcePsdAssetPath,
                        context.targetPrefabAssetPath,
                        runnerPlanJson,
                        bindingJson);
                }
                return result;
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatCleanupExecutionResult(
                    PsdHierarchyCleanupExecutionState.Partial,
                    "replay-profile",
                    result.message + Environment.NewLine +
                    "Prefab 已更新，但整理重放 Profile 保存失败：" + exception.Message);
            }
        }

        private static PsdHierarchyCleanupExecutionBackend ResolveExecutionBackend()
        {
            // ADR 0001/0002：正式清理只在 Unity 共享核心执行；CLI Runner 已退役。
            return PsdHierarchyCleanupExecutionBackend.NativeUnity;
        }

        private static PsdHierarchyCleanupExecutionBackend ResolveExecutionBackendForPlan(string runnerPlanJson)
        {
            return ResolveExecutionBackendForPlan(ResolveExecutionBackend(), runnerPlanJson);
        }

        internal static PsdHierarchyCleanupExecutionBackend ResolveExecutionBackendForPlan(
            PsdHierarchyCleanupExecutionBackend selectedBackend,
            string runnerPlanJson)
        {
            if (HasLocalPrefabExtractions(runnerPlanJson))
            {
                return PsdHierarchyCleanupExecutionBackend.NativeUnity;
            }

            // v2（node:<id>）执行计划只由 NativeUnity 消费。
            // CLI 的 render_prefab_cleanup.py 仍要求 version=1（路径版 runner 计划），
            // 把 v2 直接丢给 Python 会报 "version must be 1"。
            if (IsVersionTwoPlan(runnerPlanJson))
            {
                return PsdHierarchyCleanupExecutionBackend.NativeUnity;
            }

            return selectedBackend;
        }

        private static bool IsVersionTwoPlan(string planJson)
        {
            if (string.IsNullOrWhiteSpace(planJson))
            {
                return false;
            }

            try
            {
                var plan = JObject.Parse(planJson);
                JToken version = plan["version"];
                return version != null && version.Type == JTokenType.Integer && version.Value<int>() == 2;
            }
            catch (Exception)
            {
                return false;
            }
        }


        private static bool HasSelectedPrefabExtractions(JObject plan)
        {
            return plan?["selectedPrefabExtractions"] is JArray extractions && extractions.Count > 0;
        }

        private static bool HasCrossParentPrefabExtractions(JObject plan)
        {
            return plan?["crossParentPrefabExtractions"] is JArray extractions && extractions.Count > 0;
        }

        private static bool HasLocalPrefabExtractions(JObject plan)
        {
            return HasSelectedPrefabExtractions(plan) || HasCrossParentPrefabExtractions(plan);
        }

        private static bool HasSelectedPrefabExtractions(string planJson)
        {
            try
            {
                return HasSelectedPrefabExtractions(JObject.Parse(planJson ?? string.Empty));
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        private static bool HasLocalPrefabExtractions(string planJson)
        {
            try
            {
                return HasLocalPrefabExtractions(JObject.Parse(planJson ?? string.Empty));
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        internal static string ExtractReviewText(string assistantReply)
        {
            string content = (assistantReply ?? string.Empty).TrimStart('\uFEFF').Trim();
            Match marker = JsonCodeBlockOpeningRegex.Match(content);
            return !marker.Success ? content : content.Substring(0, marker.Index).Trim();
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

        internal static string ExtractJsonCodeBlock(string value)
        {
            string content = (value ?? string.Empty).TrimStart('\uFEFF').Trim();
            Match marker = JsonCodeBlockOpeningRegex.Match(content);
            if (!marker.Success)
            {
                // Some CLI providers return the requested JSON without Markdown fencing.
                // Accept it only when the entire response is a JSON object; schema and
                // execution validation still run before the plan can be confirmed.
                return content.StartsWith("{", StringComparison.Ordinal) &&
                       content.EndsWith("}", StringComparison.Ordinal)
                    ? content
                    : string.Empty;
            }

            int start = marker.Index + marker.Length;
            int end = content.IndexOf("```", start, StringComparison.Ordinal);
            return end < 0 ? string.Empty : content.Substring(start, end - start).Trim();
        }

        private static readonly Regex JsonCodeBlockOpeningRegex = new Regex(
            @"```[ \t]*(?:json)?[ \t]*(?:\r?\n|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
                if (SupportedRootProperties.Contains(property.Name))
                {
                    continue;
                }

                bool isEmptyArray = property.Value is JArray array && array.Count == 0;
                if (!isEmptyArray && property.Value.Type != JTokenType.Null)
                {
                    throw new InvalidDataException(
                        "Unsupported non-empty plan field: " + property.Name +
                        ". Refusing to silently ignore unknown operations or metadata.");
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
            if (plan["selectedPrefabExtractions"] is JArray)
            {
                AddObjectPropertySlots(plan, "selectedPrefabExtractions", "parent", false, slots, errors);
                AddStringArraySlots(plan, "selectedPrefabExtractions", "sources", slots, errors);
            }
            if (plan["crossParentPrefabExtractions"] is JArray)
            {
                AddObjectPropertySlots(plan, "crossParentPrefabExtractions", "root", false, slots, errors);
                AddStringArraySlots(plan, "crossParentPrefabExtractions", "templateSources", slots, errors);
                AddNestedStringArraySlots(plan, "crossParentPrefabExtractions", "instances", "sources", slots, errors);
                AddStringArraySlots(plan, "crossParentPrefabExtractions", "unmatched", slots, errors);
            }

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
                    errors.Add(
                        slot.label + " 必须使用当前快照中的 node:<id>，不能填写层级路径。" +
                        " 正确示例：\"node:n000010\"。请从 snapshot 的 componentFamilyCandidates 原样复制 parent/sources，不要用带 / 的路径。");
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



        // Transitional source compatibility for editor tests and callers. This
        // method no longer creates a v1 runner document; it returns the v2
        // execution plan unchanged after validation.
        internal static bool TryPrepareRunnerPlan(
            PsdHierarchyChatContext context,
            string planJson,
            out string executionPlanJson,
            out string error)
        {
            return TryPrepareExecutionPlan(context, planJson, out executionPlanJson, out error);
        }

        // 分组后抽取意图形状的唯一权威校验点是 PsdHierarchyPostGroupingExtraction.ValidateIntents：
        // 预检层与共享执行核心调用同一个方法，避免两份规则漂移。











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

        private static void AddNestedStringArraySlots(
            JObject plan,
            string arrayProperty,
            string nestedArrayProperty,
            string referencesProperty,
            List<NodeReferenceSlot> slots,
            List<string> errors)
        {
            if (!(plan[arrayProperty] is JArray items))
            {
                errors.Add(arrayProperty + " must be an array.");
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!(items[index] is JObject item) || !(item[nestedArrayProperty] is JArray nestedItems))
                {
                    errors.Add(arrayProperty + "[" + index + "]." + nestedArrayProperty + " must be an array.");
                    continue;
                }

                for (int nestedIndex = 0; nestedIndex < nestedItems.Count; nestedIndex++)
                {
                    if (!(nestedItems[nestedIndex] is JObject nestedItem) ||
                        !(nestedItem[referencesProperty] is JArray references))
                    {
                        errors.Add(arrayProperty + "[" + index + "]." + nestedArrayProperty + "[" + nestedIndex + "]." + referencesProperty + " must be an array.");
                        continue;
                    }

                    for (int referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
                    {
                        slots.Add(new NodeReferenceSlot(
                            references[referenceIndex],
                            arrayProperty + "[" + index + "]." + nestedArrayProperty + "[" + nestedIndex + "]." + referencesProperty + "[" + referenceIndex + "]",
                            false));
                    }
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








    }
}
