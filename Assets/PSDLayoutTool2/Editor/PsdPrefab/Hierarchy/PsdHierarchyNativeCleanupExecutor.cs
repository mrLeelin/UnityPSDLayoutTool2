namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Executes the safe, hierarchy-only subset of a reviewed cleanup plan in
    /// the current Unity Editor process. Complex component and asset operations
    /// are delegated to the uLoop runner only when a reviewed plan actually
    /// needs those operations.
    /// </summary>
    internal static class PsdHierarchyNativeCleanupExecutor
    {
        private static readonly string[] UloopOnlyProperties =
        {
            "textureRenames",
            "spriteAtlasRenames",
            "componentFamilyDecisions",
            "componentExtractions",
            "stateComponentExtractions",
            "variantComponentExtractions",
            "statefulComponentExtractions",
            "requiredComponentFamilies",
            "containmentFindings",
            "containmentResolutions",
        };

        internal static bool RequiresUloopRunner(string planJson)
        {
            try
            {
                var plan = JObject.Parse(planJson ?? string.Empty);
                return UloopOnlyProperties.Any(property =>
                    plan[property] is JArray operations && operations.Count > 0);
            }
            catch
            {
                // Let the native preflight report malformed JSON with its existing
                // diagnostic instead of treating it as a backend-routing decision.
                return false;
            }
        }

        internal static bool TryValidatePlanCapabilities(string planJson, out string error)
        {
            try
            {
                var plan = JObject.Parse(planJson ?? string.Empty);
                foreach (string property in UloopOnlyProperties)
                {
                    JArray operations = plan[property] as JArray;
                    if (operations != null && operations.Count > 0)
                    {
                        error = "Native Unity backend does not yet support " + property +
                                ". Select the optional uLoop backend before applying this plan.";
                        return false;
                    }
                }

                JObject verify = plan["verify"] as JObject;
                if (verify == null)
                {
                    error = "Plan is missing the verify object.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Native Unity backend could not read the plan: " + exception.Message;
                return false;
            }
        }

        internal static PsdHierarchyChatCleanupExecutionResult Validate(string planJson)
        {
            if (!TryValidatePlanCapabilities(planJson, out string capabilityError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, capabilityError);
            }

            if (!TryReadPrefabPath(planJson, out string prefabPath, out string pathError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, pathError);
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                ApplyPlan(root, JObject.Parse(planJson));
                return new PsdHierarchyChatCleanupExecutionResult(true, string.Empty);
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "Native Unity preflight failed: " + exception.Message);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        internal static PsdHierarchyChatCleanupExecutionResult Apply(string planJson)
        {
            PsdHierarchyChatCleanupExecutionResult preflight = Validate(planJson);
            if (!preflight.success)
            {
                return preflight;
            }

            if (!TryReadPrefabPath(planJson, out string prefabPath, out string pathError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, pathError);
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                ApplyPlan(root, JObject.Parse(planJson));
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException("Failed to save Prefab: " + prefabPath);
                }

                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, "Native Unity Prefab update failed: " + exception.Message);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return new PsdHierarchyChatCleanupExecutionResult(
                true,
                "Prefab updated by the Native Unity backend." + VerifyPersistedPrefab(prefabPath, planJson));
        }

        private static bool TryReadPrefabPath(string planJson, out string prefabPath, out string error)
        {
            prefabPath = string.Empty;
            error = string.Empty;
            try
            {
                prefabPath = JObject.Parse(planJson ?? string.Empty).Value<string>("prefabAssetPath") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(prefabPath) || !prefabPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    error = "Native Unity backend requires a project-relative prefabAssetPath.";
                    return false;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    error = "Native Unity backend could not load the target Prefab: " + prefabPath;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "Native Unity backend could not read prefabAssetPath: " + exception.Message;
                return false;
            }
        }

        private static void ApplyPlan(GameObject root, JObject plan)
        {
            var wrapperParents = new List<Transform>();
            var moves = new List<NativeMove>();
            var renames = new List<NativeRename>();
            var tightBounds = new List<NativeTightBounds>();
            var emptyContainerRemovals = new List<Transform>();
            JArray wrappers = ReadArray(plan, "wrappers");
            JArray moveOperations = ReadArray(plan, "moves");
            JArray renameOperations = ReadArray(plan, "renames");
            JArray tightBoundsOperations = ReadArray(plan, "tightBounds");
            JArray removalOperations = ReadArray(plan, "emptyContainerRemovals");

            for (int index = 0; index < wrappers.Count; index++)
            {
                JObject wrapper = ReadObject(wrappers[index], "wrappers[" + index + "]");
                string parent = ReadString(wrapper, "parent", "wrappers[" + index + "]");
                wrapperParents.Add(parent.StartsWith("@", StringComparison.Ordinal) ? null : FindByPath(root, parent).transform);
            }

            for (int index = 0; index < moveOperations.Count; index++)
            {
                JObject move = ReadObject(moveOperations[index], "moves[" + index + "]");
                string destination = ReadString(move, "destination", "moves[" + index + "]");
                moves.Add(new NativeMove(
                    FindByPath(root, ReadString(move, "source", "moves[" + index + "]")).transform,
                    destination.StartsWith("@", StringComparison.Ordinal) ? null : FindByPath(root, destination).transform,
                    destination,
                    ReadNonNegativeInt(move, "siblingIndex", "moves[" + index + "]")));
            }

            for (int index = 0; index < renameOperations.Count; index++)
            {
                JObject rename = ReadObject(renameOperations[index], "renames[" + index + "]");
                string target = ReadString(rename, "target", "renames[" + index + "]");
                renames.Add(new NativeRename(
                    target.StartsWith("@", StringComparison.Ordinal) ? null : FindByPath(root, target).transform,
                    target,
                    ReadString(rename, "name", "renames[" + index + "]")));
            }

            for (int index = 0; index < tightBoundsOperations.Count; index++)
            {
                JObject tightBound = ReadObject(tightBoundsOperations[index], "tightBounds[" + index + "]");
                string target = ReadString(tightBound, "target", "tightBounds[" + index + "]");
                tightBounds.Add(new NativeTightBounds(
                    target.StartsWith("@", StringComparison.Ordinal)
                        ? null
                        : FindByPath(root, target).GetComponent<RectTransform>(),
                    target));
            }

            for (int index = 0; index < removalOperations.Count; index++)
            {
                JObject removal = ReadObject(removalOperations[index], "emptyContainerRemovals[" + index + "]");
                emptyContainerRemovals.Add(FindByPath(
                    root,
                    ReadString(removal, "source", "emptyContainerRemovals[" + index + "]")).transform);
            }

            var wrappersById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            for (int index = 0; index < wrappers.Count; index++)
            {
                JObject wrapper = ReadObject(wrappers[index], "wrappers[" + index + "]");
                string id = ReadString(wrapper, "id", "wrappers[" + index + "]");
                string parentReference = ReadString(wrapper, "parent", "wrappers[" + index + "]");
                Transform parent = parentReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, parentReference).transform
                    : wrapperParents[index];
                if (parent == null)
                {
                    throw new InvalidOperationException("Wrapper parent was not found: " + parentReference);
                }

                wrappersById.Add(id, CreateWrapper(
                    parent,
                    ReadString(wrapper, "name", "wrappers[" + index + "]"),
                    ReadNonNegativeInt(wrapper, "siblingIndex", "wrappers[" + index + "]")));
            }

            foreach (NativeMove move in moves)
            {
                Transform destination = move.destinationReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, move.destinationReference).transform
                    : move.destination;
                if (move.source == null || destination == null)
                {
                    throw new InvalidOperationException("Move source or destination was not found.");
                }

                move.source.SetParent(destination, true);
                move.source.SetSiblingIndex(move.siblingIndex);
            }

            foreach (NativeRename rename in renames)
            {
                Transform target = rename.targetReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, rename.targetReference).transform
                    : rename.target;
                if (target == null)
                {
                    throw new InvalidOperationException("Rename target was not found: " + rename.targetReference);
                }

                target.name = rename.name;
            }

            foreach (NativeTightBounds operation in tightBounds)
            {
                RectTransform target = operation.targetReference.StartsWith("@", StringComparison.Ordinal)
                    ? ResolveWrapper(wrappersById, operation.targetReference).GetComponent<RectTransform>()
                    : operation.target;
                TightenToChildren(target, operation.targetReference);
            }

            foreach (Transform container in emptyContainerRemovals)
            {
                RemoveEmptyContainer(root.transform, container);
            }

        }

        private static string VerifyPersistedPrefab(string prefabPath, string planJson)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                JObject verify = JObject.Parse(planJson)["verify"] as JObject;
                Verify(root, verify);
                string unsupported = DescribeUnsupportedVerificationFields(verify);
                return string.IsNullOrEmpty(unsupported)
                    ? Environment.NewLine + "VERIFY_OK"
                    : Environment.NewLine + "VERIFY_WARN issue=Native Unity backend did not evaluate " + unsupported + ".";
            }
            catch (Exception exception)
            {
                return Environment.NewLine + "VERIFY_WARN issue=" + exception.Message;
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static string DescribeUnsupportedVerificationFields(JObject verify)
        {
            if (verify == null)
            {
                return "verify";
            }

            var unsupported = new List<string>();
            foreach (JProperty property in verify.Properties())
            {
                if (property.Name != "nodes" &&
                    property.Name != "hierarchy" &&
                    property.Name != "absentPaths" &&
                    property.Name != "directChildren" &&
                    property.Name != "tightBounds")
                {
                    unsupported.Add("verify." + property.Name);
                }
            }

            return string.Join(", ", unsupported.ToArray());
        }

        private static void RemoveEmptyContainer(Transform prefabRoot, Transform container)
        {
            if (container == null || container.parent == null)
            {
                throw new InvalidOperationException("Cannot remove the Prefab root.");
            }

            if (container.childCount != 0)
            {
                throw new InvalidOperationException(
                    "Container is not empty after planned moves: " + container.name + ".");
            }

            foreach (Component component in container.GetComponents<Component>())
            {
                if (component != null && !(component is Transform))
                {
                    throw new InvalidOperationException(
                        "Container has non-Transform components: " + container.name + ".");
                }
            }

            AssertNoExternalReferences(prefabRoot, container);
            UnityEngine.Object.DestroyImmediate(container.gameObject);
        }

        private static void AssertNoExternalReferences(Transform prefabRoot, Transform source)
        {
            var forbidden = new HashSet<UnityEngine.Object>();
            foreach (Transform node in source.GetComponentsInChildren<Transform>(true))
            {
                forbidden.Add(node.gameObject);
                foreach (Component component in node.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        forbidden.Add(component);
                    }
                }
            }

            foreach (Component owner in prefabRoot.GetComponentsInChildren<Component>(true))
            {
                if (owner == null || owner is Transform || forbidden.Contains(owner))
                {
                    continue;
                }

                var serialized = new SerializedObject(owner);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null &&
                        forbidden.Contains(property.objectReferenceValue))
                    {
                        throw new InvalidOperationException(
                            "Cannot remove a container referenced outside its hierarchy: " + source.name +
                            " by " + owner.GetType().FullName + "." + property.propertyPath);
                    }
                }
            }
        }

        private static GameObject FindByPath(GameObject root, string path)
        {
            Transform current = root.transform;
            string[] parts = path.Split('/');
            int index = parts.Length > 0 && string.Equals(parts[0], current.name, StringComparison.Ordinal) ? 1 : 0;
            for (; index < parts.Length; index++)
            {
                string segment = parts[index];
                int occurrence = 0;
                int marker = segment.LastIndexOf('#');
                if (marker > 0 && marker < segment.Length - 1 &&
                    int.TryParse(segment.Substring(marker + 1), out int parsedOccurrence) && parsedOccurrence >= 0)
                {
                    occurrence = parsedOccurrence;
                    segment = segment.Substring(0, marker);
                }

                Transform next = null;
                int matched = 0;
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);
                    if (string.Equals(child.name, segment, StringComparison.Ordinal) && matched++ == occurrence)
                    {
                        next = child;
                        break;
                    }
                }

                if (next == null)
                {
                    throw new InvalidOperationException("Plan source path was not found: " + path);
                }

                current = next;
            }

            return current.gameObject;
        }

        private static GameObject CreateWrapper(Transform parent, string name, int siblingIndex)
        {
            var wrapper = new GameObject(name, typeof(RectTransform));
            RectTransform rect = wrapper.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetSiblingIndex(siblingIndex);
            return wrapper;
        }

        private static void TightenToChildren(RectTransform rect, string target)
        {
            if (rect == null)
            {
                throw new InvalidOperationException("Tight-bounds target is not a RectTransform: " + target);
            }

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null || rect.childCount == 0)
            {
                throw new InvalidOperationException("Tight-bounds target has no RectTransform parent or children: " + target);
            }

            var bounds = new Bounds();
            bool initialized = false;
            for (int childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                RectTransform child = rect.GetChild(childIndex) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 point = parent.InverseTransformPoint(corners[cornerIndex]);
                    if (!initialized)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            if (!initialized)
            {
                throw new InvalidOperationException("Tight-bounds target has no RectTransform children: " + target);
            }

            var children = new List<Transform>();
            var siblingIndices = new List<int>();
            for (int childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                Transform child = rect.GetChild(childIndex);
                children.Add(child);
                siblingIndices.Add(child.GetSiblingIndex());
            }

            foreach (Transform child in children)
            {
                child.SetParent(parent, true);
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);
            rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
            for (int index = 0; index < children.Count; index++)
            {
                children[index].SetParent(rect, true);
                children[index].SetSiblingIndex(siblingIndices[index]);
            }
        }

        private static void Verify(GameObject root, JObject verify)
        {
            if (verify == null)
            {
                throw new InvalidOperationException("Plan is missing the verify object.");
            }

            if (verify["nodes"] != null)
            {
                int expectedNodes = ReadNonNegativeInt(verify, "nodes", "verify");
                int actualNodes = CountNodes(root.transform);
                if (actualNodes != expectedNodes)
                {
                    throw new InvalidOperationException(
                        "Node count differs. Expected=" + expectedNodes + " Actual=" + actualNodes + ".");
                }
            }

            JArray hierarchy = verify["hierarchy"] as JArray;
            if (hierarchy != null)
            {
                for (int index = 0; index < hierarchy.Count; index++)
                {
                    JObject item = ReadObject(hierarchy[index], "verify.hierarchy[" + index + "]");
                    int expected = ReadNonNegativeInt(item, "childCount", "verify.hierarchy[" + index + "]");
                    int actual = FindByPath(root, ReadString(item, "path", "verify.hierarchy[" + index + "]")).transform.childCount;
                    if (actual != expected)
                    {
                        throw new InvalidOperationException("Hierarchy child count differs at verify.hierarchy[" + index + "].");
                    }
                }
            }

            JArray absentPaths = verify["absentPaths"] as JArray;
            if (absentPaths != null)
            {
                foreach (JToken item in absentPaths)
                {
                    string path = item.Value<string>();
                    if (TryFindByPath(root, path) != null)
                    {
                        throw new InvalidOperationException("Planned absent path still exists: " + path);
                    }
                }
            }

            JArray directChildren = verify["directChildren"] as JArray;
            if (directChildren != null)
            {
                for (int index = 0; index < directChildren.Count; index++)
                {
                    JObject item = ReadObject(directChildren[index], "verify.directChildren[" + index + "]");
                    Transform node = FindByPath(root, ReadString(item, "path", "verify.directChildren[" + index + "]")).transform;
                    JArray expected = ReadArray(item, "children");
                    if (node.childCount != expected.Count)
                    {
                        throw new InvalidOperationException("Direct child count differs at " + item.Value<string>("path") + ".");
                    }

                    for (int childIndex = 0; childIndex < expected.Count; childIndex++)
                    {
                        if (!string.Equals(node.GetChild(childIndex).name, expected[childIndex].Value<string>(), StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Direct child order differs at " + item.Value<string>("path") + ".");
                        }
                    }
                }
            }

            JArray tightBounds = verify["tightBounds"] as JArray;
            if (tightBounds != null)
            {
                for (int index = 0; index < tightBounds.Count; index++)
                {
                    JObject item = ReadObject(tightBounds[index], "verify.tightBounds[" + index + "]");
                    string path = ReadString(item, "path", "verify.tightBounds[" + index + "]");
                    AssertTightBounds(FindByPath(root, path).GetComponent<RectTransform>(), path);
                }
            }
        }

        private static int CountNodes(Transform root)
        {
            int count = 1;
            for (int index = 0; index < root.childCount; index++)
            {
                count += CountNodes(root.GetChild(index));
            }

            return count;
        }

        private static void AssertTightBounds(RectTransform rect, string path)
        {
            if (rect == null || rect.parent == null || rect.childCount == 0)
            {
                throw new InvalidOperationException("Tight-bounds invariant cannot be evaluated: " + path);
            }

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null)
            {
                throw new InvalidOperationException("Tight-bounds parent is not a RectTransform: " + path);
            }

            var rectCorners = new Vector3[4];
            rect.GetWorldCorners(rectCorners);
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            bool initialized = false;
            for (int childIndex = 0; childIndex < rect.childCount; childIndex++)
            {
                RectTransform child = rect.GetChild(childIndex) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = parent.InverseTransformPoint(corner);
                    if (!initialized)
                    {
                        min = point;
                        max = point;
                        initialized = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, point);
                        max = Vector3.Max(max, point);
                    }
                }
            }

            Vector3 wrapperMin = parent.InverseTransformPoint(rectCorners[0]);
            Vector3 wrapperMax = parent.InverseTransformPoint(rectCorners[2]);
            if (!initialized ||
                Vector2.Distance(new Vector2(min.x, min.y), new Vector2(wrapperMin.x, wrapperMin.y)) > 0.01f ||
                Vector2.Distance(new Vector2(max.x, max.y), new Vector2(wrapperMax.x, wrapperMax.y)) > 0.01f)
            {
                throw new InvalidOperationException("Tight-bounds invariant failed: " + path);
            }
        }

        private static GameObject ResolveWrapper(IDictionary<string, GameObject> wrappers, string reference)
        {
            string id = reference.Substring(1);
            if (!wrappers.TryGetValue(id, out GameObject wrapper))
            {
                throw new InvalidOperationException("Wrapper reference was not found: " + reference);
            }

            return wrapper;
        }

        private static GameObject TryFindByPath(GameObject root, string path)
        {
            try
            {
                return FindByPath(root, path);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static JArray ReadArray(JObject owner, string name)
        {
            JArray value = owner[name] as JArray;
            if (value == null)
            {
                throw new InvalidDataException("Plan is missing array " + name + ".");
            }

            return value;
        }

        private static JObject ReadObject(JToken value, string label)
        {
            JObject result = value as JObject;
            if (result == null)
            {
                throw new InvalidDataException(label + " must be an object.");
            }

            return result;
        }

        private static string ReadString(JObject owner, string name, string label)
        {
            string value = owner.Value<string>(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(label + "." + name + " must be a non-empty string.");
            }

            return value;
        }

        private static int ReadNonNegativeInt(JObject owner, string name, string label)
        {
            JToken value = owner[name];
            if (value == null || value.Type != JTokenType.Integer || value.Value<int>() < 0)
            {
                throw new InvalidDataException(label + "." + name + " must be a non-negative integer.");
            }

            return value.Value<int>();
        }

        private readonly struct NativeMove
        {
            internal NativeMove(Transform source, Transform destination, string destinationReference, int siblingIndex)
            {
                this.source = source;
                this.destination = destination;
                this.destinationReference = destinationReference;
                this.siblingIndex = siblingIndex;
            }

            internal readonly Transform source;
            internal readonly Transform destination;
            internal readonly string destinationReference;
            internal readonly int siblingIndex;
        }

        private readonly struct NativeRename
        {
            internal NativeRename(Transform target, string targetReference, string name)
            {
                this.target = target;
                this.targetReference = targetReference;
                this.name = name;
            }

            internal readonly Transform target;
            internal readonly string targetReference;
            internal readonly string name;
        }

        private readonly struct NativeTightBounds
        {
            internal NativeTightBounds(RectTransform target, string targetReference)
            {
                this.target = target;
                this.targetReference = targetReference;
            }

            internal readonly RectTransform target;
            internal readonly string targetReference;
        }
    }
}
