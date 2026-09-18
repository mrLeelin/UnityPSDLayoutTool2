namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Executes reviewed cleanup plans in the current Unity Editor process.
    /// Basic v2 hierarchy operations execute directly; capabilities awaiting
    /// migration are rejected before saving any assets.
    /// </summary>
    internal static class PsdHierarchyNativeCleanupExecutor
    {
        private static string GetProjectAssetFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, (assetPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
        }

        internal static Task<PsdHierarchyChatCleanupExecutionResult> ValidateAsync(
            PsdHierarchyChatContext context,
            string planJson)
        {
            return Task.FromResult(ExecuteV2(context, planJson, save: false));
        }

        internal static Task<PsdHierarchyChatCleanupExecutionResult> ApplyAsync(
            PsdHierarchyChatContext context,
            string planJson)
        {
            PsdHierarchyChatCleanupExecutionResult preflight = ExecuteV2(context, planJson, save: false);
            return Task.FromResult(preflight.success ? ExecuteV2(context, planJson, save: true) : preflight);
        }

        internal static Task<PsdHierarchyChatCleanupExecutionResult> ReapplyAsync(
            string projectRoot,
            string planJson)
        {
            JObject plan;
            try
            {
                plan = JObject.Parse(planJson ?? string.Empty);
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                return Task.FromResult(Result(PsdHierarchyCleanupExecutionState.Rejected, "replay",
                    "重放计划无法解析：" + exception.Message));
            }

            if (plan.Value<int?>("version") != 2)
            {
                // v1 路径计划没有节点身份证据，无法在重新生成的结果上证明对应关系。
                return Task.FromResult(Result(PsdHierarchyCleanupExecutionState.Rejected, "replay",
                    PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage));
            }

            string targetAssetPath = (plan.Value<string>("prefabAssetPath") ?? string.Empty).Trim().Replace('\\', '/');
            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    string.Empty,
                    targetAssetPath,
                    out PsdHierarchyChatContext context,
                    out string contextError))
            {
                return Task.FromResult(Result(PsdHierarchyCleanupExecutionState.Rejected, "replay",
                    "重放无法为重新生成的结果建立权威快照：" + contextError));
            }

            return Task.FromResult(ExecuteV2(
                context,
                planJson,
                save: true,
                replayOfConfirmedStage: true));
        }

        internal static PsdHierarchyChatCleanupExecutionResult ExecuteV2(
            PsdHierarchyChatContext context,
            string planJson,
            bool save,
            Action<GameObject> unloadPrefabContents = null,
            Func<GameObject, string, GameObject> savePrefabAsset = null,
            bool replayOfConfirmedStage = false)
        {
            unloadPrefabContents = unloadPrefabContents ?? PrefabUtility.UnloadPrefabContents;
            savePrefabAsset = savePrefabAsset ?? ((contents, path) => PrefabUtility.SaveAsPrefabAsset(contents, path));
            if (!TryReadV2Plan(context, planJson, out JObject plan, out string prefabPath, out string error))
                return Result(PsdHierarchyCleanupExecutionState.Rejected, "preflight", error);

            JArray postGroupingIntents;
            try
            {
                postGroupingIntents = PsdHierarchyPostGroupingExtraction.ValidateIntents(plan);
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is InvalidOperationException)
            {
                return Result(PsdHierarchyCleanupExecutionState.Rejected, "preflight", exception.Message);
            }

            GameObject root = null;
            bool saveAttempted = false;
            bool businessWritesAttempted = false;
            string preservedMissingReferenceNotice = string.Empty;
            PersistedExpectations expectations = null;
            ValidatedCleanupPlan execution = null;
            IReadOnlyList<NativeAssetRename> renames = Array.Empty<NativeAssetRename>();
            IReadOnlyList<string> temporaryExtractionAssets = Array.Empty<string>();
            IReadOnlyList<string> temporaryStateAssets = Array.Empty<string>();
            IReadOnlyList<string> temporaryVariantAssets = Array.Empty<string>();
            IReadOnlyList<string> temporaryStatefulAssets = Array.Empty<string>();
            PsdHierarchyChatCleanupExecutionResult result = default;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                var existingMissingReferences = PersistedExpectations.CaptureMissingReferences(root);
                preservedMissingReferenceNotice = MissingReferenceNotice(existingMissingReferences);
                execution = BindPlan(root, plan, context, replayOfConfirmedStage);
                renames = PsdHierarchyNativeAssetRenamer.Bind(plan, prefabPath);
                if (save && renames.Count > 0)
                    businessWritesAttempted = true;
                PsdHierarchyNativeAssetRenamer.Apply(renames, save);
                ApplyPlan(root, execution);
                Action beforeBusinessWrite = () => businessWritesAttempted = true;
                PsdHierarchyPrefabExtraction.Apply(
                    root, execution.extractions, save, out temporaryExtractionAssets, replayOfConfirmedStage, beforeBusinessWrite);
                PsdHierarchyPrefabExtraction.ApplyStateExtractions(
                    root, execution.stateExtractions, save, out temporaryStateAssets, replayOfConfirmedStage, beforeBusinessWrite);
                PsdHierarchyPrefabExtraction.ApplyVariantExtractions(
                    root, execution.variantExtractions, save, out temporaryVariantAssets, replayOfConfirmedStage, beforeBusinessWrite);
                PsdHierarchyPrefabExtraction.ApplyStatefulExtractions(
                    root, execution.statefulExtractions, save, out temporaryStatefulAssets, replayOfConfirmedStage, beforeBusinessWrite);
                PsdHierarchyPrefabExtraction.ApplySelectedExtractions(
                    root, execution.selectedExtractions, save, replayOfConfirmedStage, beforeBusinessWrite);
                PsdHierarchyPrefabExtraction.ApplyCrossParentExtractions(
                    root, execution.crossParentExtractions, save, replayOfConfirmedStage, beforeBusinessWrite);
                execution.verification.Verify(root);
                PsdHierarchyPrefabExtraction.Verify(root, execution.extractions, save);
                PsdHierarchyPrefabExtraction.VerifyStateExtractions(root, execution.stateExtractions, save);
                PsdHierarchyPrefabExtraction.VerifyVariantExtractions(root, execution.variantExtractions, save);
                PsdHierarchyPrefabExtraction.VerifyStatefulExtractions(root, execution.statefulExtractions, save);
                PsdHierarchyPrefabExtraction.VerifySelectedExtractions(root, execution.selectedExtractions, save);
                PsdHierarchyPrefabExtraction.VerifyCrossParentExtractions(root, execution.crossParentExtractions, save);
                PsdHierarchyNativeAssetRenamer.Verify(renames, prefabPath, save);
                expectations = PersistedExpectations.Capture(root, existingMissingReferences);
                if (save)
                {
                    saveAttempted = true;
                    if (savePrefabAsset(root, prefabPath) == null)
                        throw new InvalidOperationException("Failed to save Prefab: " + prefabPath);
                }
                result = Result(PsdHierarchyCleanupExecutionState.Success, "preflight",
                    "PREFLIGHT_OK" + preservedMissingReferenceNotice);
            }
            catch (Exception exception)
            {
                // 改名和抽取都可能先写业务资产；主 Prefab 保存结果不确定时优先报告 uncertain。
                PsdHierarchyCleanupExecutionState state = saveAttempted
                    ? PsdHierarchyCleanupExecutionState.Uncertain
                    : businessWritesAttempted
                        ? PsdHierarchyCleanupExecutionState.Partial
                        : PsdHierarchyCleanupExecutionState.Rejected;
                string stage = saveAttempted ? "save" : businessWritesAttempted ? "assets" : "preflight";
                string prefix = saveAttempted
                    ? "Prefab save outcome is uncertain: "
                    : businessWritesAttempted
                        ? "Business assets were already written before the failure: "
                        : "Native Unity preflight failed: ";
                result = Result(state, stage, prefix + exception.Message);
            }
            finally
            {
                // 预检的一次性抽取资产必须在这里删除：核验需要它仍然是有效的 Prefab 资产。
                PsdHierarchyPrefabExtraction.CleanupTemporaryAssets(temporaryExtractionAssets);
                PsdHierarchyPrefabExtraction.CleanupTemporaryAssets(temporaryStateAssets);
                PsdHierarchyPrefabExtraction.CleanupTemporaryAssets(temporaryVariantAssets);
                PsdHierarchyPrefabExtraction.CleanupTemporaryAssets(temporaryStatefulAssets);
                result = UnloadContents(root, unloadPrefabContents, result, saveAttempted, "save-cleanup");
                root = null;
            }
            if (!result.success || !save)
                return result;

            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                execution.verification.Verify(root);
                PsdHierarchyPrefabExtraction.Verify(root, execution.extractions, true);
                PsdHierarchyPrefabExtraction.VerifyStateExtractions(root, execution.stateExtractions, true);
                PsdHierarchyPrefabExtraction.VerifyVariantExtractions(root, execution.variantExtractions, true);
                PsdHierarchyPrefabExtraction.VerifyStatefulExtractions(root, execution.statefulExtractions, true);
                PsdHierarchyPrefabExtraction.VerifySelectedExtractions(root, execution.selectedExtractions, true);
                PsdHierarchyPrefabExtraction.VerifyCrossParentExtractions(root, execution.crossParentExtractions, true);
                PsdHierarchyNativeAssetRenamer.Verify(renames, prefabPath, true);
                expectations.Verify(root);
                result = Result(PsdHierarchyCleanupExecutionState.Success, "verify",
                    "VERIFY_OK" + preservedMissingReferenceNotice);
            }
            catch (Exception exception)
            {
                result = Result(PsdHierarchyCleanupExecutionState.Partial, "verify",
                    "Prefab was saved but verification failed: " + exception.Message);
            }
            finally
            {
                result = UnloadContents(root, unloadPrefabContents, result, true, "verify-cleanup");
            }

            if (postGroupingIntents == null || postGroupingIntents.Count == 0 ||
                result.state != PsdHierarchyCleanupExecutionState.Success)
            {
                return result;
            }

            // 首阶段已保存并核验：用刷新后的权威快照解析已审阅的后续抽取意图，再走同一个共享核心。
            if (!PsdHierarchyChatContextBuilder.TryCreate(
                    context.sourcePsdAssetPath,
                    prefabPath,
                    out PsdHierarchyChatContext refreshed,
                    out string refreshedError))
            {
                return Result(PsdHierarchyCleanupExecutionState.Partial, "post-grouping",
                    "Stage 1 was applied and saved, but the authoritative snapshot could not be refreshed: " + refreshedError);
            }

            JObject stageTwoPlan = PsdHierarchyPostGroupingExtraction.BuildStageTwoPlan(
                refreshed, postGroupingIntents, prefabPath, out string stageTwoError);
            if (stageTwoPlan == null)
            {
                return Result(PsdHierarchyCleanupExecutionState.Partial, "post-grouping",
                    "Stage 1 was applied and saved, but the reviewed post-grouping extraction could not be resolved: " + stageTwoError);
            }

            PsdHierarchyChatCleanupExecutionResult stageTwo = ExecuteV2(
                refreshed,
                stageTwoPlan.ToString(Newtonsoft.Json.Formatting.None),
                save: true,
                unloadPrefabContents,
                savePrefabAsset,
                replayOfConfirmedStage);
            if (!stageTwo.success)
            {
                PsdHierarchyCleanupExecutionState state = stageTwo.state == PsdHierarchyCleanupExecutionState.Rejected
                    ? PsdHierarchyCleanupExecutionState.Partial
                    : stageTwo.state;
                return Result(state, "post-grouping",
                    "Stage 1 was applied and saved; the post-grouping stage did not complete (" +
                    stageTwo.stage + "): " + stageTwo.message);
            }

            return Result(PsdHierarchyCleanupExecutionState.Success, "verify",
                "VERIFY_OK (hierarchy stage and post-grouping extraction stage)" +
                Environment.NewLine + stageTwo.message);
        }

        private static string MissingReferenceNotice(Dictionary<Component, Dictionary<string, int>> missing)
        {
            string[] fields = missing.SelectMany(entry => entry.Value.Keys.Select(field =>
                entry.Key.name + ":" + entry.Key.GetType().Name + "." + field)).ToArray();
            return fields.Length == 0 ? string.Empty : Environment.NewLine +
                "Existing unresolved references preserved: " + fields.Length + ". " + string.Join(", ", fields) +
                ". These references were already missing before cleanup.";
        }

        private static PsdHierarchyChatCleanupExecutionResult UnloadContents(
            GameObject root, Action<GameObject> unload, PsdHierarchyChatCleanupExecutionResult result,
            bool saveAttempted, string stage)
        {
            try
            {
                if (root != null) unload(root);
                return result;
            }
            catch (Exception exception)
            {
                string message = result.message + Environment.NewLine + "Prefab contents cleanup failed: " + exception.Message;
                if (!result.success)
                    return Result(result.state, result.stage, message);
                return Result(saveAttempted ? PsdHierarchyCleanupExecutionState.Partial : PsdHierarchyCleanupExecutionState.Rejected,
                    saveAttempted ? stage : "preflight-cleanup", message);
            }
        }

        private static bool TryReadV2Plan(
            PsdHierarchyChatContext context,
            string planJson,
            out JObject plan,
            out string prefabPath,
            out string error)
        {
            plan = null;
            prefabPath = string.Empty;
            error = string.Empty;
            try
            {
                if (context == null)
                    throw new InvalidDataException("Authoritative hierarchy snapshot context is required.");
                if (!PsdHierarchyChatCleanupExecution.TryPrepareExecutionPlan(
                        context, planJson, out string prepared, out string preparationError))
                    throw new InvalidDataException(preparationError);
                plan = JObject.Parse(prepared);
                prefabPath = plan.Value<string>("prefabAssetPath") ?? string.Empty;
                if (!string.Equals(prefabPath, context.targetPrefabAssetPath, StringComparison.Ordinal))
                    throw new InvalidDataException("Plan target does not match the authoritative Prefab context.");
                if (!(plan["output"] is JObject output) ||
                    !string.Equals(output.Value<string>("mode"), "in_place", StringComparison.Ordinal) ||
                    !string.Equals(output.Value<string>("assetPath"), prefabPath, StringComparison.Ordinal))
                    throw new InvalidDataException("Version 2 basic cleanup requires output.mode=in_place for the target Prefab.");
                if (!string.Equals(plan.Value<string>("snapshotFingerprint"), context.hierarchySnapshotFingerprint, StringComparison.Ordinal))
                    throw new InvalidDataException("Plan snapshot fingerprint does not match the authoritative context.");
                string fullPath = GetProjectAssetFullPath(prefabPath);
                if (!File.Exists(fullPath) ||
                    !string.Equals(PsdHierarchyChatContextBuilder.ComputeFileFingerprint(fullPath), context.hierarchySnapshotFingerprint, StringComparison.Ordinal))
                    throw new InvalidDataException("Target Prefab changed after the hierarchy snapshot was captured.");
                if (!(plan["verify"] is JObject verify))
                    throw new InvalidDataException("Plan is missing the verify object.");
                ValidateOperationFields(plan, "wrappers", "id", "name", "parent", "siblingIndex");
                ValidateOperationFields(plan, "moves", "source", "destination", "siblingIndex");
                ValidateOperationFields(plan, "renames", "target", "name");
                ValidateOperationFields(plan, "tightBounds", "target");
                ValidateOperationFields(plan, "emptyContainerRemovals", "source");
                ValidateVerifySchema(verify);
                string unsupportedVerify = DescribeUnsupportedVerificationFields(verify);
                if (!string.IsNullOrEmpty(unsupportedVerify))
                    throw new InvalidDataException("Unsupported verification fields: " + unsupportedVerify + ".");

                string[] unsupportedOperations =
                {
                    "containmentResolutions", "flatSiblingResolutions",
                };
                foreach (string property in unsupportedOperations)
                {
                    if (plan[property] != null && (!(plan[property] is JArray values) || values.Count > 0))
                        throw new InvalidDataException(property + " is not supported by the version 2 basic cleanup executor.");
                }
                var knownArrays = new HashSet<string>(StringComparer.Ordinal)
                {
                    "wrappers", "moves", "renames", "emptyContainerRemovals", "tightBounds",
                    "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions",
                    "containmentResolutions", "flatSiblingResolutions", "componentExtractions",
                    "stateComponentExtractions", "variantComponentExtractions", "statefulComponentExtractions",
                    "selectedPrefabExtractions", "crossParentPrefabExtractions", "postGroupingExtractionIntents",
                    "requiredComponentFamilies", "containmentFindings", "flatSiblingFindings",
                };
                foreach (JProperty property in plan.Properties())
                {
                    if (property.Value is JArray values && values.Count > 0 && !knownArrays.Contains(property.Name))
                        throw new InvalidDataException("Unknown operation array is not executable: " + property.Name + ".");
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static PsdHierarchyChatCleanupExecutionResult Result(
            PsdHierarchyCleanupExecutionState state,
            string stage,
            string message)
        {
            return new PsdHierarchyChatCleanupExecutionResult(state, stage, message);
        }

        private static void ValidateOperationFields(JObject plan, string operation, params string[] fields)
        {
            foreach (JToken item in ReadArray(plan, operation))
            {
                JObject value = ReadObject(item, operation);
                foreach (JProperty property in value.Properties())
                    if (!fields.Contains(property.Name))
                        throw new InvalidDataException("Unsupported field: " + operation + "." + property.Name);
                foreach (string field in fields)
                {
                    if (field == "siblingIndex") ReadNonNegativeInt(value, field, operation);
                    else ReadString(value, field, operation);
                }
            }
        }

        private static void ValidateVerifySchema(JObject verify)
        {
            if (verify["nodes"] != null &&
                (verify["nodes"].Type != JTokenType.Integer || verify["nodes"].Value<int>() < 0))
                throw new InvalidDataException("verify.nodes must be a non-negative integer.");
            foreach (string property in new[] { "hierarchy", "absentPaths", "directChildren", "tightBounds" })
            {
                if (verify[property] != null && !(verify[property] is JArray))
                    throw new InvalidDataException("verify." + property + " must be an array.");
            }
        }

        private static ValidatedCleanupPlan BindPlan(
            GameObject root,
            JObject plan,
            PsdHierarchyChatContext context,
            bool replayOfConfirmedStage)
        {
            var wrapperParents = new List<Transform>();
            var moves = new List<NativeMove>();
            var renames = new List<NativeRename>();
            var tightBounds = new List<NativeTightBounds>();
            var emptyContainerRemovals = new List<Transform>();
            JArray wrappers = ReadArray(plan, "wrappers");
            JArray moveOperations = ReadArray(plan, "moves");
            JArray renameOperations = ReadArray(plan, "renames");
            JArray tightBoundsOperations = ReadArray(plan, "tightBounds");
            JArray removalOperations = ReadArray(plan, "emptyContainerRemovals");
            var boundWrappers = new List<NativeWrapper>();
            var declaredWrappers = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < wrappers.Count; index++)
            {
                JObject wrapper = ReadObject(wrappers[index], "wrappers[" + index + "]");
                string parent = ReadString(wrapper, "parent", "wrappers[" + index + "]");
                wrapperParents.Add(parent.StartsWith("@", StringComparison.Ordinal)
                    ? null
                    : ResolveExisting(root, context, parent, "wrappers[" + index + "].parent"));
                if (parent.StartsWith("@", StringComparison.Ordinal) && !declaredWrappers.Contains(parent.Substring(1)))
                    throw new InvalidDataException("Wrapper parent must reference an earlier wrapper: " + parent);
                string id = ReadString(wrapper, "id", "wrappers");
                if (!declaredWrappers.Add(id))
                    throw new InvalidDataException("Duplicate wrapper id: " + id);
                boundWrappers.Add(new NativeWrapper(id, ReadString(wrapper, "name", "wrappers"),
                    wrapperParents[index], parent, ReadNonNegativeInt(wrapper, "siblingIndex", "wrappers")));
            }

            for (int index = 0; index < moveOperations.Count; index++)
            {
                JObject move = ReadObject(moveOperations[index], "moves[" + index + "]");
                string destination = ReadString(move, "destination", "moves[" + index + "]");
                moves.Add(new NativeMove(
                    ResolveExisting(root, context, ReadString(move, "source", "moves[" + index + "]"), "moves[" + index + "].source"),
                    destination.StartsWith("@", StringComparison.Ordinal)
                        ? null
                        : ResolveExisting(root, context, destination, "moves[" + index + "].destination"),
                    destination,
                    ReadNonNegativeInt(move, "siblingIndex", "moves[" + index + "]")));
            }

            for (int index = 0; index < renameOperations.Count; index++)
            {
                JObject rename = ReadObject(renameOperations[index], "renames[" + index + "]");
                string target = ReadString(rename, "target", "renames[" + index + "]");
                renames.Add(new NativeRename(
                    target.StartsWith("@", StringComparison.Ordinal)
                        ? null
                        : ResolveExisting(root, context, target, "renames[" + index + "].target"),
                    target,
                    ReadString(rename, "name", "renames[" + index + "]")));
            }

            for (int index = 0; index < tightBoundsOperations.Count; index++)
            {
                JObject tightBound = ReadObject(tightBoundsOperations[index], "tightBounds[" + index + "]");
                string target = ReadString(tightBound, "target", "tightBounds[" + index + "]");
                tightBounds.Add(new NativeTightBounds(
                    target.StartsWith("@", StringComparison.Ordinal)
                        ? null
                        : ResolveExisting(root, context, target, "tightBounds[" + index + "].target").GetComponent<RectTransform>(),
                    target));
            }

            for (int index = 0; index < removalOperations.Count; index++)
            {
                JObject removal = ReadObject(removalOperations[index], "emptyContainerRemovals[" + index + "]");
                emptyContainerRemovals.Add(ResolveExisting(
                    root,
                    context,
                    ReadString(removal, "source", "emptyContainerRemovals[" + index + "]"),
                    "emptyContainerRemovals[" + index + "].source"));
            }

            ValidateBoundOperations(root.transform, wrapperParents, moves, renames, tightBounds, emptyContainerRemovals);

            foreach (string reference in moves.Select(move => move.destinationReference)
                         .Concat(renames.Select(rename => rename.targetReference))
                         .Concat(tightBounds.Select(operation => operation.targetReference)))
                if (reference.StartsWith("@", StringComparison.Ordinal) && !declaredWrappers.Contains(reference.Substring(1)))
                    throw new InvalidDataException("Unknown wrapper reference: " + reference);

            return new ValidatedCleanupPlan(plan.ToString(Newtonsoft.Json.Formatting.None),
                boundWrappers, moves, renames, tightBounds, emptyContainerRemovals,
                PsdHierarchyPrefabExtraction.Bind(
                    plan,
                    root,
                    context,
                    plan.Value<string>("prefabAssetPath") ?? context.targetPrefabAssetPath,
                    replaceExistingTargets: replayOfConfirmedStage),
                PsdHierarchyPrefabExtraction.BindStateExtractions(
                    plan,
                    root,
                    context,
                    plan.Value<string>("prefabAssetPath") ?? context.targetPrefabAssetPath,
                    replaceExistingTargets: replayOfConfirmedStage),
                PsdHierarchyPrefabExtraction.BindVariantExtractions(
                    plan,
                    root,
                    context,
                    plan.Value<string>("prefabAssetPath") ?? context.targetPrefabAssetPath,
                    replaceExistingTargets: replayOfConfirmedStage),
                PsdHierarchyPrefabExtraction.BindStatefulExtractions(
                    plan,
                    root,
                    context,
                    plan.Value<string>("prefabAssetPath") ?? context.targetPrefabAssetPath,
                    replaceExistingTargets: replayOfConfirmedStage),
                PsdHierarchyPrefabExtraction.BindSelectedExtractions(
                    plan,
                    root,
                    context,
                    plan.Value<string>("prefabAssetPath") ?? context.targetPrefabAssetPath,
                    replaceExistingTargets: replayOfConfirmedStage),
                PsdHierarchyPrefabExtraction.BindCrossParentExtractions(
                    plan,
                    root,
                    context,
                    plan.Value<string>("prefabAssetPath") ?? context.targetPrefabAssetPath,
                    replaceExistingTargets: replayOfConfirmedStage),
                CleanupVerification.Read((JObject)plan["verify"]));
        }

        private static void ApplyPlan(GameObject root, ValidatedCleanupPlan plan)
        {
            var wrappersById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (NativeWrapper wrapper in plan.wrappers)
            {
                string parentReference = wrapper.parentReference;
                Transform parent = parentReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, parentReference).transform
                    : wrapper.parent;
                if (parent == null)
                {
                    throw new InvalidOperationException("Wrapper parent was not found: " + parentReference);
                }

                wrappersById.Add(wrapper.id, CreateWrapper(parent, wrapper.name, wrapper.siblingIndex));
            }

            foreach (NativeMove move in plan.moves)
            {
                Transform destination = move.destinationReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, move.destinationReference).transform
                    : move.destination;
                if (move.source == null || destination == null)
                {
                    throw new InvalidOperationException("Move source or destination was not found.");
                }

                if (move.source == root.transform || destination == move.source || destination.IsChildOf(move.source))
                    throw new InvalidOperationException("Move would mutate the Prefab root or create a hierarchy cycle.");

                move.source.SetParent(destination, true);
                move.source.SetSiblingIndex(move.siblingIndex);
            }

            foreach (NativeRename rename in plan.renames)
            {
                Transform target = rename.targetReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, rename.targetReference).transform
                    : rename.target;
                if (target == null)
                {
                    throw new InvalidOperationException("Rename target was not found: " + rename.targetReference);
                }

                target.name = rename.name;
            }

            foreach (NativeTightBounds operation in plan.tightBounds)
            {
                RectTransform target = operation.targetReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, operation.targetReference).GetComponent<RectTransform>()
                    : operation.target;
                TightenToChildren(target, operation.targetReference);
            }

            foreach (Transform container in plan.removals)
            {
                RemoveEmptyContainer(root.transform, container);
            }


        }











        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory) || !directory.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Selected Nested Prefab has an invalid asset directory: " + assetPath);
            }

            string[] segments = directory.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static bool IsPascalCaseIdentifier(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   char.IsUpper(value[0]) &&
                   value.All(char.IsLetterOrDigit);
        }



        private static string DescribeUnsupportedVerificationFields(JObject verify)
        {
            if (verify == null)
            {
                return "verify";
            }

            var unsupported = new List<string>();
            foreach (JProperty property in verify.Properties())
            {
                if (property.Name != "nodes" &&
                    property.Name != "hierarchy" &&
                    property.Name != "absentPaths" &&
                    property.Name != "directChildren" &&
                    property.Name != "tightBounds")
                {
                    unsupported.Add("verify." + property.Name);
                }
            }

            return string.Join(", ", unsupported.ToArray());
        }

        private static void RemoveEmptyContainer(Transform prefabRoot, Transform container)
        {
            if (container == null || container.parent == null)
            {
                throw new InvalidOperationException("Cannot remove the Prefab root.");
            }

            if (container.childCount != 0)
            {
                throw new InvalidOperationException(
                    "Container is not empty after planned moves: " + container.name + ".");
            }

            foreach (Component component in container.GetComponents<Component>())
            {
                if (component != null && !(component is Transform))
                {
                    throw new InvalidOperationException(
                        "Container has non-Transform components: " + container.name + ".");
                }
            }

            AssertNoExternalReferences(prefabRoot, container);
            UnityEngine.Object.DestroyImmediate(container.gameObject);
        }

        private static void AssertNoExternalReferences(Transform prefabRoot, Transform source)
        {
            AssertNoExternalReferences(prefabRoot, new[] { source }, source == null ? string.Empty : source.name);
        }

        private static void AssertNoExternalReferences(
            Transform prefabRoot,
            IEnumerable<Transform> sources,
            string sourceDescription)
        {
            var forbidden = new HashSet<UnityEngine.Object>();
            foreach (Transform source in sources ?? Array.Empty<Transform>())
            {
                if (source == null)
                {
                    continue;
                }

                foreach (Transform node in source.GetComponentsInChildren<Transform>(true))
                {
                    forbidden.Add(node.gameObject);
                    foreach (Component component in node.GetComponents<Component>())
                    {
                        if (component != null)
                        {
                            forbidden.Add(component);
                        }
                    }
                }
            }

            foreach (Component owner in prefabRoot.GetComponentsInChildren<Component>(true))
            {
                if (owner == null || owner is Transform || forbidden.Contains(owner))
                {
                    continue;
                }

                var serialized = new SerializedObject(owner);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null &&
                        forbidden.Contains(property.objectReferenceValue))
                    {
                        throw new InvalidOperationException(
                            "Cannot extract or remove a hierarchy referenced outside its boundary: " + sourceDescription +
                            " by " + owner.GetType().FullName + "." + property.propertyPath);
                    }
                }
            }
        }

        private static GameObject FindByPath(GameObject root, string path)
        {
            Transform current = root.transform;
            string[] parts = path.Split('/');
            int index = parts.Length > 0 && string.Equals(parts[0], current.name, StringComparison.Ordinal) ? 1 : 0;
            for (; index < parts.Length; index++)
            {
                string segment = parts[index];
                int occurrence = 0;
                int marker = segment.LastIndexOf('#');
                if (marker > 0 && marker < segment.Length - 1 &&
                    int.TryParse(segment.Substring(marker + 1), out int parsedOccurrence) && parsedOccurrence >= 0)
                {
                    occurrence = parsedOccurrence;
                    segment = segment.Substring(0, marker);
                }

                Transform next = null;
                int matched = 0;
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);
                    if (string.Equals(child.name, segment, StringComparison.Ordinal) && matched++ == occurrence)
                    {
                        next = child;
                        break;
                    }
                }

                if (next == null)
                {
                    throw new InvalidOperationException("Plan source path was not found: " + path);
                }

                current = next;
            }

            return current.gameObject;
        }

        private static Transform ResolveExisting(
            GameObject root,
            PsdHierarchyChatContext context,
            string reference,
            string label)
        {
            if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("node:", StringComparison.Ordinal))
                throw new InvalidDataException(label + " must use a node:<id> reference.");
            string nodeId = reference.Substring("node:".Length);
            if (!context.TryGetNodePath(nodeId, out string path))
                throw new InvalidDataException(label + " references an unknown snapshot node: " + reference + ".");
            return FindByPath(root, path).transform;
        }

        private static void ValidateBoundOperations(
            Transform root,
            IEnumerable<Transform> wrapperParents,
            IEnumerable<NativeMove> moves,
            IEnumerable<NativeRename> renames,
            IEnumerable<NativeTightBounds> tightBounds,
            IEnumerable<Transform> removals)
        {
            foreach (Transform parent in wrapperParents)
            {
                if (parent != null)
                    AssertMutableBoundary(parent, "Wrapper parent");
            }
            foreach (NativeMove move in moves)
            {
                AssertMutableBoundary(move.source, "Move source");
                if (move.destination != null)
                    AssertMutableBoundary(move.destination, "Move destination");
                if (move.source == root || move.destination == move.source ||
                    (move.destination != null && move.destination.IsChildOf(move.source)))
                    throw new InvalidOperationException("Move would mutate the Prefab root or create a hierarchy cycle.");
            }
            foreach (NativeRename rename in renames)
            {
                if (rename.target != null)
                {
                    if (rename.target == root)
                        throw new InvalidOperationException("Cannot rename the Prefab root.");
                    AssertMutableBoundary(rename.target, "Rename target");
                }
            }
            foreach (NativeTightBounds operation in tightBounds)
            {
                if (operation.target != null)
                {
                    if (operation.target == root)
                        throw new InvalidOperationException("Cannot tighten the Prefab root.");
                    AssertMutableBoundary(operation.target, "Tight-bounds target");
                }
            }
            foreach (Transform removal in removals)
            {
                if (removal == root)
                    throw new InvalidOperationException("Cannot remove the Prefab root.");
                AssertMutableBoundary(removal, "Removal target");
            }
        }

        private static void AssertMutableBoundary(Transform target, string label)
        {
            if (target == null)
                throw new InvalidOperationException(label + " was not found.");
            if (PrefabUtility.IsPartOfPrefabInstance(target.gameObject))
                throw new InvalidOperationException(label + " is inside a nested Prefab instance: " + target.name + ".");
        }

        private static GameObject CreateWrapper(Transform parent, string name, int siblingIndex)
        {
            var wrapper = new GameObject(name, typeof(RectTransform));
            RectTransform rect = wrapper.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetSiblingIndex(siblingIndex);
            return wrapper;
        }

        private static void TightenToChildren(RectTransform rect, string target)
        {
            if (rect == null)
            {
                throw new InvalidOperationException("Tight-bounds target is not a RectTransform: " + target);
            }

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null)
            {
                throw new InvalidOperationException("Tight-bounds target has no RectTransform parent: " + target);
            }

            if (rect.childCount == 0)
            {
                throw new InvalidOperationException("Cannot tighten an empty wrapper: " + target);
            }

            var bounds = new Bounds();
            bool initialized = false;
            for (int childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                RectTransform child = rect.GetChild(childIndex) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 point = parent.InverseTransformPoint(corners[cornerIndex]);
                    if (!initialized)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            if (!initialized)
            {
                throw new InvalidOperationException("Tight-bounds target has no RectTransform children: " + target);
            }

            var children = new List<Transform>();
            var siblingIndices = new List<int>();
            for (int childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                Transform child = rect.GetChild(childIndex);
                children.Add(child);
                siblingIndices.Add(child.GetSiblingIndex());
            }

            foreach (Transform child in children)
            {
                child.SetParent(parent, true);
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);
            rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
            for (int index = 0; index < children.Count; index++)
            {
                children[index].SetParent(rect, true);
                children[index].SetSiblingIndex(siblingIndices[index]);
            }
        }

        private static void Verify(GameObject root, JObject verify)
        {
            if (verify == null)
            {
                throw new InvalidOperationException("Plan is missing the verify object.");
            }

            if (verify["nodes"] != null)
            {
                int expectedNodes = ReadNonNegativeInt(verify, "nodes", "verify");
                int actualNodes = CountNodes(root.transform);
                if (actualNodes != expectedNodes)
                {
                    throw new InvalidOperationException(
                        "Node count differs. Expected=" + expectedNodes + " Actual=" + actualNodes + ".");
                }
            }

            JArray hierarchy = verify["hierarchy"] as JArray;
            if (hierarchy != null)
            {
                for (int index = 0; index < hierarchy.Count; index++)
                {
                    JObject item = ReadObject(hierarchy[index], "verify.hierarchy[" + index + "]");
                    int expected = ReadNonNegativeInt(item, "childCount", "verify.hierarchy[" + index + "]");
                    int actual = FindByPath(root, ReadString(item, "path", "verify.hierarchy[" + index + "]")).transform.childCount;
                    if (actual != expected)
                    {
                        throw new InvalidOperationException("Hierarchy child count differs at verify.hierarchy[" + index + "].");
                    }
                }
            }

            JArray absentPaths = verify["absentPaths"] as JArray;
            if (absentPaths != null)
            {
                foreach (JToken item in absentPaths)
                {
                    string path = item.Value<string>();
                    if (TryFindByPath(root, path) != null)
                    {
                        throw new InvalidOperationException("Planned absent path still exists: " + path);
                    }
                }
            }

            JArray directChildren = verify["directChildren"] as JArray;
            if (directChildren != null)
            {
                for (int index = 0; index < directChildren.Count; index++)
                {
                    JObject item = ReadObject(directChildren[index], "verify.directChildren[" + index + "]");
                    Transform node = FindByPath(root, ReadString(item, "path", "verify.directChildren[" + index + "]")).transform;
                    JArray expected = ReadArray(item, "children");
                    if (node.childCount != expected.Count)
                    {
                        throw new InvalidOperationException("Direct child count differs at " + item.Value<string>("path") + ".");
                    }

                    for (int childIndex = 0; childIndex < expected.Count; childIndex++)
                    {
                        if (!string.Equals(node.GetChild(childIndex).name, expected[childIndex].Value<string>(), StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Direct child order differs at " + item.Value<string>("path") + ".");
                        }
                    }
                }
            }

            JArray tightBounds = verify["tightBounds"] as JArray;
            if (tightBounds != null)
            {
                for (int index = 0; index < tightBounds.Count; index++)
                {
                    JObject item = ReadObject(tightBounds[index], "verify.tightBounds[" + index + "]");
                    string path = ReadString(item, "path", "verify.tightBounds[" + index + "]");
                    AssertTightBounds(FindByPath(root, path).GetComponent<RectTransform>(), path);
                }
            }
        }

        private static int CountNodes(Transform root)
        {
            int count = 1;
            for (int index = 0; index < root.childCount; index++)
            {
                count += CountNodes(root.GetChild(index));
            }

            return count;
        }

        private static void AssertTightBounds(RectTransform rect, string path)
        {
            if (rect == null || rect.parent == null || rect.childCount == 0)
            {
                throw new InvalidOperationException("Tight-bounds invariant cannot be evaluated: " + path);
            }

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null)
            {
                throw new InvalidOperationException("Tight-bounds parent is not a RectTransform: " + path);
            }

            var rectCorners = new Vector3[4];
            rect.GetWorldCorners(rectCorners);
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            bool initialized = false;
            for (int childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                RectTransform child = rect.GetChild(childIndex) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = parent.InverseTransformPoint(corner);
                    if (!initialized)
                    {
                        min = point;
                        max = point;
                        initialized = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, point);
                        max = Vector3.Max(max, point);
                    }
                }
            }

            Vector3 wrapperMin = parent.InverseTransformPoint(rectCorners[0]);
            Vector3 wrapperMax = parent.InverseTransformPoint(rectCorners[2]);
            if (!initialized ||
                Vector2.Distance(new Vector2(min.x, min.y), new Vector2(wrapperMin.x, wrapperMin.y)) > 0.01f ||
                Vector2.Distance(new Vector2(max.x, max.y), new Vector2(wrapperMax.x, wrapperMax.y)) > 0.01f)
            {
                throw new InvalidOperationException("Tight-bounds invariant failed: " + path);
            }
        }

        private static GameObject ResolveWrapper(IDictionary<string, GameObject> wrappers, string reference)
        {
            string id = reference.Substring(1);
            if (!wrappers.TryGetValue(id, out GameObject wrapper))
            {
                throw new InvalidOperationException("Wrapper reference was not found: " + reference);
            }

            return wrapper;
        }

        private static GameObject TryFindByPath(GameObject root, string path)
        {
            try
            {
                return FindByPath(root, path);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static JArray ReadArray(JObject owner, string name)
        {
            JArray value = owner[name] as JArray;
            if (value == null)
            {
                throw new InvalidDataException("Plan is missing array " + name + ".");
            }

            return value;
        }

        private static JObject ReadObject(JToken value, string label)
        {
            JObject result = value as JObject;
            if (result == null)
            {
                throw new InvalidDataException(label + " must be an object.");
            }

            return result;
        }

        private static string ReadString(JObject owner, string name, string label)
        {
            if (owner[name]?.Type != JTokenType.String)
                throw new InvalidDataException(label + "." + name + " must be a string.");
            string value = owner.Value<string>(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(label + "." + name + " must be a non-empty string.");
            }

            return value;
        }

        private static int ReadNonNegativeInt(JObject owner, string name, string label)
        {
            JToken value = owner[name];
            if (value == null || value.Type != JTokenType.Integer || value.Value<int>() < 0)
            {
                throw new InvalidDataException(label + "." + name + " must be a non-negative integer.");
            }

            return value.Value<int>();
        }

        // Bindings belong to one loaded Prefab; mutation never resolves JSON or node paths.
        private sealed class ValidatedCleanupPlan
        {
            internal readonly string reviewedDocument;
            internal readonly IReadOnlyList<NativeWrapper> wrappers;
            internal readonly IReadOnlyList<NativeMove> moves;
            internal readonly IReadOnlyList<NativeRename> renames;
            internal readonly IReadOnlyList<NativeTightBounds> tightBounds;
            internal readonly IReadOnlyList<Transform> removals;
            internal readonly IReadOnlyList<NativeExtractionOperation> extractions;
            internal readonly IReadOnlyList<NativeStateExtractionOperation> stateExtractions;
            internal readonly IReadOnlyList<NativeVariantExtractionOperation> variantExtractions;
            internal readonly IReadOnlyList<NativeStatefulExtractionOperation> statefulExtractions;
            internal readonly IReadOnlyList<NativeSelectedExtractionOperation> selectedExtractions;
            internal readonly IReadOnlyList<NativeCrossParentExtractionOperation> crossParentExtractions;
            internal readonly CleanupVerification verification;

            internal ValidatedCleanupPlan(string reviewedDocument, List<NativeWrapper> wrappers,
                List<NativeMove> moves, List<NativeRename> renames, List<NativeTightBounds> tightBounds,
                List<Transform> removals, IReadOnlyList<NativeExtractionOperation> extractions,
                IReadOnlyList<NativeStateExtractionOperation> stateExtractions,
                IReadOnlyList<NativeVariantExtractionOperation> variantExtractions,
                IReadOnlyList<NativeStatefulExtractionOperation> statefulExtractions,
                IReadOnlyList<NativeSelectedExtractionOperation> selectedExtractions,
                IReadOnlyList<NativeCrossParentExtractionOperation> crossParentExtractions,
                CleanupVerification verification)
            {
                this.reviewedDocument = reviewedDocument;
                this.wrappers = wrappers.AsReadOnly();
                this.moves = moves.AsReadOnly();
                this.renames = renames.AsReadOnly();
                this.tightBounds = tightBounds.AsReadOnly();
                this.removals = removals.AsReadOnly();
                this.extractions = extractions ?? Array.Empty<NativeExtractionOperation>();
                this.stateExtractions = stateExtractions ?? Array.Empty<NativeStateExtractionOperation>();
                this.variantExtractions = variantExtractions ?? Array.Empty<NativeVariantExtractionOperation>();
                this.statefulExtractions = statefulExtractions ?? Array.Empty<NativeStatefulExtractionOperation>();
                this.selectedExtractions = selectedExtractions ?? Array.Empty<NativeSelectedExtractionOperation>();
                this.crossParentExtractions = crossParentExtractions ?? Array.Empty<NativeCrossParentExtractionOperation>();
                this.verification = verification;
            }
        }

        private sealed class CleanupVerification
        {
            private int? nodeCount;
            private readonly List<KeyValuePair<string, int>> hierarchy = new List<KeyValuePair<string, int>>();
            private readonly List<KeyValuePair<string, string[]>> children = new List<KeyValuePair<string, string[]>>();
            private readonly List<string> absentPaths = new List<string>();
            private readonly List<string> tightPaths = new List<string>();

            internal static CleanupVerification Read(JObject document)
            {
                var result = new CleanupVerification();
                if (document["nodes"] != null) result.nodeCount = ReadNonNegativeInt(document, "nodes", "verify");
                foreach (JToken token in document["hierarchy"] as JArray ?? new JArray())
                {
                    JObject item = ReadEntry(token, "hierarchy", "path", "childCount");
                    result.hierarchy.Add(new KeyValuePair<string, int>(ReadString(item, "path", "verify.hierarchy"),
                        ReadNonNegativeInt(item, "childCount", "verify.hierarchy")));
                }
                foreach (JToken token in document["directChildren"] as JArray ?? new JArray())
                {
                    JObject item = ReadEntry(token, "directChildren", "path", "children");
                    result.children.Add(new KeyValuePair<string, string[]>(ReadString(item, "path", "verify.directChildren"),
                        ReadArray(item, "children").Select(value => ReadText(value, "verify.directChildren.children")).ToArray()));
                }
                foreach (JToken token in document["absentPaths"] as JArray ?? new JArray())
                    result.absentPaths.Add(ReadText(token, "verify.absentPaths"));
                foreach (JToken token in document["tightBounds"] as JArray ?? new JArray())
                {
                    JObject item = ReadEntry(token, "tightBounds", "path");
                    result.tightPaths.Add(ReadString(item, "path", "verify.tightBounds"));
                }
                return result;
            }

            private static JObject ReadEntry(JToken token, string label, params string[] fields)
            {
                JObject item = ReadObject(token, "verify." + label);
                foreach (JProperty property in item.Properties())
                    if (!fields.Contains(property.Name))
                        throw new InvalidDataException("Unsupported verification field: verify." + label + "." + property.Name);
                return item;
            }

            private static string ReadText(JToken token, string label)
            {
                if (token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>()))
                    throw new InvalidDataException(label + " must contain non-empty strings.");
                return token.Value<string>();
            }

            internal void Verify(GameObject root)
            {
                if (nodeCount.HasValue && CountNodes(root.transform) != nodeCount.Value)
                    throw new InvalidOperationException("Node count differs from verify.nodes.");
                foreach (var item in hierarchy)
                    if (FindByPath(root, item.Key).transform.childCount != item.Value)
                        throw new InvalidOperationException("Hierarchy child count differs at " + item.Key);
                foreach (string path in absentPaths)
                    if (TryFindByPath(root, path) != null)
                        throw new InvalidOperationException("Planned absent path still exists: " + path);
                foreach (var item in children)
                {
                    Transform node = FindByPath(root, item.Key).transform;
                    if (node.childCount != item.Value.Length)
                        throw new InvalidOperationException("Direct child count differs at " + item.Key);
                    for (int index = 0; index < item.Value.Length; index++)
                        if (!string.Equals(node.GetChild(index).name, item.Value[index], StringComparison.Ordinal))
                            throw new InvalidOperationException("Direct child order differs at " + item.Key);
                }
                foreach (string path in tightPaths)
                    AssertTightBounds(FindByPath(root, path).GetComponent<RectTransform>(), path);
            }
        }

        private readonly struct NativeWrapper
        {
            internal readonly string id;
            internal readonly string name;
            internal readonly Transform parent;
            internal readonly string parentReference;
            internal readonly int siblingIndex;

            internal NativeWrapper(string id, string name, Transform parent, string parentReference, int siblingIndex)
            {
                this.id = id;
                this.name = name;
                this.parent = parent;
                this.parentReference = parentReference;
                this.siblingIndex = siblingIndex;
            }
        }

        private readonly struct NativeMove
        {
            internal NativeMove(Transform source, Transform destination, string destinationReference, int siblingIndex)
            {
                this.source = source;
                this.destination = destination;
                this.destinationReference = destinationReference;
                this.siblingIndex = siblingIndex;
            }

            internal readonly Transform source;
            internal readonly Transform destination;
            internal readonly string destinationReference;
            internal readonly int siblingIndex;
        }

        private readonly struct NativeRename
        {
            internal NativeRename(Transform target, string targetReference, string name)
            {
                this.target = target;
                this.targetReference = targetReference;
                this.name = name;
            }

            internal readonly Transform target;
            internal readonly string targetReference;
            internal readonly string name;
        }

        private readonly struct NativeTightBounds
        {
            internal NativeTightBounds(RectTransform target, string targetReference)
            {
                this.target = target;
                this.targetReference = targetReference;
            }

            internal readonly RectTransform target;
            internal readonly string targetReference;
        }

        private sealed class PersistedExpectations
        {
            private PersistedExpectations(IReadOnlyList<PersistedNode> nodes, IReadOnlyList<string> references)
            {
                this.nodes = nodes;
                this.references = references;
            }

            private readonly IReadOnlyList<PersistedNode> nodes;
            private readonly IReadOnlyList<string> references;

            internal static Dictionary<Component, Dictionary<string, int>> CaptureMissingReferences(GameObject root)
            {
                var missing = new Dictionary<Component, Dictionary<string, int>>();
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    using (var serialized = new SerializedObject(component))
                    {
                        SerializedProperty property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference ||
                                IsPrefabLinkageProperty(property.propertyPath) ||
                                property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0)
                                continue;
                            if (!missing.TryGetValue(component, out Dictionary<string, int> fields))
                                missing.Add(component, fields = new Dictionary<string, int>(StringComparer.Ordinal));
                            fields.Add(property.propertyPath, property.objectReferenceInstanceIDValue);
                        }
                    }
                }
                return missing;
            }

            internal static PersistedExpectations Capture(
                GameObject root, Dictionary<Component, Dictionary<string, int>> existingMissingReferences)
            {
                // Component identity survives moves and renames. Replaced/deleted owners are not eligible for tolerance.
                foreach (var entry in existingMissingReferences)
                {
                    if (entry.Key == null)
                        throw new InvalidOperationException("A component with an existing unresolved reference was removed or replaced.");
                    using (var serialized = new SerializedObject(entry.Key))
                    {
                        foreach (var field in entry.Value)
                        {
                            SerializedProperty property = serialized.FindProperty(field.Key);
                            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference ||
                                property.objectReferenceValue != null || property.objectReferenceInstanceIDValue != field.Value)
                                throw new InvalidOperationException("An existing unresolved reference changed: " +
                                    entry.Key.name + ":" + entry.Key.GetType().Name + "." + field.Key);
                        }
                    }
                }
                var nodes = new List<PersistedNode>();
                CaptureNode(root.transform, "0", nodes);
                return new PersistedExpectations(nodes, CaptureReferences(root, existingMissingReferences));
            }

            internal void Verify(GameObject root)
            {
                var actual = new List<PersistedNode>();
                CaptureNode(root.transform, "0", actual);
                if (actual.Count != nodes.Count)
                    throw new InvalidOperationException("Persisted hierarchy node count differs from the simulated result.");
                for (int index = 0; index < nodes.Count; index++)
                    nodes[index].AssertMatches(actual[index]);
                if (!references.SequenceEqual(CaptureReferences(root, comparePersisted: true)))
                    throw new InvalidOperationException("Persisted component types or serialized object references differ from the simulated result.");
            }

            private static List<string> CaptureReferences(GameObject root,
                Dictionary<Component, Dictionary<string, int>> existingMissingReferences = null,
                bool comparePersisted = false)
            {
                var identities = new Dictionary<UnityEngine.Object, string>();
                var components = new List<Component>();
                BindIdentities(root.transform, "0", identities, components);
                var result = new List<string>();
                foreach (Component component in components)
                {
                    if (component == null)
                        throw new InvalidOperationException("Missing scripts cannot be verified safely.");
                    string owner = identities[component] + ":" + component.GetType().AssemblyQualifiedName;
                    result.Add(owner);
                    using (var serialized = new SerializedObject(component))
                    {
                        SerializedProperty property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            // Prefab 链接管道（m_PrefabInstance / m_PrefabAsset / m_CorrespondingSourceObject）
                            // 是 Unity 内部对象，不属于用户数据，也无法用 GUID 表达。
                            if (IsPrefabLinkageProperty(property.propertyPath)) continue;
                            UnityEngine.Object value = property.objectReferenceValue;
                            string identity;
                            if (value == null)
                            {
                                int missingId = property.objectReferenceInstanceIDValue;
                                if (missingId != 0)
                                {
                                    bool existed = existingMissingReferences != null &&
                                        existingMissingReferences.TryGetValue(component, out Dictionary<string, int> fields) &&
                                        fields.TryGetValue(property.propertyPath, out int previousId) && previousId == missingId;
                                    if (!comparePersisted && !existed)
                                        throw new InvalidOperationException("New unresolved serialized reference: " + owner + "." + property.propertyPath);
                                    // Instance IDs are compared only within this synchronous save/reload operation, never persisted as replay evidence.
                                    identity = "missing:" + missingId;
                                }
                                else identity = "null";
                            }
                            else if (!identities.TryGetValue(value, out identity))
                            {
                                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId))
                                    throw new InvalidOperationException("Unverifiable external reference: " + property.propertyPath);
                                identity = guid + ":" + localId;
                            }
                            result.Add(owner + "/" + property.propertyPath + "=" + identity);
                        }
                    }
                }
                return result;
            }

            private static bool IsPrefabLinkageProperty(string propertyPath)
            {
                return propertyPath == "m_PrefabInstance" ||
                       propertyPath == "m_PrefabAsset" ||
                       propertyPath == "m_CorrespondingSourceObject" ||
                       propertyPath == "m_PrefabInternal" ||
                       propertyPath.EndsWith(".m_PrefabInstance", StringComparison.Ordinal) ||
                       propertyPath.EndsWith(".m_CorrespondingSourceObject", StringComparison.Ordinal);
            }

            private static void BindIdentities(Transform node, string address,
                IDictionary<UnityEngine.Object, string> identities, ICollection<Component> components)
            {
                identities.Add(node.gameObject, address);
                Component[] attached = node.GetComponents<Component>();
                for (int index = 0; index < attached.Length; index++)
                {
                    if (attached[index] != null) identities.Add(attached[index], address + ":" + index);
                    components.Add(attached[index]);
                }
                for (int index = 0; index < node.childCount; index++)
                    BindIdentities(node.GetChild(index), address + "/" + index, identities, components);
            }

            private static void CaptureNode(Transform node, string address, ICollection<PersistedNode> output)
            {
                output.Add(new PersistedNode(node, address));
                for (int childIndex = 0; childIndex < node.childCount; childIndex++)
                    CaptureNode(node.GetChild(childIndex), address + "/" + childIndex, output);
            }
        }

        private readonly struct PersistedNode
        {
            internal PersistedNode(Transform transform, string address)
            {
                this.address = address;
                name = transform.name;
                childCount = transform.childCount;
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
                RectTransform rect = transform as RectTransform;
                hasRect = rect != null;
                anchorMin = rect == null ? Vector2.zero : rect.anchorMin;
                anchorMax = rect == null ? Vector2.zero : rect.anchorMax;
                pivot = rect == null ? Vector2.zero : rect.pivot;
                anchoredPosition = rect == null ? Vector2.zero : rect.anchoredPosition;
                sizeDelta = rect == null ? Vector2.zero : rect.sizeDelta;
            }

            internal void AssertMatches(PersistedNode actual)
            {
                if (!string.Equals(address, actual.address, StringComparison.Ordinal) ||
                    !string.Equals(name, actual.name, StringComparison.Ordinal) ||
                    childCount != actual.childCount || hasRect != actual.hasRect ||
                    Vector3.Distance(localPosition, actual.localPosition) > 0.001f ||
                    Quaternion.Angle(localRotation, actual.localRotation) > 0.001f ||
                    Vector3.Distance(localScale, actual.localScale) > 0.001f ||
                    Vector2.Distance(anchorMin, actual.anchorMin) > 0.001f ||
                    Vector2.Distance(anchorMax, actual.anchorMax) > 0.001f ||
                    Vector2.Distance(pivot, actual.pivot) > 0.001f ||
                    Vector2.Distance(anchoredPosition, actual.anchoredPosition) > 0.001f ||
                    Vector2.Distance(sizeDelta, actual.sizeDelta) > 0.001f)
                {
                    throw new InvalidOperationException("Persisted hierarchy differs from the simulated result at " + address + ".");
                }
            }

            private readonly string address;
            private readonly string name;
            private readonly int childCount;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;
            private readonly bool hasRect;
            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;
            private readonly Vector2 pivot;
            private readonly Vector2 anchoredPosition;
            private readonly Vector2 sizeDelta;
        }

        /// <summary>
        /// 获取 Transform 的完整层级路径
        /// </summary>
        private static string GetFullPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var pathSegments = new System.Collections.Generic.List<string>();
            Transform current = transform;
            while (current != null)
            {
                pathSegments.Add(current.name);
                current = current.parent;
            }

            pathSegments.Reverse();
            return string.Join("/", pathSegments.ToArray());
        }
    }
}
