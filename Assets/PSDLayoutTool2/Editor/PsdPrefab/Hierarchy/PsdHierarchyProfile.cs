namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;

    [Serializable]
    public sealed class PsdHierarchyProfileNode
    {
        public string stableId;
        public string contentFingerprint;
        public string structureFingerprint;
        public string geometryFingerprint;

        // Added without changing the schema version: zero/empty are the safe
        // defaults for profiles created before transactional identity tracking.
        // Such records are never guessed from their diagnostic path.
        public long localFileId;
        public string lastKnownPath;
        public bool pendingCreation;
    }

    [Serializable]
    public sealed class PsdHierarchyProfileGroup
    {
        public string key;
        public string parentKey;
        public string displayName;
        public List<string> stableLayerIds = new List<string>();
        public long localFileId;
        public string lastKnownPath;
        public byte[] GetPlanBytes()
        {
            // Bytes are a deterministic view of validated structured fields,
            // not separately serialized state. This makes it impossible to hide
            // an unstable ID in an opaque payload alongside a clean member list.
            List<string> durableMembers = (stableLayerIds ?? new List<string>())
                .Where(PsdStableLayerIdUtility.IsPersistable)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var value = new StringBuilder("hierarchy-group-v1|");
            Append(value, PsdHierarchyProfile.IsValidGroupKey(key)
                ? key
                : PsdHierarchyProfile.BuildGeneratedGroupKey(durableMembers));
            // parentKey is preserved exactly from the already validated plan.
            // Profile normalization must not repair broken nesting silently.
            Append(value, parentKey);
            foreach (string stableId in durableMembers)
            {
                Append(value, stableId);
            }

            return Encoding.UTF8.GetBytes(value.ToString());
        }

        private static void Append(StringBuilder target, string value)
        {
            value = value ?? string.Empty;
            target.Append(value.Length);
            target.Append(':');
            target.Append(value);
            target.Append('|');
        }
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

    public enum PsdHierarchyProfileSchemaStatus
    {
        Current,
        RequiresRebuild,
        UnsupportedFuture
    }

    public struct PsdHierarchyProfileSchemaResult
    {
        public PsdHierarchyProfileSchemaStatus status;
        public bool canApply;
    }

    /// <summary>
    /// Persisted hierarchy decisions keyed exclusively by native Photoshop layer
    /// IDs. Reconcile deliberately preserves missing records until the user
    /// confirms cleanup, preventing a temporary PSD omission from deleting a
    /// valid hierarchy decision or business reference.
    /// </summary>
    [Serializable]
    public sealed class PsdHierarchyProfile : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string sourcePsdGuid;
        public string sourceFingerprint;
        public List<PsdHierarchyProfileNode> nodes = new List<PsdHierarchyProfileNode>();
        public List<PsdHierarchyProfileGroup> groups = new List<PsdHierarchyProfileGroup>();
        public List<PsdHierarchyProfileRename> renames = new List<PsdHierarchyProfileRename>();

        public static PsdHierarchyProfile Create(
            PsdPrefabDocumentModel document,
            IEnumerable<PsdHierarchyProfileGroup> sourceGroups,
            IEnumerable<PsdHierarchyProfileRename> sourceRenames,
            string sourcePsdGuid = "")
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            PsdHierarchyProfile profile = CreateInstance<PsdHierarchyProfile>();
            profile.sourcePsdGuid = sourcePsdGuid ?? string.Empty;
            profile.sourceFingerprint = ResolveDocumentFingerprint(document);
            profile.nodes = (document.nodes ?? new List<PsdPrefabNodeModel>())
                .Where(node => node != null && PsdStableLayerIdUtility.IsPersistable(node.stableId))
                .GroupBy(node => node.stableId, StringComparer.Ordinal)
                .Select(group => Snapshot(group.First()))
                .ToList();

            var ownedStableIds = new HashSet<string>(StringComparer.Ordinal);
            var usedGroupKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyProfileGroup source in sourceGroups ?? Enumerable.Empty<PsdHierarchyProfileGroup>())
            {
                if (source == null)
                {
                    continue;
                }

                List<string> members = DurableDistinct(source.stableLayerIds)
                    .Where(ownedStableIds.Add)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();
                if (members.Count == 0)
                {
                    continue;
                }

                string key = ResolveUniqueGroupKey(source.key, members, usedGroupKeys);

                var group = new PsdHierarchyProfileGroup
                {
                    key = key,
                    parentKey = source.parentKey ?? string.Empty,
                    displayName = source.displayName ?? string.Empty,
                    stableLayerIds = members
                };
                profile.groups.Add(group);
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

        public bool IsStale(string psdGuid, string fingerprint)
        {
            return !CheckSchema().canApply ||
                   !string.Equals(sourcePsdGuid ?? string.Empty, psdGuid ?? string.Empty, StringComparison.Ordinal) ||
                   !string.Equals(sourceFingerprint ?? string.Empty, fingerprint ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// Explicit schema gate for callers loading a persisted Profile. Older
        /// profiles require a deterministic rebuild from the PSD; future schemas
        /// are rejected so an older tool cannot silently discard new decisions.
        /// </summary>
        public PsdHierarchyProfileSchemaResult CheckSchema()
        {
            if (schemaVersion == CurrentSchemaVersion)
            {
                return new PsdHierarchyProfileSchemaResult
                {
                    status = PsdHierarchyProfileSchemaStatus.Current,
                    canApply = true
                };
            }

            return new PsdHierarchyProfileSchemaResult
            {
                status = schemaVersion < CurrentSchemaVersion
                    ? PsdHierarchyProfileSchemaStatus.RequiresRebuild
                    : PsdHierarchyProfileSchemaStatus.UnsupportedFuture,
                canApply = false
            };
        }

        public PsdHierarchyReconciliationResult Reconcile(PsdPrefabDocumentModel document, bool confirmMissingCleanup = false)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            PsdHierarchyProfileSchemaResult schema = CheckSchema();
            if (!schema.canApply)
            {
                throw new InvalidOperationException("Hierarchy Profile schema cannot be applied: " + schema.status);
            }

            NormalizePersistedCollections();
            var result = new PsdHierarchyReconciliationResult();
            Dictionary<string, PsdHierarchyProfileNode> previous = nodes.ToDictionary(node => node.stableId, StringComparer.Ordinal);
            Dictionary<string, PsdPrefabNodeModel> current = (document.nodes ?? new List<PsdPrefabNodeModel>())
                .Where(node => node != null && PsdStableLayerIdUtility.IsPersistable(node.stableId))
                .GroupBy(node => node.stableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (PsdPrefabNodeModel unstable in (document.nodes ?? new List<PsdPrefabNodeModel>())
                         .Where(node => node != null && !PsdStableLayerIdUtility.IsPersistable(node.stableId)))
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
            bool sourceCanAdvance = !result.requiresReplan &&
                                    result.geometryValidationStableIds.Count == 0 &&
                                    result.pendingMissingStableIds.Count == 0 &&
                                    result.unsortedUnstableIds.Count == 0;
            if (sourceCanAdvance)
            {
                sourceFingerprint = ResolveDocumentFingerprint(document);
            }

            return result;
        }

        public static string BuildGeneratedGroupKey(IEnumerable<string> stableLayerIds)
        {
            string canonical = string.Join("|", DurableDistinct(stableLayerIds).OrderBy(id => id, StringComparer.Ordinal).ToArray());
            return "generated_" + PsdStableLayerIdUtility.ComputeFnv1a(canonical);
        }

        internal static bool IsValidGroupKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 128 || key.StartsWith("fallback_", StringComparison.Ordinal))
            {
                return false;
            }

            return key.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.');
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
            nodes = (nodes ?? new List<PsdHierarchyProfileNode>())
                .Where(node => node != null && PsdStableLayerIdUtility.IsPersistable(node.stableId))
                .GroupBy(node => node.stableId, StringComparer.Ordinal).Select(group => group.First()).ToList();
            var normalizedGroups = new List<PsdHierarchyProfileGroup>();
            var ownedStableIds = new HashSet<string>(StringComparer.Ordinal);
            var usedGroupKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyProfileGroup group in (groups ?? new List<PsdHierarchyProfileGroup>()).Where(value => value != null))
            {
                List<string> members = DurableDistinct(group.stableLayerIds)
                    .Where(ownedStableIds.Add)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();
                if (members.Count == 0)
                {
                    continue;
                }

                group.stableLayerIds = members;
                group.key = ResolveUniqueGroupKey(group.key, members, usedGroupKeys);
                normalizedGroups.Add(group);
            }
            groups = normalizedGroups;
            renames = (renames ?? new List<PsdHierarchyProfileRename>())
                .Where(rename => rename != null && PsdStableLayerIdUtility.IsPersistable(rename.stableId))
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

        private static string ResolveUniqueGroupKey(
            string preferredKey,
            IEnumerable<string> members,
            HashSet<string> usedKeys)
        {
            if (IsValidGroupKey(preferredKey) && usedKeys.Add(preferredKey))
            {
                return preferredKey;
            }

            string generated = BuildGeneratedGroupKey(members);
            if (usedKeys.Add(generated))
            {
                return generated;
            }

            // A hash collision or pre-existing generated key is repaired with a
            // deterministic ordinal suffix. Enumeration order is already stable.
            for (int suffix = 2; ; suffix++)
            {
                string candidate = generated + "_" + suffix;
                if (usedKeys.Add(candidate))
                {
                    return candidate;
                }
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            value = value ?? string.Empty;
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static string ResolveDocumentFingerprint(PsdPrefabDocumentModel document)
        {
            return !string.IsNullOrEmpty(document.sourceFingerprint)
                ? document.sourceFingerprint
                : PsdHierarchyFingerprints.Document(document);
        }
    }
}
