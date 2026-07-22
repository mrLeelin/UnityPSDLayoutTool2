namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class PsdHierarchyApplyException : InvalidOperationException
    {
        public PsdHierarchyApplyException(string message) : base(message) { }
    }

    public enum PsdHierarchyApplyStage
    {
        GroupPrepared,
        MemberMoved,
        BeforeVerification
    }

    /// <summary>
    /// Identity evidence returned to the incremental Profile writer. Existing
    /// group identities come only from the caller's key map; newly created keys
    /// are explicitly reported so Task 6 can persist their local file IDs.
    /// </summary>
    public sealed class PsdHierarchyApplyResult
    {
        public readonly Dictionary<string, RectTransform> groupsByKey =
            new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        public readonly HashSet<string> createdGroupKeys = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Applies a validated plan to loaded existing Prefab contents. Group reuse
    /// is never inferred from names or shape: only a Profile-resolved explicit
    /// key mapping grants ownership of an existing RectTransform.
    /// </summary>
    public static class PsdHierarchyApplier
    {
        private sealed class TransformState
        {
            public Transform transform;
            public Transform parent;
            public int siblingIndex;
            public int depth;
            public string name;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public bool isRect;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector3 anchoredPosition3D;
            public Vector2 sizeDelta;
            public Vector2 offsetMin;
            public Vector2 offsetMax;
        }

        private sealed class AtomicChild
        {
            public RectTransform transform;
            public int rank;
            public TransformState authoredLeafState;
        }

        public static PsdHierarchyApplyResult Apply(
            RectTransform root,
            PsdHierarchyPlan plan,
            IReadOnlyDictionary<string, RectTransform> registry,
            IReadOnlyDictionary<string, RectTransform> existingGroupsByKey)
        {
            return Apply(root, plan, registry, existingGroupsByKey, null);
        }

        internal static PsdHierarchyApplyResult Apply(
            RectTransform root,
            PsdHierarchyPlan plan,
            IReadOnlyDictionary<string, RectTransform> registry,
            IReadOnlyDictionary<string, RectTransform> existingGroupsByKey,
            Action<PsdHierarchyApplyStage> failureInjector)
        {
            if (root == null) throw new ArgumentNullException("root");
            if (plan == null) throw new ArgumentNullException("plan");
            if (registry == null) throw new ArgumentNullException("registry");
            if (existingGroupsByKey == null) throw new ArgumentNullException("existingGroupsByKey");

            Dictionary<string, RectTransform> leaves = ReadRegistry(root, registry);
            Dictionary<string, PsdHierarchyPlanGroup> groups = ReadPlanGroups(plan, leaves);
            // Materialize before any mutation so unknown parents and cycles are
            // rejected while the graph is still untouched.
            TopologicalGroups(groups).ToList();
            Dictionary<string, RectTransform> ownedGroups = ReadOwnedGroups(root, groups, leaves, existingGroupsByKey);
            Dictionary<RectTransform, string> oldKeysByTransform = ownedGroups.ToDictionary(pair => pair.Value, pair => pair.Key);
            foreach (RectTransform leaf in leaves.Values) ValidateMovable(root, leaf);

            PsdHierarchyApplySnapshot verification = PsdHierarchyApplyVerifier.Capture(root, leaves, ownedGroups.Values);
            List<TransformState> rollbackStates = CaptureGraph(root);
            var created = new List<RectTransform>();
            var result = new PsdHierarchyApplyResult();
            var groupRanks = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, RectTransform> logicalRootParents = CaptureLogicalRootParents(groups, ownedGroups, oldKeysByTransform);

            try
            {
                EvacuateDisappearedGroups(
                    groups, ownedGroups, oldKeysByTransform, rollbackStates, failureInjector);

                foreach (PsdHierarchyPlanGroup rootGroup in groups.Values
                             .Where(group => string.IsNullOrEmpty(group.parentKey))
                             .OrderBy(group => group.key, StringComparer.Ordinal))
                {
                    ResolveGroupChildFirst(
                        rootGroup.key,
                        groups,
                        ownedGroups,
                        leaves,
                        rollbackStates,
                        result,
                        groupRanks,
                        created,
                        failureInjector);
                }

                EnforcePlanGroupParents(
                    groups, result.groupsByKey, logicalRootParents, oldKeysByTransform, rollbackStates, groupRanks);
                EvacuateRemovedMembersFromSurvivingGroups(
                    groups, ownedGroups, oldKeysByTransform, leaves, rollbackStates, result.groupsByKey);

                foreach (PsdHierarchyPlanRename rename in plan.renames ?? new List<PsdHierarchyPlanRename>())
                    leaves[rename.stableId].name = rename.name;

                Invoke(failureInjector, PsdHierarchyApplyStage.BeforeVerification);
                VerifyHierarchyConformance(
                    groups, leaves, ownedGroups, result.groupsByKey, logicalRootParents);
                ValidateNoSerializedReferencesToDisappearedGroups(root, groups, ownedGroups);
                PsdHierarchyApplyVerifier.VerifyUnchanged(verification, root, leaves);

                foreach (KeyValuePair<string, RectTransform> oldGroup in ownedGroups
                             .Where(pair => !groups.ContainsKey(pair.Key))
                             .OrderByDescending(pair => Depth(pair.Value)))
                {
                    if (oldGroup.Value.childCount != 0)
                        Fail("Disappeared generated group '" + oldGroup.Key + "' was not empty after verification.");
                    UnityEngine.Object.DestroyImmediate(oldGroup.Value.gameObject);
                }
                return result;
            }
            catch
            {
                Rollback(rollbackStates, created);
                throw;
            }
        }

        private static Dictionary<string, RectTransform> ReadRegistry(
            RectTransform root,
            IReadOnlyDictionary<string, RectTransform> registry)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var objects = new HashSet<RectTransform>();
            foreach (KeyValuePair<string, RectTransform> pair in registry)
            {
                if (!PsdStableLayerIdUtility.IsPersistable(pair.Key) || pair.Value == null)
                    Fail("Registry contains a missing or unstable PSD layer identity.");
                if (pair.Value != root && !pair.Value.IsChildOf(root)) Fail("Registry member is outside the requested Prefab root.");
                if (!result.TryAdd(pair.Key, pair.Value)) Fail("Registry contains duplicate stable ID '" + pair.Key + "'.");
                if (!objects.Add(pair.Value)) Fail("Different stable IDs cannot point to the same RectTransform.");
            }
            return result;
        }

        private static Dictionary<string, PsdHierarchyPlanGroup> ReadPlanGroups(
            PsdHierarchyPlan plan,
            Dictionary<string, RectTransform> leaves)
        {
            var result = new Dictionary<string, PsdHierarchyPlanGroup>(StringComparer.Ordinal);
            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (group == null || string.IsNullOrWhiteSpace(group.key) || string.IsNullOrWhiteSpace(group.displayName))
                    Fail("Plan contains an invalid group.");
                if (!result.TryAdd(group.key, group)) Fail("Plan contains duplicate group key '" + group.key + "'.");
                foreach (string id in group.memberStableIds ?? new List<string>())
                    if (!leaves.ContainsKey(id) || !owners.Add(id)) Fail("Plan contains an unknown or multiply-owned member '" + id + "'.");
            }
            foreach (PsdHierarchyPlanGroup group in result.Values)
                if (!string.IsNullOrEmpty(group.parentKey) && !result.ContainsKey(group.parentKey))
                    Fail("Plan references unknown parent group '" + group.parentKey + "'.");
            foreach (PsdHierarchyPlanRename rename in plan.renames ?? new List<PsdHierarchyPlanRename>())
                if (rename == null || !leaves.ContainsKey(rename.stableId) || string.IsNullOrWhiteSpace(rename.name))
                    Fail("Plan contains an invalid rename.");
            return result;
        }

        private static Dictionary<string, RectTransform> ReadOwnedGroups(
            RectTransform root,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> leaves,
            IReadOnlyDictionary<string, RectTransform> source)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var objects = new HashSet<RectTransform>();
            var leafObjects = new HashSet<RectTransform>(leaves.Values);
            foreach (KeyValuePair<string, RectTransform> pair in source)
            {
                RectTransform value = pair.Value;
                if (string.IsNullOrWhiteSpace(pair.Key) || value == null || value == root || !value.IsChildOf(root))
                    Fail("Existing group identity '" + pair.Key + "' is invalid for this plan.");
                if (leafObjects.Contains(value) || !objects.Add(value)) Fail("Existing group identities must be one-to-one and cannot claim PSD leaves.");
                Component[] components = value.GetComponents<Component>();
                if (components.Length != 1 || !(components[0] is RectTransform))
                    Fail("Existing organizer group '" + pair.Key + "' contains a project-owned component.");
                result.Add(pair.Key, value);
            }
            return result;
        }

        private static IEnumerable<PsdHierarchyPlanGroup> TopologicalGroups(Dictionary<string, PsdHierarchyPlanGroup> groups)
        {
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            while (emitted.Count < groups.Count)
            {
                List<PsdHierarchyPlanGroup> ready = groups.Values.Where(group => !emitted.Contains(group.key) &&
                    (string.IsNullOrEmpty(group.parentKey) || emitted.Contains(group.parentKey)))
                    .OrderBy(group => group.key, StringComparer.Ordinal).ToList();
                if (ready.Count == 0) Fail("Plan group hierarchy contains a cycle.");
                foreach (PsdHierarchyPlanGroup group in ready) { emitted.Add(group.key); yield return group; }
            }
        }

        private static Dictionary<string, RectTransform> CaptureLogicalRootParents(
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> oldGroups,
            Dictionary<RectTransform, string> oldKeysByTransform)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in groups.Values.Where(item => string.IsNullOrEmpty(item.parentKey)))
            {
                RectTransform existing;
                if (oldGroups.TryGetValue(group.key, out existing))
                    result[group.key] = FindNearestNonGeneratedParent(existing.parent, oldKeysByTransform);
            }
            return result;
        }

        private static void EvacuateDisappearedGroups(
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> oldGroups,
            Dictionary<RectTransform, string> oldKeysByTransform,
            List<TransformState> rollbackStates,
            Action<PsdHierarchyApplyStage> failureInjector)
        {
            foreach (KeyValuePair<string, RectTransform> oldGroup in oldGroups
                         .Where(pair => !groups.ContainsKey(pair.Key))
                         .OrderByDescending(pair => Depth(pair.Value)))
            {
                RectTransform target = FindPromotionParent(oldGroup.Value, groups, oldGroups, oldKeysByTransform);
                Transform[] children = Enumerable.Range(0, oldGroup.Value.childCount)
                    .Select(oldGroup.Value.GetChild).ToArray();
                foreach (Transform child in children)
                {
                    // A disappeared generated child container is evacuated by
                    // its own pass and then destroyed; never preserve the shell.
                    RectTransform childRect = child as RectTransform;
                    string childOldKey;
                    if (childRect != null && oldKeysByTransform.TryGetValue(childRect, out childOldKey) &&
                        !groups.ContainsKey(childOldKey)) continue;
                    MovePreservingState(child, target, rollbackStates);
                    Invoke(failureInjector, PsdHierarchyApplyStage.MemberMoved);
                }
            }
        }

        private static void EnforcePlanGroupParents(
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> planTransforms,
            Dictionary<string, RectTransform> logicalRootParents,
            Dictionary<RectTransform, string> oldKeysByTransform,
            List<TransformState> rollbackStates,
            Dictionary<string, int> groupRanks)
        {
            foreach (PsdHierarchyPlanGroup group in TopologicalGroups(groups))
            {
                RectTransform transform = planTransforms[group.key];
                RectTransform desired;
                if (!string.IsNullOrEmpty(group.parentKey))
                {
                    desired = planTransforms[group.parentKey];
                }
                else if (!logicalRootParents.TryGetValue(group.key, out desired))
                {
                    desired = FindNearestNonGeneratedParent(transform.parent, oldKeysByTransform);
                    logicalRootParents[group.key] = desired;
                }
                if (transform.parent != desired) transform.SetParent(desired, false);
                ConfigureIdentityContainer(transform);
                SetSiblingByOriginalRank(
                    transform, desired, groupRanks[group.key], rollbackStates, planTransforms, groupRanks);
            }
        }

        private static void EvacuateRemovedMembersFromSurvivingGroups(
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> oldGroups,
            Dictionary<RectTransform, string> oldKeysByTransform,
            Dictionary<string, RectTransform> leaves,
            List<TransformState> rollbackStates,
            Dictionary<string, RectTransform> planTransforms)
        {
            var stableIdByTransform = leaves.ToDictionary(pair => pair.Value, pair => pair.Key);
            foreach (KeyValuePair<string, RectTransform> oldGroup in oldGroups.Where(pair => groups.ContainsKey(pair.Key)))
            {
                var allowedMembers = new HashSet<string>(groups[oldGroup.Key].memberStableIds ?? new List<string>(), StringComparer.Ordinal);
                Transform[] children = Enumerable.Range(0, oldGroup.Value.childCount).Select(oldGroup.Value.GetChild).ToArray();
                foreach (Transform child in children)
                {
                    RectTransform rect = child as RectTransform;
                    string stableId;
                    if (rect == null || !stableIdByTransform.TryGetValue(rect, out stableId) || allowedMembers.Contains(stableId))
                        continue;

                    // A leaf explicitly assigned elsewhere was already moved by
                    // ResolveGroupChildFirst. Remaining leaves were removed from
                    // this group and must be promoted above their old owner.
                    RectTransform target = FindPlanAncestorOrLogicalParent(
                        groups[oldGroup.Key].parentKey, oldGroup.Value, groups, planTransforms, oldKeysByTransform);
                    MovePreservingState(rect, target, rollbackStates);
                }
            }
        }

        private static RectTransform FindPromotionParent(
            RectTransform oldGroup,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> oldGroups,
            Dictionary<RectTransform, string> oldKeysByTransform)
        {
            for (Transform cursor = oldGroup.parent; cursor != null; cursor = cursor.parent)
            {
                RectTransform rect = cursor as RectTransform;
                string key;
                if (rect != null && oldKeysByTransform.TryGetValue(rect, out key))
                {
                    if (groups.ContainsKey(key)) return oldGroups[key];
                    continue;
                }
                if (rect != null) return rect;
            }
            Fail("Cannot find a safe non-generated parent for disappeared group.");
            return null;
        }

        private static RectTransform FindPlanAncestorOrLogicalParent(
            string parentKey,
            RectTransform oldOwner,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> planTransforms,
            Dictionary<RectTransform, string> oldKeysByTransform)
        {
            string current = parentKey;
            while (!string.IsNullOrEmpty(current))
            {
                RectTransform target;
                if (planTransforms.TryGetValue(current, out target)) return target;
                PsdHierarchyPlanGroup group;
                current = groups.TryGetValue(current, out group) ? group.parentKey : string.Empty;
            }
            return FindNearestNonGeneratedParent(oldOwner.parent, oldKeysByTransform);
        }

        private static RectTransform FindNearestNonGeneratedParent(
            Transform start,
            Dictionary<RectTransform, string> oldKeysByTransform)
        {
            for (Transform cursor = start; cursor != null; cursor = cursor.parent)
            {
                RectTransform rect = cursor as RectTransform;
                if (rect != null && !oldKeysByTransform.ContainsKey(rect)) return rect;
            }
            Fail("Cannot resolve the logical non-generated hierarchy parent.");
            return null;
        }

        private static void MovePreservingState(
            Transform child,
            RectTransform target,
            List<TransformState> rollbackStates)
        {
            TransformState state = rollbackStates.First(item => item.transform == child);
            child.SetParent(target, false);
            RestoreLocalState(state);
            int rank = rollbackStates.IndexOf(state);
            int destinationIndex = Enumerable.Range(0, target.childCount)
                .Select(target.GetChild)
                .Where(item => item != child)
                .Count(item => OriginalRank(item, rollbackStates, null, null) < rank);
            child.SetSiblingIndex(destinationIndex);
        }

        private static void OrderChildrenByOriginalRank(
            RectTransform parent,
            List<TransformState> states,
            Dictionary<string, RectTransform> planGroups,
            Dictionary<string, int> groupRanks)
        {
            Transform[] children = Enumerable.Range(0, parent.childCount).Select(parent.GetChild)
                .OrderBy(child => OriginalRank(child, states, planGroups, groupRanks)).ToArray();
            for (int index = 0; index < children.Length; index++) children[index].SetSiblingIndex(index);
        }

        private static void SetSiblingByOriginalRank(
            Transform child,
            Transform parent,
            int rank,
            List<TransformState> states,
            Dictionary<string, RectTransform> planGroups,
            Dictionary<string, int> groupRanks)
        {
            int index = Enumerable.Range(0, parent.childCount).Select(parent.GetChild)
                .Where(item => item != child)
                .Count(item => OriginalRank(item, states, planGroups, groupRanks) < rank);
            child.SetSiblingIndex(index);
        }

        private static int OriginalRank(
            Transform transform,
            List<TransformState> states,
            Dictionary<string, RectTransform> planGroups,
            Dictionary<string, int> groupRanks)
        {
            if (planGroups != null && groupRanks != null)
            {
                foreach (KeyValuePair<string, RectTransform> pair in planGroups)
                    if (pair.Value == transform) return groupRanks[pair.Key];
            }
            int original = states.FindIndex(state => state.transform == transform);
            if (original >= 0) return original;
            return int.MaxValue;
        }

        private static void VerifyHierarchyConformance(
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> leaves,
            Dictionary<string, RectTransform> oldGroups,
            Dictionary<string, RectTransform> resultGroups,
            Dictionary<string, RectTransform> logicalRootParents)
        {
            if (!new HashSet<string>(groups.Keys, StringComparer.Ordinal).SetEquals(resultGroups.Keys))
                Fail("Apply result does not exactly match the current plan group keys.");
            foreach (PsdHierarchyPlanGroup group in groups.Values)
            {
                RectTransform expectedParent = string.IsNullOrEmpty(group.parentKey)
                    ? logicalRootParents[group.key]
                    : resultGroups[group.parentKey];
                if (resultGroups[group.key].parent != expectedParent)
                    Fail("Group '" + group.key + "' does not conform to parentKey.");
                foreach (string member in group.memberStableIds ?? new List<string>())
                    if (leaves[member].parent != resultGroups[group.key])
                        Fail("Plan member '" + member + "' is not a direct child of group '" + group.key + "'.");
            }
            var disappeared = new HashSet<RectTransform>(oldGroups.Where(pair => !groups.ContainsKey(pair.Key))
                .Select(pair => pair.Value));
            foreach (RectTransform old in disappeared)
                if (!ContainsOnlyDisappearedEmptyShells(old, disappeared, new HashSet<RectTransform>()))
                    Fail("A disappeared generated group still owns a non-generated or non-empty child.");
            foreach (RectTransform leaf in leaves.Values)
                for (Transform cursor = leaf.parent; cursor != null; cursor = cursor.parent)
                    if (cursor is RectTransform && disappeared.Contains((RectTransform)cursor))
                        Fail("A PSD leaf retains disappeared generated ancestry.");
        }

        private static bool ContainsOnlyDisappearedEmptyShells(
            RectTransform group,
            HashSet<RectTransform> disappeared,
            HashSet<RectTransform> visiting)
        {
            if (!visiting.Add(group)) return false;
            for (int index = 0; index < group.childCount; index++)
            {
                RectTransform child = group.GetChild(index) as RectTransform;
                if (child == null || !disappeared.Contains(child) ||
                    !ContainsOnlyDisappearedEmptyShells(child, disappeared, visiting))
                    return false;
            }
            visiting.Remove(group);
            return true;
        }

        private static void ValidateNoSerializedReferencesToDisappearedGroups(
            RectTransform root,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> oldGroups)
        {
            var forbidden = new HashSet<UnityEngine.Object>();
            foreach (KeyValuePair<string, RectTransform> pair in oldGroups.Where(item => !groups.ContainsKey(item.Key)))
            {
                forbidden.Add(pair.Value.gameObject);
                foreach (Component component in pair.Value.GetComponents<Component>()) forbidden.Add(component);
            }
            if (forbidden.Count == 0) return;

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || forbidden.Contains(component)) continue;
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                // Next(true) performs a complete depth-first traversal through
                // hidden fields, arrays/lists, and nested serializable objects.
                // We only reject exact references in the forbidden target set;
                // unrelated fields such as m_Script remain harmless.
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null && forbidden.Contains(property.objectReferenceValue))
                    {
                        Fail("Project component '" + component.GetType().FullName +
                             "' still references a disappeared generated group at property '" + property.propertyPath + "'.");
                    }
                }
            }
        }

        /// <summary>
        /// Resolves direct child groups first, then treats those groups and this
        /// group's direct PSD members as opaque atomic children. This is the
        /// crucial distinction from flattening descendant leaves: an existing
        /// inner group can remain intact while a new outer group wraps it.
        /// </summary>
        private static RectTransform ResolveGroupChildFirst(
            string groupKey,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> ownedGroups,
            Dictionary<string, RectTransform> leaves,
            List<TransformState> rollbackStates,
            PsdHierarchyApplyResult result,
            Dictionary<string, int> groupRanks,
            List<RectTransform> created,
            Action<PsdHierarchyApplyStage> failureInjector)
        {
            RectTransform alreadyResolved;
            if (result.groupsByKey.TryGetValue(groupKey, out alreadyResolved)) return alreadyResolved;

            PsdHierarchyPlanGroup group = groups[groupKey];
            List<PsdHierarchyPlanGroup> directChildGroups = groups.Values
                .Where(candidate => string.Equals(candidate.parentKey, groupKey, StringComparison.Ordinal))
                .OrderBy(candidate => candidate.key, StringComparer.Ordinal).ToList();
            foreach (PsdHierarchyPlanGroup child in directChildGroups)
            {
                ResolveGroupChildFirst(
                    child.key, groups, ownedGroups, leaves, rollbackStates, result, groupRanks, created, failureInjector);
            }

            var atoms = new List<AtomicChild>();
            foreach (string stableId in group.memberStableIds ?? new List<string>())
            {
                RectTransform leaf = leaves[stableId];
                TransformState state = rollbackStates.First(item => item.transform == leaf);
                atoms.Add(new AtomicChild { transform = leaf, rank = rollbackStates.IndexOf(state), authoredLeafState = state });
            }
            foreach (PsdHierarchyPlanGroup child in directChildGroups)
            {
                atoms.Add(new AtomicChild
                {
                    transform = result.groupsByKey[child.key],
                    rank = groupRanks[child.key]
                });
            }
            if (atoms.Count == 0) Fail("Group '" + groupKey + "' has no direct member or direct child group.");

            RectTransform container;
            bool reused = ownedGroups.TryGetValue(groupKey, out container);
            int groupRank = atoms.Min(atom => atom.rank);
            // Identity comes from the Profile mapping, while draw order comes
            // from the minimum planned descendant visual/member.
            if (!reused)
            {
                Transform[] parents = atoms.Select(atom => atom.transform.parent).Distinct().ToArray();
                if (parents.Length != 1 || !(parents[0] is RectTransform))
                    Fail("Group '" + groupKey + "' atomic children do not share one current RectTransform parent.");
                container = CreateContainer((RectTransform)parents[0], group.displayName);
                created.Add(container);
                result.createdGroupKeys.Add(groupKey);
            }

            container.name = group.displayName;
            ConfigureIdentityContainer(container);
            result.groupsByKey.Add(groupKey, container);
            groupRanks.Add(groupKey, groupRank);
            Invoke(failureInjector, PsdHierarchyApplyStage.GroupPrepared);

            foreach (AtomicChild atom in atoms.OrderBy(atom => atom.rank))
            {
                if (atom.transform.parent != container) atom.transform.SetParent(container, false);
                if (atom.authoredLeafState != null)
                {
                    RestoreLocalState(atom.authoredLeafState);
                }
                else
                {
                    ConfigureIdentityContainer(atom.transform);
                }
                Invoke(failureInjector, PsdHierarchyApplyStage.MemberMoved);
            }
            OrderChildrenByOriginalRank(container, rollbackStates, result.groupsByKey, groupRanks);
            SetSiblingByOriginalRank(container, container.parent, groupRank, rollbackStates, result.groupsByKey, groupRanks);
            return container;
        }

        private static RectTransform CreateContainer(RectTransform parent, string name)
        {
            if (parent == null) Fail("Cannot create an organizer group without a RectTransform parent.");
            RectTransform value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(parent, false);
            ConfigureIdentityContainer(value);
            return value;
        }

        private static void ConfigureIdentityContainer(RectTransform value)
        {
            value.anchorMin = Vector2.zero;
            value.anchorMax = Vector2.one;
            value.pivot = new Vector2(0.5f, 0.5f);
            value.sizeDelta = Vector2.zero;
            value.anchoredPosition3D = Vector3.zero;
            value.offsetMin = Vector2.zero;
            value.offsetMax = Vector2.zero;
            value.localRotation = Quaternion.identity;
            value.localScale = Vector3.one;
        }

        private static List<TransformState> CaptureGraph(RectTransform root)
        {
            return root.GetComponentsInChildren<Transform>(true).Select(Capture).ToList();
        }

        private static TransformState Capture(Transform transform)
        {
            var state = new TransformState
            {
                transform = transform,
                parent = transform.parent,
                siblingIndex = transform.GetSiblingIndex(),
                depth = Depth(transform),
                name = transform.name,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale
            };
            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                state.isRect = true;
                state.anchorMin = rect.anchorMin;
                state.anchorMax = rect.anchorMax;
                state.pivot = rect.pivot;
                state.anchoredPosition = rect.anchoredPosition;
                state.anchoredPosition3D = rect.anchoredPosition3D;
                state.sizeDelta = rect.sizeDelta;
                state.offsetMin = rect.offsetMin;
                state.offsetMax = rect.offsetMax;
            }
            return state;
        }

        private static int Depth(Transform transform)
        {
            int result = 0;
            for (Transform cursor = transform.parent; cursor != null; cursor = cursor.parent) result++;
            return result;
        }

        private static void RestoreLocalState(TransformState state)
        {
            RectTransform rect = state.transform as RectTransform;
            if (state.isRect && rect != null)
            {
                rect.anchorMin = state.anchorMin;
                rect.anchorMax = state.anchorMax;
                rect.pivot = state.pivot;
                rect.offsetMin = state.offsetMin;
                rect.offsetMax = state.offsetMax;
                rect.sizeDelta = state.sizeDelta;
                rect.anchoredPosition3D = state.anchoredPosition3D;
                rect.anchoredPosition = state.anchoredPosition;
                state.transform.localPosition = new Vector3(
                    state.transform.localPosition.x,
                    state.transform.localPosition.y,
                    state.localPosition.z);
            }
            else
            {
                state.transform.localPosition = state.localPosition;
            }
            state.transform.localRotation = state.localRotation;
            state.transform.localScale = state.localScale;
        }

        private static void Rollback(List<TransformState> states, List<RectTransform> created)
        {
            foreach (TransformState state in states.OrderBy(state => state.depth))
                if (state.transform != null && state.transform.parent != state.parent) state.transform.SetParent(state.parent, false);
            foreach (TransformState state in states.Where(state => state.transform != null))
            {
                state.transform.name = state.name;
                RestoreLocalState(state);
            }
            foreach (IGrouping<Transform, TransformState> siblings in states.Where(state => state.transform != null && state.parent != null)
                         .GroupBy(state => state.parent))
                foreach (TransformState state in siblings.OrderBy(state => state.siblingIndex)) state.transform.SetSiblingIndex(state.siblingIndex);
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) UnityEngine.Object.DestroyImmediate(created[index].gameObject);
        }

        private static void ValidateMovable(RectTransform root, RectTransform leaf)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(leaf.gameObject)) Fail("Cannot move a nested Prefab member: '" + leaf.name + "'.");
            for (Transform cursor = leaf; cursor != null && cursor != root; cursor = cursor.parent)
            {
                if (cursor.GetComponent<Canvas>() != null || cursor.GetComponent<Mask>() != null ||
                    cursor.GetComponent<RectMask2D>() != null || cursor.GetComponent<Selectable>() != null ||
                    cursor.GetComponent<Animator>() != null) Fail("Cannot cross protected UI boundary at '" + cursor.name + "'.");
                foreach (Component component in cursor.GetComponents<Component>())
                    if (!IsAllowedGeneratedComponent(component)) Fail("Cannot cross project-owned component boundary at '" + cursor.name + "'.");
            }
        }

        private static bool IsAllowedGeneratedComponent(Component component)
        {
            return component is RectTransform || component is CanvasRenderer || component is Graphic ||
                   component is BaseMeshEffect || component is AspectRatioFitter;
        }

        private static void Invoke(Action<PsdHierarchyApplyStage> injector, PsdHierarchyApplyStage stage)
        {
            if (injector != null) injector(stage);
        }

        private static void Fail(string message)
        {
            throw new PsdHierarchyApplyException(message);
        }
    }
}
