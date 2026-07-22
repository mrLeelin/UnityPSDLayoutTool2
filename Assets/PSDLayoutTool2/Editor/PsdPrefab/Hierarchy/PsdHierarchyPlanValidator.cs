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

            ValidateStructuredQuotas(plan, request);

            PsdHierarchyPlanFingerprintStatus fingerprintStatus = EvaluateFingerprints(plan, request);
            if (fingerprintStatus == PsdHierarchyPlanFingerprintStatus.RequiresReplan)
            {
                Fail("Plan structure fingerprint does not match the current PSD context and requires replanning.");
            }

            if (fingerprintStatus == PsdHierarchyPlanFingerprintStatus.RequiresGeometryValidation)
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

        private static void ValidateStructuredQuotas(PsdHierarchyPlan plan, PsdHierarchyRequest request)
        {
            List<PsdHierarchyPlanGroup> groups = plan.groups ?? new List<PsdHierarchyPlanGroup>();
            List<PsdHierarchyPlanRename> renames = plan.renames ?? new List<PsdHierarchyPlanRename>();
            List<PsdHierarchyRequestNode> nodes = request.nodes ?? new List<PsdHierarchyRequestNode>();
            List<PsdHierarchyPrefabNodeMetadata> prefab = request.currentPrefabHierarchy ??
                                                           new List<PsdHierarchyPrefabNodeMetadata>();
            List<PsdHierarchyPreviewReference> previews = request.previews ?? new List<PsdHierarchyPreviewReference>();

            if (groups.Count > PsdHierarchyContractLimits.MaxGroups)
            {
                Fail("Plan exceeds the group limit.");
            }

            if (renames.Count > PsdHierarchyContractLimits.MaxRenames)
            {
                Fail("Plan exceeds the rename limit.");
            }

            if (nodes.Count > PsdHierarchyContractLimits.MaxContextNodes)
            {
                Fail("Request exceeds the context node limit.");
            }

            if (prefab.Count > PsdHierarchyContractLimits.MaxPrefabMetadataNodes)
            {
                Fail("Request exceeds the Prefab metadata node limit.");
            }

            if (previews.Count > PsdHierarchyContractLimits.MaxPreviews)
            {
                Fail("Request exceeds the preview limit.");
            }

            int totalMemberships = 0;
            foreach (PsdHierarchyPlanGroup group in groups)
            {
                int memberCount = group == null || group.memberStableIds == null ? 0 : group.memberStableIds.Count;
                if (memberCount > PsdHierarchyContractLimits.MaxMembersPerGroup)
                {
                    Fail("Plan group exceeds the member limit.");
                }

                totalMemberships += memberCount;
                if (totalMemberships > PsdHierarchyContractLimits.MaxTotalMemberships)
                {
                    Fail("Plan exceeds the total membership limit.");
                }
            }

            foreach (PsdHierarchyPrefabNodeMetadata metadata in prefab)
            {
                if (metadata != null && metadata.componentTypes != null &&
                    metadata.componentTypes.Count > PsdHierarchyContractLimits.MaxComponentTypesPerNode)
                {
                    Fail("Request Prefab metadata exceeds the component type limit.");
                }

                if (metadata != null && (metadata.hierarchyPath ?? string.Empty).Length >
                    PsdHierarchyContractLimits.MaxHierarchyPathLength)
                {
                    Fail("Request Prefab hierarchy path exceeds the length limit.");
                }
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
            }
        }

        private static void Fail(string message)
        {
            throw new PsdHierarchyPlanValidationException(message);
        }
    }
}
