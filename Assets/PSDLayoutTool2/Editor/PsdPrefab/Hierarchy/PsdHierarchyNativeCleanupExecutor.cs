namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Executes reviewed cleanup plans in the current Unity Editor process.
    /// Hierarchy-only plans use the direct path; component and asset operations
    /// use the generated Native payload path.
    /// </summary>
    internal static class PsdHierarchyNativeCleanupExecutor
    {
        // Kept as a capability query for callers that need to describe a plan.
        internal static bool RequiresUnityCliRunner(string planJson)
        {
            try
            {
                var plan = JObject.Parse(planJson ?? string.Empty);
                return new[]
                {
                    "textureRenames",
                    "spriteAtlasRenames",
                    "componentExtractions",
                    "stateComponentExtractions",
                    "variantComponentExtractions",
                    "statefulComponentExtractions",
                    "containmentFindings",
                    "containmentResolutions",
                }.Any(property =>
                    plan[property] is JArray operations && operations.Count > 0);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryValidatePlanCapabilities(string planJson, out string error)
        {
            try
            {
                var plan = JObject.Parse(planJson ?? string.Empty);
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

            if (RequiresUnityCliRunner(planJson))
            {
                return new PsdHierarchyChatCleanupExecutionResult(
                    false,
                    "Complex Native Unity plans must run through the asynchronous Native payload executor.");
            }

            if (!TryReadPrefabPath(planJson, out string prefabPath, out string pathError))
            {
                return new PsdHierarchyChatCleanupExecutionResult(false, pathError);
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                ApplyPlan(root, JObject.Parse(planJson), applySelectedPrefabExtractions: false);
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
            string prefabFullPath = GetProjectAssetFullPath(prefabPath);
            byte[] prefabBackup = File.Exists(prefabFullPath) ? File.ReadAllBytes(prefabFullPath) : null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                ApplyPlan(root, JObject.Parse(planJson), applySelectedPrefabExtractions: true);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException("Failed to save Prefab: " + prefabPath);
                }

                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                RestorePrefabBackup(prefabPath, prefabFullPath, prefabBackup);
                DeleteCreatedCrossParentPrefabAssets(planJson);
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

        private static string GetProjectAssetFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, (assetPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
        }

        private static void RestorePrefabBackup(string prefabPath, string prefabFullPath, byte[] prefabBackup)
        {
            if (prefabBackup == null || string.IsNullOrWhiteSpace(prefabFullPath))
            {
                return;
            }

            try
            {
                File.WriteAllBytes(prefabFullPath, prefabBackup);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            }
            catch
            {
                // Preserve the original execution failure as the useful error for callers.
            }
        }

        private static void DeleteCreatedCrossParentPrefabAssets(string planJson)
        {
            try
            {
                JArray extractions = JObject.Parse(planJson ?? string.Empty)["crossParentPrefabExtractions"] as JArray;
                if (extractions == null)
                {
                    return;
                }

                foreach (JObject extraction in extractions.OfType<JObject>())
                {
                    string assetPath = extraction.Value<string>("assetPath");
                    if (!string.IsNullOrWhiteSpace(assetPath) && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                }
            }
            catch
            {
                // Preserve the original execution failure as the useful error for callers.
            }
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ValidateAsync(
            PsdHierarchyChatContext context,
            string planJson)
        {
            if (!TryValidatePlanCapabilities(planJson, out string capabilityError))
                return new PsdHierarchyChatCleanupExecutionResult(false, capabilityError);
            if (!RequiresUnityCliRunner(planJson)) return Validate(planJson);

            PsdHierarchyNativePayloadResult result = await PsdHierarchyNativePayloadExecutor.ExecuteAsync(
                ResolveProjectRoot(context),
                context?.skillFullPath,
                planJson,
                "preflight");
            return new PsdHierarchyChatCleanupExecutionResult(result.success, result.message);
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ApplyAsync(
            PsdHierarchyChatContext context,
            string planJson)
        {
            if (!RequiresUnityCliRunner(planJson)) return Apply(planJson);

            PsdHierarchyChatCleanupExecutionResult preflight = await ValidateAsync(context, planJson);
            if (!preflight.success) return preflight;

            PsdHierarchyNativePayloadResult result = await PsdHierarchyNativePayloadExecutor.ExecuteAsync(
                ResolveProjectRoot(context),
                context?.skillFullPath,
                planJson,
                "apply");
            return result.success
                ? new PsdHierarchyChatCleanupExecutionResult(
                    true,
                    "Prefab updated by the Native Unity backend." +
                    (string.IsNullOrWhiteSpace(result.message) ? string.Empty : Environment.NewLine + result.message))
                : new PsdHierarchyChatCleanupExecutionResult(false, result.message);
        }

        internal static async Task<PsdHierarchyChatCleanupExecutionResult> ReapplyAsync(
            string projectRoot,
            string planJson)
        {
            if (!RequiresUnityCliRunner(planJson)) return Apply(planJson);
            if (!TryValidatePlanCapabilities(planJson, out string capabilityError))
                return new PsdHierarchyChatCleanupExecutionResult(false, capabilityError);

            PsdHierarchyNativePayloadResult preflight = await PsdHierarchyNativePayloadExecutor.ExecuteAsync(
                projectRoot,
                string.Empty,
                BuildReapplyPreflightPlan(planJson),
                "preflight");
            if (!preflight.success)
                return new PsdHierarchyChatCleanupExecutionResult(false, preflight.message);

            PsdHierarchyNativePayloadResult result = await PsdHierarchyNativePayloadExecutor.ExecuteAsync(
                projectRoot,
                string.Empty,
                planJson,
                "reapply");
            return new PsdHierarchyChatCleanupExecutionResult(result.success, result.message);
        }

        internal static string BuildReapplyPreflightPlan(string planJson)
        {
            var plan = JObject.Parse(planJson ?? string.Empty);
            plan["textureRenames"] = new JArray();
            plan["spriteAtlasRenames"] = new JArray();
            return plan.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string ResolveProjectRoot(PsdHierarchyChatContext context)
        {
            if (!string.IsNullOrWhiteSpace(context?.projectRoot)) return context.projectRoot;
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            return projectRoot?.FullName ?? string.Empty;
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

        private static void ApplyPlan(
            GameObject root,
            JObject plan,
            bool applySelectedPrefabExtractions)
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
            JArray selectedPrefabExtractions = plan["selectedPrefabExtractions"] as JArray ?? new JArray();
            JArray crossParentPrefabExtractions = plan["crossParentPrefabExtractions"] as JArray ?? new JArray();

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
                // 智能过滤：跳过仍然有子节点的容器（AI 规划错误的容错处理）
                if (container != null && container.childCount > 0)
                {
                    UnityEngine.Debug.LogWarning(
                        $"跳过删除非空容器：{GetFullPath(container)}（包含 {container.childCount} 个子节点）。" +
                        "这通常是 AI 规划错误，已自动跳过以避免执行失败。");
                    continue;
                }

                RemoveEmptyContainer(root.transform, container);
            }

            ApplySelectedPrefabExtractions(root, selectedPrefabExtractions, applySelectedPrefabExtractions);
            ApplyCrossParentPrefabExtractions(root, crossParentPrefabExtractions, applySelectedPrefabExtractions);

        }

        private static void ApplyCrossParentPrefabExtractions(
            GameObject root,
            JArray extractions,
            bool createAssets)
        {
            for (int extractionIndex = 0; extractionIndex < extractions.Count; extractionIndex++)
            {
                JObject extraction = ReadObject(
                    extractions[extractionIndex],
                    "crossParentPrefabExtractions[" + extractionIndex + "]");
                string label = "crossParentPrefabExtractions[" + extractionIndex + "]";
                string componentName = ReadString(extraction, "name", label);
                string assetPath = ReadString(extraction, "assetPath", label);
                Transform componentRoot = FindByPath(root, ReadString(extraction, "root", label)).transform;
                if (!(componentRoot is RectTransform))
                {
                    throw new InvalidOperationException(label + ".root must use RectTransform.");
                }

                if (!IsPascalCaseIdentifier(componentName))
                {
                    throw new InvalidOperationException(label + ".name must be a PascalCase identifier.");
                }

                ValidateNewPrefabAssetPath(assetPath, componentName, label);
                var templateSources = ReadCrossParentSources(root, extraction["templateSources"] as JArray, label + ".templateSources");
                if (templateSources.Count < 2)
                {
                    throw new InvalidOperationException(label + ".templateSources requires at least two nodes.");
                }

                if (!(extraction["instances"] is JArray instanceOperations) || instanceOperations.Count < 2)
                {
                    throw new InvalidOperationException(label + ".instances requires at least two complete groups.");
                }

                var groups = new List<IReadOnlyList<Transform>>();
                var allSources = new HashSet<Transform>();
                bool includesTemplate = false;
                for (int instanceIndex = 0; instanceIndex < instanceOperations.Count; instanceIndex++)
                {
                    JObject instance = ReadObject(instanceOperations[instanceIndex], label + ".instances[" + instanceIndex + "]");
                    List<Transform> sources = ReadCrossParentSources(
                        root,
                        instance["sources"] as JArray,
                        label + ".instances[" + instanceIndex + "].sources");
                    if (sources.Count != templateSources.Count)
                    {
                        throw new InvalidOperationException(label + ".instances[" + instanceIndex + "] has a different member count.");
                    }

                    foreach (Transform source in sources)
                    {
                        if (!source.IsChildOf(componentRoot) || source == componentRoot || !allSources.Add(source))
                        {
                            throw new InvalidOperationException(label + ".instances must contain unique descendants of the common root.");
                        }

                        AssertCrossParentSourceIsSafe(componentRoot, source, label);
                    }

                    includesTemplate |= new HashSet<Transform>(sources).SetEquals(templateSources);
                    groups.Add(sources);
                }

                if (!includesTemplate)
                {
                    throw new InvalidOperationException(label + ".instances must include the templateSources group.");
                }

                foreach (Transform source in templateSources)
                {
                    if (!source.IsChildOf(componentRoot) || source == componentRoot || !allSources.Contains(source))
                    {
                        throw new InvalidOperationException(label + ".templateSources must be one reviewed group below the common root.");
                    }
                }

                AssertNoExternalReferences(root.transform, allSources, componentName);
                if (!createAssets)
                {
                    continue;
                }

                CreateCrossParentPrefabAsset(
                    componentRoot,
                    templateSources,
                    groups,
                    componentName,
                    assetPath);
            }
        }

        private static List<Transform> ReadCrossParentSources(GameObject root, JArray sourcePaths, string label)
        {
            if (sourcePaths == null || sourcePaths.Count == 0)
            {
                throw new InvalidOperationException(label + " must contain source paths.");
            }

            var sources = new List<Transform>();
            var seenSources = new HashSet<Transform>();
            foreach (JToken sourcePath in sourcePaths)
            {
                if (sourcePath.Type != JTokenType.String || string.IsNullOrWhiteSpace(sourcePath.Value<string>()))
                {
                    throw new InvalidOperationException(label + " must contain non-empty source paths.");
                }

                Transform source = FindByPath(root, sourcePath.Value<string>()).transform;
                if (!(source is RectTransform) || !seenSources.Add(source))
                {
                    throw new InvalidOperationException(label + " must contain unique RectTransform nodes.");
                }

                sources.Add(source);
            }

            return sources;
        }

        private static void AssertCrossParentSourceIsSafe(Transform componentRoot, Transform source, string label)
        {
            for (Transform current = source.parent; current != null && current != componentRoot; current = current.parent)
            {
                if (current.localScale != Vector3.one || Quaternion.Angle(current.localRotation, Quaternion.identity) > 0.001f)
                {
                    throw new InvalidOperationException(label + " cannot cross a scaled or rotated UI parent: " + current.name + ".");
                }

                foreach (Component component in current.GetComponents<Component>())
                {
                    if (component == null || component is Transform)
                    {
                        continue;
                    }

                    string typeName = component.GetType().Name;
                    if (typeName == "Canvas" || typeName == "CanvasGroup" || typeName == "Mask" ||
                        typeName == "RectMask2D" || typeName == "ScrollRect" ||
                        typeName.Contains("Layout", StringComparison.Ordinal) ||
                        typeName == "ContentSizeFitter" || typeName == "AspectRatioFitter")
                    {
                        throw new InvalidOperationException(label + " cannot cross UI behavior parent: " + current.name + " (" + typeName + ").");
                    }
                }
            }
        }

        private static void CreateCrossParentPrefabAsset(
            Transform componentRoot,
            IReadOnlyList<Transform> templateSources,
            IReadOnlyList<IReadOnlyList<Transform>> groups,
            string componentName,
            string assetPath)
        {
            EnsureAssetFolder(assetPath);
            GameObject wrapper = CreateWrapper(componentRoot, componentName, componentRoot.childCount);
            bool assetCreated = false;
            try
            {
                foreach (Transform source in templateSources)
                {
                    source.SetParent(wrapper.transform, true);
                }

                GameObject componentAsset = PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
                if (componentAsset == null)
                {
                    throw new InvalidOperationException("Failed to save cross-parent local Prefab: " + assetPath);
                }

                assetCreated = true;
                foreach (IReadOnlyList<Transform> group in groups)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(componentAsset) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException("Failed to instantiate cross-parent local Prefab: " + assetPath);
                    }

                    Transform destinationRoot = instance.transform;
                    destinationRoot.SetParent(componentRoot, false);
                    ResetCrossParentInstanceRoot(destinationRoot);
                    if (destinationRoot.childCount != group.Count)
                    {
                        throw new InvalidOperationException("Cross-parent local Prefab member count changed after instantiation.");
                    }

                    for (int sourceIndex = 0; sourceIndex < group.Count; sourceIndex++)
                    {
                        CopyCrossParentSourceState(componentRoot, group[sourceIndex], destinationRoot.GetChild(sourceIndex));
                    }
                }

                foreach (IReadOnlyList<Transform> group in groups)
                {
                    foreach (Transform source in group)
                    {
                        if (source != null && source.gameObject != wrapper)
                        {
                            UnityEngine.Object.DestroyImmediate(source.gameObject);
                        }
                    }
                }

                UnityEngine.Object.DestroyImmediate(wrapper);
            }
            catch
            {
                if (wrapper != null)
                {
                    UnityEngine.Object.DestroyImmediate(wrapper);
                }

                if (assetCreated)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                throw;
            }
        }

        private static void ResetCrossParentInstanceRoot(Transform root)
        {
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            if (root is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition3D = Vector3.zero;
                rect.sizeDelta = Vector2.zero;
            }
        }

        private static void CopyCrossParentSourceState(Transform componentRoot, Transform source, Transform destination)
        {
            destination.position = source.position;
            destination.rotation = source.rotation;
            destination.localScale = source.localScale;
            if (source is RectTransform sourceRect && destination is RectTransform destinationRect)
            {
                destinationRect.anchorMin = new Vector2(0.5f, 0.5f);
                destinationRect.anchorMax = new Vector2(0.5f, 0.5f);
                destinationRect.pivot = sourceRect.pivot;
                destinationRect.sizeDelta = sourceRect.rect.size;
            }

            foreach (Component sourceComponent in source.GetComponents<Component>())
            {
                if (sourceComponent == null || sourceComponent is Transform || !IsSupportedCrossParentOverride(sourceComponent))
                {
                    continue;
                }

                Component destinationComponent = destination.GetComponent(sourceComponent.GetType());
                if (destinationComponent == null)
                {
                    throw new InvalidOperationException("Cross-parent local Prefab component mismatch: " + sourceComponent.GetType().FullName + ".");
                }

                EditorUtility.CopySerialized(sourceComponent, destinationComponent);
            }
        }

        private static bool IsSupportedCrossParentOverride(Component component)
        {
            string typeName = component.GetType().FullName;
            return string.Equals(typeName, "UnityEngine.UI.Image", StringComparison.Ordinal) ||
                   string.Equals(typeName, "TMPro.TextMeshProUGUI", StringComparison.Ordinal);
        }

        private static void ApplySelectedPrefabExtractions(
            GameObject root,
            JArray extractions,
            bool createAssets)
        {
            for (int extractionIndex = 0; extractionIndex < extractions.Count; extractionIndex++)
            {
                JObject extraction = ReadObject(
                    extractions[extractionIndex],
                    "selectedPrefabExtractions[" + extractionIndex + "]");
                string label = "selectedPrefabExtractions[" + extractionIndex + "]";
                string componentName = ReadString(extraction, "name", label);
                string assetPath = ReadString(extraction, "assetPath", label);
                string parentPath = ReadString(extraction, "parent", label);
                JArray sources = ReadArray(extraction, "sources");
                if (sources.Count < 2)
                {
                    throw new InvalidOperationException(label + " requires at least two selected direct children.");
                }

                if (!IsPascalCaseIdentifier(componentName))
                {
                    throw new InvalidOperationException(label + ".name must be a PascalCase identifier.");
                }

                ValidateNewPrefabAssetPath(assetPath, componentName, label);
                Transform parent = FindByPath(root, parentPath).transform;
                var sourceTransforms = new List<Transform>();
                var seenSources = new HashSet<Transform>();
                foreach (JToken source in sources)
                {
                    if (source.Type != JTokenType.String || string.IsNullOrWhiteSpace(source.Value<string>()))
                    {
                        throw new InvalidOperationException(label + ".sources must contain non-empty paths.");
                    }

                    Transform sourceTransform = FindByPath(root, source.Value<string>()).transform;
                    if (sourceTransform.parent != parent)
                    {
                        throw new InvalidOperationException(label + ".sources must be direct children of " + parentPath + ".");
                    }

                    if (!seenSources.Add(sourceTransform))
                    {
                        throw new InvalidOperationException(label + ".sources contains a duplicate node.");
                    }

                    if (!(sourceTransform is RectTransform))
                    {
                        throw new InvalidOperationException(label + ".sources must all use RectTransform.");
                    }

                    sourceTransforms.Add(sourceTransform);
                }

                if (!(parent is RectTransform))
                {
                    throw new InvalidOperationException(label + ".parent must use RectTransform.");
                }

                AssertNoExternalReferences(root.transform, sourceTransforms, componentName);
                if (!createAssets)
                {
                    continue;
                }

                CreateSelectedPrefabAsset(parent, sourceTransforms, componentName, assetPath);
            }
        }

        private static void CreateSelectedPrefabAsset(
            Transform parent,
            IReadOnlyList<Transform> sources,
            string componentName,
            string assetPath)
        {
            int siblingIndex = sources.Min(source => source.GetSiblingIndex());
            List<Transform> orderedSources = sources.OrderBy(source => source.GetSiblingIndex()).ToList();
            EnsureAssetFolder(assetPath);
            var wrapper = CreateWrapper(parent, componentName, siblingIndex);
            bool assetCreated = false;
            try
            {
                foreach (Transform source in orderedSources)
                {
                    source.SetParent(wrapper.transform, true);
                }

                TightenToChildren(wrapper.GetComponent<RectTransform>(), "@selected_prefab_extraction");
                GameObject componentAsset = PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
                if (componentAsset == null)
                {
                    throw new InvalidOperationException("Failed to save selected Nested Prefab: " + assetPath);
                }

                assetCreated = true;
                GameObject instance = PrefabUtility.InstantiatePrefab(componentAsset) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException("Failed to instantiate selected Nested Prefab: " + assetPath);
                }

                Transform destination = instance.transform;
                destination.SetParent(parent, false);
                CopyTransformData(wrapper.transform, destination);
                destination.SetSiblingIndex(siblingIndex);
                UnityEngine.Object.DestroyImmediate(wrapper);
            }
            catch
            {
                if (wrapper != null)
                {
                    UnityEngine.Object.DestroyImmediate(wrapper);
                }

                if (assetCreated)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                throw;
            }
        }

        private static void ValidateNewPrefabAssetPath(string assetPath, string componentName, string label)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileNameWithoutExtension(assetPath), componentName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(label + ".assetPath must be a new Assets/.../" + componentName + ".prefab path.");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
            {
                throw new InvalidOperationException(label + ".assetPath already exists: " + assetPath);
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory) || !directory.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Selected Nested Prefab has an invalid asset directory: " + assetPath);
            }

            string[] segments = directory.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static bool IsPascalCaseIdentifier(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   char.IsUpper(value[0]) &&
                   value.All(char.IsLetterOrDigit);
        }

        private static void CopyTransformData(Transform source, Transform destination)
        {
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
            if (source is RectTransform sourceRect && destination is RectTransform destinationRect)
            {
                destinationRect.anchorMin = sourceRect.anchorMin;
                destinationRect.anchorMax = sourceRect.anchorMax;
                destinationRect.pivot = sourceRect.pivot;
                destinationRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                destinationRect.sizeDelta = sourceRect.sizeDelta;
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
            AssertNoExternalReferences(prefabRoot, new[] { source }, source == null ? string.Empty : source.name);
        }

        private static void AssertNoExternalReferences(
            Transform prefabRoot,
            IEnumerable<Transform> sources,
            string sourceDescription)
        {
            var forbidden = new HashSet<UnityEngine.Object>();
            foreach (Transform source in sources ?? Array.Empty<Transform>())
            {
                if (source == null)
                {
                    continue;
                }

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
                            "Cannot extract or remove a hierarchy referenced outside its boundary: " + sourceDescription +
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
            if (parent == null)
            {
                throw new InvalidOperationException("Tight-bounds target has no RectTransform parent: " + target);
            }

            if (rect.childCount == 0)
            {
                if (!target.StartsWith("@", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Cannot tighten an empty wrapper: " + target);
                }

                UnityEngine.Object.DestroyImmediate(rect.gameObject);
                return;
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

        /// <summary>
        /// 获取 Transform 的完整层级路径
        /// </summary>
        private static string GetFullPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var pathSegments = new System.Collections.Generic.List<string>();
            Transform current = transform;
            while (current != null)
            {
                pathSegments.Add(current.name);
                current = current.parent;
            }

            pathSegments.Reverse();
            return string.Join("/", pathSegments.ToArray());
        }
    }
}
