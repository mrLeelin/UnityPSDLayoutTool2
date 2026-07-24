namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Produces read-only, explainable Prefab boundary candidates from structural facts.
    /// It deliberately does not name, create, or apply Prefabs; AI may only enrich these facts later.
    /// </summary>
    public sealed class PsdPrefabCandidate
    {
        public string rootStableId;
        public List<string> instanceRootStableIds = new List<string>();
        public float score;
        public List<string> evidence = new List<string>();
    }

    public static class PsdHierarchyPrefabCandidateAnalyzer
    {
        public static List<PsdPrefabCandidate> Analyze(IEnumerable<PsdHierarchyRequestNode> source)
        {
            List<PsdHierarchyRequestNode> nodes = (source ?? Enumerable.Empty<PsdHierarchyRequestNode>()).Where(node => node != null).ToList();
            var children = nodes.GroupBy(node => node.parentStableId ?? string.Empty).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var result = new List<PsdPrefabCandidate>();
            var rootsBySignature = nodes
                .Where(root => Descendants(root.stableId, children).Count >= 2)
                .GroupBy(root => Signature(root.stableId, children), StringComparer.Ordinal);
            foreach (IGrouping<string, PsdHierarchyRequestNode> matchingRoots in rootsBySignature)
            {
                List<PsdHierarchyRequestNode> instances = matchingRoots.ToList();
                PsdHierarchyRequestNode root = instances[0];
                List<PsdHierarchyRequestNode> descendants = Descendants(root.stableId, children);
                float score = 0f;
                var evidence = new List<string>();
                if (instances.Any(instance => instance.hasProjectComponents))
                {
                    score += 0.45f;
                    evidence.Add("project component boundary");
                }
                if (descendants.Count >= 3)
                {
                    score += 0.20f;
                    evidence.Add("contains " + descendants.Count + " related layers");
                }
                if (instances.Count > 1)
                {
                    score += 0.35f;
                    evidence.Add("repeated structure");
                }
                if (score >= 0.45f) result.Add(new PsdPrefabCandidate
                {
                    rootStableId = root.stableId,
                    instanceRootStableIds = instances.Select(instance => instance.stableId).ToList(),
                    score = Math.Min(1f, score),
                    evidence = evidence
                });
            }
            return result.OrderByDescending(candidate => candidate.score).ThenBy(candidate => candidate.rootStableId, StringComparer.Ordinal).ToList();
        }

        private static List<PsdHierarchyRequestNode> Descendants(string rootId, Dictionary<string, List<PsdHierarchyRequestNode>> children)
        {
            var result = new List<PsdHierarchyRequestNode>();
            var pending = new Queue<string>(); pending.Enqueue(rootId);
            while (pending.Count > 0)
            {
                List<PsdHierarchyRequestNode> direct;
                if (!children.TryGetValue(pending.Dequeue(), out direct)) continue;
                foreach (PsdHierarchyRequestNode child in direct) { result.Add(child); pending.Enqueue(child.stableId); }
            }
            return result;
        }

        private static string Signature(string rootId, Dictionary<string, List<PsdHierarchyRequestNode>> children)
        {
            List<PsdHierarchyRequestNode> direct;
            if (!children.TryGetValue(rootId, out direct)) return string.Empty;
            return string.Join("|", direct.Select(node =>
                (node.kind ?? string.Empty) + "[" + Signature(node.stableId, children) + "]").ToArray());
        }
    }
}
