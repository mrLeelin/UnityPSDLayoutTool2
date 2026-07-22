namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Validates a fully merged plan against the complete current request. The
    /// same primitive is reusable after focused replanning so partial output is
    /// never trusted without whole-document identity and topology checks.
    /// </summary>
    public static class PsdHierarchyPlanValidator
    {
        public static void Validate(PsdHierarchyPlan plan, PsdHierarchyRequest request)
        {
            ValidateInternal(plan, request, false);
        }

        /// <summary>
        /// Reuses a previous plan after geometry-only drift while running every
        /// identity, ownership, parent, protected-boundary, descendant closure,
        /// contiguity and render-order rule. Only the fingerprint gate differs
        /// from direct apply; no planner or model is invoked.
        /// </summary>
        public static void ValidateGeometryReuse(PsdHierarchyPlan plan, PsdHierarchyRequest request)
        {
            if (EvaluateFingerprints(plan, request) != PsdHierarchyPlanFingerprintStatus.RequiresGeometryValidation)
                Fail("Geometry reuse requires geometry-only fingerprint drift.");
            ValidateInternal(plan, request, true);
        }

        private static void ValidateInternal(PsdHierarchyPlan plan, PsdHierarchyRequest request, bool allowGeometryDrift)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (plan.schemaVersion != PsdHierarchyPlan.CurrentSchemaVersion)
            {
                Fail("Unsupported plan schema version.");
            }

            if (request.schemaVersion != PsdHierarchyRequest.CurrentSchemaVersion)
            {
                Fail("Unsupported request schema version.");
            }

            ValidatePlanContract(plan);
            ValidateRequestContract(request);

            if (!string.Equals(plan.sourcePsdGuid, request.sourcePsdGuid, StringComparison.Ordinal))
            {
                Fail("Plan source PSD GUID does not match the current PSD context.");
            }

            PsdHierarchyPlanFingerprintStatus fingerprintStatus = EvaluateFingerprints(plan, request);
            if (fingerprintStatus == PsdHierarchyPlanFingerprintStatus.RequiresReplan)
            {
                Fail("Plan structure fingerprint does not match the current PSD context and requires replanning.");
            }

            if (fingerprintStatus == PsdHierarchyPlanFingerprintStatus.RequiresGeometryValidation && !allowGeometryDrift)
            {
                Fail("Plan geometry fingerprint changed and requires geometry validation before apply.");
            }

            Dictionary<string, PsdHierarchyRequestNode> nodes = BuildNodeIndex(request.nodes);
            ApplyPrefabProtectionMetadata(nodes, request.currentPrefabHierarchy);
            List<PsdHierarchyPlanGroup> groups = plan.groups ?? new List<PsdHierarchyPlanGroup>();
            Dictionary<string, PsdHierarchyPlanGroup> groupsByKey = BuildGroupIndex(groups);
            ValidateGroupParents(groupsByKey);
            ValidateMembership(groups, nodes);
            ValidateDescendantClosures(groupsByKey, nodes);
            ValidateRenames(plan.renames ?? new List<PsdHierarchyPlanRename>(), nodes);
        }

        public static PsdHierarchyPlanFingerprintStatus EvaluateFingerprints(
            PsdHierarchyPlan plan,
            PsdHierarchyRequest request)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (string.IsNullOrEmpty(plan.structureFingerprint) ||
                string.IsNullOrEmpty(request.structureFingerprint) ||
                !string.Equals(plan.structureFingerprint, request.structureFingerprint, StringComparison.Ordinal))
            {
                return PsdHierarchyPlanFingerprintStatus.RequiresReplan;
            }

            if (string.IsNullOrEmpty(plan.geometryFingerprint) ||
                string.IsNullOrEmpty(request.geometryFingerprint) ||
                !string.Equals(plan.geometryFingerprint, request.geometryFingerprint, StringComparison.Ordinal))
            {
                return PsdHierarchyPlanFingerprintStatus.RequiresGeometryValidation;
            }

            // Content deliberately does not participate in hierarchy staleness.
            // A later import may replace text or pixels while preserving structure.
            return PsdHierarchyPlanFingerprintStatus.Valid;
        }

        /// <summary>
        /// Revalidates an in-memory request before serialization or use. This is
        /// intentionally independent of ContextBuilder because tests, merge code,
        /// or future callers can construct contract objects programmatically.
        /// </summary>
        public static void ValidateRequestContract(PsdHierarchyRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.schemaVersion != PsdHierarchyRequest.CurrentSchemaVersion)
            {
                Fail("Unsupported request schema version.");
            }

            RequireString(request.sourcePsdGuid, PsdHierarchyContractLimits.MaxSourceGuidLength, false, "Request source PSD GUID");
            RequireString(request.sourceFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, true, "Request source fingerprint");
            RequireString(request.contentFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, false, "Request content fingerprint");
            RequireString(request.structureFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, false, "Request structure fingerprint");
            RequireString(request.geometryFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, false, "Request geometry fingerprint");
            if (request.documentWidth < 0 || request.documentWidth > PsdHierarchyContractLimits.MaxDocumentDimension ||
                request.documentHeight < 0 || request.documentHeight > PsdHierarchyContractLimits.MaxDocumentDimension)
            {
                Fail("Request document dimensions are outside the allowed range.");
            }

            if (request.nodes == null || request.nodes.Count > PsdHierarchyContractLimits.MaxContextNodes)
            {
                Fail("Request nodes are missing or exceed the context node limit.");
            }

            if (request.currentPrefabHierarchy == null ||
                request.currentPrefabHierarchy.Count > PsdHierarchyContractLimits.MaxPrefabMetadataNodes)
            {
                Fail("Request Prefab metadata is missing or exceeds the node limit.");
            }

            if (request.previews == null || request.previews.Count > PsdHierarchyContractLimits.MaxPreviews)
            {
                Fail("Request previews are missing or exceed the preview limit.");
            }

            foreach (PsdHierarchyRequestNode node in request.nodes)
            {
                if (node == null)
                {
                    Fail("Request contains a null node.");
                }

                RequireString(node.stableId, PsdHierarchyContractLimits.MaxIdentifierLength, false, "Request node stable ID");
                RequireString(node.parentStableId, PsdHierarchyContractLimits.MaxIdentifierLength, true, "Request node parent ID");
                RequireString(node.originalName, PsdHierarchyContractLimits.MaxNameLength, true, "Request node name");
                RequireString(node.kind, PsdHierarchyContractLimits.MaxNameLength, false, "Request node kind");
                RequireString(node.protectedBoundaryStableId, PsdHierarchyContractLimits.MaxIdentifierLength, true,
                    "Request node protected boundary ID");
                if (node.siblingIndex < 0 || node.siblingIndex > PsdHierarchyContractLimits.MaxContextNodes)
                {
                    Fail("Request node sibling index is outside the allowed range.");
                }

                ValidateRectangle(node.rectangle, "Request node rectangle");
            }

            int totalComponentTypes = 0;
            foreach (PsdHierarchyPrefabNodeMetadata metadata in request.currentPrefabHierarchy)
            {
                if (metadata == null)
                {
                    Fail("Request contains null Prefab metadata.");
                }

                RequireString(metadata.stableId, PsdHierarchyContractLimits.MaxIdentifierLength, true, "Prefab stable ID");
                RequireString(metadata.parentStableId, PsdHierarchyContractLimits.MaxIdentifierLength, true, "Prefab parent ID");
                RequireString(metadata.hierarchyPath, PsdHierarchyContractLimits.MaxHierarchyPathLength, true, "Prefab hierarchy path");
                RequireString(metadata.protectedBoundaryStableId, PsdHierarchyContractLimits.MaxIdentifierLength, true,
                    "Prefab protected boundary ID");
                if (metadata.siblingIndex < 0 || metadata.siblingIndex > PsdHierarchyContractLimits.MaxContextNodes)
                {
                    Fail("Prefab metadata sibling index is outside the allowed range.");
                }

                if (metadata.componentTypes == null ||
                    metadata.componentTypes.Count > PsdHierarchyContractLimits.MaxComponentTypesPerNode)
                {
                    Fail("Request Prefab metadata component types are missing or exceed the per-node limit.");
                }

                totalComponentTypes += metadata.componentTypes.Count;
                if (totalComponentTypes > PsdHierarchyContractLimits.MaxTotalComponentTypes)
                {
                    Fail("Request exceeds the total component type limit.");
                }

                foreach (string componentType in metadata.componentTypes)
                {
                    RequireString(componentType, PsdHierarchyContractLimits.MaxNameLength, false, "Prefab component type");
                }
            }

            foreach (PsdHierarchyPreviewReference preview in request.previews)
            {
                if (preview == null)
                {
                    Fail("Request contains a null preview reference.");
                }

                RequireString(preview.key, PsdHierarchyContractLimits.MaxIdentifierLength, false, "Preview key");
                RequireString(preview.kind, PsdHierarchyContractLimits.MaxPreviewKindLength, false, "Preview kind");
                ValidateRectangle(preview.crop, "Preview crop");
            }
        }

        private static void ValidatePlanContract(PsdHierarchyPlan plan)
        {
            RequireString(plan.sourcePsdGuid, PsdHierarchyContractLimits.MaxSourceGuidLength, false, "Plan source PSD GUID");
            RequireString(plan.sourceFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, true, "Plan source fingerprint");
            RequireString(plan.contentFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, false, "Plan content fingerprint");
            RequireString(plan.structureFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, false, "Plan structure fingerprint");
            RequireString(plan.geometryFingerprint, PsdHierarchyContractLimits.MaxFingerprintLength, false, "Plan geometry fingerprint");
            if (plan.groups == null || plan.groups.Count > PsdHierarchyContractLimits.MaxGroups)
            {
                Fail("Plan groups are missing or exceed the group limit.");
            }

            if (plan.renames == null || plan.renames.Count > PsdHierarchyContractLimits.MaxRenames)
            {
                Fail("Plan renames are missing or exceed the rename limit.");
            }

            int totalMemberships = 0;
            foreach (PsdHierarchyPlanGroup group in plan.groups)
            {
                if (group == null)
                {
                    Fail("Plan contains a null group.");
                }

                RequireString(group.key, PsdHierarchyContractLimits.MaxIdentifierLength, false, "Plan group key");
                RequireString(group.parentKey, PsdHierarchyContractLimits.MaxIdentifierLength, true, "Plan group parent key");
                RequireString(group.displayName, PsdHierarchyContractLimits.MaxNameLength, true, "Plan group display name");
                RequireString(group.evidence, PsdHierarchyContractLimits.MaxEvidenceLength, true, "Plan group evidence");
                ValidateConfidence(group.confidence, "Plan group confidence");
                if (group.memberStableIds == null ||
                    group.memberStableIds.Count > PsdHierarchyContractLimits.MaxMembersPerGroup)
                {
                    Fail("Plan group members are missing or exceed the member limit.");
                }

                totalMemberships += group.memberStableIds.Count;
                if (totalMemberships > PsdHierarchyContractLimits.MaxTotalMemberships)
                {
                    Fail("Plan exceeds the total membership limit.");
                }

                foreach (string memberId in group.memberStableIds)
                {
                    RequireString(memberId, PsdHierarchyContractLimits.MaxIdentifierLength, false, "Plan member stable ID");
                }
            }

            foreach (PsdHierarchyPlanRename rename in plan.renames)
            {
                if (rename == null)
                {
                    Fail("Plan contains a null rename.");
                }

                RequireString(rename.stableId, PsdHierarchyContractLimits.MaxIdentifierLength, false, "Rename stable ID");
                RequireString(rename.name, PsdHierarchyContractLimits.MaxNameLength, false, "Rename name");
                RequireString(rename.evidence, PsdHierarchyContractLimits.MaxEvidenceLength, true, "Rename evidence");
                ValidateConfidence(rename.confidence, "Rename confidence");
            }
        }

        private static void RequireString(string value, int maximumLength, bool allowEmpty, string label)
        {
            if (value == null || (!allowEmpty && value.Length == 0) || value.Length > maximumLength)
            {
                Fail(label + " is missing or exceeds the length limit.");
            }
        }

        private static void ValidateConfidence(double value, string label)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d)
            {
                Fail(label + " must be finite and between 0 and 1.");
            }
        }

        private static void ValidateRectangle(PsdHierarchyRectangle value, string label)
        {
            ValidateCoordinate(value.x, label);
            ValidateCoordinate(value.y, label);
            ValidateCoordinate(value.width, label);
            ValidateCoordinate(value.height, label);
            if (value.width < 0f || value.height < 0f)
            {
                Fail(label + " cannot have negative size.");
            }
        }

        private static void ValidateCoordinate(float value, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) ||
                Math.Abs(value) > PsdHierarchyContractLimits.MaxCoordinateMagnitude)
            {
                Fail(label + " contains a non-finite or out-of-range value.");
            }
        }

        private static Dictionary<string, PsdHierarchyRequestNode> BuildNodeIndex(
            IEnumerable<PsdHierarchyRequestNode> source)
        {
            var result = new Dictionary<string, PsdHierarchyRequestNode>(StringComparer.Ordinal);
            foreach (PsdHierarchyRequestNode node in source ?? Enumerable.Empty<PsdHierarchyRequestNode>())
            {
                if (node == null || string.IsNullOrEmpty(node.stableId))
                {
                    Fail("Request contains a node without a stable ID.");
                }

                if (!result.TryAdd(node.stableId, CloneNode(node)))
                {
                    Fail("Request contains duplicate stable ID '" + node.stableId + "'.");
                }
            }

            return result;
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

        private static void ApplyPrefabProtectionMetadata(
            Dictionary<string, PsdHierarchyRequestNode> nodes,
            IEnumerable<PsdHierarchyPrefabNodeMetadata> source)
        {
            var metadataByStableId = new Dictionary<string, PsdHierarchyPrefabNodeMetadata>(StringComparer.Ordinal);
            int count = 0;
            foreach (PsdHierarchyPrefabNodeMetadata metadata in source ?? Enumerable.Empty<PsdHierarchyPrefabNodeMetadata>())
            {
                count++;
                if (count > PsdHierarchyContractLimits.MaxPrefabMetadataNodes)
                {
                    Fail("Request exceeds the Prefab metadata node limit.");
                }

                if (metadata == null || string.IsNullOrEmpty(metadata.stableId))
                {
                    continue;
                }

                if (!metadataByStableId.TryAdd(metadata.stableId, metadata))
                {
                    Fail("Request contains duplicate Prefab metadata stable ID '" + metadata.stableId + "'.");
                }
            }

            foreach (KeyValuePair<string, PsdHierarchyRequestNode> pair in nodes)
            {
                PsdHierarchyPrefabNodeMetadata metadata;
                if (!metadataByStableId.TryGetValue(pair.Key, out metadata))
                {
                    continue;
                }

                pair.Value.hasProjectComponents = metadata.hasProjectComponents;
                pair.Value.isProtectedBoundary = metadata.isProtectedBoundary;
                pair.Value.protectedBoundaryStableId = metadata.protectedBoundaryStableId ?? string.Empty;
            }
        }

        private static Dictionary<string, PsdHierarchyPlanGroup> BuildGroupIndex(
            IEnumerable<PsdHierarchyPlanGroup> groups)
        {
            var result = new Dictionary<string, PsdHierarchyPlanGroup>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in groups)
            {
                if (group == null || !PsdHierarchyProfile.IsValidGroupKey(group.key))
                {
                    Fail("Plan contains an invalid group key.");
                }

                if (!result.TryAdd(group.key, group))
                {
                    Fail("Plan contains duplicate group key '" + group.key + "'.");
                }
            }

            return result;
        }

        private static void ValidateGroupParents(Dictionary<string, PsdHierarchyPlanGroup> groups)
        {
            foreach (PsdHierarchyPlanGroup group in groups.Values)
            {
                if (!string.IsNullOrEmpty(group.parentKey) && !groups.ContainsKey(group.parentKey))
                {
                    Fail("Group '" + group.key + "' has unknown parent key '" + group.parentKey + "'.");
                }
            }

            var visitState = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string key in groups.Keys)
            {
                VisitGroup(key, groups, visitState);
            }
        }

        private static void VisitGroup(
            string key,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, int> visitState)
        {
            int state;
            if (visitState.TryGetValue(key, out state))
            {
                if (state == 1)
                {
                    Fail("Plan contains a group parent cycle at '" + key + "'.");
                }

                return;
            }

            visitState[key] = 1;
            string parentKey = groups[key].parentKey;
            if (!string.IsNullOrEmpty(parentKey))
            {
                VisitGroup(parentKey, groups, visitState);
            }

            visitState[key] = 2;
        }

        private static void ValidateMembership(
            IEnumerable<PsdHierarchyPlanGroup> groups,
            Dictionary<string, PsdHierarchyRequestNode> nodes)
        {
            var ownerByMember = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in groups)
            {
                List<string> members = group.memberStableIds ?? new List<string>();
                if (members.Count == 0)
                {
                    Fail("Group '" + group.key + "' has no members.");
                }

                var uniqueWithinGroup = new HashSet<string>(StringComparer.Ordinal);
                var memberNodes = new List<PsdHierarchyRequestNode>();
                foreach (string memberId in members)
                {
                    if (!uniqueWithinGroup.Add(memberId ?? string.Empty))
                    {
                        Fail("Group '" + group.key + "' contains duplicate member ID '" + memberId + "'.");
                    }

                    PsdHierarchyRequestNode node = null;
                    if (!PsdStableLayerIdUtility.IsPersistable(memberId) || !nodes.TryGetValue(memberId, out node))
                    {
                        Fail("Group '" + group.key + "' contains unknown or unstable member ID '" + memberId + "'.");
                    }

                    string previousOwner;
                    if (ownerByMember.TryGetValue(memberId, out previousOwner))
                    {
                        Fail("Member '" + memberId + "' has multiple group parents: '" + previousOwner + "' and '" + group.key + "'.");
                    }

                    ownerByMember.Add(memberId, group.key);
                    memberNodes.Add(node);
                }

                ValidateSameCurrentParent(group.key, memberNodes);
                ValidateProtectedBoundary(group.key, memberNodes);
                ValidateContiguousSiblings(group.key, memberNodes);
            }
        }

        private static void ValidateDescendantClosures(
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, PsdHierarchyRequestNode> nodes)
        {
            var childrenByParent = groups.Keys.ToDictionary(
                key => key,
                key => new List<string>(),
                StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in groups.Values)
            {
                if (!string.IsNullOrEmpty(group.parentKey))
                {
                    childrenByParent[group.parentKey].Add(group.key);
                }
            }

            var closureByGroup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string groupKey in groups.Keys)
            {
                HashSet<string> closure = BuildDescendantClosure(groupKey, groups, childrenByParent, closureByGroup);
                List<PsdHierarchyRequestNode> closureNodes = closure.Select(id => nodes[id]).ToList();
                ValidateSameCurrentParent(groupKey + " descendant closure", closureNodes);
                ValidateProtectedBoundary(groupKey + " descendant closure", closureNodes);
                ValidateContiguousSiblings(groupKey + " descendant closure", closureNodes);
            }
        }

        private static HashSet<string> BuildDescendantClosure(
            string groupKey,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, List<string>> childrenByParent,
            Dictionary<string, HashSet<string>> cache)
        {
            HashSet<string> cached;
            if (cache.TryGetValue(groupKey, out cached))
            {
                return cached;
            }

            // Cycle validation has already completed before this recursion.
            var result = new HashSet<string>(groups[groupKey].memberStableIds, StringComparer.Ordinal);
            foreach (string childKey in childrenByParent[groupKey])
            {
                result.UnionWith(BuildDescendantClosure(childKey, groups, childrenByParent, cache));
            }

            cache[groupKey] = result;
            return result;
        }

        private static void ValidateSameCurrentParent(string groupKey, IList<PsdHierarchyRequestNode> nodes)
        {
            string parentId = nodes[0].parentStableId ?? string.Empty;
            if (nodes.Any(node => !string.Equals(node.parentStableId ?? string.Empty, parentId, StringComparison.Ordinal)))
            {
                Fail("Group '" + groupKey + "' moves members from multiple current parents.");
            }
        }

        private static void ValidateProtectedBoundary(string groupKey, IList<PsdHierarchyRequestNode> nodes)
        {
            string boundaryId = nodes[0].protectedBoundaryStableId ?? string.Empty;
            if (nodes.Any(node => node.isProtectedBoundary || node.hasProjectComponents ||
                                  !string.Equals(node.protectedBoundaryStableId ?? string.Empty, boundaryId, StringComparison.Ordinal)))
            {
                Fail("Group '" + groupKey + "' crosses a protected Prefab boundary.");
            }
        }

        private static void ValidateContiguousSiblings(string groupKey, IList<PsdHierarchyRequestNode> nodes)
        {
            int[] indices = nodes.Select(node => node.siblingIndex).OrderBy(index => index).ToArray();
            for (int index = 1; index < indices.Length; index++)
            {
                if (indices[index] != indices[index - 1] + 1)
                {
                    Fail("Group '" + groupKey + "' contains a non-contiguous sibling move.");
                }
            }
        }

        private static void ValidateRenames(
            IEnumerable<PsdHierarchyPlanRename> renames,
            Dictionary<string, PsdHierarchyRequestNode> nodes)
        {
            var renamed = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanRename rename in renames)
            {
                if (rename == null || !PsdStableLayerIdUtility.IsPersistable(rename.stableId) ||
                    !nodes.ContainsKey(rename.stableId))
                {
                    Fail("Plan contains a rename for an unknown or unstable ID.");
                }

                if (!renamed.Add(rename.stableId))
                {
                    Fail("Plan contains duplicate renames for ID '" + rename.stableId + "'.");
                }

                if (string.IsNullOrWhiteSpace(rename.name))
                {
                    Fail("Plan contains an empty rename suggestion for ID '" + rename.stableId + "'.");
                }

                PsdHierarchyRequestNode node = nodes[rename.stableId];
                if (node.isProtectedBoundary || node.hasProjectComponents ||
                    !string.IsNullOrEmpty(node.protectedBoundaryStableId))
                {
                    Fail("Plan cannot rename protected or project-owned node '" + rename.stableId + "'.");
                }
            }
        }

        private static void Fail(string message)
        {
            throw new PsdHierarchyPlanValidationException(message);
        }
    }
}
