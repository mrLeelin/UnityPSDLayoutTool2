namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal static class PsdHierarchyWebSnapshotBuilder
    {
        public static PsdHierarchyWebSnapshotDto Build(
            PsdHierarchyOrganizerPreviewModel previewModel)
        {
            if (previewModel == null)
            {
                throw new ArgumentNullException("previewModel");
            }

            PsdHierarchyRequest request = previewModel.requestSnapshot;
            PsdHierarchyPlan plan = previewModel.proposedPlan;
            var snapshot = new PsdHierarchyWebSnapshotDto
            {
                canvas = new PsdHierarchyWebBoundsDto
                {
                    width = request.documentWidth,
                    height = request.documentHeight
                }
            };

            Dictionary<string, PsdHierarchyRequestNode> nodesById =
                (request.nodes ?? new List<PsdHierarchyRequestNode>())
                    .Where(node => node != null)
                    .ToDictionary(node => node.stableId ?? string.Empty, StringComparer.Ordinal);
            Dictionary<string, string> proposedNames = BuildProposedNames(plan);
            Dictionary<string, string> proposedGroups = BuildProposedGroups(plan);
            HashSet<string> acceptedGroupKeys = new HashSet<string>(
                previewModel.acceptedGroupKeys ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            HashSet<string> lockedGroupKeys = ExpandAcceptedSubtrees(plan, acceptedGroupKeys);

            foreach (PsdHierarchyRequestNode source in request.nodes ?? new List<PsdHierarchyRequestNode>())
            {
                if (source == null)
                {
                    continue;
                }

                string stableId = source.stableId ?? string.Empty;
                string proposedGroupKey;
                proposedGroups.TryGetValue(stableId, out proposedGroupKey);
                string proposedName;
                proposedNames.TryGetValue(stableId, out proposedName);
                bool accepted = !string.IsNullOrEmpty(proposedGroupKey) &&
                                lockedGroupKeys.Contains(proposedGroupKey);
                bool warning = source.isProtectedBoundary || source.hasProjectComponents;

                snapshot.nodes.Add(new PsdHierarchyWebNodeDto
                {
                    stableId = stableId,
                    parentStableId = source.parentStableId ?? string.Empty,
                    name = source.originalName ?? string.Empty,
                    proposedName = string.IsNullOrEmpty(proposedName)
                        ? source.originalName ?? string.Empty
                        : proposedName,
                    kind = source.kind ?? string.Empty,
                    bounds = ClipToCanvas(
                        source.rectangle, request.documentWidth, request.documentHeight),
                    sourceGroupKey = source.parentStableId ?? string.Empty,
                    proposedGroupKey = proposedGroupKey ?? string.Empty,
                    isAccepted = accepted,
                    isLocked = accepted || warning,
                    hasWarning = warning
                });
            }

            Dictionary<string, PsdHierarchyWebNodeDto> webNodesById = snapshot.nodes
                .ToDictionary(node => node.stableId, StringComparer.Ordinal);
            BuildGroups(
                snapshot,
                plan,
                nodesById,
                webNodesById,
                acceptedGroupKeys,
                lockedGroupKeys);
            BuildWarnings(snapshot, request, previewModel);
            BuildPrefabCandidates(snapshot, previewModel);
            return snapshot;
        }

        private static Dictionary<string, string> BuildProposedNames(PsdHierarchyPlan plan)
        {
            return (plan.renames ?? new List<PsdHierarchyPlanRename>())
                .Where(rename => rename != null && !string.IsNullOrEmpty(rename.stableId))
                .GroupBy(rename => rename.stableId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().name ?? string.Empty,
                    StringComparer.Ordinal);
        }

        private static Dictionary<string, string> BuildProposedGroups(PsdHierarchyPlan plan)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (group == null)
                {
                    continue;
                }

                foreach (string stableId in group.memberStableIds ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(stableId))
                    {
                        result[stableId] = group.key ?? string.Empty;
                    }
                }
            }
            return result;
        }

        private static HashSet<string> ExpandAcceptedSubtrees(
            PsdHierarchyPlan plan,
            HashSet<string> acceptedGroupKeys)
        {
            var result = new HashSet<string>(acceptedGroupKeys, StringComparer.Ordinal);
            PsdHierarchyPlanGroup[] groups = (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                .Where(group => group != null)
                .ToArray();
            bool changed;
            do
            {
                changed = false;
                foreach (PsdHierarchyPlanGroup group in groups)
                {
                    if (result.Contains(group.parentKey ?? string.Empty))
                    {
                        changed |= result.Add(group.key ?? string.Empty);
                    }
                }
            }
            while (changed);
            return result;
        }

        private static void BuildGroups(
            PsdHierarchyWebSnapshotDto snapshot,
            PsdHierarchyPlan plan,
            Dictionary<string, PsdHierarchyRequestNode> nodesById,
            Dictionary<string, PsdHierarchyWebNodeDto> webNodesById,
            HashSet<string> acceptedGroupKeys,
            HashSet<string> lockedGroupKeys)
        {
            foreach (PsdHierarchyPlanGroup source in plan.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (source == null)
                {
                    continue;
                }

                List<string> memberStableIds = (source.memberStableIds ?? new List<string>())
                    .Where(stableId => !string.IsNullOrEmpty(stableId))
                    .ToList();
                bool containsProtectedNode = memberStableIds.Any(stableId =>
                {
                    PsdHierarchyRequestNode node;
                    return nodesById.TryGetValue(stableId, out node) &&
                           (node.isProtectedBoundary || node.hasProjectComponents);
                });
                snapshot.groups.Add(new PsdHierarchyWebGroupDto
                {
                    key = source.key ?? string.Empty,
                    parentKey = source.parentKey ?? string.Empty,
                    displayName = source.displayName ?? string.Empty,
                    memberStableIds = memberStableIds,
                    bounds = UnionBounds(memberStableIds, webNodesById),
                    isAccepted = acceptedGroupKeys.Contains(source.key ?? string.Empty),
                    isLocked = lockedGroupKeys.Contains(source.key ?? string.Empty) ||
                               containsProtectedNode,
                    evidence = source.evidence ?? string.Empty,
                    confidence = source.confidence
                });
            }
        }

        private static PsdHierarchyWebBoundsDto UnionBounds(
            IEnumerable<string> stableIds,
            Dictionary<string, PsdHierarchyWebNodeDto> nodesById)
        {
            List<PsdHierarchyWebBoundsDto> bounds = stableIds
                .Where(nodesById.ContainsKey)
                .Select(stableId => nodesById[stableId].bounds)
                .ToList();
            if (bounds.Count == 0)
            {
                return new PsdHierarchyWebBoundsDto();
            }

            float left = bounds.Min(value => value.x);
            float top = bounds.Min(value => value.y);
            float right = bounds.Max(value => value.x + value.width);
            float bottom = bounds.Max(value => value.y + value.height);
            return new PsdHierarchyWebBoundsDto
            {
                x = left,
                y = top,
                width = right - left,
                height = bottom - top
            };
        }

        private static void BuildWarnings(
            PsdHierarchyWebSnapshotDto snapshot,
            PsdHierarchyRequest request,
            PsdHierarchyOrganizerPreviewModel previewModel)
        {
            AddNodeWarning(
                snapshot,
                request.nodes.Where(node => node != null && node.isProtectedBoundary)
                    .Select(node => node.stableId),
                "protected-boundary",
                "Protected hierarchy boundaries cannot be reorganized automatically.");
            AddNodeWarning(
                snapshot,
                request.nodes.Where(node => node != null && node.hasProjectComponents)
                    .Select(node => node.stableId),
                "project-components",
                "Nodes with project-owned components are locked.");
            AddNodeWarning(
                snapshot,
                previewModel.pendingMissingStableIds,
                "pending-missing",
                "Missing PSD nodes require explicit cleanup confirmation.");

            foreach (string error in previewModel.validationErrors ?? new List<string>())
            {
                snapshot.warnings.Add(new PsdHierarchyWebWarningDto
                {
                    code = "validation-error",
                    message = error ?? string.Empty
                });
            }
        }

        private static void AddNodeWarning(
            PsdHierarchyWebSnapshotDto snapshot,
            IEnumerable<string> stableIds,
            string code,
            string message)
        {
            List<string> ids = (stableIds ?? Enumerable.Empty<string>())
                .Where(stableId => !string.IsNullOrEmpty(stableId))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (ids.Count == 0)
            {
                return;
            }

            snapshot.warnings.Add(new PsdHierarchyWebWarningDto
            {
                code = code,
                message = message,
                stableIds = ids
            });
            HashSet<string> warningIds = new HashSet<string>(ids, StringComparer.Ordinal);
            foreach (PsdHierarchyWebNodeDto node in snapshot.nodes.Where(
                         node => warningIds.Contains(node.stableId)))
            {
                node.hasWarning = true;
            }
        }

        private static void BuildPrefabCandidates(
            PsdHierarchyWebSnapshotDto snapshot,
            PsdHierarchyOrganizerPreviewModel previewModel)
        {
            Dictionary<string, PsdHierarchyWebNodeDto> nodesById = snapshot.nodes
                .ToDictionary(node => node.stableId, StringComparer.Ordinal);
            foreach (PsdPrefabCandidate candidate in previewModel.prefabCandidates)
            {
                PsdHierarchyWebNodeDto representative;
                nodesById.TryGetValue(candidate.rootStableId ?? string.Empty, out representative);
                string rootStableId = candidate.rootStableId ?? string.Empty;
                snapshot.prefabCandidates.Add(new PsdHierarchyWebPrefabCandidateDto
                {
                    candidateId = "candidate:" + rootStableId,
                    proposedName = representative != null
                        ? representative.proposedName
                        : string.Empty,
                    representativeStableId = rootStableId,
                    instanceStableIds = new List<string> { rootStableId },
                    instanceControlledDifferences =
                        new List<string>(candidate.evidence ?? new List<string>())
                });
            }
        }

        private static PsdHierarchyWebBoundsDto ClipToCanvas(
            PsdHierarchyRectangle source,
            int canvasWidth,
            int canvasHeight)
        {
            float left = Math.Max(0f, Math.Min(canvasWidth, source.x));
            float top = Math.Max(0f, Math.Min(canvasHeight, source.y));
            float right = Math.Max(left, Math.Min(canvasWidth, source.x + Math.Max(0f, source.width)));
            float bottom = Math.Max(top, Math.Min(canvasHeight, source.y + Math.Max(0f, source.height)));
            return new PsdHierarchyWebBoundsDto
            {
                x = left,
                y = top,
                width = right - left,
                height = bottom - top
            };
        }
    }
}
