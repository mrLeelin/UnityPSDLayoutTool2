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
            foreach (PsdHierarchyRequestNode root in nodes)
            {
                List<PsdHierarchyRequestNode> descendants = Descendants(root.stableId, children);
                if (descendants.Count < 2) continue;
                float score = 0f;
                var evidence = new List<string>();
                if (root.hasProjectComponents)
                {
                    score += 0.45f;
                    evidence.Add("project component boundary");
                }
                if (descendants.Count >= 3)
                {
                    score += 0.20f;
                    evidence.Add("contains " + descendants.Count + " related layers");
                }
                string signature = Signature(root.stableId, children);
                int repeats = nodes.Count(node => node.stableId != root.stableId && Signature(node.stableId, children) == signature);
                if (repeats > 0)
                {
                    score += 0.35f;
                    evidence.Add("repeated structure");
                }
                if (score >= 0.45f) result.Add(new PsdPrefabCandidate { rootStableId = root.stableId, score = Math.Min(1f, score), evidence = evidence });
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
            List<PsdHierarchyRequestNode> descendants = Descendants(rootId, children);
            return string.Join("|", descendants.Select(node => node.kind ?? string.Empty).OrderBy(kind => kind, StringComparer.Ordinal).ToArray());
        }
    }
}
