namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Pure preview/orchestration state. It clones all plans and pending lists,
    /// so opening, retrying, confirming cleanup, cancelling, or closing a window
    /// cannot mutate the Prefab or persisted Profile.
    /// </summary>
    public sealed class PsdHierarchyOrganizerPreviewModel
    {
        private readonly PsdHierarchyRequest fullRequest;
        private readonly PsdHierarchyPlan baselinePlan;
        private readonly PsdHierarchyReconciliationResult reconciliation;
        private readonly IPsdHierarchyAiRunner runner;
        private PsdHierarchyPlan proposedPlanValue;
        private readonly HashSet<string> acceptedGroupKeysValue = new HashSet<string>(StringComparer.Ordinal);

        public PsdHierarchyOrganizerPreviewModel(
            string targetPrefabPath,
            PsdHierarchyRequest fullRequest,
            PsdHierarchyPlan baselinePlan,
            PsdHierarchyReconciliationResult reconciliation,
            IPsdHierarchyAiRunner runner)
        {
            this.targetPrefabPath = targetPrefabPath ?? string.Empty;
            this.fullRequest = CloneRequest(fullRequest ?? throw new ArgumentNullException("fullRequest"));
            this.baselinePlan = ClonePlan(baselinePlan ?? throw new ArgumentNullException("baselinePlan"));
            this.reconciliation = CloneReconciliation(
                reconciliation ?? throw new ArgumentNullException("reconciliation"));
            this.runner = runner ?? throw new ArgumentNullException("runner");
            proposedPlanValue = ClonePlan(this.baselinePlan);
            pendingMissingStableIds = new List<string>(this.reconciliation.pendingMissingStableIds);
        }

        public string targetPrefabPath { get; private set; }
        public string sourcePsdGuid
        {
            get { return fullRequest.sourcePsdGuid ?? string.Empty; }
        }
        public IList<PsdHierarchyRequestNode> currentTreeNodes
        {
            get { return fullRequest.nodes.Select(CloneNode).ToList(); }
        }
        internal PsdHierarchyRequest requestSnapshot
        {
            get { return CloneRequest(fullRequest); }
        }
        public PsdHierarchyPlan proposedPlan
        {
            get { return ClonePlan(proposedPlanValue); }
        }
        public IReadOnlyCollection<string> acceptedGroupKeys
        {
            get { return acceptedGroupKeysValue.ToArray(); }
        }
        public IList<PsdPrefabCandidate> prefabCandidates
        {
            get { return PsdHierarchyPrefabCandidateAnalyzer.Analyze(fullRequest.nodes); }
        }
        public List<string> validationErrors { get; } = new List<string>();
        public List<string> pendingMissingStableIds { get; private set; }
        public bool canApply { get; private set; }
        public bool isRunning { get; private set; }

        public void AcceptGroup(string groupKey)
        {
            SetGroupAccepted(groupKey, true);
        }

        public void SetGroupAccepted(string groupKey, bool accepted)
        {
            if (string.IsNullOrEmpty(groupKey) || !(proposedPlanValue.groups ?? new List<PsdHierarchyPlanGroup>()).Any(group => group != null && group.key == groupKey))
                throw new ArgumentException("草稿中不存在该分组。", "groupKey");
            if (accepted) acceptedGroupKeysValue.Add(groupKey);
            else acceptedGroupKeysValue.Remove(groupKey);
        }

        public void MoveNodeIntoGroup(string sourceStableId, string targetStableId, string targetGroupKey)
        {
            if (!PsdStableLayerIdUtility.IsPersistable(sourceStableId))
                throw new ArgumentException("拖动的图层没有稳定 ID。", "sourceStableId");
            PsdHierarchyPlan working = ClonePlan(proposedPlanValue);
            Dictionary<string, PsdHierarchyRequestNode> nodes = fullRequest.nodes
                .Where(node => node != null)
                .ToDictionary(node => node.stableId, StringComparer.Ordinal);
            PsdHierarchyRequestNode source;
            if (!nodes.TryGetValue(sourceStableId, out source))
                throw new ArgumentException("拖动的图层已不存在。", "sourceStableId");
            EnsureManualMoveNodeIsUnlocked(source);

            List<PsdHierarchyPlanGroup> groups = working.groups ?? new List<PsdHierarchyPlanGroup>();
            PsdHierarchyPlanGroup target = FindMoveTargetGroup(groups, targetStableId, targetGroupKey);
            if (target == null)
            {
                PsdHierarchyRequestNode targetNode;
                if (!nodes.TryGetValue(targetStableId ?? string.Empty, out targetNode))
                    throw new ArgumentException("放置目标已不存在。", "targetStableId");
                EnsureManualMoveNodeIsUnlocked(targetNode);
                target = new PsdHierarchyPlanGroup
                {
                    key = CreateManualGroupKey(groups, sourceStableId, targetStableId),
                    parentKey = string.Empty,
                    displayName = "手动分组",
                    evidence = "手动拖动操作。",
                    confidence = 1d,
                    memberStableIds = new List<string> { targetStableId }
                };
                groups.Add(target);
            }
            EnsureManualMoveGroupIsUnlocked(target);
            if (target.memberStableIds.Contains(sourceStableId)) return;

            PsdHierarchyPlanGroup sourceOwner = groups.FirstOrDefault(group =>
                group != null && (group.memberStableIds ?? new List<string>()).Contains(sourceStableId));
            if (sourceOwner != null && sourceOwner != target)
            {
                EnsureManualMoveGroupIsUnlocked(sourceOwner);
                sourceOwner.memberStableIds.RemoveAll(id => string.Equals(id, sourceStableId, StringComparison.Ordinal));
                if (sourceOwner.memberStableIds.Count == 0)
                {
                    if (groups.Any(group => group != null && string.Equals(group.parentKey, sourceOwner.key, StringComparison.Ordinal)))
                        throw new InvalidOperationException("不能清空仍包含子分组的分组。 ");
                    groups.Remove(sourceOwner);
                    acceptedGroupKeysValue.Remove(sourceOwner.key);
                }
            }
            target.memberStableIds.Add(sourceStableId);
            working.groups = groups;
            CommitManualHierarchyMove(working);
        }

        public void CreateGroupForNode(string stableId)
        {
            if (!PsdStableLayerIdUtility.IsPersistable(stableId))
                throw new ArgumentException("拖动的图层没有稳定 ID。", "stableId");

            PsdHierarchyPlan working = ClonePlan(proposedPlanValue);
            Dictionary<string, PsdHierarchyRequestNode> nodes = fullRequest.nodes
                .Where(node => node != null)
                .ToDictionary(node => node.stableId, StringComparer.Ordinal);
            PsdHierarchyRequestNode node;
            if (!nodes.TryGetValue(stableId, out node))
                throw new ArgumentException("拖动的图层已不存在。", "stableId");
            EnsureManualMoveNodeIsUnlocked(node);

            List<PsdHierarchyPlanGroup> groups = working.groups ?? new List<PsdHierarchyPlanGroup>();
            if (groups.Any(group => group != null && (group.memberStableIds ?? new List<string>()).Contains(stableId)))
                throw new InvalidOperationException("该图层已属于一个分组。");

            groups.Add(new PsdHierarchyPlanGroup
            {
                key = CreateManualGroupKey(groups, stableId),
                parentKey = string.Empty,
                displayName = "手动分组",
                evidence = "手动创建分组。",
                confidence = 1d,
                memberStableIds = new List<string> { stableId }
            });
            working.groups = groups;
            CommitManualHierarchyMove(working);
        }

        public void MoveGroupIntoGroup(string sourceGroupKey, string targetGroupKey)
        {
            PsdHierarchyPlan working = ClonePlan(proposedPlanValue);
            List<PsdHierarchyPlanGroup> groups = working.groups ?? new List<PsdHierarchyPlanGroup>();
            PsdHierarchyPlanGroup source = groups.FirstOrDefault(group => group != null && group.key == sourceGroupKey);
            PsdHierarchyPlanGroup target = groups.FirstOrDefault(group => group != null && group.key == targetGroupKey);
            if (source == null || target == null)
                throw new ArgumentException("拖动的分组或放置目标已不存在。 ");
            if (source == target) throw new InvalidOperationException("分组不能拖放到自身。");
            EnsureManualMoveGroupIsUnlocked(source);
            EnsureManualMoveGroupIsUnlocked(target);
            if (IsGroupDescendant(groups, target.key, source.key))
                throw new InvalidOperationException("分组不能拖放到其子分组中。 ");
            source.parentKey = target.key;
            CommitManualHierarchyMove(working);
        }

        private void EnsureManualMoveNodeIsUnlocked(PsdHierarchyRequestNode node)
        {
            if (node == null || node.isProtectedBoundary || node.hasProjectComponents ||
                !string.IsNullOrEmpty(node.protectedBoundaryStableId))
                throw new InvalidOperationException("受保护或由项目托管的图层不能手动移动。 ");
        }

        private void EnsureManualMoveGroupIsUnlocked(PsdHierarchyPlanGroup group)
        {
            if (group == null) throw new ArgumentException("该分组已不存在。 ");
            if (GetAcceptedSubtreeGroupKeys(proposedPlanValue).Contains(group.key))
                throw new InvalidOperationException("已接受的分组已锁定，不能移动。 ");
        }

        private static PsdHierarchyPlanGroup FindMoveTargetGroup(
            IEnumerable<PsdHierarchyPlanGroup> groups,
            string targetStableId,
            string targetGroupKey)
        {
            if (!string.IsNullOrEmpty(targetGroupKey))
                return groups.FirstOrDefault(group => group != null && group.key == targetGroupKey);
            if (string.IsNullOrEmpty(targetStableId)) return null;
            return groups.FirstOrDefault(group => group != null &&
                (group.memberStableIds ?? new List<string>()).Contains(targetStableId));
        }

        private static bool IsGroupDescendant(
            IEnumerable<PsdHierarchyPlanGroup> groups,
            string groupKey,
            string possibleAncestorKey)
        {
            var parents = groups.Where(group => group != null)
                .ToDictionary(group => group.key, group => group.parentKey ?? string.Empty, StringComparer.Ordinal);
            string current = groupKey;
            while (!string.IsNullOrEmpty(current))
            {
                if (string.Equals(current, possibleAncestorKey, StringComparison.Ordinal)) return true;
                if (!parents.TryGetValue(current, out current)) return false;
            }
            return false;
        }

        private static string CreateManualGroupKey(
            IEnumerable<PsdHierarchyPlanGroup> groups,
            params string[] memberStableIds)
        {
            var used = new HashSet<string>(groups.Where(group => group != null)
                .Select(group => group.key), StringComparer.Ordinal);
            string baseKey = "manual_" + PsdStableLayerIdUtility.ComputeFnv1a(
                string.Join("|", memberStableIds.OrderBy(value => value, StringComparer.Ordinal)));
            string key = baseKey;
            for (int suffix = 2; !used.Add(key); suffix++) key = baseKey + "_" + suffix;
            return key;
        }

        private void CommitManualHierarchyMove(PsdHierarchyPlan working)
        {
            AdoptCurrentIdentity(working, fullRequest);
            PsdHierarchyPlanValidator.Validate(working, fullRequest);
            proposedPlanValue = ClonePlan(working);
            validationErrors.Clear();
            canApply = pendingMissingStableIds.Count == 0;
        }

        public async Task RefineGroupAsync(string groupKey, CancellationToken cancellationToken)
        {
            if (acceptedGroupKeysValue.Contains(groupKey)) throw new InvalidOperationException("已接受的分组已锁定。 ");
            PsdHierarchyPlanGroup selected = (proposedPlanValue.groups ?? new List<PsdHierarchyPlanGroup>())
                .FirstOrDefault(group => group != null && group.key == groupKey);
            if (selected == null) throw new ArgumentException("草稿中不存在该分组。", "groupKey");
            await RefineSelectionAsync(selected.memberStableIds, string.Empty, cancellationToken);
        }

        public async Task RefineSelectionAsync(
            IReadOnlyCollection<string> stableIds,
            string instruction,
            CancellationToken cancellationToken)
        {
            if (stableIds == null || stableIds.Count == 0)
                throw new ArgumentException("至少需要一个稳定 ID。", "stableIds");
            if (instruction != null && instruction.Length > 2000)
                throw new ArgumentException("精修说明不能超过 2000 个字符。", "instruction");

            PsdHierarchyPlan working = ClonePlan(proposedPlanValue);
            HashSet<string> immutableGroupKeys = GetAcceptedSubtreeGroupKeys(working);
            HashSet<string> requiredAncestorGroupKeys = GetRequiredAncestorGroupKeys(working, immutableGroupKeys);
            HashSet<string> protectedGroupKeys = new HashSet<string>(immutableGroupKeys, StringComparer.Ordinal);
            protectedGroupKeys.UnionWith(requiredAncestorGroupKeys);
            HashSet<string> locked = GetGroupMemberIds(working, protectedGroupKeys);
            var modifiableIds = new HashSet<string>(
                fullRequest.nodes
                    .Where(node => node != null &&
                                   PsdStableLayerIdUtility.IsPersistable(node.stableId) &&
                                   !node.isProtectedBoundary &&
                                   !node.hasProjectComponents &&
                                   string.IsNullOrEmpty(node.protectedBoundaryStableId))
                    .Select(node => node.stableId),
                StringComparer.Ordinal);
            var scope = new HashSet<string>(
                stableIds.Where(id => modifiableIds.Contains(id) && !locked.Contains(id)),
                StringComparer.Ordinal);
            if (scope.Count == 0) throw new InvalidOperationException("所选分组没有可供 AI 精修的未锁定成员。");

            isRunning = true;
            canApply = false;
            validationErrors.Clear();
            try
            {
                HashSet<string> context = BuildContextIds(scope);
                FocusedGroupContext groups = BuildFocusedGroupContext(working, scope, context);
                LockGroups(groups, protectedGroupKeys);
                var request = new PsdHierarchyAiRunRequest
                {
                    operationId = Guid.NewGuid().ToString("N"), request = CloneScopedRequest(fullRequest, context, scope), targetPrefabPath = targetPrefabPath, timeout = TimeSpan.FromMinutes(2),
                    instruction = (instruction ?? string.Empty).Trim(),
                    modifiableStableIds = scope.OrderBy(id => id, StringComparer.Ordinal).ToList(), contextStableIds = context.OrderBy(id => id, StringComparer.Ordinal).ToList(), baselineGroups = groups.baselineGroups,
                    modifiableGroupKeys = groups.modifiableGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(), scopeOwnedGroupKeys = groups.scopeOwnedGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    hybridGroupKeys = groups.hybridGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(), readonlyNeighborGroupKeys = groups.readonlyNeighborGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    structuralDependentGroupKeys = groups.structuralDependentGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(), immutableGroupKeys = immutableGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    requiredAncestorGroupKeys = requiredAncestorGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    existingGroupKeys = working.groups.Where(group => group != null).Select(group => group.key).OrderBy(key => key, StringComparer.Ordinal).ToList()
                };
                PsdHierarchyAiRunResult result = await runner.RunAsync(request, cancellationToken);
                if (result == null || !result.succeeded || result.plan == null) throw new InvalidOperationException(result != null ? result.error : "层级规划器未返回通过校验的方案。");
                PsdHierarchyFocusedPlanValidator.ValidatePartial(result.plan, request);
                MergeScope(working, result.plan, request);
                AdoptCurrentIdentity(working, fullRequest);
                PsdHierarchyPlanValidator.Validate(working, fullRequest);
                proposedPlanValue = ClonePlan(working);
                canApply = pendingMissingStableIds.Count == 0;
            }
            finally { isRunning = false; }
        }

        public async Task ReplanAllUnlockedAsync(CancellationToken cancellationToken)
        {
            PsdHierarchyPlan working = ClonePlan(proposedPlanValue);
            HashSet<string> immutableGroupKeys = GetAcceptedSubtreeGroupKeys(working);
            HashSet<string> requiredAncestorGroupKeys = GetRequiredAncestorGroupKeys(working, immutableGroupKeys);
            HashSet<string> protectedGroupKeys = new HashSet<string>(immutableGroupKeys, StringComparer.Ordinal);
            protectedGroupKeys.UnionWith(requiredAncestorGroupKeys);
            HashSet<string> locked = GetGroupMemberIds(working, protectedGroupKeys);
            var scope = new HashSet<string>(
                fullRequest.nodes
                    .Where(node => node != null &&
                                   PsdStableLayerIdUtility.IsPersistable(node.stableId) &&
                                   !node.isProtectedBoundary &&
                                   !node.hasProjectComponents &&
                                   string.IsNullOrEmpty(node.protectedBoundaryStableId) &&
                                   !locked.Contains(node.stableId))
                    .Select(node => node.stableId),
                StringComparer.Ordinal);
            if (scope.Count == 0)
                throw new InvalidOperationException("没有可重新整理的未锁定层级节点。 ");

            isRunning = true;
            canApply = false;
            validationErrors.Clear();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new HashSet<string>(
                    fullRequest.nodes
                        .Where(node => node != null && PsdStableLayerIdUtility.IsPersistable(node.stableId))
                        .Select(node => node.stableId),
                    StringComparer.Ordinal);
                FocusedGroupContext groups = BuildFocusedGroupContext(working, scope, context);
                LockGroups(groups, protectedGroupKeys);
                var request = new PsdHierarchyAiRunRequest
                {
                    operationId = Guid.NewGuid().ToString("N"),
                    request = CloneScopedRequest(fullRequest, context, scope),
                    targetPrefabPath = targetPrefabPath,
                    timeout = TimeSpan.FromMinutes(2),
                    modifiableStableIds = scope.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                    contextStableIds = context.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                    baselineGroups = groups.baselineGroups,
                    modifiableGroupKeys = groups.modifiableGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    scopeOwnedGroupKeys = groups.scopeOwnedGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    hybridGroupKeys = groups.hybridGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    readonlyNeighborGroupKeys = groups.readonlyNeighborGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    structuralDependentGroupKeys = groups.structuralDependentGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    immutableGroupKeys = immutableGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    requiredAncestorGroupKeys = requiredAncestorGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                    existingGroupKeys = working.groups.Where(group => group != null)
                        .Select(group => group.key).OrderBy(key => key, StringComparer.Ordinal).ToList()
                };
                PsdHierarchyAiRunResult result = await runner.RunAsync(request, cancellationToken);
                if (result == null || !result.succeeded || result.plan == null)
                    throw new InvalidOperationException(result != null
                        ? result.error
                        : "层级规划器未返回通过校验的方案。");
                PsdHierarchyFocusedPlanValidator.ValidatePartial(result.plan, request);
                MergeScope(working, result.plan, request);
                AdoptCurrentIdentity(working, fullRequest);
                PsdHierarchyPlanValidator.Validate(working, fullRequest);
                proposedPlanValue = ClonePlan(working);
                canApply = pendingMissingStableIds.Count == 0;
            }
            finally
            {
                isRunning = false;
            }
        }

        public async Task RefreshAsync(bool confirmMissingCleanup, CancellationToken cancellationToken)
        {
            isRunning = true;
            canApply = false;
            validationErrors.Clear();
            pendingMissingStableIds = confirmMissingCleanup
                ? new List<string>()
                : new List<string>(reconciliation.pendingMissingStableIds);
            PsdHierarchyPlan working = ClonePlan(baselinePlan);

            try
            {
                List<HashSet<string>> scopes = BuildFocusedScopes(working);
                HashSet<string> immutableGroupKeys = GetAcceptedSubtreeGroupKeys(working);
                HashSet<string> requiredAncestorGroupKeys = GetRequiredAncestorGroupKeys(working, immutableGroupKeys);
                HashSet<string> protectedGroupKeys = new HashSet<string>(immutableGroupKeys, StringComparer.Ordinal);
                protectedGroupKeys.UnionWith(requiredAncestorGroupKeys);
                HashSet<string> locked = GetGroupMemberIds(working, protectedGroupKeys);
                foreach (HashSet<string> scope in scopes)
                {
                    scope.ExceptWith(locked);
                }
                scopes.RemoveAll(scope => scope.Count == 0);
                foreach (HashSet<string> scope in scopes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    HashSet<string> contextIds = BuildContextIds(scope);
                    FocusedGroupContext groupContext = BuildFocusedGroupContext(working, scope, contextIds);
                    LockGroups(groupContext, protectedGroupKeys);
                    PsdHierarchyRequest scopedRequest = CloneScopedRequest(fullRequest, contextIds, scope);
                    var runRequest = new PsdHierarchyAiRunRequest
                    {
                        operationId = Guid.NewGuid().ToString("N"),
                        request = scopedRequest,
                        targetPrefabPath = targetPrefabPath,
                        timeout = TimeSpan.FromMinutes(2),
                        modifiableStableIds = scope.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                        contextStableIds = contextIds.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                        baselineGroups = groupContext.baselineGroups,
                        modifiableGroupKeys = groupContext.modifiableGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        scopeOwnedGroupKeys = groupContext.scopeOwnedGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        hybridGroupKeys = groupContext.hybridGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        readonlyNeighborGroupKeys = groupContext.readonlyNeighborGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        structuralDependentGroupKeys = groupContext.structuralDependentGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        immutableGroupKeys = immutableGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        requiredAncestorGroupKeys = requiredAncestorGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        existingGroupKeys = working.groups.Where(group => group != null)
                            .Select(group => group.key).OrderBy(key => key, StringComparer.Ordinal).ToList()
                    };
                    PsdHierarchyAiRunResult result = await runner.RunAsync(runRequest, cancellationToken);
                    if (result == null || !result.succeeded || result.plan == null)
                    {
                        validationErrors.Add(result != null && !string.IsNullOrWhiteSpace(result.error)
                            ? result.error
                            : "层级规划器未返回通过校验的方案。");
                        proposedPlanValue = ClonePlan(working);
                        return;
                    }

                    PsdHierarchyFocusedPlanValidator.ValidatePartial(result.plan, runRequest);
                    MergeScope(working, result.plan, runRequest);
                }

                if (confirmMissingCleanup)
                {
                    RemoveConfirmedMissing(working, reconciliation.pendingMissingStableIds);
                }
                AdoptCurrentIdentity(working, fullRequest);
                PsdHierarchyPlanValidator.Validate(working, fullRequest);
                proposedPlanValue = ClonePlan(working);
                if (pendingMissingStableIds.Count > 0)
                {
                    validationErrors.Add("缺失的 PSD ID 正等待明确确认后才会从草稿中清理。");
                    return;
                }
                canApply = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is PsdHierarchyPlanValidationException ||
                exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                validationErrors.Add(exception.Message);
                proposedPlanValue = ClonePlan(working);
            }
            finally
            {
                isRunning = false;
            }
        }

        public void ImportManualPlan(string json)
        {
            canApply = false;
            validationErrors.Clear();
            try
            {
                PsdHierarchyPlan candidate = PsdHierarchyPlanJson.Parse(json);
                PsdHierarchyPlanValidator.Validate(candidate, fullRequest);
                proposedPlanValue = ClonePlan(candidate);
                if (pendingMissingStableIds.Count > 0)
                {
                    validationErrors.Add("缺失的 PSD ID 正等待明确确认后才会从草稿中清理。");
                    return;
                }
                canApply = true;
            }
            catch (Exception exception) when (
                exception is PsdHierarchyPlanFormatException ||
                exception is PsdHierarchyPlanValidationException)
            {
                validationErrors.Add(exception.Message);
            }
        }

        public void ResetDraft()
        {
            if (isRunning)
                throw new InvalidOperationException("层级预览仍在运行。");

            PsdHierarchyPlan working = ClonePlan(baselinePlan);
            proposedPlanValue = working;
            acceptedGroupKeysValue.Clear();
            validationErrors.Clear();
            pendingMissingStableIds = new List<string>(reconciliation.pendingMissingStableIds);
            canApply = false;

            if (pendingMissingStableIds.Count > 0)
            {
                validationErrors.Add("缺失的 PSD ID 正等待明确确认后才会从草稿中清理。");
                return;
            }

            try
            {
                PsdHierarchyPlanValidator.Validate(working, fullRequest);
                canApply = true;
            }
            catch (PsdHierarchyPlanValidationException exception)
            {
                validationErrors.Add(exception.Message);
            }
        }

        public bool TryCreateValidatedApplyPlan(out PsdHierarchyPlan plan, out string error)
        {
            plan = ClonePlan(proposedPlanValue);
            error = string.Empty;
            if (!canApply || isRunning)
            {
                error = "层级预览尚未准备好应用。";
                plan = null;
                return false;
            }
            try
            {
                PsdHierarchyPlanValidator.Validate(plan, fullRequest);
                plan = ClonePlan(plan);
                return true;
            }
            catch (Exception exception) when (exception is PsdHierarchyPlanValidationException || exception is ArgumentException)
            {
                error = exception.Message;
                plan = null;
                canApply = false;
                return false;
            }
        }

        private List<HashSet<string>> BuildFocusedScopes(PsdHierarchyPlan plan)
        {
            var seeds = reconciliation.focusedInvalidatedScopeStableIds
                .Concat(reconciliation.unsortedNewStableIds)
                .Concat(reconciliation.geometryValidationStableIds)
                .Where(PsdStableLayerIdUtility.IsPersistable)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var unassigned = new HashSet<string>(seeds, StringComparer.Ordinal);
            var scopes = new List<HashSet<string>>();

            // These nodes have no prior group boundary to preserve, so one
            // request can safely organize them together. Sending each one to a
            // separate Codex process makes a large import appear frozen while
            // paying process/model startup cost once per layer.
            var ungrouped = new HashSet<string>(
                unassigned.Where(stableId => !(plan.groups ?? new List<PsdHierarchyPlanGroup>())
                    .Any(group => group != null && (group.memberStableIds ?? new List<string>()).Contains(stableId))),
                StringComparer.Ordinal);
            if (ungrouped.Count > 0)
            {
                scopes.Add(ungrouped);
                unassigned.ExceptWith(ungrouped);
            }

            while (unassigned.Count > 0)
            {
                string seed = unassigned.OrderBy(value => value, StringComparer.Ordinal).First();
                var scope = new HashSet<string>(StringComparer.Ordinal) { seed };

                // Coalesce only invalidated seeds that already share a group.
                // Unchanged members are deliberately not promoted into scope:
                // they make that group hybrid and remain readonly.
                bool expanded;
                do
                {
                    expanded = false;
                    foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
                    {
                        if (group != null && group.memberStableIds.Any(scope.Contains))
                        {
                            int before = scope.Count;
                            scope.UnionWith(group.memberStableIds.Where(unassigned.Contains));
                            expanded |= before != scope.Count;
                        }
                    }
                }
                while (expanded);

                unassigned.ExceptWith(scope);
                scopes.Add(scope);
            }
            return scopes;
        }

        private HashSet<string> GetAcceptedSubtreeGroupKeys(PsdHierarchyPlan plan)
        {
            PsdHierarchyPlanGroup[] groups = (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null).ToArray();
            var result = new HashSet<string>(
                groups.Where(group => acceptedGroupKeysValue.Contains(group.key))
                    .Select(group => group.key),
                StringComparer.Ordinal);
            bool changed;
            do
            {
                changed = false;
                foreach (PsdHierarchyPlanGroup group in groups)
                {
                    if (result.Contains(group.parentKey ?? string.Empty))
                    {
                        changed |= result.Add(group.key);
                    }
                }
            }
            while (changed);
            return result;
        }

        private static HashSet<string> GetGroupMemberIds(
            PsdHierarchyPlan plan,
            HashSet<string> groupKeys)
        {
            return new HashSet<string>(
                (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                    .Where(group => group != null && groupKeys.Contains(group.key))
                    .SelectMany(group => group.memberStableIds ?? new List<string>()),
                StringComparer.Ordinal);
        }

        private static HashSet<string> GetRequiredAncestorGroupKeys(
            PsdHierarchyPlan plan,
            HashSet<string> immutableGroupKeys)
        {
            var byKey = (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null)
                .ToDictionary(group => group.key, StringComparer.Ordinal);
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string immutableKey in immutableGroupKeys)
            {
                PsdHierarchyPlanGroup current;
                if (!byKey.TryGetValue(immutableKey, out current))
                {
                    continue;
                }

                string parentKey = current.parentKey ?? string.Empty;
                while (!string.IsNullOrEmpty(parentKey) && byKey.TryGetValue(parentKey, out current))
                {
                    if (!immutableGroupKeys.Contains(parentKey))
                    {
                        result.Add(parentKey);
                    }
                    parentKey = current.parentKey ?? string.Empty;
                }
            }
            return result;
        }

        private static void LockGroups(
            FocusedGroupContext context,
            HashSet<string> groupKeys)
        {
            context.modifiableGroupKeys.ExceptWith(groupKeys);
            context.scopeOwnedGroupKeys.ExceptWith(groupKeys);
            context.hybridGroupKeys.ExceptWith(groupKeys);
            context.readonlyNeighborGroupKeys.ExceptWith(groupKeys);
            context.structuralDependentGroupKeys.ExceptWith(groupKeys);
        }

        private static PsdHierarchyReconciliationResult CloneReconciliation(PsdHierarchyReconciliationResult source)
        {
            var snapshot = new PsdHierarchyReconciliationResult { requiresReplan = source.requiresReplan };
            snapshot.contentOnlyStableIds.AddRange(source.contentOnlyStableIds);
            snapshot.geometryValidationStableIds.AddRange(source.geometryValidationStableIds);
            snapshot.focusedInvalidatedScopeStableIds.AddRange(source.focusedInvalidatedScopeStableIds);
            snapshot.unsortedNewStableIds.AddRange(source.unsortedNewStableIds);
            snapshot.unsortedUnstableIds.AddRange(source.unsortedUnstableIds);
            snapshot.pendingMissingStableIds.AddRange(source.pendingMissingStableIds);
            return snapshot;
        }

        private HashSet<string> BuildContextIds(HashSet<string> scope)
        {
            var context = new HashSet<string>(scope, StringComparer.Ordinal);
            foreach (PsdHierarchyRequestNode focused in fullRequest.nodes.Where(node => node != null && scope.Contains(node.stableId)))
            {
                foreach (PsdHierarchyRequestNode sibling in fullRequest.nodes.Where(node => node != null &&
                             string.Equals(node.parentStableId ?? string.Empty, focused.parentStableId ?? string.Empty, StringComparison.Ordinal) &&
                             Math.Abs(node.siblingIndex - focused.siblingIndex) == 1))
                {
                    context.Add(sibling.stableId);
                }

                string boundary = focused.protectedBoundaryStableId ?? string.Empty;
                if (!string.IsNullOrEmpty(boundary))
                {
                    foreach (PsdHierarchyRequestNode boundaryNode in fullRequest.nodes.Where(node => node != null &&
                                 (string.Equals(node.stableId, boundary, StringComparison.Ordinal) ||
                                  string.Equals(node.protectedBoundaryStableId ?? string.Empty, boundary, StringComparison.Ordinal))))
                    {
                        context.Add(boundaryNode.stableId);
                    }
                }
            }
            return context;
        }

        private static FocusedGroupContext BuildFocusedGroupContext(
            PsdHierarchyPlan plan,
            HashSet<string> scope,
            HashSet<string> contextIds)
        {
            PsdHierarchyPlanGroup[] groups = (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null).ToArray();
            var byKey = groups.ToDictionary(group => group.key, StringComparer.Ordinal);
            var selectedKeys = new HashSet<string>(groups
                .Where(group => (group.memberStableIds ?? new List<string>()).Any(contextIds.Contains))
                .Select(group => group.key), StringComparer.Ordinal);
            var ownedKeys = new HashSet<string>(groups.Where(group =>
                    (group.memberStableIds ?? new List<string>()).Count > 0 &&
                    (group.memberStableIds ?? new List<string>()).All(scope.Contains))
                .Select(group => group.key), StringComparer.Ordinal);

            // Work from immutable snapshots during each pass. This makes group
            // ordering irrelevant and includes every ancestor and descendant of
            // an owned group before classification or request serialization.
            bool changed;
            do
            {
                changed = false;
                string[] selectedSnapshot = selectedKeys.ToArray();
                foreach (string key in selectedSnapshot)
                {
                    PsdHierarchyPlanGroup group;
                    if (!byKey.TryGetValue(key, out group)) continue;
                    string parentKey = group.parentKey ?? string.Empty;
                    if (!string.IsNullOrEmpty(parentKey) && byKey.ContainsKey(parentKey))
                        changed |= selectedKeys.Add(parentKey);
                }

                string[] ownedOrSelectedSnapshot = selectedKeys.Union(ownedKeys).ToArray();
                foreach (PsdHierarchyPlanGroup group in groups)
                {
                    if (ownedOrSelectedSnapshot.Contains(group.parentKey ?? string.Empty, StringComparer.Ordinal))
                        changed |= selectedKeys.Add(group.key);
                }

                int beforeContext = contextIds.Count;
                foreach (PsdHierarchyPlanGroup group in groups.Where(group => selectedKeys.Contains(group.key)).ToArray())
                    contextIds.UnionWith(group.memberStableIds ?? new List<string>());
                changed |= beforeContext != contextIds.Count;

                foreach (PsdHierarchyPlanGroup group in groups)
                {
                    if ((group.memberStableIds ?? new List<string>()).Any(contextIds.Contains))
                        changed |= selectedKeys.Add(group.key);
                }
            }
            while (changed);

            var hybridKeys = new HashSet<string>(groups.Where(group =>
                    (group.memberStableIds ?? new List<string>()).Any(scope.Contains) &&
                    (group.memberStableIds ?? new List<string>()).Any(id => !scope.Contains(id)))
                .Select(group => group.key), StringComparer.Ordinal);
            ownedKeys.ExceptWith(hybridKeys);

            var structuralKeys = new HashSet<string>(StringComparer.Ordinal);
            var frontier = new HashSet<string>(ownedKeys, StringComparer.Ordinal);
            while (frontier.Count > 0)
            {
                string[] parents = frontier.ToArray();
                frontier.Clear();
                foreach (PsdHierarchyPlanGroup group in groups.Where(group =>
                             parents.Contains(group.parentKey ?? string.Empty, StringComparer.Ordinal)).ToArray())
                {
                    if (!ownedKeys.Contains(group.key) && !hybridKeys.Contains(group.key) &&
                        structuralKeys.Add(group.key))
                        frontier.Add(group.key);
                }
            }

            var readonlyKeys = new HashSet<string>(selectedKeys, StringComparer.Ordinal);
            readonlyKeys.ExceptWith(ownedKeys);
            readonlyKeys.ExceptWith(hybridKeys);
            readonlyKeys.ExceptWith(structuralKeys);
            var modifiableKeys = new HashSet<string>(ownedKeys, StringComparer.Ordinal);
            modifiableKeys.UnionWith(hybridKeys);
            modifiableKeys.UnionWith(readonlyKeys);
            modifiableKeys.UnionWith(structuralKeys);

            return new FocusedGroupContext
            {
                baselineGroups = selectedKeys.Where(byKey.ContainsKey)
                    .OrderBy(key => key, StringComparer.Ordinal).Select(key => CloneGroup(byKey[key])).ToList(),
                modifiableGroupKeys = modifiableKeys,
                scopeOwnedGroupKeys = ownedKeys,
                hybridGroupKeys = hybridKeys,
                readonlyNeighborGroupKeys = readonlyKeys,
                structuralDependentGroupKeys = structuralKeys
            };
        }

        private sealed class FocusedGroupContext
        {
            public List<PsdHierarchyPlanGroup> baselineGroups = new List<PsdHierarchyPlanGroup>();
            public HashSet<string> modifiableGroupKeys = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> scopeOwnedGroupKeys = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> hybridGroupKeys = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> readonlyNeighborGroupKeys = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> structuralDependentGroupKeys = new HashSet<string>(StringComparer.Ordinal);
        }

        private static void MergeScope(
            PsdHierarchyPlan target,
            PsdHierarchyPlan partial,
            PsdHierarchyAiRunRequest runRequest)
        {
            var scopeOwnedKeys = new HashSet<string>(
                runRequest.scopeOwnedGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var replaceAuthorizedKeys = new HashSet<string>(scopeOwnedKeys, StringComparer.Ordinal);
            var returnedKeys = new HashSet<string>((partial.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null).Select(group => group.key));
            replaceAuthorizedKeys.UnionWith((runRequest.hybridGroupKeys ?? new List<string>())
                .Where(returnedKeys.Contains));
            replaceAuthorizedKeys.UnionWith((runRequest.readonlyNeighborGroupKeys ?? new List<string>())
                .Where(returnedKeys.Contains));
            replaceAuthorizedKeys.UnionWith((runRequest.structuralDependentGroupKeys ?? new List<string>())
                .Where(returnedKeys.Contains));

            var removedOwnedKeys = new HashSet<string>(scopeOwnedKeys, StringComparer.Ordinal);
            removedOwnedKeys.ExceptWith(returnedKeys);
            var removedParentByKey = target.groups
                .Where(group => group != null && removedOwnedKeys.Contains(group.key))
                .ToDictionary(group => group.key, group => group.parentKey ?? string.Empty, StringComparer.Ordinal);
            target.groups.RemoveAll(group => group != null && replaceAuthorizedKeys.Contains(group.key));

            // Omitted dependent groups survive an owned-parent dissolve. Resolve
            // the complete removed chain, so the result is deterministic even
            // when parent/child/grandchild groups were stored out of order.
            foreach (PsdHierarchyPlanGroup group in target.groups.Where(group => group != null).ToArray())
                group.parentKey = ResolveSurvivingParent(group.parentKey, removedParentByKey);

            var scope = new HashSet<string>(
                runRequest.modifiableStableIds ?? new List<string>(), StringComparer.Ordinal);
            target.renames.RemoveAll(rename => rename != null && scope.Contains(rename.stableId));
            target.groups.AddRange((partial.groups ?? new List<PsdHierarchyPlanGroup>()).Select(CloneGroup));
            target.renames.AddRange((partial.renames ?? new List<PsdHierarchyPlanRename>()).Select(CloneRename));
        }

        private static string ResolveSurvivingParent(
            string parentKey,
            IDictionary<string, string> removedParentByKey)
        {
            string current = parentKey ?? string.Empty;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(current) && removedParentByKey.ContainsKey(current))
            {
                if (!visited.Add(current))
                    throw new PsdHierarchyPlanValidationException("移除后的层级包含父级循环。 ");
                current = removedParentByKey[current] ?? string.Empty;
            }
            return current;
        }

        private static void RemoveConfirmedMissing(PsdHierarchyPlan plan, IEnumerable<string> missingStableIds)
        {
            var missing = new HashSet<string>(missingStableIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            plan.renames.RemoveAll(rename => rename != null && missing.Contains(rename.stableId));
            foreach (PsdHierarchyPlanGroup group in plan.groups)
            {
                group.memberStableIds.RemoveAll(missing.Contains);
            }
            while (true)
            {
                PsdHierarchyPlanGroup empty = plan.groups.FirstOrDefault(group => group.memberStableIds.Count == 0);
                if (empty == null) break;
                foreach (PsdHierarchyPlanGroup child in plan.groups.Where(group =>
                             string.Equals(group.parentKey, empty.key, StringComparison.Ordinal)))
                {
                    child.parentKey = empty.parentKey ?? string.Empty;
                }
                plan.groups.Remove(empty);
            }
        }

        private static PsdHierarchyRequest CloneScopedRequest(
            PsdHierarchyRequest source,
            HashSet<string> contextIds,
            HashSet<string> focusedIds)
        {
            PsdHierarchyRectangle focusedBounds = ComputeBounds(source.nodes.Where(node => node != null && focusedIds.Contains(node.stableId)));
            return new PsdHierarchyRequest
            {
                schemaVersion = source.schemaVersion,
                sourcePsdGuid = source.sourcePsdGuid,
                sourceFingerprint = source.sourceFingerprint,
                contentFingerprint = source.contentFingerprint,
                structureFingerprint = source.structureFingerprint,
                geometryFingerprint = source.geometryFingerprint,
                documentWidth = source.documentWidth,
                documentHeight = source.documentHeight,
                nodes = source.nodes.Where(node => node != null && contextIds.Contains(node.stableId)).Select(CloneNode).ToList(),
                currentPrefabHierarchy = source.currentPrefabHierarchy
                    .Where(node => node != null && contextIds.Contains(node.stableId))
                    .Select(CloneMetadata).ToList(),
                previews = source.previews.Where(preview => preview != null && Intersects(preview.crop, focusedBounds))
                    .Select(preview => CloneClippedPreview(preview, focusedBounds)).ToList()
            };
        }

        private static PsdHierarchyRectangle ComputeBounds(IEnumerable<PsdHierarchyRequestNode> nodes)
        {
            List<PsdHierarchyRequestNode> values = nodes.ToList();
            if (values.Count == 0) return new PsdHierarchyRectangle();
            float minX = values.Min(node => node.rectangle.x);
            float minY = values.Min(node => node.rectangle.y);
            float maxX = values.Max(node => node.rectangle.x + node.rectangle.width);
            float maxY = values.Max(node => node.rectangle.y + node.rectangle.height);
            return new PsdHierarchyRectangle { x = minX, y = minY, width = maxX - minX, height = maxY - minY };
        }

        private static bool Intersects(PsdHierarchyRectangle left, PsdHierarchyRectangle right)
        {
            return left.x < right.x + right.width && left.x + left.width > right.x &&
                   left.y < right.y + right.height && left.y + left.height > right.y;
        }

        private static PsdHierarchyPreviewReference CloneClippedPreview(
            PsdHierarchyPreviewReference source,
            PsdHierarchyRectangle bounds)
        {
            float x = Math.Max(source.crop.x, bounds.x);
            float y = Math.Max(source.crop.y, bounds.y);
            float right = Math.Min(source.crop.x + source.crop.width, bounds.x + bounds.width);
            float bottom = Math.Min(source.crop.y + source.crop.height, bounds.y + bounds.height);
            return new PsdHierarchyPreviewReference
            {
                key = source.key,
                kind = source.kind,
                crop = new PsdHierarchyRectangle { x = x, y = y, width = Math.Max(0, right - x), height = Math.Max(0, bottom - y) }
            };
        }

        private static PsdHierarchyRequest CloneRequest(PsdHierarchyRequest source)
        {
            return new PsdHierarchyRequest
            {
                schemaVersion = source.schemaVersion,
                sourcePsdGuid = source.sourcePsdGuid,
                sourceFingerprint = source.sourceFingerprint,
                contentFingerprint = source.contentFingerprint,
                structureFingerprint = source.structureFingerprint,
                geometryFingerprint = source.geometryFingerprint,
                documentWidth = source.documentWidth,
                documentHeight = source.documentHeight,
                nodes = (source.nodes ?? new List<PsdHierarchyRequestNode>()).Where(node => node != null).Select(CloneNode).ToList(),
                currentPrefabHierarchy = (source.currentPrefabHierarchy ?? new List<PsdHierarchyPrefabNodeMetadata>())
                    .Where(node => node != null).Select(CloneMetadata).ToList(),
                previews = (source.previews ?? new List<PsdHierarchyPreviewReference>())
                    .Where(preview => preview != null).Select(ClonePreview).ToList()
            };
        }

        private static void AdoptCurrentIdentity(PsdHierarchyPlan plan, PsdHierarchyRequest request)
        {
            plan.schemaVersion = PsdHierarchyPlan.CurrentSchemaVersion;
            plan.sourcePsdGuid = request.sourcePsdGuid;
            plan.sourceFingerprint = request.sourceFingerprint;
            plan.contentFingerprint = request.contentFingerprint;
            plan.structureFingerprint = request.structureFingerprint;
            plan.geometryFingerprint = request.geometryFingerprint;
        }

        private static PsdHierarchyPlan ClonePlan(PsdHierarchyPlan source)
        {
            return new PsdHierarchyPlan
            {
                schemaVersion = source.schemaVersion,
                sourcePsdGuid = source.sourcePsdGuid,
                sourceFingerprint = source.sourceFingerprint,
                contentFingerprint = source.contentFingerprint,
                structureFingerprint = source.structureFingerprint,
                geometryFingerprint = source.geometryFingerprint,
                groups = (source.groups ?? new List<PsdHierarchyPlanGroup>()).Select(CloneGroup).ToList(),
                renames = (source.renames ?? new List<PsdHierarchyPlanRename>()).Select(CloneRename).ToList()
            };
        }

        private static PsdHierarchyPlanGroup CloneGroup(PsdHierarchyPlanGroup source)
        {
            return new PsdHierarchyPlanGroup
            {
                key = source.key,
                parentKey = source.parentKey,
                memberStableIds = new List<string>(source.memberStableIds ?? new List<string>()),
                displayName = source.displayName,
                evidence = source.evidence,
                confidence = source.confidence
            };
        }

        private static PsdHierarchyPlanRename CloneRename(PsdHierarchyPlanRename source)
        {
            return new PsdHierarchyPlanRename
            {
                stableId = source.stableId,
                name = source.name,
                evidence = source.evidence,
                confidence = source.confidence
            };
        }

        private static PsdHierarchyRequestNode CloneNode(PsdHierarchyRequestNode source)
        {
            return new PsdHierarchyRequestNode
            {
                stableId = source.stableId,
                originalName = source.originalName,
                kind = source.kind,
                parentStableId = source.parentStableId,
                siblingIndex = source.siblingIndex,
                rectangle = source.rectangle,
                hasProjectComponents = source.hasProjectComponents,
                isProtectedBoundary = source.isProtectedBoundary,
                protectedBoundaryStableId = source.protectedBoundaryStableId
            };
        }

        private static PsdHierarchyPrefabNodeMetadata CloneMetadata(PsdHierarchyPrefabNodeMetadata source)
        {
            return new PsdHierarchyPrefabNodeMetadata
            {
                stableId = source.stableId,
                parentStableId = source.parentStableId,
                siblingIndex = source.siblingIndex,
                hierarchyPath = source.hierarchyPath,
                componentTypes = new List<string>(source.componentTypes ?? new List<string>()),
                hasProjectComponents = source.hasProjectComponents,
                isProtectedBoundary = source.isProtectedBoundary,
                protectedBoundaryStableId = source.protectedBoundaryStableId
            };
        }

        private static PsdHierarchyPreviewReference ClonePreview(PsdHierarchyPreviewReference source)
        {
            return new PsdHierarchyPreviewReference { key = source.key, kind = source.kind, crop = source.crop };
        }
    }

    /// <summary>
    /// Validates the untrusted focused response without pretending it is a full
    /// hierarchy. Ancestor keys may live only in the supplied baseline graph;
    /// the merged complete plan is still validated by Task 3 afterwards.
    /// </summary>
    public static class PsdHierarchyFocusedPlanValidator
    {
        public static void ValidatePartial(PsdHierarchyPlan partial, PsdHierarchyAiRunRequest runRequest)
        {
            if (partial == null || runRequest == null || runRequest.request == null)
                throw new PsdHierarchyPlanValidationException("局部方案缺少上下文。 ");

            PsdHierarchyRequest request = runRequest.request;
            if (partial.schemaVersion != PsdHierarchyPlan.CurrentSchemaVersion ||
                !string.Equals(partial.sourcePsdGuid, request.sourcePsdGuid, StringComparison.Ordinal) ||
                !string.Equals(partial.sourceFingerprint, request.sourceFingerprint, StringComparison.Ordinal) ||
                !string.Equals(partial.contentFingerprint, request.contentFingerprint, StringComparison.Ordinal) ||
                !string.Equals(partial.structureFingerprint, request.structureFingerprint, StringComparison.Ordinal) ||
                !string.Equals(partial.geometryFingerprint, request.geometryFingerprint, StringComparison.Ordinal))
            {
                throw new PsdHierarchyPlanValidationException("局部方案的身份或指纹与请求不匹配。 ");
            }

            var allowedIds = new HashSet<string>(runRequest.modifiableStableIds ?? new List<string>(), StringComparer.Ordinal);
            var contextIds = new HashSet<string>((request.nodes ?? new List<PsdHierarchyRequestNode>())
                .Where(node => node != null).Select(node => node.stableId), StringComparer.Ordinal);
            var baselineByKey = (runRequest.baselineGroups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null).ToDictionary(group => group.key, StringComparer.Ordinal);
            var modifiableGroupKeys = new HashSet<string>(
                runRequest.modifiableGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var scopeOwnedGroupKeys = new HashSet<string>(
                runRequest.scopeOwnedGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var hybridGroupKeys = new HashSet<string>(
                runRequest.hybridGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var readonlyNeighborGroupKeys = new HashSet<string>(
                runRequest.readonlyNeighborGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var structuralDependentGroupKeys = new HashSet<string>(
                runRequest.structuralDependentGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var existingGroupKeys = new HashSet<string>(
                runRequest.existingGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var immutableGroupKeys = new HashSet<string>(
                runRequest.immutableGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var requiredAncestorGroupKeys = new HashSet<string>(
                runRequest.requiredAncestorGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var categorizedGroupKeys = new HashSet<string>(scopeOwnedGroupKeys, StringComparer.Ordinal);
            categorizedGroupKeys.UnionWith(hybridGroupKeys);
            categorizedGroupKeys.UnionWith(readonlyNeighborGroupKeys);
            categorizedGroupKeys.UnionWith(structuralDependentGroupKeys);
            int categorizedCount = scopeOwnedGroupKeys.Count + hybridGroupKeys.Count +
                                   readonlyNeighborGroupKeys.Count + structuralDependentGroupKeys.Count;
            if (categorizedGroupKeys.Count != categorizedCount ||
                !categorizedGroupKeys.SetEquals(modifiableGroupKeys))
                throw new PsdHierarchyPlanValidationException("局部分组的归属元数据不一致。 ");
            if (immutableGroupKeys.Overlaps(modifiableGroupKeys))
                throw new PsdHierarchyPlanValidationException(
                    "局部分组范围不能将不可变分组设为可修改。 ");
            if (requiredAncestorGroupKeys.Overlaps(modifiableGroupKeys) ||
                requiredAncestorGroupKeys.Overlaps(immutableGroupKeys))
                throw new PsdHierarchyPlanValidationException(
                    "局部分组范围包含不一致的必需祖先分组。 ");
            foreach (string groupKey in modifiableGroupKeys)
            {
                PsdHierarchyPlanGroup baselineGroup;
                if (!baselineByKey.TryGetValue(groupKey, out baselineGroup) ||
                    !(baselineGroup.memberStableIds ?? new List<string>()).Any(id =>
                        allowedIds.Contains(id) || contextIds.Contains(id)))
                {
                    throw new PsdHierarchyPlanValidationException(
                        "局部请求授予了无效的分组范围：'" + groupKey + "'。 ");
                }
            }
            var partialKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (PsdHierarchyPlanGroup group in partial.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (group == null || !partialKeys.Add(group.key))
                    throw new PsdHierarchyPlanValidationException("局部方案包含空分组键或重复分组键。 ");
                if (immutableGroupKeys.Contains(group.key))
                    throw new PsdHierarchyPlanValidationException(
                        "局部方案修改了不可变分组：'" + group.key + "'。 ");
                if (immutableGroupKeys.Contains(group.parentKey ?? string.Empty))
                    throw new PsdHierarchyPlanValidationException(
                        "局部方案在不可变分组下新增或修改了子分组：'" + group.parentKey + "'。 ");
                if (requiredAncestorGroupKeys.Contains(group.key))
                    throw new PsdHierarchyPlanValidationException(
                        "局部方案修改了必需祖先分组：'" + group.key + "'。 ");
                if (baselineByKey.ContainsKey(group.key) && !modifiableGroupKeys.Contains(group.key))
                    throw new PsdHierarchyPlanValidationException("局部方案修改了范围外的分组：'" + group.key + "'。 ");
                if (!baselineByKey.ContainsKey(group.key) && existingGroupKeys.Contains(group.key))
                    throw new PsdHierarchyPlanValidationException(
                        "局部方案复用了范围外已有的分组键：'" + group.key + "'。 ");

                PsdHierarchyPlanGroup baselineGroup;
                if (baselineByKey.TryGetValue(group.key, out baselineGroup))
                {
                    List<string> baselineMembers = baselineGroup.memberStableIds ?? new List<string>();
                    bool hybrid = hybridGroupKeys.Contains(group.key);
                    bool readonlyNeighbor = readonlyNeighborGroupKeys.Contains(group.key);
                    bool structuralDependent = structuralDependentGroupKeys.Contains(group.key);
                    bool protectsReadonlyState = hybrid || readonlyNeighbor || structuralDependent;
                    List<string> readonlyBefore = baselineMembers.Where(id => !allowedIds.Contains(id)).ToList();
                    List<string> readonlyAfter = (group.memberStableIds ?? new List<string>())
                        .Where(id => !allowedIds.Contains(id)).ToList();
                    if (protectsReadonlyState && !readonlyBefore.SequenceEqual(readonlyAfter, StringComparer.Ordinal))
                        throw new PsdHierarchyPlanValidationException(
                            "局部方案修改了只读分组的成员或顺序：'" + group.key + "'。 ");
                    if ((hybrid || readonlyNeighbor) &&
                        !string.Equals(group.parentKey ?? string.Empty, baselineGroup.parentKey ?? string.Empty, StringComparison.Ordinal))
                        throw new PsdHierarchyPlanValidationException(
                            "局部方案移动了只读分组：'" + group.key + "'。 ");
                    if (protectsReadonlyState &&
                        (!string.Equals(group.displayName ?? string.Empty, baselineGroup.displayName ?? string.Empty, StringComparison.Ordinal) ||
                         !string.Equals(group.evidence ?? string.Empty, baselineGroup.evidence ?? string.Empty, StringComparison.Ordinal) ||
                         group.confidence != baselineGroup.confidence))
                        throw new PsdHierarchyPlanValidationException(
                            "局部方案修改了只读分组的元数据：'" + group.key + "'。 ");
                    if (structuralDependent &&
                        !baselineMembers.SequenceEqual(group.memberStableIds ?? new List<string>(), StringComparer.Ordinal))
                        throw new PsdHierarchyPlanValidationException(
                            "局部方案修改了结构依赖分组的成员：'" + group.key + "'。 ");
                    if (readonlyNeighbor &&
                        !(group.memberStableIds ?? new List<string>()).Any(allowedIds.Contains))
                        throw new PsdHierarchyPlanValidationException(
                            "局部方案重述了只读分组但未加入可修改 ID：'" + group.key + "'。 ");
                }

                foreach (string member in group.memberStableIds ?? new List<string>())
                {
                    bool baselineReadonlyMember = baselineGroup != null &&
                        (baselineGroup.memberStableIds ?? new List<string>()).Contains(member);
                    if ((!allowedIds.Contains(member) && !baselineReadonlyMember) || !contextIds.Contains(member))
                        throw new PsdHierarchyPlanValidationException(
                            "局部方案修改了范围外的 ID：'" + member + "'。 ");
                }
            }

            foreach (PsdHierarchyPlanGroup group in partial.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (!string.IsNullOrEmpty(group.parentKey) &&
                    !partialKeys.Contains(group.parentKey) &&
                    !baselineByKey.ContainsKey(group.parentKey))
                {
                    throw new PsdHierarchyPlanValidationException(
                        "局部分组 '" + group.key + "' 引用了未知祖先分组 '" + group.parentKey + "'。 ");
                }
            }

            var renamed = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanRename rename in partial.renames ?? new List<PsdHierarchyPlanRename>())
            {
                if (rename == null || !renamed.Add(rename.stableId) || !allowedIds.Contains(rename.stableId) || !contextIds.Contains(rename.stableId))
                    throw new PsdHierarchyPlanValidationException("局部重命名修改了范围外的 ID。 ");
            }

            bool hasFocusedDecision = (partial.groups ?? new List<PsdHierarchyPlanGroup>())
                                          .Any(group => group != null &&
                                              (group.memberStableIds ?? new List<string>()).Any(allowedIds.Contains)) ||
                                      (partial.renames ?? new List<PsdHierarchyPlanRename>())
                                          .Any(rename => rename != null && allowedIds.Contains(rename.stableId));
            if (!hasFocusedDecision && scopeOwnedGroupKeys.Count == 0 && hybridGroupKeys.Count == 0)
                throw new PsdHierarchyPlanValidationException(
                    "局部重新整理没有返回可修改 ID 的处理结果。 ");
        }
    }

    /// <summary>Bounded manual import path shared by the preview and tests.</summary>
    public static class PsdHierarchyManualPlanLoader
    {
        public static PsdHierarchyPlan Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("未找到层级方案文件。", path);
            if (new FileInfo(path).Length > PsdHierarchyContractLimits.MaxJsonUtf8Bytes)
                throw new PsdHierarchyPlanFormatException("层级方案超过 UTF-8 字节数限制。 ");
            string json;
            using (var reader = new StreamReader(path, System.Text.Encoding.UTF8, true, 4096))
            {
                try
                {
                    json = PsdHierarchyBoundedTextReader.Read(
                        reader, PsdHierarchyContractLimits.MaxJsonCharacters);
                }
                catch (PsdHierarchyOutputLimitException exception)
                {
                    throw new PsdHierarchyPlanFormatException(exception.Message, exception);
                }
            }
            return PsdHierarchyPlanJson.Parse(json);
        }
    }

    /// <summary>
    /// Non-mutating Editor preview. Apply is exposed as an event only after full
    /// validation; the window itself never saves an Asset, Prefab or Profile.
    /// </summary>
    public sealed class PsdHierarchyOrganizerWindow : EditorWindow
    {
        private const string WindowStylePath =
            "Assets/UnityPSDLayoutTool2/Assets/PSDLayoutTool2/Editor/PsdPrefab/Hierarchy/PsdHierarchyOrganizerWindow.uss";

        private PsdHierarchyOrganizerPreviewModel model;
        private CancellationTokenSource cancellation;
        private Vector2 currentTreeScroll;
        private Vector2 proposedTreeScroll;
        private bool confirmMissingCleanup;
        private Action<PsdHierarchyPlan> applyHandler;
        private string selectedGroupKey = string.Empty;
        private readonly Dictionary<string, bool> currentTreeFoldouts = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> proposedTreeFoldouts = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly List<DraftTreeItem> draftItems = new List<DraftTreeItem>();
        private TreeView draftTree;
        private VisualElement previewContent;
        private VisualElement inspectorContent;
        private VisualElement diagnosticsContent;
        private Label statusLabel;
        private Label draftSummaryLabel;
        private Button applyButton;
        private Button cancelButton;
        private Toggle missingCleanupToggle;
        private DraftTreeItem selectedDraftItem;
        private DraftTreeItem dragSourceItem;
        private DraftTreeItem dragTargetItem;
        private Texture2D compositePreviewTexture;
        private string sourcePsdPath = string.Empty;

        public static PsdHierarchyOrganizerWindow Open(
            PsdHierarchyOrganizerPreviewModel previewModel,
            Action<PsdHierarchyPlan> applyHandler = null,
            string psdAssetPath = null)
        {
            var window = GetWindow<PsdHierarchyOrganizerWindow>(true, "PSD 层级整理", true);
            window.ReplaceContext(previewModel, applyHandler);
            window.sourcePsdPath = psdAssetPath ?? string.Empty;
            window.ReleaseCompositePreviewTexture();
            window.minSize = new Vector2(940f, 560f);
            window.Show();
            window.RefreshUi();
            return window;
        }

        private void OnDisable()
        {
            ClearContext();
        }

        internal void ReplaceContext(
            PsdHierarchyOrganizerPreviewModel previewModel,
            Action<PsdHierarchyPlan> handler)
        {
            CancelRunningRequest();
            model = previewModel ?? throw new ArgumentNullException("previewModel");
            applyHandler = handler;
            confirmMissingCleanup = false;
            selectedDraftItem = null;
            dragSourceItem = null;
            dragTargetItem = null;
            RefreshUi();
        }

        internal void ClearContext()
        {
            CancelRunningRequest();
            applyHandler = null;
            model = null;
            confirmMissingCleanup = false;
            selectedDraftItem = null;
            ReleaseCompositePreviewTexture();
        }

        internal void DispatchApply(PsdHierarchyPlan plan)
        {
            Action<PsdHierarchyPlan> current = applyHandler;
            if (current != null) current(plan);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(WindowStylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            rootVisualElement.AddToClassList("psd-organizer-root");
            rootVisualElement.Add(BuildHeader());
            rootVisualElement.Add(BuildDiagnostics());
            rootVisualElement.Add(BuildColumns());
            rootVisualElement.Add(BuildFooter());
            RefreshUi();
        }

        internal void CreateGUIForTests()
        {
            CreateGUI();
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("psd-organizer-header");
            var identity = new VisualElement();
            identity.AddToClassList("psd-organizer-identity");
            identity.Add(new Label("PSD 层级整理") { name = "organizer-title" });
            statusLabel = new Label { name = "organizer-status" };
            statusLabel.AddToClassList("psd-organizer-status");
            identity.Add(statusLabel);
            header.Add(identity);

            var actions = new VisualElement();
            actions.AddToClassList("psd-organizer-actions");
            actions.Add(CreateActionButton("重新分析", StartRefresh));
            actions.Add(CreateActionButton("重新整理", StartFullReplan));
            actions.Add(CreateActionButton("导入方案", ImportManualPlan));
            cancelButton = CreateActionButton("取消", CancelRunningRequest);
            cancelButton.AddToClassList("psd-organizer-button-muted");
            actions.Add(cancelButton);
            header.Add(actions);
            return header;
        }

        private VisualElement BuildDiagnostics()
        {
            diagnosticsContent = new VisualElement { name = "organizer-diagnostics" };
            diagnosticsContent.AddToClassList("psd-organizer-diagnostics");
            return diagnosticsContent;
        }

        private VisualElement BuildColumns()
        {
            var columns = new VisualElement();
            columns.AddToClassList("psd-organizer-columns");
            columns.Add(BuildDraftPane());
            columns.Add(BuildPreviewPane());
            columns.Add(BuildInspectorPane());
            return columns;
        }

        private VisualElement BuildDraftPane()
        {
            var pane = CreatePane("草稿层级", "draft-hierarchy-pane");
            draftTree = new TreeView();
            draftTree.name = "draft-hierarchy";
            draftTree.selectionType = SelectionType.Single;
            draftTree.fixedItemHeight = 24;
            draftTree.makeItem = MakeDraftTreeRow;
            draftTree.bindItem = BindDraftTreeRow;
            draftTree.selectionChanged += OnDraftTreeSelectionChanged;
            pane.Add(draftTree);
            return pane;
        }

        private VisualElement BuildPreviewPane()
        {
            var pane = CreatePane("PSD 预览与分析", "psd-preview-pane");
            previewContent = new VisualElement { name = "psd-preview" };
            previewContent.AddToClassList("psd-organizer-preview");
            pane.Add(previewContent);
            return pane;
        }

        private VisualElement BuildInspectorPane()
        {
            var pane = CreatePane("选中项属性", "selection-inspector-pane");
            inspectorContent = new ScrollView { name = "selection-inspector" };
            inspectorContent.AddToClassList("psd-organizer-inspector");
            pane.Add(inspectorContent);
            return pane;
        }

        private VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("psd-organizer-footer");
            draftSummaryLabel = new Label { name = "draft-summary" };
            draftSummaryLabel.AddToClassList("psd-organizer-footer-summary");
            footer.Add(draftSummaryLabel);
            Button discard = CreateActionButton("放弃草稿", DiscardDraft);
            discard.AddToClassList("psd-organizer-button-muted");
            footer.Add(discard);
            applyButton = CreateActionButton("应用已校验方案", ApplyValidatedPlan);
            applyButton.AddToClassList("psd-organizer-button-primary");
            footer.Add(applyButton);
            return footer;
        }

        private static VisualElement CreatePane(string title, string name)
        {
            var pane = new VisualElement { name = name };
            pane.AddToClassList("psd-organizer-pane");
            var heading = new Label(title);
            heading.AddToClassList("psd-organizer-pane-heading");
            pane.Add(heading);
            return pane;
        }

        private static Button CreateActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("psd-organizer-button");
            return button;
        }

        private void RefreshUi()
        {
            if (rootVisualElement == null || rootVisualElement.childCount == 0)
            {
                return;
            }

            RefreshStatus();
            RefreshDiagnostics();
            RefreshDraftTree();
            RefreshPreview();
            RefreshInspector();
            if (applyButton != null)
            {
                applyButton.SetEnabled(model != null && model.canApply && !model.isRunning);
            }
            if (cancelButton != null)
            {
                cancelButton.SetEnabled(model != null && model.isRunning);
            }
        }

        private void RefreshStatus()
        {
            if (statusLabel == null)
            {
                return;
            }
            if (model == null)
            {
                statusLabel.text = "没有预览上下文";
                return;
            }
            statusLabel.text = model.isRunning ? "AI 正在处理" :
                model.canApply ? "草稿已校验" : "草稿需要处理";
        }

        private void RefreshDiagnostics()
        {
            if (diagnosticsContent == null)
            {
                return;
            }
            diagnosticsContent.Clear();
            if (model == null)
            {
                diagnosticsContent.Add(new Label("请从已生成 Prefab 的 PSD 打开此工具。"));
                return;
            }
            diagnosticsContent.Add(new Label("目标 Prefab：" + model.targetPrefabPath));
            if (model.pendingMissingStableIds.Count > 0)
            {
                missingCleanupToggle = new Toggle("确认只在草稿中清理缺失的 PSD ID")
                {
                    value = confirmMissingCleanup
                };
                missingCleanupToggle.RegisterValueChangedCallback(change => confirmMissingCleanup = change.newValue);
                diagnosticsContent.Add(missingCleanupToggle);
            }
            foreach (string error in model.validationErrors)
            {
                var item = new Label(error);
                item.AddToClassList("psd-organizer-diagnostic-error");
                diagnosticsContent.Add(item);
            }
        }

        private void RefreshDraftTree()
        {
            if (draftTree == null)
            {
                return;
            }
            List<DraftTreeItem> previous = selectedDraftItem == null
                ? new List<DraftTreeItem>()
                : new List<DraftTreeItem> { selectedDraftItem };
            draftItems.Clear();
            draftItems.AddRange(BuildDraftTree(model));
            List<TreeViewItemData<DraftTreeItem>> roots = BuildTreeViewRoots(draftItems);
            draftTree.SetRootItems(roots);
            draftTree.Rebuild();
            if (previous.Count == 1)
            {
                DraftTreeItem retained = draftItems.FirstOrDefault(item => item.id == previous[0].id);
                if (retained != null)
                {
                    draftTree.SetSelectionById(retained.id);
                }
            }
            if (draftSummaryLabel != null)
            {
                int groupCount = model == null ? 0 : (model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>()).Count;
                draftSummaryLabel.text = "草稿：" + groupCount + " 个分组，" + draftItems.Count(item => !item.isGroup) + " 个图层";
            }
        }

        private static List<TreeViewItemData<DraftTreeItem>> BuildTreeViewRoots(IEnumerable<DraftTreeItem> items)
        {
            Dictionary<int, List<DraftTreeItem>> byParent = (items ?? Enumerable.Empty<DraftTreeItem>())
                .GroupBy(item => item.parentId)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.sortOrder).ToList());
            return BuildTreeViewChildren(byParent, 0);
        }

        private static List<TreeViewItemData<DraftTreeItem>> BuildTreeViewChildren(
            IReadOnlyDictionary<int, List<DraftTreeItem>> byParent,
            int parentId)
        {
            List<DraftTreeItem> children;
            if (!byParent.TryGetValue(parentId, out children))
            {
                return new List<TreeViewItemData<DraftTreeItem>>();
            }
            return children.Select(item => new TreeViewItemData<DraftTreeItem>(
                item.id,
                item,
                BuildTreeViewChildren(byParent, item.id))).ToList();
        }

        private VisualElement MakeDraftTreeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("psd-organizer-tree-row");
            row.Add(new Label { name = "kind" });
            var label = new Label { name = "name" };
            label.AddToClassList("psd-organizer-tree-name");
            row.Add(label);
            var detail = new Label { name = "detail" };
            detail.AddToClassList("psd-organizer-tree-detail");
            row.Add(detail);
            row.RegisterCallback<PointerDownEvent>(OnDraftRowPointerDown);
            row.RegisterCallback<PointerUpEvent>(OnDraftRowPointerUp);
            return row;
        }

        private void BindDraftTreeRow(VisualElement row, int index)
        {
            DraftTreeItem item = draftTree.GetItemDataForIndex<DraftTreeItem>(index);
            row.userData = item;
            row.Q<Label>("kind").text = item.isGroup ? "组" :
                string.Equals(item.kind, "Text", StringComparison.Ordinal) ? "文" : "层";
            row.Q<Label>("name").text = item.displayName;
            row.Q<Label>("detail").text = item.isGroup ? item.memberCount + " 个图层" : GetNodeKindDisplayName(item.kind);
            row.EnableInClassList("psd-organizer-tree-row-group", item.isGroup);
        }

        private void OnDraftRowPointerDown(PointerDownEvent evt)
        {
            VisualElement row = evt.currentTarget as VisualElement;
            dragSourceItem = row == null ? null : row.userData as DraftTreeItem;
        }

        private void OnDraftRowPointerUp(PointerUpEvent evt)
        {
            VisualElement row = evt.currentTarget as VisualElement;
            dragTargetItem = row == null ? null : row.userData as DraftTreeItem;
            if (dragSourceItem == null || dragTargetItem == null || dragSourceItem.id == dragTargetItem.id)
            {
                dragSourceItem = null;
                return;
            }
            TryMoveDraftItem(dragSourceItem, dragTargetItem);
            dragSourceItem = null;
            dragTargetItem = null;
        }

        private void OnDraftTreeSelectionChanged(IEnumerable<object> selection)
        {
            selectedDraftItem = selection == null ? null : selection.OfType<DraftTreeItem>().FirstOrDefault();
            RefreshInspector();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (previewContent == null)
            {
                return;
            }
            previewContent.Clear();
            if (model == null)
            {
                previewContent.Add(new Label("未加载 PSD。"));
                return;
            }
            previewContent.Add(new Label("合成预览"));
            if (compositePreviewTexture == null && !string.IsNullOrEmpty(sourcePsdPath))
            {
                TryBuildCompositePreview();
            }
            if (compositePreviewTexture != null)
            {
                var image = new Image { image = compositePreviewTexture, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("psd-organizer-composite-image");
                previewContent.Add(image);
            }
            previewContent.Add(new Label(selectedDraftItem == null
                ? "选择一个分组或图层以查看其规划归属。"
                : "当前选中：" + selectedDraftItem.displayName));
            previewContent.Add(new Label("分组数：" + (model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>()).Count));
            previewContent.Add(new Label("候选 Prefab：" + model.prefabCandidates.Count));
            var note = new Label("预览在 Unity 编辑器本地生成，不会通过 HTTP 发送。");
            note.AddToClassList("psd-organizer-muted");
            previewContent.Add(note);
        }

        private void TryBuildCompositePreview()
        {
            try
            {
                compositePreviewTexture = Editor.PsdHierarchyCompositePreviewWriter.BuildTexture(sourcePsdPath);
            }
            catch (Exception exception)
            {
                ShowDraftDiagnostic("无法生成合成预览：" + exception.Message);
            }
        }

        private void ReleaseCompositePreviewTexture()
        {
            if (compositePreviewTexture != null)
            {
                DestroyImmediate(compositePreviewTexture);
                compositePreviewTexture = null;
            }
        }

        private void RefreshInspector()
        {
            if (inspectorContent == null)
            {
                return;
            }
            inspectorContent.Clear();
            if (model == null || selectedDraftItem == null)
            {
                inspectorContent.Add(new Label("请选择一个分组或图层。"));
                return;
            }
            if (!selectedDraftItem.isGroup)
            {
                inspectorContent.Add(new Label("图层"));
                inspectorContent.Add(new Label(selectedDraftItem.displayName));
                inspectorContent.Add(new Label("稳定 ID：" + selectedDraftItem.stableId));
                Button group = CreateActionButton("创建单图层分组", () => TryCreateManualGroup(selectedDraftItem.stableId));
                inspectorContent.Add(group);
                inspectorContent.Add(CreateActionButton("在 Prefab 中定位图层", () => SelectPrefabMembers(new[] { selectedDraftItem.stableId })));
                return;
            }

            PsdHierarchyPlanGroup groupPlan = FindSelectedGroup();
            if (groupPlan == null)
            {
                inspectorContent.Add(new Label("所选分组已不在当前草稿中。"));
                return;
            }
            inspectorContent.Add(new Label(groupPlan.displayName));
            inspectorContent.Add(new Label("置信度：" + groupPlan.confidence.ToString("0.00")));
            inspectorContent.Add(new Label(groupPlan.evidence ?? string.Empty));
            inspectorContent.Add(new Label("成员"));
            foreach (string stableId in groupPlan.memberStableIds ?? new List<string>())
            {
                inspectorContent.Add(new Label(FindNodeName(stableId)));
            }
            bool accepted = model.acceptedGroupKeys.Contains(groupPlan.key);
            Button accept = CreateActionButton(accepted ? "已接受" : "接受分组", () => AcceptGroup(groupPlan.key));
            accept.SetEnabled(!accepted && !model.isRunning);
            inspectorContent.Add(accept);
            Button refine = CreateActionButton("使用 AI 精修", () => StartGroupRefinement(groupPlan.key));
            refine.SetEnabled(!accepted && !model.isRunning);
            inspectorContent.Add(refine);
            inspectorContent.Add(CreateActionButton("在 Prefab 中定位分组", () => SelectPrefabMembers(groupPlan.memberStableIds)));
        }

        internal static IReadOnlyList<DraftTreeItem> BuildDraftTreeForTests(
            PsdHierarchyOrganizerPreviewModel previewModel)
        {
            return BuildDraftTree(previewModel);
        }

        private static List<DraftTreeItem> BuildDraftTree(PsdHierarchyOrganizerPreviewModel previewModel)
        {
            var result = new List<DraftTreeItem>();
            if (previewModel == null)
            {
                return result;
            }

            PsdHierarchyPlan plan = previewModel.proposedPlan;
            List<PsdHierarchyPlanGroup> groups = (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null && !string.IsNullOrEmpty(group.key))
                .OrderBy(group => group.parentKey ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(group => group.displayName ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(group => group.key, StringComparer.Ordinal)
                .ToList();
            var groupIds = new Dictionary<string, int>(StringComparer.Ordinal);
            var usedIds = new HashSet<int>();
            foreach (PsdHierarchyPlanGroup group in groups)
            {
                int id = CreateTreeItemId("group:" + group.key, usedIds);
                groupIds.Add(group.key, id);
            }

            var memberOwner = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in groups)
            {
                foreach (string stableId in group.memberStableIds ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(stableId) && !memberOwner.ContainsKey(stableId))
                    {
                        memberOwner.Add(stableId, group.key);
                    }
                }
            }

            int sortOrder = 0;
            foreach (PsdHierarchyPlanGroup group in groups)
            {
                int parentId = 0;
                if (!string.IsNullOrEmpty(group.parentKey) && !groupIds.TryGetValue(group.parentKey, out parentId))
                {
                    parentId = 0;
                }
                result.Add(new DraftTreeItem(
                    groupIds[group.key],
                    parentId,
                    true,
                    group.displayName ?? group.key,
                    string.Empty,
                    group.key,
                    "Group",
                    (group.memberStableIds ?? new List<string>()).Count,
                    sortOrder++));
            }

            foreach (PsdHierarchyRequestNode node in previewModel.currentTreeNodes
                         .Where(node => node != null && !string.IsNullOrEmpty(node.stableId))
                         .OrderBy(node => node.siblingIndex)
                         .ThenBy(node => node.stableId, StringComparer.Ordinal))
            {
                string owner;
                int parentId = 0;
                if (memberOwner.TryGetValue(node.stableId, out owner))
                {
                    groupIds.TryGetValue(owner, out parentId);
                }
                result.Add(new DraftTreeItem(
                    CreateTreeItemId("layer:" + node.stableId, usedIds),
                    parentId,
                    false,
                    node.originalName ?? node.stableId,
                    node.stableId,
                    string.Empty,
                    node.kind ?? "Layer",
                    0,
                    sortOrder++));
            }
            return result;
        }

        private static int CreateTreeItemId(string value, ISet<int> usedIds)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty)
                {
                    hash = hash * 31 + character;
                }
                hash = Math.Abs(hash == int.MinValue ? int.MaxValue : hash);
                if (hash == 0)
                {
                    hash = 1;
                }
                while (!usedIds.Add(hash))
                {
                    hash = hash == int.MaxValue ? 1 : hash + 1;
                }
                return hash;
            }
        }

        private void TryMoveDraftItem(DraftTreeItem source, DraftTreeItem target)
        {
            if (model == null)
            {
                return;
            }
            try
            {
                if (!source.isGroup && target.isGroup)
                {
                    model.MoveNodeIntoGroup(source.stableId, string.Empty, target.groupKey);
                }
                else if (source.isGroup && target.isGroup)
                {
                    model.MoveGroupIntoGroup(source.groupKey, target.groupKey);
                }
                else if (!source.isGroup && !target.isGroup)
                {
                    model.MoveNodeIntoGroup(source.stableId, target.stableId, string.Empty);
                }
                else
                {
                    ShowDraftDiagnostic("分组只能拖放到另一个分组上。");
                    return;
                }
                selectedDraftItem = source;
                RefreshUi();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                ShowDraftDiagnostic(exception.Message);
            }
        }

        internal void MoveDraftLayerForTests(string stableId, string targetGroupKey)
        {
            if (model == null)
            {
                throw new InvalidOperationException("未加载层级预览模型。");
            }
            model.MoveNodeIntoGroup(stableId, string.Empty, targetGroupKey);
            RefreshUi();
        }

        private void TryCreateManualGroup(string stableId)
        {
            try
            {
                model.CreateGroupForNode(stableId);
                RefreshUi();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                ShowDraftDiagnostic(exception.Message);
            }
        }

        private void AcceptGroup(string groupKey)
        {
            try
            {
                model.AcceptGroup(groupKey);
                RefreshUi();
            }
            catch (ArgumentException exception)
            {
                ShowDraftDiagnostic(exception.Message);
            }
        }

        private void DiscardDraft()
        {
            if (model == null || model.isRunning)
            {
                return;
            }
            model.ResetDraft();
            selectedDraftItem = null;
            RefreshUi();
        }

        private void ApplyValidatedPlan()
        {
            if (model == null)
            {
                return;
            }
            PsdHierarchyPlan plan;
            string error;
            if (model.TryCreateValidatedApplyPlan(out plan, out error))
            {
                DispatchApply(plan);
                return;
            }
            ShowDraftDiagnostic(error);
        }

        private PsdHierarchyPlanGroup FindSelectedGroup()
        {
            if (model == null || selectedDraftItem == null || !selectedDraftItem.isGroup)
            {
                return null;
            }
            return (model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>()).FirstOrDefault(
                group => group != null && string.Equals(group.key, selectedDraftItem.groupKey, StringComparison.Ordinal));
        }

        private string FindNodeName(string stableId)
        {
            PsdHierarchyRequestNode node = model.currentTreeNodes.FirstOrDefault(
                value => value != null && string.Equals(value.stableId, stableId, StringComparison.Ordinal));
            return node == null ? stableId : node.originalName ?? stableId;
        }

        private static string GetNodeKindDisplayName(string kind)
        {
            switch (kind)
            {
                case "Text": return "文字";
                case "Layer": return "图层";
                case "Image": return "图片";
                case "Button": return "按钮";
                case "Group": return "分组";
                default: return string.IsNullOrEmpty(kind) ? "图层" : kind;
            }
        }

        private void ShowDraftDiagnostic(string message)
        {
            if (diagnosticsContent == null || string.IsNullOrEmpty(message))
            {
                return;
            }
            var label = new Label(message);
            label.AddToClassList("psd-organizer-diagnostic-error");
            diagnosticsContent.Add(label);
        }

        private async void StartRefresh()
        {
            CancelRunningRequest();
            cancellation = new CancellationTokenSource();
            try
            {
                await model.RefreshAsync(confirmMissingCleanup, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // User cancellation/closing is an expected non-mutating outcome.
            }
            finally
            {
                cancellation?.Dispose();
                cancellation = null;
                RefreshUi();
            }
        }

        private void ImportManualPlan()
        {
            string path = EditorUtility.OpenFilePanel("导入 PSD 层级方案", string.Empty, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                PsdHierarchyPlan plan = PsdHierarchyManualPlanLoader.Load(path);
                model.ImportManualPlan(Newtonsoft.Json.JsonConvert.SerializeObject(plan));
                selectedDraftItem = null;
                RefreshUi();
            }
            catch (Exception exception)
            {
                ShowDraftDiagnostic(exception.Message);
            }
        }

        private void CancelRunningRequest()
        {
            if (cancellation != null && !cancellation.IsCancellationRequested)
            {
                cancellation.Cancel();
            }
        }

        private void DrawCurrentTree()
        {
            EditorGUILayout.LabelField("CURRENT PREFAB", EditorStyles.boldLabel);
            List<PsdHierarchyRequestNode> nodes = model.currentTreeNodes.ToList();
            var childrenByParent = nodes.GroupBy(node => node.parentStableId ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.OrderBy(node => node.siblingIndex).ToList(), StringComparer.Ordinal);
            DrawCurrentTreeChildren(childrenByParent, string.Empty, 0);
        }

        private void DrawProposedTree()
        {
            EditorGUILayout.LabelField("PROPOSED STRUCTURE", EditorStyles.boldLabel);
            PsdHierarchyPlan plan = model.proposedPlan;
            foreach (PsdHierarchyPlanGroup group in (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                         .Where(group => string.IsNullOrEmpty(group.parentKey)).OrderBy(group => group.key, StringComparer.Ordinal))
                DrawProposedGroup(group, plan.groups ?? new List<PsdHierarchyPlanGroup>(), 0);

            if ((plan.renames ?? new List<PsdHierarchyPlanRename>()).Count > 0)
            {
                GUILayout.Space(6f);
                EditorGUILayout.LabelField("Renames", EditorStyles.miniBoldLabel);
                foreach (PsdHierarchyPlanRename rename in plan.renames)
                    EditorGUILayout.LabelField(rename.stableId + "  →  " + rename.name, EditorStyles.miniLabel);
            }
        }

        private async void StartFullReplan()
        {
            CancelRunningRequest();
            cancellation = new CancellationTokenSource();
            try
            {
                await model.ReplanAllUnlockedAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // User cancellation/closing is an expected non-mutating outcome.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                cancellation?.Dispose();
                cancellation = null;
                RefreshUi();
            }
        }

        private void DrawHierarchyPanes(float top, float bottom)
        {
            Rect area = new Rect(4f, top, Mathf.Max(0f, position.width - 8f), Mathf.Max(120f, bottom - top));
            float leftWidth = Mathf.Clamp(area.width * 0.28f, 220f, 320f);
            Rect left = new Rect(area.x, area.y, leftWidth, area.height);
            Rect center = new Rect(left.xMax + 4f, area.y, area.width * 0.40f - 4f, area.height);
            Rect right = new Rect(center.xMax + 4f, area.y, area.xMax - center.xMax - 4f, area.height);
            GUI.Box(left, GUIContent.none, EditorStyles.helpBox);
            GUI.Box(center, GUIContent.none, EditorStyles.helpBox);
            GUI.Box(right, GUIContent.none, EditorStyles.helpBox);

            GUILayout.BeginArea(new Rect(left.x + 6f, left.y + 6f, left.width - 12f, left.height - 12f));
            currentTreeScroll = EditorGUILayout.BeginScrollView(currentTreeScroll);
            DrawCurrentTree();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(center.x + 6f, center.y + 6f, center.width - 12f, center.height - 12f));
            proposedTreeScroll = EditorGUILayout.BeginScrollView(proposedTreeScroll);
            DrawProposedTree();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(right.x + 6f, right.y + 6f, right.width - 12f, right.height - 12f));
            DrawSelectedGroupInspector();
            GUILayout.EndArea();
        }

        private void DrawCurrentTreeChildren(Dictionary<string, List<PsdHierarchyRequestNode>> childrenByParent, string parentId, int depth)
        {
            List<PsdHierarchyRequestNode> children;
            if (!childrenByParent.TryGetValue(parentId, out children)) return;
            foreach (PsdHierarchyRequestNode node in children)
            {
                List<PsdHierarchyRequestNode> grandChildren;
                bool hasChildren = childrenByParent.TryGetValue(node.stableId, out grandChildren) && grandChildren.Count > 0;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(depth * 14f);
                    if (hasChildren)
                        currentTreeFoldouts[node.stableId] = EditorGUILayout.Foldout(GetFoldout(currentTreeFoldouts, node.stableId), "", true);
                    else GUILayout.Space(14f);
                    GUIContent content = new GUIContent((node.kind == "Text" ? "T  " : "◆  ") + node.originalName, node.stableId + "  |  " + node.kind);
                    GUILayout.Label(content, EditorStyles.label);
                }
                if (hasChildren && GetFoldout(currentTreeFoldouts, node.stableId)) DrawCurrentTreeChildren(childrenByParent, node.stableId, depth + 1);
            }
        }

        private void DrawProposedGroup(PsdHierarchyPlanGroup group, List<PsdHierarchyPlanGroup> allGroups, int depth)
        {
            List<PsdHierarchyPlanGroup> children = allGroups.Where(value => value != null && value.parentKey == group.key).OrderBy(value => value.key, StringComparer.Ordinal).ToList();
            bool expanded = GetFoldout(proposedTreeFoldouts, group.key);
            Rect row = EditorGUILayout.GetControlRect(false, 20f);
            row.x += depth * 14f;
            row.width -= depth * 14f;
            if (selectedGroupKey == group.key) EditorGUI.DrawRect(row, new Color(0.20f, 0.42f, 0.62f, 0.45f));
            else EditorGUI.DrawRect(row, new Color(0.16f, 0.28f, 0.38f, 0.22f));
            Rect foldout = new Rect(row.x + 3f, row.y + 2f, 16f, row.height);
            proposedTreeFoldouts[group.key] = EditorGUI.Foldout(foldout, expanded, GUIContent.none, true);
            Rect pingAll = new Rect(row.xMax - 72f, row.y + 1f, 68f, row.height - 2f);
            if (GUI.Button(new Rect(row.x + 20f, row.y, row.width - 96f, row.height), "▣  " + group.displayName + "  ·  " + (group.memberStableIds ?? new List<string>()).Count + " 个图层", EditorStyles.label))
                selectedGroupKey = group.key;
            if (GUI.Button(pingAll, "Ping 全部", EditorStyles.miniButton))
                SelectPrefabMembers(group.memberStableIds);
            if (!GetFoldout(proposedTreeFoldouts, group.key)) return;
            foreach (PsdHierarchyPlanGroup child in children) DrawProposedGroup(child, allGroups, depth + 1);
        }

        private void DrawSelectedGroupInspector()
        {
            if (string.IsNullOrEmpty(selectedGroupKey)) return;
            PsdHierarchyPlanGroup group = (model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>())
                .FirstOrDefault(value => value != null && value.key == selectedGroupKey);
            if (group == null) return;
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("GROUP DETAILS", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Confidence  " + group.confidence.ToString("0.00"), EditorStyles.miniLabel);
            EditorGUILayout.LabelField(group.evidence ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            List<PsdPrefabCandidate> candidates = model.prefabCandidates
                .Where(candidate => (group.memberStableIds ?? new List<string>()).Contains(candidate.rootStableId)).ToList();
            foreach (PsdPrefabCandidate candidate in candidates)
                EditorGUILayout.LabelField("Prefab 候选  " + candidate.score.ToString("0.00") + "  ·  " + string.Join("、", candidate.evidence.ToArray()), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("成员", EditorStyles.miniBoldLabel);
            foreach (string member in group.memberStableIds ?? new List<string>())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(CreateMemberContent(member), EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(46f))) SelectPrefabMembers(new[] { member });
                }
            }
            bool accepted = model.acceptedGroupKeys.Contains(group.key);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !accepted && !model.isRunning;
                if (GUILayout.Button("接受分组", EditorStyles.miniButton))
                    model.AcceptGroup(group.key);
                GUI.enabled = !accepted && !model.isRunning;
                if (GUILayout.Button("二次 AI 修整", EditorStyles.miniButton))
                    StartGroupRefinement(group.key);
                GUI.enabled = true;
            }
            if (accepted) EditorGUILayout.LabelField("已接受：二次 AI 不会再修改此分组。", EditorStyles.miniLabel);
        }

        private async void StartGroupRefinement(string groupKey)
        {
            CancelRunningRequest();
            cancellation = new CancellationTokenSource();
            try { await model.RefineGroupAsync(groupKey, cancellation.Token); }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                cancellation?.Dispose();
                cancellation = null;
                RefreshUi();
            }
        }

        private GUIContent CreateMemberContent(string stableId)
        {
            PsdHierarchyRequestNode node = model.currentTreeNodes
                .FirstOrDefault(value => value != null && string.Equals(value.stableId, stableId, StringComparison.Ordinal));
            if (node == null) return new GUIContent("◆  <无法解析的图层>", "PSD layer ID: " + stableId);
            string icon = string.Equals(node.kind, "Text", StringComparison.Ordinal) ? "T" : "◆";
            string tooltip = "点击后在目标 Prefab 中定位\nPSD layer ID: " + stableId;
            return new GUIContent(icon + "  " + node.originalName, tooltip);
        }

        /// <summary>
        /// Uses the durable Profile local-file IDs, never layer names or hierarchy-path guesses,
        /// to select exactly the generated Prefab objects represented by the clicked proposal row.
        /// </summary>
        private void SelectPrefabMembers(IEnumerable<string> stableIds)
        {
            List<string> requestedIds = (stableIds ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToList();
            if (requestedIds.Count == 0) return;

            string profilePath = PsdPrefabTransactionalSave.GetProfilePath(model.targetPrefabPath, model.sourcePsdGuid);
            PsdHierarchyProfile profile = PsdPrefabTransactionalSave.ResolveBoundProfileForImport(profilePath, model.targetPrefabPath);
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(model.targetPrefabPath);
            if (profile == null || prefabRoot == null)
            {
                ShowNotification(new GUIContent("找不到可定位的 Prefab Profile。请先完成一次导入。"));
                return;
            }

            var pathByStableId = (profile.nodes ?? new List<PsdHierarchyProfileNode>())
                .Where(node => node != null && !string.IsNullOrEmpty(node.lastKnownPath))
                .GroupBy(node => node.stableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().lastKnownPath, StringComparer.Ordinal);
            var requestedPaths = new List<string>();
            foreach (string stableId in requestedIds)
            {
                string path;
                if (pathByStableId.TryGetValue(stableId, out path)) requestedPaths.Add(path);
            }
            if (requestedPaths.Count == 0)
            {
                ShowNotification(new GUIContent("这些图层尚未写入目标 Prefab。请先应用并重新导入。"));
                return;
            }

            AssetDatabase.OpenAsset(prefabRoot);
            EditorApplication.delayCall += () => SelectOpenedPrefabStageMembers(requestedPaths);
        }

        private void SelectOpenedPrefabStageMembers(List<string> requestedPaths)
        {
            UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || !string.Equals(stage.assetPath, model.targetPrefabPath, StringComparison.Ordinal))
            {
                ShowNotification(new GUIContent("Prefab 已打开，但尚未准备好定位。请再次点击 Ping。"));
                return;
            }

            Transform[] stageNodes = stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true);
            var stageObjects = new List<UnityEngine.Object>(stageNodes.Length);
            foreach (Transform node in stageNodes) stageObjects.Add(node.gameObject);
            IReadOnlyList<UnityEngine.Object> targets;
            try
            {
                targets = PsdHierarchyPrefabStageSelection.ResolveStageTargets(
                    requestedPaths,
                    stageNodes.Select(node => BuildHierarchyPath(node, stage.prefabContentsRoot.transform)).ToList(),
                    stageObjects,
                    StringComparer.Ordinal);
            }
            catch (InvalidOperationException exception)
            {
                ShowNotification(new GUIContent(exception.Message));
                return;
            }
            if (targets.Count == 0)
            {
                ShowNotification(new GUIContent("目标图层未出现在当前 Prefab Stage 中。"));
                return;
            }

            Selection.objects = targets.ToArray();
            EditorGUIUtility.PingObject(targets[0]);
        }

        private static string BuildHierarchyPath(Transform target, Transform root)
        {
            var names = new Stack<string>();
            for (Transform cursor = target; cursor != null; cursor = cursor.parent)
            {
                names.Push(cursor.name);
                if (cursor == root) break;
            }
            return string.Join("/", names.ToArray());
        }

        private static bool GetFoldout(Dictionary<string, bool> state, string key)
        {
            bool value;
            if (state.TryGetValue(key, out value)) return value;
            state[key] = true;
            return true;
        }

        internal sealed class DraftTreeItem
        {
            public DraftTreeItem(
                int id,
                int parentId,
                bool isGroup,
                string displayName,
                string stableId,
                string groupKey,
                string kind,
                int memberCount,
                int sortOrder)
            {
                this.id = id;
                this.parentId = parentId;
                this.isGroup = isGroup;
                this.displayName = displayName ?? string.Empty;
                this.stableId = stableId ?? string.Empty;
                this.groupKey = groupKey ?? string.Empty;
                this.kind = kind ?? string.Empty;
                this.memberCount = memberCount;
                this.sortOrder = sortOrder;
            }

            public int id { get; private set; }
            public int parentId { get; private set; }
            public bool isGroup { get; private set; }
            public string displayName { get; private set; }
            public string stableId { get; private set; }
            public string groupKey { get; private set; }
            public string kind { get; private set; }
            public int memberCount { get; private set; }
            public int sortOrder { get; private set; }
        }
    }
}
