namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json.Linq;

    internal static class PsdHierarchyPlanBatchPartitioner
    {
        private static readonly Regex CandidateIdRegex = new Regex(
            @"candidateId\s*[=:]\s*(?<id>[A-Za-z0-9_\-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex NodeIdRegex = new Regex(
            @"node:(?<id>[A-Za-z0-9_\-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex OperationIndexRegex = new Regex(
            @"(?<array>componentExtractions|stateComponentExtractions|variantComponentExtractions|statefulComponentExtractions)\[(?<index>\d+)\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static bool TryBuildSafePlan(
            JObject sourcePlan,
            PsdHierarchyChatContext context,
            IEnumerable<string> quarantinedCandidateIds,
            out JObject safePlan,
            out string error)
        {
            safePlan = null;
            if (sourcePlan == null)
            {
                error = "缺少待分批的计划。";
                return false;
            }

            if (context == null)
            {
                error = "缺少当前层级快照。";
                return false;
            }

            var candidatesById = (context.componentFamilyCandidates ?? Array.Empty<PsdHierarchyComponentFamilyCandidate>())
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.id))
                .ToDictionary(candidate => candidate.id, StringComparer.Ordinal);
            var quarantinedIds = new HashSet<string>(
                quarantinedCandidateIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            PsdHierarchyComponentFamilyCandidate[] quarantinedCandidates = quarantinedIds
                .Select(candidateId => candidatesById.TryGetValue(candidateId, out PsdHierarchyComponentFamilyCandidate candidate)
                    ? candidate
                    : null)
                .Where(candidate => candidate != null)
                .ToArray();
            if (quarantinedCandidates.Length != quarantinedIds.Count)
            {
                string[] unknownIds = quarantinedIds.Where(candidateId => !candidatesById.ContainsKey(candidateId)).ToArray();
                error = "隔离批次引用了当前快照中不存在的候选：" + string.Join(", ", unknownIds);
                return false;
            }

            var ownedNodeReferences = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyComponentFamilyCandidate candidate in quarantinedCandidates)
            {
                foreach (string nodeId in context.GetNodeIdsWithinSubtrees(candidate.sources))
                {
                    ownedNodeReferences.Add("node:" + nodeId);
                }
            }

            safePlan = (JObject)sourcePlan.DeepClone();
            foreach (JProperty property in safePlan.Properties().ToArray())
            {
                if (!(property.Value is JArray operations))
                {
                    continue;
                }

                for (int index = operations.Count - 1; index >= 0; index--)
                {
                    if (BelongsToQuarantinedCandidate(
                            operations[index],
                            quarantinedIds,
                            ownedNodeReferences))
                    {
                        operations.RemoveAt(index);
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        internal static bool TryResolveCandidateIdFromFailure(
            string failure,
            JObject plan,
            PsdHierarchyChatContext context,
            out string candidateId)
        {
            candidateId = string.Empty;
            if (context == null)
            {
                return false;
            }

            var candidates = (context.componentFamilyCandidates ?? Array.Empty<PsdHierarchyComponentFamilyCandidate>())
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.id))
                .ToArray();
            Match directCandidate = CandidateIdRegex.Match(failure ?? string.Empty);
            if (directCandidate.Success)
            {
                string directId = directCandidate.Groups["id"].Value;
                if (candidates.Any(candidate => string.Equals(candidate.id, directId, StringComparison.Ordinal)))
                {
                    candidateId = directId;
                    return true;
                }
            }

            Match operation = OperationIndexRegex.Match(failure ?? string.Empty);
            if (operation.Success &&
                int.TryParse(operation.Groups["index"].Value, out int operationIndex) &&
                plan?[operation.Groups["array"].Value] is JArray operations &&
                operationIndex >= 0 && operationIndex < operations.Count &&
                TryResolveUniqueCandidateFromToken(operations[operationIndex], candidates, context, out candidateId))
            {
                return true;
            }

            var failedNodeReferences = new HashSet<string>(
                NodeIdRegex.Matches(failure ?? string.Empty)
                    .Cast<Match>()
                    .Select(match => "node:" + match.Groups["id"].Value),
                StringComparer.Ordinal);
            if (failedNodeReferences.Count == 0)
            {
                return false;
            }

            PsdHierarchyComponentFamilyCandidate[] owners = candidates
                .Where(candidate => GetOwnedNodeReferences(candidate, context).Overlaps(failedNodeReferences))
                .ToArray();
            if (owners.Length != 1)
            {
                return false;
            }

            candidateId = owners[0].id;
            return true;
        }

        private static bool TryResolveUniqueCandidateFromToken(
            JToken token,
            IReadOnlyList<PsdHierarchyComponentFamilyCandidate> candidates,
            PsdHierarchyChatContext context,
            out string candidateId)
        {
            candidateId = string.Empty;
            if (token == null)
            {
                return false;
            }

            string extractionId = (token as JObject)?.Value<string>("id");
            var references = new HashSet<string>(StringComparer.Ordinal);
            CollectStringValues(token, references);
            PsdHierarchyComponentFamilyCandidate[] owners = candidates
                .Where(candidate => GetOwnedNodeReferences(candidate, context).Overlaps(references))
                .ToArray();
            if (owners.Length == 1)
            {
                candidateId = owners[0].id;
                return true;
            }

            if (string.IsNullOrWhiteSpace(extractionId))
            {
                return false;
            }

            JObject root = token.Root as JObject;
            JArray decisions = root?["componentFamilyDecisions"] as JArray;
            JObject decision = decisions?
                .OfType<JObject>()
                .SingleOrDefault(item => string.Equals(
                    item.Value<string>("extractionId"),
                    extractionId,
                    StringComparison.Ordinal));
            string resolvedCandidateId = decision?.Value<string>("candidateId");
            if (string.IsNullOrWhiteSpace(resolvedCandidateId) ||
                !candidates.Any(candidate => string.Equals(candidate.id, resolvedCandidateId, StringComparison.Ordinal)))
            {
                return false;
            }

            candidateId = resolvedCandidateId;
            return true;
        }

        private static HashSet<string> GetOwnedNodeReferences(
            PsdHierarchyComponentFamilyCandidate candidate,
            PsdHierarchyChatContext context)
        {
            return new HashSet<string>(
                context.GetNodeIdsWithinSubtrees(candidate.sources).Select(nodeId => "node:" + nodeId),
                StringComparer.Ordinal);
        }

        private static bool BelongsToQuarantinedCandidate(
            JToken operation,
            ISet<string> quarantinedCandidateIds,
            ISet<string> ownedNodeReferences)
        {
            if (operation is JObject operationObject)
            {
                string candidateId = operationObject.Value<string>("candidateId");
                if (!string.IsNullOrWhiteSpace(candidateId) && quarantinedCandidateIds.Contains(candidateId))
                {
                    return true;
                }
            }

            var values = new HashSet<string>(StringComparer.Ordinal);
            CollectStringValues(operation, values);
            return values.Overlaps(ownedNodeReferences);
        }

        private static void CollectStringValues(JToken token, ISet<string> values)
        {
            if (token == null)
            {
                return;
            }

            if (token.Type == JTokenType.String)
            {
                values.Add(token.Value<string>());
                return;
            }

            foreach (JToken child in token.Children())
            {
                CollectStringValues(child, values);
            }
        }
    }
}
