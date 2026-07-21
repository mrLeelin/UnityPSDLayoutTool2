namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;

    public enum PsdPrefabChangeKind
    {
        Unchanged,
        Added,
        Updated,
        Removed
    }

    [Serializable]
    public sealed class PsdPrefabNodeChange
    {
        public PsdPrefabChangeKind kind;
        public string stableId;
        public PsdPrefabNodeModel previous;
        public PsdPrefabNodeModel current;
    }

    /// <summary>只按稳定 ID 比较节点，不按层名称或数组位置匹配。</summary>
    public static class PsdPrefabDiff
    {
        public static List<PsdPrefabNodeChange> Compare(
            PsdPrefabDocumentModel previous,
            PsdPrefabDocumentModel current)
        {
            var changes = new List<PsdPrefabNodeChange>();
            var oldById = Index(previous);
            var newById = Index(current);

            foreach (KeyValuePair<string, PsdPrefabNodeModel> pair in newById)
            {
                PsdPrefabNodeModel oldNode;
                if (!oldById.TryGetValue(pair.Key, out oldNode))
                {
                    changes.Add(new PsdPrefabNodeChange
                    {
                        kind = PsdPrefabChangeKind.Added,
                        stableId = pair.Key,
                        current = pair.Value
                    });
                    continue;
                }

                changes.Add(new PsdPrefabNodeChange
                {
                    kind = string.Equals(oldNode.contentFingerprint, pair.Value.contentFingerprint, StringComparison.Ordinal)
                        ? PsdPrefabChangeKind.Unchanged
                        : PsdPrefabChangeKind.Updated,
                    stableId = pair.Key,
                    previous = oldNode,
                    current = pair.Value
                });
            }

            foreach (KeyValuePair<string, PsdPrefabNodeModel> pair in oldById)
            {
                if (!newById.ContainsKey(pair.Key))
                {
                    changes.Add(new PsdPrefabNodeChange
                    {
                        kind = PsdPrefabChangeKind.Removed,
                        stableId = pair.Key,
                        previous = pair.Value
                    });
                }
            }

            return changes;
        }

        private static Dictionary<string, PsdPrefabNodeModel> Index(PsdPrefabDocumentModel document)
        {
            var result = new Dictionary<string, PsdPrefabNodeModel>(StringComparer.Ordinal);
            if (document == null || document.nodes == null)
            {
                return result;
            }

            foreach (PsdPrefabNodeModel node in document.nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.stableId))
                {
                    result[node.stableId] = node;
                }
            }

            return result;
        }
    }
}
