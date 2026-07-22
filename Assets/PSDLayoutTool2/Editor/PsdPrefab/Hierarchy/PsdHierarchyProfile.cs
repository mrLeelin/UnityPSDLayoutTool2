namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [Serializable]
    public sealed class PsdHierarchyProfileNode
    {
        public string stableId;
        public string contentFingerprint;
        public string structureFingerprint;
        public string geometryFingerprint;
    }

    [Serializable]
    public sealed class PsdHierarchyProfileGroup
    {
        public string key;
        public string displayName;
        public List<string> stableLayerIds = new List<string>();
        public byte[] planBytes = new byte[0];
    }

    [Serializable]
    public sealed class PsdHierarchyProfileRename
    {
        public string stableId;
        public string name;
    }

    public sealed class PsdHierarchyReconciliationResult
    {
        public bool requiresReplan;
        public readonly List<string> contentOnlyStableIds = new List<string>();
        public readonly List<string> geometryValidationStableIds = new List<string>();
        public readonly List<string> focusedInvalidatedScopeStableIds = new List<string>();
        public readonly List<string> unsortedNewStableIds = new List<string>();
        public readonly List<string> unsortedUnstableIds = new List<string>();
        public readonly List<string> pendingMissingStableIds = new List<string>();
    }

    /// <summary>
    /// Persisted hierarchy decisions keyed exclusively by native Photoshop layer
    /// IDs. Reconcile deliberately preserves missing records until the user
    /// confirms cleanup, preventing a temporary PSD omission from deleting a
    /// valid hierarchy decision or business reference.
    /// </summary>
    [Serializable]
    public sealed class PsdHierarchyProfile
    {
        public int schemaVersion = 1;
        public List<PsdHierarchyProfileNode> nodes = new List<PsdHierarchyProfileNode>();
        public List<PsdHierarchyProfileGroup> groups = new List<PsdHierarchyProfileGroup>();
        public List<PsdHierarchyProfileRename> renames = new List<PsdHierarchyProfileRename>();

        public static PsdHierarchyProfile Create(
            PsdPrefabDocumentModel document,
            IEnumerable<PsdHierarchyProfileGroup> sourceGroups,
            IEnumerable<PsdHierarchyProfileRename> sourceRenames)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            var profile = new PsdHierarchyProfile();
            profile.nodes = document.nodes
                .Where(node => node != null && PsdStableLayerIdUtility.IsPersistable(node.stableId))
                .GroupBy(node => node.stableId, StringComparer.Ordinal)
                .Select(group => Snapshot(group.First()))
                .ToList();

            foreach (PsdHierarchyProfileGroup source in sourceGroups ?? Enumerable.Empty<PsdHierarchyProfileGroup>())
            {
                if (source == null)
                {
                    continue;
                }

                List<string> members = DurableDistinct(source.stableLayerIds);
                if (members.Count == 0)
                {
                    continue;
                }

                string key = string.IsNullOrEmpty(source.key) ? BuildGeneratedGroupKey(members) : source.key;
                if (profile.groups.Any(group => string.Equals(group.key, key, StringComparison.Ordinal)))
                {
                    continue;
                }

                profile.groups.Add(new PsdHierarchyProfileGroup
                {
                    key = key,
                    displayName = source.displayName ?? string.Empty,
                    stableLayerIds = members,
                    planBytes = source.planBytes == null ? new byte[0] : source.planBytes.ToArray()
                });
            }

            profile.renames = (sourceRenames ?? Enumerable.Empty<PsdHierarchyProfileRename>())
                .Where(rename => rename != null && PsdStableLayerIdUtility.IsPersistable(rename.stableId))
                .GroupBy(rename => rename.stableId, StringComparer.Ordinal)
                .Select(group => new PsdHierarchyProfileRename
                {
                    stableId = group.Key,
                    name = group.First().name ?? string.Empty
                })
                .ToList();
            return profile;
        }

        public PsdHierarchyReconciliationResult Reconcile(PsdPrefabDocumentModel document, bool confirmMissingCleanup = false)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            NormalizePersistedCollections();
            var result = new PsdHierarchyReconciliationResult();
            Dictionary<string, PsdHierarchyProfileNode> previous = nodes.ToDictionary(node => node.stableId, StringComparer.Ordinal);
            Dictionary<string, PsdPrefabNodeModel> current = document.nodes
                .Where(node => node != null && PsdStableLayerIdUtility.IsPersistable(node.stableId))
                .GroupBy(node => node.stableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (PsdPrefabNodeModel unstable in document.nodes.Where(node => node != null && !PsdStableLayerIdUtility.IsPersistable(node.stableId)))
            {
                AddUnique(result.unsortedUnstableIds, unstable.stableId);
            }

            foreach (KeyValuePair<string, PsdPrefabNodeModel> pair in current.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                PsdHierarchyProfileNode old;
                if (!previous.TryGetValue(pair.Key, out old))
                {
                    AddUnique(result.unsortedNewStableIds, pair.Key);
                    AddUnique(result.focusedInvalidatedScopeStableIds, pair.Key);
                    continue;
                }

                string content = PsdHierarchyFingerprints.Content(pair.Value);
                string structure = PsdHierarchyFingerprints.Structure(pair.Value);
                string geometry = PsdHierarchyFingerprints.Geometry(pair.Value);
                bool contentChanged = !string.Equals(old.contentFingerprint, content, StringComparison.Ordinal);
                bool structureChanged = !string.Equals(old.structureFingerprint, structure, StringComparison.Ordinal);
                bool geometryChanged = !string.Equals(old.geometryFingerprint, geometry, StringComparison.Ordinal);

                if (structureChanged)
                {
                    AddUnique(result.focusedInvalidatedScopeStableIds, pair.Key);
                }
                else if (geometryChanged)
                {
                    AddUnique(result.geometryValidationStableIds, pair.Key);
                }
                else if (contentChanged)
                {
                    AddUnique(result.contentOnlyStableIds, pair.Key);
                }

                // Content is safe to accept immediately because it never changes
                // hierarchy decisions. Structure and geometry stay at the last
                // accepted plan until the later planner/validator task commits them;
                // otherwise merely opening Preview twice could hide invalidation.
                old.contentFingerprint = content;
            }

            List<string> missing = previous.Keys.Where(id => !current.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (confirmMissingCleanup)
            {
                RemoveMissing(missing);
            }
            else
            {
                result.pendingMissingStableIds.AddRange(missing);
            }

            result.requiresReplan = result.focusedInvalidatedScopeStableIds.Count > 0;
            return result;
        }

        public static string BuildGeneratedGroupKey(IEnumerable<string> stableLayerIds)
        {
            string canonical = string.Join("|", DurableDistinct(stableLayerIds).OrderBy(id => id, StringComparer.Ordinal).ToArray());
            return "generated_" + PsdStableLayerIdUtility.ComputeFnv1a(canonical);
        }

        private static PsdHierarchyProfileNode Snapshot(PsdPrefabNodeModel node)
        {
            return new PsdHierarchyProfileNode
            {
                stableId = node.stableId,
                contentFingerprint = PsdHierarchyFingerprints.Content(node),
                structureFingerprint = PsdHierarchyFingerprints.Structure(node),
                geometryFingerprint = PsdHierarchyFingerprints.Geometry(node)
            };
        }

        private void NormalizePersistedCollections()
        {
            nodes = nodes.Where(node => node != null && PsdStableLayerIdUtility.IsPersistable(node.stableId))
                .GroupBy(node => node.stableId, StringComparer.Ordinal).Select(group => group.First()).ToList();
            groups = groups.Where(group => group != null)
                .GroupBy(group => group.key ?? string.Empty, StringComparer.Ordinal).Select(group => group.First()).ToList();
            foreach (PsdHierarchyProfileGroup group in groups)
            {
                group.stableLayerIds = DurableDistinct(group.stableLayerIds);
            }
            groups.RemoveAll(group => group.stableLayerIds.Count == 0);
            renames = renames.Where(rename => rename != null && PsdStableLayerIdUtility.IsPersistable(rename.stableId))
                .GroupBy(rename => rename.stableId, StringComparer.Ordinal).Select(group => group.First()).ToList();
        }

        private void RemoveMissing(IEnumerable<string> missingIds)
        {
            var missing = new HashSet<string>(missingIds, StringComparer.Ordinal);
            nodes.RemoveAll(node => missing.Contains(node.stableId));
            renames.RemoveAll(rename => missing.Contains(rename.stableId));
            foreach (PsdHierarchyProfileGroup group in groups)
            {
                group.stableLayerIds.RemoveAll(id => missing.Contains(id));
            }
            groups.RemoveAll(group => group.stableLayerIds.Count == 0);
        }

        private static List<string> DurableDistinct(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(PsdStableLayerIdUtility.IsPersistable)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static void AddUnique(List<string> values, string value)
        {
            value = value ?? string.Empty;
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
