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
        public IList<PsdHierarchyRequestNode> currentTreeNodes
        {
            get { return fullRequest.nodes.Select(CloneNode).ToList(); }
        }
        public PsdHierarchyPlan proposedPlan
        {
            get { return ClonePlan(proposedPlanValue); }
        }
        public List<string> validationErrors { get; } = new List<string>();
        public List<string> pendingMissingStableIds { get; private set; }
        public bool canApply { get; private set; }
        public bool isRunning { get; private set; }

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
                foreach (HashSet<string> scope in scopes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    HashSet<string> contextIds = BuildContextIds(scope);
                    bool containsNewId = scope.Any(reconciliation.unsortedNewStableIds.Contains);
                    var joinableGroupKeys = new HashSet<string>(StringComparer.Ordinal);
                    if (containsNewId)
                    {
                        foreach (PsdHierarchyPlanGroup neighborGroup in working.groups.Where(group =>
                                     group != null && group.memberStableIds.Any(contextIds.Contains)))
                        {
                            joinableGroupKeys.Add(neighborGroup.key);
                            contextIds.UnionWith(neighborGroup.memberStableIds);
                        }
                    }
                    PsdHierarchyRequest scopedRequest = CloneScopedRequest(fullRequest, contextIds, scope);
                    List<PsdHierarchyPlanGroup> baselineGraph = BuildBaselineGroupGraph(working, contextIds);
                    var modifiableGroupKeys = new HashSet<string>(joinableGroupKeys, StringComparer.Ordinal);
                    modifiableGroupKeys.UnionWith(working.groups
                        .Where(group => group != null && group.memberStableIds.Any(scope.Contains))
                        .Select(group => group.key));
                    var runRequest = new PsdHierarchyAiRunRequest
                    {
                        operationId = Guid.NewGuid().ToString("N"),
                        request = scopedRequest,
                        targetPrefabPath = targetPrefabPath,
                        timeout = TimeSpan.FromMinutes(2),
                        modifiableStableIds = scope.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                        contextStableIds = contextIds.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                        baselineGroups = baselineGraph,
                        modifiableGroupKeys = modifiableGroupKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                        existingGroupKeys = working.groups.Where(group => group != null)
                            .Select(group => group.key).OrderBy(key => key, StringComparer.Ordinal).ToList()
                    };
                    PsdHierarchyAiRunResult result = await runner.RunAsync(runRequest, cancellationToken);
                    if (result == null || !result.succeeded || result.plan == null)
                    {
                        validationErrors.Add(result != null && !string.IsNullOrWhiteSpace(result.error)
                            ? result.error
                            : "Hierarchy planner returned no validated plan.");
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
                    validationErrors.Add("Missing PSD IDs are pending explicit cleanup confirmation.");
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
                    validationErrors.Add("Missing PSD IDs are pending explicit cleanup confirmation.");
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

        public bool TryCreateValidatedApplyPlan(out PsdHierarchyPlan plan, out string error)
        {
            plan = ClonePlan(proposedPlanValue);
            error = string.Empty;
            if (!canApply || isRunning)
            {
                error = "Hierarchy preview is not ready to apply.";
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
            while (unassigned.Count > 0)
            {
                string seed = unassigned.OrderBy(value => value, StringComparer.Ordinal).First();
                var scope = new HashSet<string>(StringComparer.Ordinal) { seed };

                // Existing groups are atomic decisions. If one member changes,
                // include that whole old group so unaffected groups remain exact.
                bool expanded;
                do
                {
                    expanded = false;
                    foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
                    {
                        if (group != null && group.memberStableIds.Any(scope.Contains))
                        {
                            int before = scope.Count;
                            scope.UnionWith(group.memberStableIds.Where(PsdStableLayerIdUtility.IsPersistable));
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

        private static List<PsdHierarchyPlanGroup> BuildBaselineGroupGraph(PsdHierarchyPlan plan, HashSet<string> scope)
        {
            var byKey = (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null)
                .ToDictionary(group => group.key, StringComparer.Ordinal);
            var keys = new HashSet<string>(byKey.Values
                .Where(group => group.memberStableIds.Any(scope.Contains))
                .Select(group => group.key), StringComparer.Ordinal);
            foreach (string initial in keys.ToArray())
            {
                string key = initial;
                while (byKey.ContainsKey(key) && !string.IsNullOrEmpty(byKey[key].parentKey))
                {
                    key = byKey[key].parentKey;
                    if (!keys.Add(key)) break;
                }
            }
            return keys.Where(byKey.ContainsKey).Select(key => CloneGroup(byKey[key])).ToList();
        }

        private static void MergeScope(
            PsdHierarchyPlan target,
            PsdHierarchyPlan partial,
            PsdHierarchyAiRunRequest runRequest)
        {
            var replaceAuthorizedKeys = new HashSet<string>(
                runRequest.modifiableGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            replaceAuthorizedKeys.IntersectWith((partial.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null).Select(group => group.key));
            target.groups.RemoveAll(group => group != null && replaceAuthorizedKeys.Contains(group.key));
            var scope = new HashSet<string>(
                runRequest.modifiableStableIds ?? new List<string>(), StringComparer.Ordinal);
            target.renames.RemoveAll(rename => rename != null && scope.Contains(rename.stableId));
            target.groups.AddRange((partial.groups ?? new List<PsdHierarchyPlanGroup>()).Select(CloneGroup));
            target.renames.AddRange((partial.renames ?? new List<PsdHierarchyPlanRename>()).Select(CloneRename));
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
                throw new PsdHierarchyPlanValidationException("Focused plan context is missing.");

            PsdHierarchyRequest request = runRequest.request;
            if (partial.schemaVersion != PsdHierarchyPlan.CurrentSchemaVersion ||
                !string.Equals(partial.sourcePsdGuid, request.sourcePsdGuid, StringComparison.Ordinal) ||
                !string.Equals(partial.sourceFingerprint, request.sourceFingerprint, StringComparison.Ordinal) ||
                !string.Equals(partial.contentFingerprint, request.contentFingerprint, StringComparison.Ordinal) ||
                !string.Equals(partial.structureFingerprint, request.structureFingerprint, StringComparison.Ordinal) ||
                !string.Equals(partial.geometryFingerprint, request.geometryFingerprint, StringComparison.Ordinal))
            {
                throw new PsdHierarchyPlanValidationException("Focused plan identity/fingerprints do not match its request.");
            }

            var allowedIds = new HashSet<string>(runRequest.modifiableStableIds ?? new List<string>(), StringComparer.Ordinal);
            var contextIds = new HashSet<string>((request.nodes ?? new List<PsdHierarchyRequestNode>())
                .Where(node => node != null).Select(node => node.stableId), StringComparer.Ordinal);
            var baselineByKey = (runRequest.baselineGroups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null).ToDictionary(group => group.key, StringComparer.Ordinal);
            var modifiableGroupKeys = new HashSet<string>(
                runRequest.modifiableGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            var existingGroupKeys = new HashSet<string>(
                runRequest.existingGroupKeys ?? new List<string>(), StringComparer.Ordinal);
            foreach (string groupKey in modifiableGroupKeys)
            {
                PsdHierarchyPlanGroup baselineGroup;
                if (!baselineByKey.TryGetValue(groupKey, out baselineGroup) ||
                    !(baselineGroup.memberStableIds ?? new List<string>()).Any(id =>
                        allowedIds.Contains(id) || contextIds.Contains(id)))
                {
                    throw new PsdHierarchyPlanValidationException(
                        "Focused request grants invalid group scope '" + groupKey + "'.");
                }
            }
            var partialKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (PsdHierarchyPlanGroup group in partial.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (group == null || !partialKeys.Add(group.key))
                    throw new PsdHierarchyPlanValidationException("Focused plan contains a null or duplicate group key.");
                if (baselineByKey.ContainsKey(group.key) && !modifiableGroupKeys.Contains(group.key))
                    throw new PsdHierarchyPlanValidationException("Focused plan modified group '" + group.key + "' outside its scope.");
                if (!baselineByKey.ContainsKey(group.key) && existingGroupKeys.Contains(group.key))
                    throw new PsdHierarchyPlanValidationException(
                        "Focused plan reused an existing group key outside its scope: '" + group.key + "'.");

                PsdHierarchyPlanGroup baselineGroup;
                if (baselineByKey.TryGetValue(group.key, out baselineGroup))
                {
                    List<string> baselineMembers = baselineGroup.memberStableIds ?? new List<string>();
                    List<string> readonlyBefore = baselineMembers.Where(id => !allowedIds.Contains(id)).ToList();
                    List<string> readonlyAfter = (group.memberStableIds ?? new List<string>())
                        .Where(id => !allowedIds.Contains(id)).ToList();
                    if (!readonlyBefore.SequenceEqual(readonlyAfter, StringComparer.Ordinal))
                        throw new PsdHierarchyPlanValidationException(
                            "Focused plan changed readonly membership/order in group '" + group.key + "'.");
                    if (readonlyBefore.Count > 0 &&
                        !string.Equals(group.parentKey ?? string.Empty, baselineGroup.parentKey ?? string.Empty, StringComparison.Ordinal))
                        throw new PsdHierarchyPlanValidationException(
                            "Focused plan moved readonly group '" + group.key + "'.");
                    if (readonlyBefore.Count > 0 &&
                        (!string.Equals(group.displayName ?? string.Empty, baselineGroup.displayName ?? string.Empty, StringComparison.Ordinal) ||
                         !string.Equals(group.evidence ?? string.Empty, baselineGroup.evidence ?? string.Empty, StringComparison.Ordinal) ||
                         group.confidence != baselineGroup.confidence))
                        throw new PsdHierarchyPlanValidationException(
                            "Focused plan changed readonly group metadata for '" + group.key + "'.");
                    if (readonlyBefore.Count > 0 &&
                        !(group.memberStableIds ?? new List<string>()).Any(allowedIds.Contains))
                        throw new PsdHierarchyPlanValidationException(
                            "Focused plan restated readonly group '" + group.key + "' without adding a modifiable ID.");
                }

                foreach (string member in group.memberStableIds ?? new List<string>())
                {
                    bool baselineReadonlyMember = baselineGroup != null &&
                        (baselineGroup.memberStableIds ?? new List<string>()).Contains(member);
                    if ((!allowedIds.Contains(member) && !baselineReadonlyMember) || !contextIds.Contains(member))
                        throw new PsdHierarchyPlanValidationException(
                            "Focused plan touched ID '" + member + "' outside its scope.");
                }
            }

            foreach (PsdHierarchyPlanGroup group in partial.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (!string.IsNullOrEmpty(group.parentKey) &&
                    !partialKeys.Contains(group.parentKey) &&
                    !baselineByKey.ContainsKey(group.parentKey))
                {
                    throw new PsdHierarchyPlanValidationException(
                        "Focused group '" + group.key + "' references unknown ancestor '" + group.parentKey + "'.");
                }
            }

            var renamed = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanRename rename in partial.renames ?? new List<PsdHierarchyPlanRename>())
            {
                if (rename == null || !renamed.Add(rename.stableId) || !allowedIds.Contains(rename.stableId) || !contextIds.Contains(rename.stableId))
                    throw new PsdHierarchyPlanValidationException("Focused rename touched an ID outside its scope.");
            }
        }
    }

    /// <summary>Bounded manual import path shared by the preview and tests.</summary>
    public static class PsdHierarchyManualPlanLoader
    {
        public static PsdHierarchyPlan Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Hierarchy plan file was not found.", path);
            if (new FileInfo(path).Length > PsdHierarchyContractLimits.MaxJsonUtf8Bytes)
                throw new PsdHierarchyPlanFormatException("Hierarchy plan exceeds the UTF-8 byte limit.");
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
        private PsdHierarchyOrganizerPreviewModel model;
        private CancellationTokenSource cancellation;
        private Vector2 scroll;
        private bool confirmMissingCleanup;

        public event Action<PsdHierarchyPlan> applyRequested;

        public static PsdHierarchyOrganizerWindow Open(PsdHierarchyOrganizerPreviewModel previewModel)
        {
            var window = GetWindow<PsdHierarchyOrganizerWindow>(true, "PSD Hierarchy Preview", true);
            window.model = previewModel ?? throw new ArgumentNullException("previewModel");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
            return window;
        }

        private void OnDisable()
        {
            CancelRunningRequest();
        }

        private void OnGUI()
        {
            if (model == null)
            {
                EditorGUILayout.HelpBox("No PSD hierarchy preview context is loaded.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Target Prefab (exact configured path)", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(model.targetPrefabPath, EditorStyles.textField, GUILayout.Height(20f));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !model.isRunning;
                if (GUILayout.Button("Generate / Retry Preview"))
                {
                    StartRefresh();
                }
                if (GUILayout.Button("Import Manual Plan"))
                {
                    ImportManualPlan();
                }
                GUI.enabled = model.isRunning;
                if (GUILayout.Button("Cancel"))
                {
                    CancelRunningRequest();
                }
                GUI.enabled = true;
            }

            if (model.pendingMissingStableIds.Count > 0)
            {
                confirmMissingCleanup = EditorGUILayout.ToggleLeft(
                    "Confirm cleanup of missing PSD IDs in the proposed plan only",
                    confirmMissingCleanup);
            }

            foreach (string error in model.validationErrors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawCurrentTree();
            GUILayout.Space(8f);
            DrawProposedTree();
            EditorGUILayout.EndScrollView();

            GUI.enabled = model.canApply && !model.isRunning;
            if (GUILayout.Button("Apply Validated Plan"))
            {
                PsdHierarchyPlan freshPlan;
                string error;
                if (model.TryCreateValidatedApplyPlan(out freshPlan, out error))
                {
                    applyRequested?.Invoke(freshPlan);
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError(error);
                }
            }
            GUI.enabled = true;
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
                Repaint();
            }
        }

        private void ImportManualPlan()
        {
            string path = EditorUtility.OpenFilePanel("Import PSD hierarchy plan", string.Empty, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                PsdHierarchyPlan plan = PsdHierarchyManualPlanLoader.Load(path);
                model.ImportManualPlan(Newtonsoft.Json.JsonConvert.SerializeObject(plan));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
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
            EditorGUILayout.LabelField("Current tree", EditorStyles.boldLabel);
            foreach (PsdHierarchyRequestNode node in model.currentTreeNodes
                         .OrderBy(node => node.parentStableId, StringComparer.Ordinal)
                         .ThenBy(node => node.siblingIndex))
            {
                string parent = string.IsNullOrEmpty(node.parentStableId) ? "root" : node.parentStableId;
                EditorGUILayout.LabelField(
                    node.originalName + "  [id=" + node.stableId + ", parent=" + parent + ", index=" + node.siblingIndex + "]");
            }
        }

        private void DrawProposedTree()
        {
            EditorGUILayout.LabelField("Proposed tree / evidence / confidence", EditorStyles.boldLabel);
            foreach (PsdHierarchyPlanGroup group in model.proposedPlan.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                EditorGUILayout.LabelField(group.displayName + "  [" + group.key + "]");
                EditorGUILayout.LabelField("  members: " + string.Join(", ", group.memberStableIds.ToArray()));
                EditorGUILayout.LabelField("  confidence: " + group.confidence.ToString("0.00") + "  evidence: " + group.evidence,
                    EditorStyles.wordWrappedMiniLabel);
            }
            foreach (PsdHierarchyPlanRename rename in model.proposedPlan.renames ?? new List<PsdHierarchyPlanRename>())
            {
                EditorGUILayout.LabelField(rename.stableId + " -> " + rename.name);
                EditorGUILayout.LabelField("  confidence: " + rename.confidence.ToString("0.00") + "  evidence: " + rename.evidence,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
