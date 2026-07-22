namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Immutable evidence captured before an organizer operation. The snapshot
    /// deliberately tracks the original objects by instance ID: inserting an
    /// empty grouping RectTransform is allowed, replacing a generated leaf is
    /// not. Task 6 also uses this evidence before it commits a Prefab save.
    /// </summary>
    public sealed class PsdHierarchyApplySnapshot
    {
        internal readonly Dictionary<int, PsdHierarchyObjectSnapshot> objects =
            new Dictionary<int, PsdHierarchyObjectSnapshot>();
        internal readonly List<int> visualLeafOrder = new List<int>();
    }

    internal sealed class PsdHierarchyObjectSnapshot
    {
        public GameObject gameObject;
        public bool activeSelf;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Quaternion localRotation;
        public Vector3 localScale;
        public readonly Vector3[] worldCorners = new Vector3[4];
        public string[] componentTypes;
        public string[] componentJson;
    }

    /// <summary>
    /// Captures and verifies the fields outside the hierarchy organizer's
    /// authority. Component JSON includes object references and all Image/TMP
    /// visual state, while RectTransform geometry is compared explicitly.
    /// </summary>
    public static class PsdHierarchyApplyVerifier
    {
        private const float WorldCornerTolerance = 0.01f;

        public static PsdHierarchyApplySnapshot Capture(
            RectTransform root,
            IReadOnlyDictionary<string, RectTransform> registry)
        {
            if (root == null) throw new ArgumentNullException("root");
            if (registry == null) throw new ArgumentNullException("registry");

            var snapshot = new PsdHierarchyApplySnapshot();
            foreach (RectTransform rect in registry.Values.Where(value => value != null).Distinct())
            {
                snapshot.objects.Add(rect.gameObject.GetInstanceID(), CaptureObject(rect));
            }

            // Project-owned components outside moved leaves may hold references
            // to them. Recording every pre-existing object detects accidental
            // serialized reference or component-order changes on those owners.
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!snapshot.objects.ContainsKey(transform.gameObject.GetInstanceID()))
                {
                    RectTransform rect = transform as RectTransform;
                    if (rect != null) snapshot.objects.Add(transform.gameObject.GetInstanceID(), CaptureObject(rect));
                }
            }

            CollectRegisteredLeafOrder(root, new HashSet<int>(registry.Values
                .Where(value => value != null).Select(value => value.gameObject.GetInstanceID())), snapshot.visualLeafOrder);
            return snapshot;
        }

        public static void VerifyUnchanged(
            PsdHierarchyApplySnapshot before,
            RectTransform root,
            IReadOnlyDictionary<string, RectTransform> registry)
        {
            if (before == null) throw new ArgumentNullException("before");
            if (root == null) throw new ArgumentNullException("root");
            if (registry == null) throw new ArgumentNullException("registry");

            foreach (KeyValuePair<int, PsdHierarchyObjectSnapshot> pair in before.objects)
            {
                PsdHierarchyObjectSnapshot expected = pair.Value;
                if (expected.gameObject == null)
                {
                    Fail("The organizer deleted an existing object.");
                }

                RectTransform actual = expected.gameObject.GetComponent<RectTransform>();
                if (actual == null) Fail("The organizer removed an existing RectTransform.");
                if (expected.activeSelf != expected.gameObject.activeSelf) Fail(expected.gameObject, "active state");
                Exact(expected.anchorMin, actual.anchorMin, expected.gameObject, "anchorMin");
                Exact(expected.anchorMax, actual.anchorMax, expected.gameObject, "anchorMax");
                Exact(expected.pivot, actual.pivot, expected.gameObject, "pivot");
                Exact(expected.anchoredPosition, actual.anchoredPosition, expected.gameObject, "anchoredPosition");
                Exact(expected.sizeDelta, actual.sizeDelta, expected.gameObject, "sizeDelta");
                if (expected.localRotation != actual.localRotation) Fail(expected.gameObject, "local rotation");
                if (expected.localScale != actual.localScale) Fail(expected.gameObject, "local scale");

                var corners = new Vector3[4];
                actual.GetWorldCorners(corners);
                for (int index = 0; index < corners.Length; index++)
                {
                    if (Vector3.Distance(expected.worldCorners[index], corners[index]) > WorldCornerTolerance)
                        Fail(expected.gameObject, "world corners");
                }

                Component[] components = expected.gameObject.GetComponents<Component>();
                string[] types = components.Select(ComponentType).ToArray();
                if (!expected.componentTypes.SequenceEqual(types, StringComparer.Ordinal))
                    Fail(expected.gameObject, "component order");
                string[] json = components.Select(ComponentJson).ToArray();
                if (!expected.componentJson.SequenceEqual(json, StringComparer.Ordinal))
                    Fail(expected.gameObject, "serialized component state or references");
            }

            var currentOrder = new List<int>();
            CollectRegisteredLeafOrder(root, new HashSet<int>(registry.Values
                .Where(value => value != null).Select(value => value.gameObject.GetInstanceID())), currentOrder);
            if (!before.visualLeafOrder.SequenceEqual(currentOrder))
                throw new PsdHierarchyApplyException("The organizer changed the original visual leaf order.");
        }

        private static PsdHierarchyObjectSnapshot CaptureObject(RectTransform rect)
        {
            var result = new PsdHierarchyObjectSnapshot
            {
                gameObject = rect.gameObject,
                activeSelf = rect.gameObject.activeSelf,
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta,
                localRotation = rect.localRotation,
                localScale = rect.localScale
            };
            rect.GetWorldCorners(result.worldCorners);
            Component[] components = rect.gameObject.GetComponents<Component>();
            result.componentTypes = components.Select(ComponentType).ToArray();
            result.componentJson = components.Select(ComponentJson).ToArray();
            return result;
        }

        private static string ComponentType(Component component)
        {
            return component == null ? "<missing>" : component.GetType().AssemblyQualifiedName;
        }

        private static string ComponentJson(Component component)
        {
            // RectTransform's serialized parent and sibling data is precisely
            // the organizer-owned change. Its geometry is checked separately.
            return component == null || component is RectTransform
                ? string.Empty
                : EditorJsonUtility.ToJson(component, false);
        }

        private static void CollectRegisteredLeafOrder(Transform parent, HashSet<int> registered, List<int> result)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                int id = child.gameObject.GetInstanceID();
                if (registered.Contains(id)) result.Add(id);
                CollectRegisteredLeafOrder(child, registered, result);
            }
        }

        private static void Exact(Vector2 expected, Vector2 actual, GameObject target, string field)
        {
            if (expected != actual) Fail(target, field);
        }

        private static void Fail(GameObject target, string field)
        {
            throw new PsdHierarchyApplyException("The organizer changed " + field + " on '" + target.name + "'.");
        }

        private static void Fail(string message)
        {
            throw new PsdHierarchyApplyException(message);
        }
    }
}
