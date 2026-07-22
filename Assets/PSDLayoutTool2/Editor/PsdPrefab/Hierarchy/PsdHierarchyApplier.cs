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

    /// <summary>
    /// Applies an already validated hierarchy plan to existing RectTransforms.
    /// It only creates component-free RectTransform containers, reparents the
    /// same leaf objects, restores their local rectangles, and applies approved
    /// names. It never copies candidate objects or writes visual components.
    /// </summary>
    public static class PsdHierarchyApplier
    {
        private sealed class LeafState
        {
            public RectTransform rect;
            public int originalSibling;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        public static void Apply(
            RectTransform root,
            PsdHierarchyPlan plan,
            IReadOnlyDictionary<string, RectTransform> registry)
        {
            if (root == null) throw new ArgumentNullException("root");
            if (plan == null) throw new ArgumentNullException("plan");
            if (registry == null) throw new ArgumentNullException("registry");

            Dictionary<string, RectTransform> leaves = ReadRegistry(root, registry);
            ValidatePlanReferences(plan, leaves);
            foreach (RectTransform leaf in leaves.Values.Distinct()) ValidateMovable(root, leaf);

            PsdHierarchyApplySnapshot before = PsdHierarchyApplyVerifier.Capture(root, leaves);
            Dictionary<RectTransform, LeafState> states = leaves.Values.Distinct().ToDictionary(value => value, Capture);
            Dictionary<string, PsdHierarchyPlanGroup> groups = (plan.groups ?? new List<PsdHierarchyPlanGroup>())
                .ToDictionary(group => group.key, StringComparer.Ordinal);
            var groupTransforms = new Dictionary<string, RectTransform>(StringComparer.Ordinal);

            foreach (PsdHierarchyPlanGroup group in TopologicalGroups(groups))
            {
                RectTransform container;
                RectTransform parent;
                if (string.IsNullOrEmpty(group.parentKey))
                {
                    container = FindReusableContainerInSubtree(root, group.displayName, leaves.Values);
                    parent = container != null
                        ? container.parent as RectTransform
                        : FindCommonOriginalParent(group.key, groups, leaves, states);
                }
                else
                {
                    parent = groupTransforms[group.parentKey];
                    container = FindReusableContainer(parent, group.displayName, leaves.Values);
                }

                if (parent == null || (parent != root && !parent.IsChildOf(root)))
                    throw new PsdHierarchyApplyException("Cannot resolve a safe parent for group '" + group.key + "'.");
                if (container == null) container = CreateContainer(parent, group.displayName);
                groupTransforms.Add(group.key, container);
            }

            // All containers use an identity, full-stretch rectangle. Therefore
            // restoring the leaf's exact authored local fields reconstructs the
            // same world rectangle without touching Image/TMP properties.
            foreach (PsdHierarchyPlanGroup group in TopologicalGroups(groups))
            {
                RectTransform container = groupTransforms[group.key];
                foreach (string stableId in group.memberStableIds)
                {
                    RectTransform leaf = leaves[stableId];
                    LeafState state = states[leaf];
                    leaf.SetParent(container, false);
                    Restore(state);
                }
            }

            RestoreVisualOrder(groups, groupTransforms, leaves, states);
            foreach (PsdHierarchyPlanRename rename in plan.renames ?? new List<PsdHierarchyPlanRename>())
                leaves[rename.stableId].name = rename.name;

            PsdHierarchyApplyVerifier.VerifyUnchanged(before, root, leaves);
        }

        private static Dictionary<string, RectTransform> ReadRegistry(
            RectTransform root,
            IReadOnlyDictionary<string, RectTransform> registry)
        {
            var result = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, RectTransform> pair in registry)
            {
                if (!PsdStableLayerIdUtility.IsPersistable(pair.Key) || pair.Value == null)
                    throw new PsdHierarchyApplyException("Registry contains a missing or unstable PSD layer identity.");
                if (pair.Value != root && !pair.Value.IsChildOf(root))
                    throw new PsdHierarchyApplyException("Registry member is outside the requested Prefab root.");
                if (!result.TryAdd(pair.Key, pair.Value))
                    throw new PsdHierarchyApplyException("Registry contains duplicate stable ID '" + pair.Key + "'.");
            }
            return result;
        }

        private static void ValidatePlanReferences(PsdHierarchyPlan plan, Dictionary<string, RectTransform> leaves)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
            {
                if (group == null || string.IsNullOrWhiteSpace(group.key) || string.IsNullOrWhiteSpace(group.displayName))
                    throw new PsdHierarchyApplyException("Plan contains an invalid group.");
                if (!keys.Add(group.key)) throw new PsdHierarchyApplyException("Plan contains duplicate group key '" + group.key + "'.");
                foreach (string id in group.memberStableIds ?? new List<string>())
                {
                    if (!leaves.ContainsKey(id) || !owners.Add(id))
                        throw new PsdHierarchyApplyException("Plan contains an unknown or multiply-owned member '" + id + "'.");
                }
            }
            foreach (PsdHierarchyPlanGroup group in plan.groups ?? new List<PsdHierarchyPlanGroup>())
                if (!string.IsNullOrEmpty(group.parentKey) && !keys.Contains(group.parentKey))
                    throw new PsdHierarchyApplyException("Plan references unknown parent group '" + group.parentKey + "'.");
            foreach (PsdHierarchyPlanRename rename in plan.renames ?? new List<PsdHierarchyPlanRename>())
                if (rename == null || !leaves.ContainsKey(rename.stableId) || string.IsNullOrWhiteSpace(rename.name))
                    throw new PsdHierarchyApplyException("Plan contains an invalid rename.");
        }

        private static IEnumerable<PsdHierarchyPlanGroup> TopologicalGroups(
            Dictionary<string, PsdHierarchyPlanGroup> groups)
        {
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            while (emitted.Count < groups.Count)
            {
                List<PsdHierarchyPlanGroup> ready = groups.Values
                    .Where(group => !emitted.Contains(group.key) &&
                                    (string.IsNullOrEmpty(group.parentKey) || emitted.Contains(group.parentKey)))
                    .OrderBy(group => group.key, StringComparer.Ordinal).ToList();
                if (ready.Count == 0) throw new PsdHierarchyApplyException("Plan group hierarchy contains a cycle.");
                foreach (PsdHierarchyPlanGroup group in ready)
                {
                    emitted.Add(group.key);
                    yield return group;
                }
            }
        }

        private static RectTransform FindReusableContainer(
            RectTransform parent,
            string displayName,
            IEnumerable<RectTransform> registeredLeaves)
        {
            var leafSet = new HashSet<RectTransform>(registeredLeaves);
            RectTransform match = null;
            for (int index = 0; index < parent.childCount; index++)
            {
                RectTransform child = parent.GetChild(index) as RectTransform;
                if (child == null || child.name != displayName || leafSet.Contains(child) || !IsOrganizerOnlyContainer(child)) continue;
                if (match != null) throw new PsdHierarchyApplyException("Multiple reusable groups named '" + displayName + "'.");
                match = child;
            }
            return match;
        }

        private static RectTransform FindReusableContainerInSubtree(
            RectTransform root,
            string displayName,
            IEnumerable<RectTransform> registeredLeaves)
        {
            var leafSet = new HashSet<RectTransform>(registeredLeaves);
            RectTransform match = null;
            foreach (RectTransform candidate in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (candidate == root || candidate.name != displayName || leafSet.Contains(candidate) ||
                    !IsOrganizerOnlyContainer(candidate)) continue;
                if (match != null) throw new PsdHierarchyApplyException("Multiple reusable groups named '" + displayName + "'.");
                match = candidate;
            }
            return match;
        }

        private static RectTransform FindCommonOriginalParent(
            string groupKey,
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> leaves,
            Dictionary<RectTransform, LeafState> states)
        {
            var descendantKeys = new HashSet<string>(StringComparer.Ordinal) { groupKey };
            bool changed;
            do
            {
                changed = false;
                foreach (PsdHierarchyPlanGroup candidate in groups.Values)
                    if (descendantKeys.Contains(candidate.parentKey) && descendantKeys.Add(candidate.key)) changed = true;
            } while (changed);

            Transform[] parents = groups.Values.Where(group => descendantKeys.Contains(group.key))
                .SelectMany(group => group.memberStableIds)
                .Select(id => states[leaves[id]].rect.parent)
                .Distinct().ToArray();
            if (parents.Length != 1)
                throw new PsdHierarchyApplyException("Group '" + groupKey + "' would move members from multiple parents.");
            return parents[0] as RectTransform;
        }

        private static bool IsOrganizerOnlyContainer(RectTransform value)
        {
            Component[] components = value.GetComponents<Component>();
            return components.Length == 1 && components[0] is RectTransform;
        }

        private static RectTransform CreateContainer(RectTransform parent, string name)
        {
            var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(parent, false);
            value.anchorMin = Vector2.zero;
            value.anchorMax = Vector2.one;
            value.pivot = new Vector2(0.5f, 0.5f);
            value.anchoredPosition = Vector2.zero;
            value.sizeDelta = Vector2.zero;
            value.localRotation = Quaternion.identity;
            value.localScale = Vector3.one;
            return value;
        }

        private static LeafState Capture(RectTransform rect)
        {
            return new LeafState
            {
                rect = rect,
                originalSibling = rect.GetSiblingIndex(),
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta,
                localRotation = rect.localRotation,
                localScale = rect.localScale
            };
        }

        private static void Restore(LeafState state)
        {
            RectTransform rect = state.rect;
            rect.anchorMin = state.anchorMin;
            rect.anchorMax = state.anchorMax;
            rect.pivot = state.pivot;
            rect.anchoredPosition = state.anchoredPosition;
            rect.sizeDelta = state.sizeDelta;
            rect.localRotation = state.localRotation;
            rect.localScale = state.localScale;
        }

        private static void RestoreVisualOrder(
            Dictionary<string, PsdHierarchyPlanGroup> groups,
            Dictionary<string, RectTransform> groupTransforms,
            Dictionary<string, RectTransform> leaves,
            Dictionary<RectTransform, LeafState> states)
        {
            var order = leaves.ToDictionary(pair => pair.Key, pair => states[pair.Value].originalSibling, StringComparer.Ordinal);
            var minimum = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (PsdHierarchyPlanGroup group in TopologicalGroups(groups).Reverse())
            {
                IEnumerable<int> direct = group.memberStableIds.Select(id => order[id]);
                IEnumerable<int> children = groups.Values.Where(child => child.parentKey == group.key).Select(child => minimum[child.key]);
                minimum[group.key] = direct.Concat(children).DefaultIfEmpty(int.MaxValue).Min();

                var childOrder = new List<Tuple<int, Transform>>();
                childOrder.AddRange(group.memberStableIds.Select(id => Tuple.Create(order[id], (Transform)leaves[id])));
                childOrder.AddRange(groups.Values.Where(child => child.parentKey == group.key)
                    .Select(child => Tuple.Create(minimum[child.key], (Transform)groupTransforms[child.key])));
                int index = 0;
                foreach (Tuple<int, Transform> item in childOrder.OrderBy(item => item.Item1)) item.Item2.SetSiblingIndex(index++);
            }

            foreach (PsdHierarchyPlanGroup group in groups.Values.Where(group => string.IsNullOrEmpty(group.parentKey))
                         .OrderBy(group => minimum[group.key]))
            {
                RectTransform container = groupTransforms[group.key];
                container.SetSiblingIndex(Mathf.Clamp(minimum[group.key], 0, container.parent.childCount - 1));
            }
        }

        private static void ValidateMovable(RectTransform root, RectTransform leaf)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(leaf.gameObject))
                throw new PsdHierarchyApplyException("Cannot move a nested Prefab member: '" + leaf.name + "'.");
            for (Transform cursor = leaf; cursor != null && cursor != root; cursor = cursor.parent)
            {
                if (cursor.GetComponent<Canvas>() != null || cursor.GetComponent<Mask>() != null ||
                    cursor.GetComponent<RectMask2D>() != null || cursor.GetComponent<Selectable>() != null ||
                    cursor.GetComponent<Animator>() != null)
                    throw new PsdHierarchyApplyException("Cannot cross protected UI boundary at '" + cursor.name + "'.");
                foreach (Component component in cursor.GetComponents<Component>())
                {
                    if (!IsAllowedGeneratedComponent(component))
                        throw new PsdHierarchyApplyException(
                            "Cannot cross project-owned component boundary at '" + cursor.name + "'.");
                }
            }
        }

        private static bool IsAllowedGeneratedComponent(Component component)
        {
            return component is RectTransform || component is CanvasRenderer || component is Graphic ||
                   component is BaseMeshEffect || component is AspectRatioFitter;
        }
    }
}
