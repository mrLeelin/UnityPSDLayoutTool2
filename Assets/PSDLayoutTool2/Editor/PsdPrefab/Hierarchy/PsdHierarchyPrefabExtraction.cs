namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// 公共组件抽取（v2 计划中的 componentExtractions）。
    /// 在已加载的 Prefab contents 上原地执行：
    /// 先把模板节点克隆成一个新的公共 Prefab 资产，再把每个实例源节点替换成该资产的嵌套实例，
    /// 并把各实例自己的组件取值、RectTransform、内部与外部序列化引用按其相对路径重新映射。
    /// 预检（save=false）只在临时目录生成一次性资产并在结束时删除，绝不写入声明的 assetPath。
    /// </summary>
    internal sealed class NativeExtractionOperation
    {
        internal NativeExtractionOperation(
            string id,
            string assetPath,
            string prefabName,
            Transform template,
            IReadOnlyList<string> instancePaths,
            IReadOnlyList<Transform> instanceTransforms)
        {
            this.id = id;
            this.assetPath = assetPath;
            this.prefabName = prefabName;
            this.template = template;
            this.instancePaths = instancePaths;
            this.instanceTransforms = instanceTransforms;
        }

        internal readonly string id;
        internal readonly string assetPath;
        internal readonly string prefabName;
        internal readonly Transform template;
        internal readonly IReadOnlyList<string> instancePaths;
        internal readonly IReadOnlyList<Transform> instanceTransforms;
    }

    /// <summary>
    /// 状态抽取（v2 计划中的 stateComponentExtractions）：
    /// 把同一视觉槽位上互斥的若干同级根节点折叠成一个带 [States] 容器的公共 Prefab，
    /// 原地只保留一个实例，且保存时只有 defaultState 处于激活状态。
    /// </summary>
    internal sealed class NativeStateExtractionOperation
    {
        internal NativeStateExtractionOperation(
            string id,
            string assetPath,
            string prefabName,
            string templatePath,
            Transform template,
            IReadOnlyList<string> stateIds,
            IReadOnlyList<string> stateSourcePaths,
            IReadOnlyList<Transform> stateSources,
            IReadOnlyList<string> stateNames,
            IReadOnlyList<StateExpectation> stateExpectations,
            int defaultStateIndex,
            Vector2 templateAnchoredPosition,
            Vector2 templateSizeDelta)
        {
            this.id = id;
            this.assetPath = assetPath;
            this.prefabName = prefabName;
            this.templatePath = templatePath;
            this.template = template;
            this.stateIds = stateIds;
            this.stateSourcePaths = stateSourcePaths;
            this.stateSources = stateSources;
            this.stateNames = stateNames;
            this.stateExpectations = stateExpectations;
            this.defaultStateIndex = defaultStateIndex;
            this.templateAnchoredPosition = templateAnchoredPosition;
            this.templateSizeDelta = templateSizeDelta;
        }

        internal readonly string id;
        internal readonly string assetPath;
        internal readonly string prefabName;
        internal readonly string templatePath;
        internal readonly Transform template;
        internal readonly IReadOnlyList<string> stateIds;
        internal readonly IReadOnlyList<string> stateSourcePaths;
        internal readonly IReadOnlyList<Transform> stateSources;
        internal readonly IReadOnlyList<string> stateNames;
        internal readonly IReadOnlyList<StateExpectation> stateExpectations;
        internal readonly int defaultStateIndex;
        internal readonly Vector2 templateAnchoredPosition;
        internal readonly Vector2 templateSizeDelta;
    }

    /// <summary>
    /// 变体列表抽取（v2 计划中的 variantComponentExtractions）：
    /// 一行一个实例，实例只激活自己的状态分支，并把该行的差异复制到激活分支上。
    /// 公共 Prefab 根含直接子节点 [Common]（保留为空）与 [States]。
    /// </summary>
    internal sealed class NativeVariantExtractionOperation
    {
        internal NativeVariantExtractionOperation(
            string id,
            string assetPath,
            string prefabName,
            string templatePath,
            Transform template,
            IReadOnlyList<NativeVariantState> states,
            IReadOnlyList<NativeVariantInstance> instances,
            int defaultStateIndex,
            string commonName,
            string statesName)
        {
            this.id = id;
            this.assetPath = assetPath;
            this.prefabName = prefabName;
            this.templatePath = templatePath;
            this.template = template;
            this.states = states;
            this.instances = instances;
            this.defaultStateIndex = defaultStateIndex;
            this.commonName = commonName;
            this.statesName = statesName;
        }

        internal readonly string id;
        internal readonly string assetPath;
        internal readonly string prefabName;
        internal readonly string templatePath;
        internal readonly Transform template;
        internal readonly IReadOnlyList<NativeVariantState> states;
        internal readonly IReadOnlyList<NativeVariantInstance> instances;
        internal readonly int defaultStateIndex;
        internal readonly string commonName;
        internal readonly string statesName;
    }

    internal sealed class NativeVariantState
    {
        internal NativeVariantState(string id, string sourcePath, Transform source, string name, StateExpectation expectation)
        {
            this.id = id;
            this.sourcePath = sourcePath;
            this.source = source;
            this.name = name;
            this.expectation = expectation;
        }

        internal readonly string id;
        internal readonly string sourcePath;
        internal readonly Transform source;
        internal readonly string name;
        internal readonly StateExpectation expectation;
    }

    internal sealed class NativeVariantInstance
    {
        internal NativeVariantInstance(string sourcePath, Transform source, string name, string stateId, string expectedPath)
        {
            this.sourcePath = sourcePath;
            this.source = source;
            this.name = name;
            this.stateId = stateId;
            this.expectedPath = expectedPath;
        }

        internal readonly string sourcePath;
        internal readonly Transform source;
        internal readonly string name;
        internal readonly string stateId;
        internal readonly string expectedPath;
    }

    /// <summary>
    /// 有状态重复项抽取（v2 计划中的 statefulComponentExtractions）：
    /// 一个共享 Prefab，[States]（先）与 [Common]（后）成员分区，逐实例按 commonSourceNames +
    /// stateSourceNames 完整映射自己的直接子节点，并只激活自己的状态分支。
    /// </summary>
    internal sealed class NativeStatefulExtractionOperation
    {
        internal NativeStatefulExtractionOperation(
            string id,
            string assetPath,
            string prefabName,
            string templatePath,
            Transform template,
            NativeStatefulBranch common,
            IReadOnlyList<NativeStatefulState> states,
            int defaultStateIndex,
            IReadOnlyList<NativeStatefulInstance> instances,
            string commonName,
            string statesName)
        {
            this.id = id;
            this.assetPath = assetPath;
            this.prefabName = prefabName;
            this.templatePath = templatePath;
            this.template = template;
            this.common = common;
            this.states = states;
            this.defaultStateIndex = defaultStateIndex;
            this.instances = instances;
            this.commonName = commonName;
            this.statesName = statesName;
        }

        internal readonly string id;
        internal readonly string assetPath;
        internal readonly string prefabName;
        internal readonly string templatePath;
        internal readonly Transform template;
        internal readonly NativeStatefulBranch common;
        internal readonly IReadOnlyList<NativeStatefulState> states;
        internal readonly int defaultStateIndex;
        internal readonly IReadOnlyList<NativeStatefulInstance> instances;
        internal readonly string commonName;
        internal readonly string statesName;
    }

    internal sealed class NativeStatefulBranch
    {
        internal NativeStatefulBranch(string sourcePath, Transform source, IReadOnlyList<NativeStatefulMember> members)
        {
            this.sourcePath = sourcePath;
            this.source = source;
            this.members = members;
        }

        internal readonly string sourcePath;
        internal readonly Transform source;
        internal readonly IReadOnlyList<NativeStatefulMember> members;
    }

    internal sealed class NativeStatefulState
    {
        internal NativeStatefulState(string id, string name, NativeStatefulBranch branch)
        {
            this.id = id;
            this.name = name;
            this.branch = branch;
        }

        internal readonly string id;
        internal readonly string name;
        internal readonly NativeStatefulBranch branch;
    }

    internal readonly struct NativeStatefulMember
    {
        internal NativeStatefulMember(string sourceName, string targetName)
        {
            this.sourceName = sourceName;
            this.targetName = targetName;
        }

        internal readonly string sourceName;
        internal readonly string targetName;
    }

    internal sealed class NativeStatefulInstance
    {
        internal NativeStatefulInstance(
            string sourcePath,
            Transform source,
            string name,
            string stateId,
            string expectedPath,
            IReadOnlyList<string> commonSourceNames,
            IReadOnlyList<string> stateSourceNames)
        {
            this.sourcePath = sourcePath;
            this.source = source;
            this.name = name;
            this.stateId = stateId;
            this.expectedPath = expectedPath;
            this.commonSourceNames = commonSourceNames;
            this.stateSourceNames = stateSourceNames;
        }

        internal readonly string sourcePath;
        internal readonly Transform source;
        internal readonly string name;
        internal readonly string stateId;
        internal readonly string expectedPath;
        internal readonly IReadOnlyList<string> commonSourceNames;
        internal readonly IReadOnlyList<string> stateSourceNames;
    }

    /// <summary>
    /// 局部选区抽取（v2 计划中的 selectedPrefabExtractions，仅由已锁定的局部修复生成）：
    /// 把同一父节点下选中的若干直接子节点折叠成一个新的 Nested Prefab，原地只保留一个实例。
    /// </summary>
    internal sealed class NativeSelectedExtractionOperation
    {
        internal NativeSelectedExtractionOperation(
            string id,
            string assetPath,
            string prefabName,
            string parentPath,
            Transform parent,
            IReadOnlyList<string> sourcePaths,
            IReadOnlyList<Transform> sourceTransforms)
        {
            this.id = id;
            this.assetPath = assetPath;
            this.prefabName = prefabName;
            this.parentPath = parentPath;
            this.parent = parent;
            this.sourcePaths = sourcePaths;
            this.sourceTransforms = sourceTransforms;
        }

        internal readonly string id;
        internal readonly string assetPath;
        internal readonly string prefabName;
        internal readonly string parentPath;
        internal readonly Transform parent;
        internal readonly IReadOnlyList<string> sourcePaths;
        internal readonly IReadOnlyList<Transform> sourceTransforms;
    }

    internal sealed class NativeCrossParentGroup
    {
        internal NativeCrossParentGroup(int sequence, IReadOnlyList<string> sourcePaths, IReadOnlyList<Transform> sources)
        {
            this.sequence = sequence;
            this.sourcePaths = sourcePaths;
            this.sources = sources;
        }

        internal readonly int sequence;
        internal readonly IReadOnlyList<string> sourcePaths;
        internal readonly IReadOnlyList<Transform> sources;
    }

    /// <summary>
    /// 跨父级抽取（v2 计划中的 crossParentPrefabExtractions，同样只由已锁定的局部修复生成）：
    /// 一个共享 Prefab 由跨父节点的模板成员组成，每个审核过的组各建一个实例并保留自己的差异。
    /// </summary>
    internal sealed class NativeCrossParentExtractionOperation
    {
        internal NativeCrossParentExtractionOperation(
            string id,
            string assetPath,
            string prefabName,
            string rootPath,
            Transform root,
            IReadOnlyList<string> templatePaths,
            IReadOnlyList<Transform> templateTransforms,
            IReadOnlyList<NativeCrossParentGroup> groups,
            IReadOnlyList<string> unmatchedPaths)
        {
            this.id = id;
            this.assetPath = assetPath;
            this.prefabName = prefabName;
            this.rootPath = rootPath;
            this.root = root;
            this.templatePaths = templatePaths;
            this.templateTransforms = templateTransforms;
            this.groups = groups;
            this.unmatchedPaths = unmatchedPaths;
        }

        internal readonly string id;
        internal readonly string assetPath;
        internal readonly string prefabName;
        internal readonly string rootPath;
        internal readonly Transform root;
        internal readonly IReadOnlyList<string> templatePaths;
        internal readonly IReadOnlyList<Transform> templateTransforms;
        internal readonly IReadOnlyList<NativeCrossParentGroup> groups;
        internal readonly IReadOnlyList<string> unmatchedPaths;
    }

    /// <summary>保存前后都必须成立的状态分支事实。</summary>
    internal readonly struct StateExpectation
    {
        internal StateExpectation(string name, int childCount, int rectTransformCount, Vector2 anchoredPosition, bool active)
        {
            this.name = name;
            this.childCount = childCount;
            this.rectTransformCount = rectTransformCount;
            this.anchoredPosition = anchoredPosition;
            this.active = active;
        }

        internal readonly string name;
        internal readonly int childCount;
        internal readonly int rectTransformCount;
        internal readonly Vector2 anchoredPosition;
        internal readonly bool active;
    }

    internal static class PsdHierarchyPrefabExtraction
    {
        private const string PreflightTempFolder = "Assets/PSDLayoutTool2Settings/ExtractionPreflightTemp";

        internal static IReadOnlyList<NativeExtractionOperation> Bind(
            JObject plan,
            GameObject root,
            PsdHierarchyChatContext context,
            string targetPrefabAssetPath,
            bool replaceExistingTargets = false)
        {
            JArray extractions = plan["componentExtractions"] as JArray ?? new JArray();
            var operations = new List<NativeExtractionOperation>();
            if (extractions.Count == 0)
            {
                ValidateNoMandatoryCandidateWithoutExtraction(plan, context);
                return operations;
            }

            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var claimedSources = new List<Transform>();

            foreach (JToken token in extractions)
            {
                JObject extraction = token as JObject;
                if (extraction == null)
                    throw new InvalidDataException("componentExtractions entries must be objects.");

                string id = ReadRequired(extraction, "id", "componentExtractions");
                if (!usedIds.Add(id))
                    throw new InvalidDataException("Duplicate componentExtractions id: " + id + ".");

                string assetPath = ReadRequired(extraction, "assetPath", "componentExtractions[" + id + "]");
                ValidateExtractionAssetPath(assetPath, targetPrefabAssetPath);
                // 资产冲突必须在预检就明确失败，而不是等到写入时才发现。
                if (!replaceExistingTargets && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                string templatePath = ResolveNodePath(context, extraction, "template", "componentExtractions[" + id + "]");
                Transform template = FindByPath(root, templatePath)?.transform;
                if (template == null)
                    throw new InvalidDataException(
                        "componentExtractions[" + id + "].template was not found in the loaded Prefab: " + templatePath);

                JArray instances = extraction["instances"] as JArray;
                if (instances == null || instances.Count == 0)
                    throw new InvalidDataException("componentExtractions[" + id + "].instances must not be empty.");

                var instancePaths = new List<string>();
                var instanceTransforms = new List<Transform>();
                foreach (JToken source in instances)
                {
                    string path = ResolveNodePath(context, source, null, "componentExtractions[" + id + "].instances");
                    if (instancePaths.Contains(path, StringComparer.Ordinal))
                        throw new InvalidDataException("componentExtractions[" + id + "] repeats an instance: " + path);
                    Transform instance = FindByPath(root, path)?.transform;
                    if (instance == null)
                        throw new InvalidDataException(
                            "componentExtractions[" + id + "].instances was not found in the loaded Prefab: " + path);
                    if (claimedSources.Any(claimed => claimed == instance || instance.IsChildOf(claimed)))
                        throw new InvalidDataException(
                            "componentExtractions[" + id + "] overlaps another extraction source: " + path);

                    instancePaths.Add(path);
                    instanceTransforms.Add(instance);
                }

                if (!instancePaths.Contains(templatePath, StringComparer.Ordinal))
                    throw new InvalidDataException(
                        "componentExtractions[" + id + "].template must also appear in instances: " + templatePath);

                for (int index = 0; index < instanceTransforms.Count; index++)
                {
                    if (!IsStructurallyIdentical(template, instanceTransforms[index]))
                        throw new InvalidDataException(
                            "Repeated unit structure differs for component extraction: " + instancePaths[index] +
                            " does not match " + templatePath + ".");
                }

                claimedSources.AddRange(instanceTransforms);
                string prefabName = ReadOptional(extraction, "name");
                if (string.IsNullOrWhiteSpace(prefabName))
                    prefabName = Path.GetFileNameWithoutExtension(assetPath);
                if (!IsPascalCaseIdentifier(prefabName))
                    throw new InvalidDataException(
                        "componentExtractions[" + id + "].name must be a PascalCase identifier: " + prefabName);

                operations.Add(new NativeExtractionOperation(
                    id, assetPath, prefabName, template, instancePaths, instanceTransforms));
            }

            ValidateMandatoryCandidates(plan, context);
            return operations;
        }

        /// <summary>
        /// 抽取执行。save=false 时只在临时目录生成一次性资产，并把路径交给
        /// <see cref="CleanupTemporaryAssets"/>，等调用方验证完内存状态后再删除。
        /// </summary>
        internal static void Apply(
            GameObject root,
            IReadOnlyList<NativeExtractionOperation> operations,
            bool save,
            out IReadOnlyList<string> temporaryAssets,
            bool replaceExistingTargets = false,
            Action beforeBusinessWrite = null)
        {
            temporaryAssets = Array.Empty<string>();
            if (operations == null || operations.Count == 0)
            {
                return;
            }

            var created = new List<string>();
            foreach (NativeExtractionOperation operation in operations)
            {
                string assetPath = save ? operation.assetPath : BuildPreflightAssetPath(operation);
                if (!save)
                {
                    created.Add(assetPath);
                }
                else
                {
                    beforeBusinessWrite?.Invoke();
                }

                EnsureAssetFolder(assetPath);
                string fullPath = ToProjectFullPath(assetPath);
                if (!replaceExistingTargets && File.Exists(fullPath))
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                GameObject sharedAsset = CreateSharedPrefab(operation, assetPath);

                // 替换前记录所有指向源子树的外部序列化引用，替换后重定向到实例对象。
                List<PendingReference> externalReferences = CollectExternalReferences(
                    root, operation.instanceTransforms, includeInternal: true);
                var remap = new Dictionary<int, Object>();

                foreach (Transform source in operation.instanceTransforms)
                {
                    ReplaceWithInstance(source, sharedAsset, remap);
                }

                ApplyExternalReferences(externalReferences, remap);
            }

            temporaryAssets = created;
        }

        /// <summary>预检结束（且已核验）后删除一次性资产。</summary>
        internal static void CleanupTemporaryAssets(IReadOnlyList<string> temporaryAssets)
        {
            if (temporaryAssets == null || temporaryAssets.Count == 0)
            {
                return;
            }

            foreach (string assetPath in temporaryAssets)
            {
                DeleteAssetQuietly(assetPath);
            }

            DeleteAssetQuietly(PreflightTempFolder);
        }

        /// <summary>
        /// 保存前后都必须成立：每个实例节点确实链接到声明的公共 Prefab。
        /// 预检（persisted=false）时声明的资产必须尚未存在——预检只允许生成一次性临时资产。
        /// </summary>
        internal static void Verify(
            GameObject root,
            IReadOnlyList<NativeExtractionOperation> operations,
            bool persisted)
        {
            if (operations == null)
            {
                return;
            }

            foreach (NativeExtractionOperation operation in operations)
            {
                bool assetExists = AssetDatabase.LoadMainAssetAtPath(operation.assetPath) != null;
                if (persisted && !assetExists)
                    throw new InvalidOperationException("Extracted Prefab asset is missing: " + operation.assetPath);
                if (!persisted && assetExists)
                    throw new InvalidOperationException(
                        "Preflight must not create the declared extraction asset: " + operation.assetPath);

                foreach (string path in operation.instancePaths)
                {
                    GameObject node = FindByPath(root, path);
                    if (node == null)
                        throw new InvalidOperationException("Extraction instance node disappeared: " + path);

                    string linkedAsset = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node);
                    if (string.IsNullOrEmpty(linkedAsset))
                    {
                        throw new InvalidOperationException(
                            "Extraction instance is not a Prefab instance: " + path);
                    }

                    if (persisted && !string.Equals(linkedAsset, operation.assetPath, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Extraction instance is not linked to the shared Prefab: " + path +
                            " -> " + linkedAsset);
                    }
                }
            }
        }

        private static GameObject CreateSharedPrefab(NativeExtractionOperation operation, string assetPath)
        {
            var clone = (GameObject)Object.Instantiate(operation.template.gameObject);
            try
            {
                clone.transform.SetParent(null);
                clone.name = operation.prefabName;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(clone, assetPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not create the shared Prefab: " + assetPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        private static void ReplaceWithInstance(
            Transform source,
            GameObject sharedAsset,
            IDictionary<int, Object> remap)
        {
            Transform parent = source.parent;
            if (parent == null)
                throw new InvalidOperationException("Cannot extract the Prefab root: " + source.name);

            int siblingIndex = source.GetSiblingIndex();
            string name = source.name;
            var nodeMap = new Dictionary<Transform, Transform>();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(sharedAsset, parent);
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate the shared Prefab for: " + name);
            instance.transform.SetSiblingIndex(siblingIndex);
            instance.name = name;

            var resolved = new Dictionary<Transform, Transform>();
            MapStructure(source, instance.transform, resolved);
            CopySourceValues(source, resolved);
            RegisterRemap(resolved, remap);

            Object.DestroyImmediate(source.gameObject);
        }

        /// <summary>结构已在校验阶段证明一致；这里按相对路径建立 源→实例 映射。</summary>
        private static void MapStructure(Transform source, Transform target, IDictionary<Transform, Transform> map)
        {
            map[source] = target;
            int count = Math.Min(source.childCount, target.childCount);
            for (int index = 0; index < count; index++)
            {
                MapStructure(source.GetChild(index), target.GetChild(index), map);
            }
        }

        /// <summary>把源子树的 Transform 与组件取值复制到实例上。</summary>
        private static void CopySourceValues(
            Transform source,
            IDictionary<Transform, Transform> map,
            bool renameRoot = true,
            bool renameChildren = true)
        {
            foreach (Transform sourceNode in Enumerate(source))
            {
                if (!map.TryGetValue(sourceNode, out Transform target) || target == null)
                    continue;

                // 节点名是逐实例覆盖：实例根已在替换时命名，这里补齐子节点名差异。
                bool mayRename = sourceNode == source ? renameRoot : renameChildren;
                if (mayRename && !string.Equals(target.name, sourceNode.name, StringComparison.Ordinal))
                {
                    target.name = sourceNode.name;
                }

                target.gameObject.layer = sourceNode.gameObject.layer;
                target.gameObject.tag = sourceNode.gameObject.tag;
                target.gameObject.isStatic = sourceNode.gameObject.isStatic;
                target.gameObject.SetActive(sourceNode.gameObject.activeSelf);

                target.localPosition = sourceNode.localPosition;
                target.localRotation = sourceNode.localRotation;
                target.localScale = sourceNode.localScale;
                if (sourceNode is RectTransform sourceRect && target is RectTransform targetRect)
                {
                    targetRect.anchorMin = sourceRect.anchorMin;
                    targetRect.anchorMax = sourceRect.anchorMax;
                    targetRect.pivot = sourceRect.pivot;
                    targetRect.anchoredPosition = sourceRect.anchoredPosition;
                    targetRect.sizeDelta = sourceRect.sizeDelta;
                }

                Component[] sourceComponents = sourceNode.GetComponents<Component>();
                Component[] targetComponents = target.GetComponents<Component>();
                for (int index = 0; index < sourceComponents.Length && index < targetComponents.Length; index++)
                {
                    CopyComponentValues(sourceComponents[index], targetComponents[index], map);
                }
            }
        }

        private static void CopyComponentValues(
            Component source,
            Component target,
            IDictionary<Transform, Transform> map)
        {
            if (source == null || target == null || source is Transform || target is Transform)
                return;
            if (source.GetType() != target.GetType())
                throw new InvalidOperationException("Component types differ during extraction: " + source.GetType().Name);

            var sourceObject = new SerializedObject(source);
            var targetObject = new SerializedObject(target);
            SerializedProperty sourceProperty = sourceObject.GetIterator();
            bool enterChildren = true;
            while (sourceProperty.Next(enterChildren))
            {
                enterChildren = ShouldEnterSerializedChildren(sourceProperty);
                if (ShouldSkipComponentProperty(sourceProperty.propertyPath))
                {
                    enterChildren = false;
                    continue;
                }

                SerializedProperty targetProperty = targetObject.FindProperty(sourceProperty.propertyPath);
                if (targetProperty == null)
                    continue;

                if (sourceProperty.isArray && sourceProperty.propertyType != SerializedPropertyType.String)
                {
                    if (!targetProperty.isArray)
                        throw new InvalidOperationException(
                            "Serialized array shape differs during extraction: " + sourceProperty.propertyPath);
                    targetProperty.arraySize = sourceProperty.arraySize;
                    continue;
                }

                if (sourceProperty.propertyType == SerializedPropertyType.Generic)
                    continue;

                CopyProperty(sourceProperty, targetProperty, map);
            }

            targetObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ShouldEnterSerializedChildren(SerializedProperty property)
        {
            return property != null &&
                   (property.propertyType == SerializedPropertyType.Generic ||
                    (property.isArray && property.propertyType != SerializedPropertyType.String));
        }

        private static bool ShouldSkipComponentProperty(string propertyPath)
        {
            return string.Equals(propertyPath, "m_ObjectHideFlags", StringComparison.Ordinal) ||
                   string.Equals(propertyPath, "m_CorrespondingSourceObject", StringComparison.Ordinal) ||
                   string.Equals(propertyPath, "m_PrefabInstance", StringComparison.Ordinal) ||
                   string.Equals(propertyPath, "m_PrefabAsset", StringComparison.Ordinal) ||
                   string.Equals(propertyPath, "m_GameObject", StringComparison.Ordinal) ||
                   string.Equals(propertyPath, "m_Script", StringComparison.Ordinal) ||
                   string.Equals(propertyPath, "m_EditorHideFlags", StringComparison.Ordinal) ||
                   string.Equals(propertyPath, "m_EditorClassIdentifier", StringComparison.Ordinal);
        }

        private static void CopyProperty(
            SerializedProperty source,
            SerializedProperty target,
            IDictionary<Transform, Transform> map)
        {
            try
            {
                if (source.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Object value = source.objectReferenceValue;
                    if (value is Component component && map.TryGetValue(component.transform, out Transform mapped))
                    {
                        // 同一子树内的引用：按组件在节点上的顺序映射到实例里的对应组件。
                        Component[] sourceComponents = component.transform.GetComponents<Component>();
                        Component[] mappedComponents = mapped.GetComponents<Component>();
                        int index = Array.IndexOf(sourceComponents, component);
                        value = index >= 0 && index < mappedComponents.Length && mappedComponents[index] != null
                            ? mappedComponents[index]
                            : (Object)mapped.gameObject;
                    }
                    else if (value is GameObject gameObject && map.TryGetValue(gameObject.transform, out Transform mappedObject))
                    {
                        value = mappedObject.gameObject;
                    }

                    // 子树外的引用保持原样。
                    target.objectReferenceValue = value;
                    return;
                }

                target.boxedValue = source.boxedValue;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Failed to preserve serialized property during extraction: " + source.propertyPath + ".",
                    exception);
            }
        }

        private static void RegisterRemap(
            IDictionary<Transform, Transform> map,
            IDictionary<int, Object> remap)
        {
            foreach (KeyValuePair<Transform, Transform> pair in map)
            {
                remap[pair.Key.gameObject.GetInstanceID()] = pair.Value.gameObject;
                Component[] sources = pair.Key.GetComponents<Component>();
                Component[] targets = pair.Value.GetComponents<Component>();
                for (int index = 0; index < sources.Length && index < targets.Length; index++)
                {
                    if (sources[index] != null && targets[index] != null)
                        remap[sources[index].GetInstanceID()] = targets[index];
                }
            }
        }

        private static List<PendingReference> CollectExternalReferences(
            GameObject root,
            IReadOnlyList<Transform> extractionRoots,
            bool includeInternal = false)
        {
            var extractionIds = new HashSet<int>();
            foreach (Transform extractionRoot in extractionRoots)
            {
                foreach (Transform node in Enumerate(extractionRoot))
                {
                    extractionIds.Add(node.gameObject.GetInstanceID());
                    foreach (Component component in node.GetComponents<Component>())
                    {
                        if (component != null)
                            extractionIds.Add(component.GetInstanceID());
                    }
                }
            }

            var pending = new List<PendingReference>();
            foreach (Transform node in Enumerate(root.transform))
            {
                if (!includeInternal && extractionRoots.Any(
                        extractionRoot => node == extractionRoot || node.IsChildOf(extractionRoot)))
                    continue;

                foreach (Component component in node.GetComponents<Component>())
                {
                    if (component == null || component is Transform)
                        continue;

                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool enterChildren = true;
                    while (property.Next(enterChildren))
                    {
                        enterChildren = ShouldEnterSerializedChildren(property);
                        if (ShouldSkipComponentProperty(property.propertyPath))
                        {
                            enterChildren = false;
                            continue;
                        }
                        if (property.propertyType != SerializedPropertyType.ObjectReference)
                            continue;
                        int targetId = property.objectReferenceInstanceIDValue;
                        if (targetId != 0 && extractionIds.Contains(targetId))
                            pending.Add(new PendingReference(component, property.propertyPath, targetId));
                    }
                }
            }

            return pending;
        }

        private static void ApplyExternalReferences(
            IReadOnlyList<PendingReference> pending,
            IDictionary<int, Object> remap)
        {
            foreach (PendingReference reference in pending)
            {
                Component owner = reference.component;
                if (owner == null && remap.TryGetValue(reference.componentId, out Object remappedOwner))
                    owner = remappedOwner as Component;
                if (owner == null || !remap.TryGetValue(reference.targetId, out Object replacement))
                    continue;

                var serialized = new SerializedObject(owner);
                SerializedProperty property = serialized.FindProperty(reference.propertyPath);
                if (property == null)
                    continue;
                property.objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static IEnumerable<Transform> Enumerate(Transform root)
        {
            yield return root;
            for (int index = 0; index < root.childCount; index++)
            {
                foreach (Transform child in Enumerate(root.GetChild(index)))
                {
                    yield return child;
                }
            }
        }

        // ---------- 状态抽取（stateComponentExtractions） ----------

        /// <summary>绑定并校验状态抽取契约；任何问题都在写入前抛出。</summary>
        internal static IReadOnlyList<NativeStateExtractionOperation> BindStateExtractions(
            JObject plan,
            GameObject root,
            PsdHierarchyChatContext context,
            string targetPrefabAssetPath,
            bool replaceExistingTargets = false)
        {
            JArray extractions = plan["stateComponentExtractions"] as JArray ?? new JArray();
            var operations = new List<NativeStateExtractionOperation>();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var claimedSources = new List<Transform>();

            foreach (JToken token in extractions)
            {
                if (!(token is JObject extraction))
                    throw new InvalidDataException("stateComponentExtractions entries must be objects.");

                string id = ReadRequired(extraction, "id", "stateComponentExtractions");
                if (!usedIds.Add(id))
                    throw new InvalidDataException("Duplicate stateComponentExtractions id: " + id + ".");

                string label = "stateComponentExtractions[" + id + "]";
                string assetPath = ReadRequired(extraction, "assetPath", label);
                ValidateExtractionAssetPath(assetPath, targetPrefabAssetPath);
                if (!replaceExistingTargets && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                string templatePath = ResolveNodePath(context, extraction, "template", label);
                Transform template = FindByPath(root, templatePath)?.transform;
                if (template == null)
                    throw new InvalidDataException(label + ".template was not found in the loaded Prefab: " + templatePath);
                if (template.parent == null)
                    throw new InvalidDataException("Cannot extract states from the Prefab root.");

                string defaultState = ReadRequired(extraction, "defaultState", label);
                JArray states = extraction["states"] as JArray;
                if (states == null || states.Count < 2)
                    throw new InvalidDataException(label + ".states must list at least two mutually exclusive states.");

                var stateIds = new List<string>();
                var stateSourcePaths = new List<string>();
                var stateSources = new List<Transform>();
                var stateNames = new List<string>();
                var stateExpectations = new List<StateExpectation>();

                foreach (JToken stateToken in states)
                {
                    if (!(stateToken is JObject state))
                        throw new InvalidDataException(label + ".states entries must be objects.");

                    string stateId = ReadRequired(state, "id", label + ".states");
                    if (stateIds.Contains(stateId, StringComparer.Ordinal))
                        throw new InvalidDataException(label + " repeats the state id: " + stateId + ".");
                    string stateName = ReadRequired(state, "name", label + ".states[" + stateId + "]");
                    string sourcePath = ResolveNodePath(context, state, "source", label + ".states[" + stateId + "]");
                    if (stateSourcePaths.Contains(sourcePath, StringComparer.Ordinal))
                        throw new InvalidDataException(label + " repeats a state source: " + sourcePath);
                    Transform source = FindByPath(root, sourcePath)?.transform;
                    if (source == null)
                        throw new InvalidDataException(label + ".states[" + stateId + "].source was not found: " + sourcePath);
                    if (source.parent != template.parent)
                        throw new InvalidDataException(
                            label + ".states[" + stateId + "].source must be a direct sibling of template: " + sourcePath);
                    if (claimedSources.Any(claimed => claimed == source))
                        throw new InvalidDataException(label + " reuses a state source: " + sourcePath);

                    stateIds.Add(stateId);
                    stateSourcePaths.Add(sourcePath);
                    stateSources.Add(source);
                    stateNames.Add(stateName);

                    var rect = source as RectTransform;
                    stateExpectations.Add(new StateExpectation(
                        stateName,
                        source.childCount,
                        CountRectTransforms(source),
                        rect == null ? Vector3.zero : RelativePosition(rect, template as RectTransform),
                        false));
                }

                if (!stateSourcePaths.Contains(templatePath, StringComparer.Ordinal))
                    throw new InvalidDataException(label + ".template must also appear in states[].source: " + templatePath);

                int defaultStateIndex = stateIds.IndexOf(defaultState);
                if (defaultStateIndex < 0)
                    throw new InvalidDataException(label + ".defaultState was not found in states: " + defaultState);

                // 状态抽取会把多个同级根折叠成一个：任何外部序列化引用都会变成歧义引用，必须写入前拒绝。
                List<PendingReference> external = CollectExternalReferences(root, stateSources);
                if (external.Count > 0)
                    throw new InvalidDataException(
                        label + " sources are referenced from outside the extracted states (" +
                        external.Count + " serialized reference(s)); collapsing them would break those references.");

                claimedSources.AddRange(stateSources);

                var templateRect = template as RectTransform;
                operations.Add(new NativeStateExtractionOperation(
                    id,
                    assetPath,
                    Path.GetFileNameWithoutExtension(assetPath),
                    templatePath,
                    template,
                    stateIds,
                    stateSourcePaths,
                    stateSources,
                    stateNames,
                    stateExpectations,
                    defaultStateIndex,
                    templateRect == null ? Vector2.zero : templateRect.anchoredPosition,
                    templateRect == null ? Vector2.zero : templateRect.sizeDelta));
            }

            return operations;
        }

        internal static void ApplyStateExtractions(
            GameObject root,
            IReadOnlyList<NativeStateExtractionOperation> operations,
            bool save,
            out IReadOnlyList<string> temporaryAssets,
            bool replaceExistingTargets = false,
            Action beforeBusinessWrite = null)
        {
            temporaryAssets = Array.Empty<string>();
            if (operations == null || operations.Count == 0)
            {
                return;
            }

            var created = new List<string>();
            foreach (NativeStateExtractionOperation operation in operations)
            {
                string assetPath = save ? operation.assetPath : BuildPreflightAssetPath(operation.assetPath, operation.prefabName);
                if (!save)
                {
                    created.Add(assetPath);
                }
                else
                {
                    beforeBusinessWrite?.Invoke();
                }

                EnsureAssetFolder(assetPath);
                if (!replaceExistingTargets && File.Exists(ToProjectFullPath(assetPath)))
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                GameObject sharedAsset = CreateStateComponentPrefab(operation, assetPath);
                List<PendingReference> externalReferences = CollectExternalReferences(
                    root, operation.stateSources, includeInternal: true);
                var remap = new Dictionary<int, Object>();
                ReplaceStateSourcesWithInstance(operation, sharedAsset, remap);
                ApplyExternalReferences(externalReferences, remap);
            }

            temporaryAssets = created;
        }

        internal static void VerifyStateExtractions(
            GameObject root,
            IReadOnlyList<NativeStateExtractionOperation> operations,
            bool persisted)
        {
            if (operations == null)
            {
                return;
            }

            foreach (NativeStateExtractionOperation operation in operations)
            {
                bool assetExists = AssetDatabase.LoadMainAssetAtPath(operation.assetPath) != null;
                if (persisted && !assetExists)
                    throw new InvalidOperationException("Extracted Prefab asset is missing: " + operation.assetPath);
                if (!persisted && assetExists)
                    throw new InvalidOperationException(
                        "Preflight must not create the declared extraction asset: " + operation.assetPath);

                GameObject instance = FindByPath(root, operation.templatePath);
                if (instance == null)
                    throw new InvalidOperationException("State component instance disappeared: " + operation.templatePath);

                if (persisted)
                {
                    string linked = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
                    if (!string.Equals(linked, operation.assetPath, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "State component instance is not linked to the shared Prefab: " + linked);
                }

                var instanceRect = instance.transform as RectTransform;
                if (instanceRect != null &&
                    (Vector2.Distance(instanceRect.anchoredPosition, operation.templateAnchoredPosition) > 0.001f ||
                     Vector2.Distance(instanceRect.sizeDelta, operation.templateSizeDelta) > 0.001f))
                {
                    throw new InvalidOperationException(
                        "State component instance lost the template layout: " + operation.templatePath);
                }

                Transform statesContainer = instance.transform.Find("[States]");
                if (statesContainer == null)
                    throw new InvalidOperationException("State component has no [States] container: " + operation.templatePath);
                if (statesContainer.childCount != operation.stateNames.Count)
                    throw new InvalidOperationException(
                        "State component branch count differs: " + statesContainer.childCount);

                for (int index = 0; index < operation.stateNames.Count; index++)
                {
                    StateExpectation expectation = operation.stateExpectations[index];
                    Transform branch = statesContainer.GetChild(index);
                    if (!string.Equals(branch.name, expectation.name, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "State component branch order differs at " + index + ": " + branch.name);
                    if (branch.childCount != expectation.childCount ||
                        CountRectTransforms(branch) != expectation.rectTransformCount)
                    {
                        throw new InvalidOperationException("State component branch content differs: " + branch.name);
                    }

                    var branchRect = branch as RectTransform;
                    if (branchRect != null &&
                        Vector2.Distance(branchRect.anchoredPosition, expectation.anchoredPosition) > 0.001f)
                    {
                        throw new InvalidOperationException("State component branch layout differs: " + branch.name);
                    }

                    bool shouldBeActive = index == operation.defaultStateIndex;
                    if (branch.gameObject.activeSelf != shouldBeActive)
                        throw new InvalidOperationException(
                            "State component must have exactly one active default state: " + branch.name);
                }
            }
        }

        private static GameObject CreateStateComponentPrefab(
            NativeStateExtractionOperation operation,
            string assetPath)
        {
            var builder = new GameObject(operation.prefabName, typeof(RectTransform));
            try
            {
                CopyRectTransformGeometry(operation.template, builder.transform);
                builder.layer = operation.template.gameObject.layer;
                builder.tag = operation.template.gameObject.tag;

                var statesContainer = new GameObject("[States]", typeof(RectTransform));
                statesContainer.transform.SetParent(builder.transform, false);
                CentreRect(statesContainer.GetComponent<RectTransform>());

                for (int index = 0; index < operation.stateSources.Count; index++)
                {
                    Transform source = operation.stateSources[index];
                    var clone = (GameObject)Object.Instantiate(source.gameObject);
                    clone.name = operation.stateNames[index];
                    clone.transform.SetParent(statesContainer.transform, false);
                    CopyRectTransformGeometry(source, clone.transform);
                    if (clone.transform is RectTransform cloneRect &&
                        operation.template is RectTransform templateRect)
                    {
                        cloneRect.anchoredPosition = RelativePosition(cloneRect, templateRect);
                    }

                    clone.SetActive(index == operation.defaultStateIndex);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(builder, assetPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not create the state component Prefab: " + assetPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(builder);
            }
        }

        private static void ReplaceStateSourcesWithInstance(
            NativeStateExtractionOperation operation,
            GameObject sharedAsset,
            IDictionary<int, Object> remap)
        {
            Transform parent = operation.template.parent;
            if (parent == null)
                throw new InvalidOperationException("Cannot replace state sources at the Prefab root.");
            int siblingIndex = operation.template.GetSiblingIndex();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(sharedAsset, parent);
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate the state component Prefab.");
            instance.transform.SetSiblingIndex(siblingIndex);
            instance.name = operation.template.name;
            instance.layer = operation.template.gameObject.layer;
            instance.tag = operation.template.gameObject.tag;
            CopyRectTransformGeometry(operation.template, instance.transform);

            Transform states = instance.transform.Find("[States]");
            if (states == null || states.childCount != operation.stateSources.Count)
                throw new InvalidOperationException("State component instance does not match its source branches.");
            for (int index = 0; index < operation.stateSources.Count; index++)
            {
                var branchMap = new Dictionary<Transform, Transform>();
                MapStructure(operation.stateSources[index], states.GetChild(index), branchMap);
                RegisterRemap(branchMap, remap);
            }

            foreach (Transform source in operation.stateSources)
            {
                if (source != null && source.gameObject != instance)
                    Object.DestroyImmediate(source.gameObject);
            }
        }

        private static Vector2 RelativePosition(RectTransform source, RectTransform template)
        {
            return source.anchoredPosition - template.anchoredPosition;
        }

        private static int CountRectTransforms(Transform node)
        {
            int count = node is RectTransform ? 1 : 0;
            for (int index = 0; index < node.childCount; index++)
            {
                count += CountRectTransforms(node.GetChild(index));
            }

            return count;
        }

        private static void CopyRectTransformGeometry(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            if (source is RectTransform sourceRect && target is RectTransform targetRect)
            {
                targetRect.anchorMin = sourceRect.anchorMin;
                targetRect.anchorMax = sourceRect.anchorMax;
                targetRect.pivot = sourceRect.pivot;
                targetRect.anchoredPosition = sourceRect.anchoredPosition;
                targetRect.sizeDelta = sourceRect.sizeDelta;
            }
        }

        private static void CentreRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        // ---------- 变体列表抽取（variantComponentExtractions） ----------

        internal static IReadOnlyList<NativeVariantExtractionOperation> BindVariantExtractions(
            JObject plan,
            GameObject root,
            PsdHierarchyChatContext context,
            string targetPrefabAssetPath,
            bool replaceExistingTargets = false)
        {
            JArray extractions = plan["variantComponentExtractions"] as JArray ?? new JArray();
            var operations = new List<NativeVariantExtractionOperation>();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (JToken token in extractions)
            {
                if (!(token is JObject extraction))
                    throw new InvalidDataException("variantComponentExtractions entries must be objects.");

                string id = ReadRequired(extraction, "id", "variantComponentExtractions");
                if (!usedIds.Add(id))
                    throw new InvalidDataException("Duplicate variantComponentExtractions id: " + id + ".");

                string label = "variantComponentExtractions[" + id + "]";
                string assetPath = ReadRequired(extraction, "assetPath", label);
                ValidateExtractionAssetPath(assetPath, targetPrefabAssetPath);
                if (!replaceExistingTargets && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                string templatePath = ResolveNodePath(context, extraction, "template", label);
                Transform template = FindByPath(root, templatePath)?.transform;
                if (template == null)
                    throw new InvalidDataException(label + ".template was not found in the loaded Prefab: " + templatePath);
                if (template.parent == null)
                    throw new InvalidDataException("Cannot extract variants from the Prefab root.");

                string defaultState = ReadRequired(extraction, "defaultState", label);
                string commonName = ReadOptional(extraction, "commonName");
                if (string.IsNullOrWhiteSpace(commonName))
                    commonName = "[Common]";
                string statesName = ReadOptional(extraction, "statesName");
                if (string.IsNullOrWhiteSpace(statesName))
                    statesName = "[States]";

                var states = new List<NativeVariantState>();
                foreach (JToken stateToken in extraction["states"] as JArray ?? new JArray())
                {
                    if (!(stateToken is JObject state))
                        throw new InvalidDataException(label + ".states entries must be objects.");
                    string stateId = ReadRequired(state, "id", label + ".states");
                    if (states.Any(item => string.Equals(item.id, stateId, StringComparison.Ordinal)))
                        throw new InvalidDataException(label + " repeats the state id: " + stateId + ".");
                    string stateName = ReadRequired(state, "name", label + ".states[" + stateId + "]");
                    string sourcePath = ResolveNodePath(context, state, "source", label + ".states[" + stateId + "]");
                    Transform source = FindByPath(root, sourcePath)?.transform;
                    if (source == null)
                        throw new InvalidDataException(label + ".states[" + stateId + "].source was not found: " + sourcePath);
                    if (source.parent != template.parent)
                        throw new InvalidDataException(
                            label + ".states[" + stateId + "].source must be a direct sibling of template: " + sourcePath);

                    var rect = source as RectTransform;
                    var templateRect = template as RectTransform;
                    states.Add(new NativeVariantState(stateId, sourcePath, source, stateName, new StateExpectation(
                        stateName,
                        source.childCount,
                        CountRectTransforms(source),
                        rect == null || templateRect == null ? Vector2.zero : rect.anchoredPosition - templateRect.anchoredPosition,
                        false)));
                }

                if (states.Count < 2)
                    throw new InvalidDataException(
                        label + ".states must list at least two distinct observed visual states.");

                int defaultStateIndex = states.FindIndex(item => string.Equals(item.id, defaultState, StringComparison.Ordinal));
                if (defaultStateIndex < 0)
                    throw new InvalidDataException(label + ".defaultState was not found in states: " + defaultState);

                var instances = new List<NativeVariantInstance>();
                var instancePaths = new List<string>();
                var instanceNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken instanceToken in extraction["instances"] as JArray ?? new JArray())
                {
                    if (!(instanceToken is JObject instance))
                        throw new InvalidDataException(label + ".instances entries must be objects.");
                    string sourcePath = ResolveNodePath(context, instance, "source", label + ".instances");
                    if (instancePaths.Contains(sourcePath, StringComparer.Ordinal))
                        throw new InvalidDataException(label + " repeats an instance source: " + sourcePath);
                    Transform source = FindByPath(root, sourcePath)?.transform;
                    if (source == null)
                        throw new InvalidDataException(label + ".instances source was not found: " + sourcePath);
                    if (source.parent != template.parent)
                        throw new InvalidDataException(
                            label + ".instances source must be a direct sibling of template: " + sourcePath);

                    string instanceName = ReadRequired(instance, "name", label + ".instances");
                    if (!instanceNames.Add(instanceName))
                        throw new InvalidDataException(label + " repeats an instance name: " + instanceName + ".");

                    string stateId = ReadRequired(instance, "state", label + ".instances[" + instanceName + "]");
                    NativeVariantState state = states.FirstOrDefault(item => string.Equals(item.id, stateId, StringComparison.Ordinal));
                    if (state == null)
                        throw new InvalidDataException(
                            label + ".instances[" + instanceName + "] selects an unknown state: " + stateId);
                    if (!IsStructurallyIdentical(state.source, source))
                        throw new InvalidDataException(
                            "Repeated unit structure differs for variant extraction: " + sourcePath +
                            " does not match its state source " + state.sourcePath + ".");

                    instancePaths.Add(sourcePath);
                    string parentPath = sourcePath.Substring(0, sourcePath.Length - source.name.Length);
                    instances.Add(new NativeVariantInstance(sourcePath, source, instanceName, stateId, parentPath + instanceName));
                }

                if (instances.Count == 0)
                    throw new InvalidDataException(label + ".instances must contain every visible repeated row.");
                foreach (NativeVariantState state in states)
                {
                    if (!instancePaths.Contains(state.sourcePath, StringComparer.Ordinal))
                        throw new InvalidDataException(
                            label + " must list the state representative " + state.sourcePath + " in instances.");
                }

                operations.Add(new NativeVariantExtractionOperation(
                    id,
                    assetPath,
                    Path.GetFileNameWithoutExtension(assetPath),
                    templatePath,
                    template,
                    states,
                    instances,
                    defaultStateIndex,
                    commonName,
                    statesName));
            }

            return operations;
        }

        internal static void ApplyVariantExtractions(
            GameObject root,
            IReadOnlyList<NativeVariantExtractionOperation> operations,
            bool save,
            out IReadOnlyList<string> temporaryAssets,
            bool replaceExistingTargets = false,
            Action beforeBusinessWrite = null)
        {
            temporaryAssets = Array.Empty<string>();
            if (operations == null || operations.Count == 0)
            {
                return;
            }

            var created = new List<string>();
            foreach (NativeVariantExtractionOperation operation in operations)
            {
                string assetPath = save ? operation.assetPath : BuildPreflightAssetPath(operation.assetPath, operation.prefabName);
                if (!save)
                {
                    created.Add(assetPath);
                }
                else
                {
                    beforeBusinessWrite?.Invoke();
                }

                EnsureAssetFolder(assetPath);
                if (!replaceExistingTargets && File.Exists(ToProjectFullPath(assetPath)))
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                GameObject sharedAsset = CreateVariantComponentPrefab(operation, assetPath);

                List<PendingReference> externalReferences = CollectExternalReferences(
                    root, operation.instances.Select(item => item.source).ToArray(), includeInternal: true);
                var remap = new Dictionary<int, Object>();
                foreach (NativeVariantInstance instance in operation.instances)
                {
                    ReplaceVariantSourceWithInstance(operation, instance, sharedAsset, remap);
                }

                ApplyExternalReferences(externalReferences, remap);
            }

            temporaryAssets = created;
        }

        internal static void VerifyVariantExtractions(
            GameObject root,
            IReadOnlyList<NativeVariantExtractionOperation> operations,
            bool persisted)
        {
            if (operations == null)
            {
                return;
            }

            foreach (NativeVariantExtractionOperation operation in operations)
            {
                bool assetExists = AssetDatabase.LoadMainAssetAtPath(operation.assetPath) != null;
                if (persisted && !assetExists)
                    throw new InvalidOperationException("Extracted Prefab asset is missing: " + operation.assetPath);
                if (!persisted && assetExists)
                    throw new InvalidOperationException(
                        "Preflight must not create the declared extraction asset: " + operation.assetPath);

                foreach (NativeVariantInstance instance in operation.instances)
                {
                    GameObject node = FindByPath(root, instance.expectedPath);
                    if (node == null)
                        throw new InvalidOperationException("Variant instance disappeared: " + instance.expectedPath);
                    if (!string.Equals(node.name, instance.name, StringComparison.Ordinal))
                        throw new InvalidOperationException("Variant instance name differs: " + node.name);

                    if (persisted)
                    {
                        string linked = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node);
                        if (!string.Equals(linked, operation.assetPath, StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "Variant instance is not linked to the shared Prefab: " + linked);
                    }

                    Transform common = node.transform.Find(operation.commonName);
                    Transform states = node.transform.Find(operation.statesName);
                    if (common == null || states == null)
                        throw new InvalidOperationException(
                            "Variant instance must contain " + operation.commonName + " and " + operation.statesName + ".");

                    NativeVariantState selected = operation.states
                        .First(item => string.Equals(item.id, instance.stateId, StringComparison.Ordinal));
                    if (states.childCount != operation.states.Count)
                        throw new InvalidOperationException("Variant instance state count differs: " + instance.expectedPath);

                    int activeCount = 0;
                    for (int index = 0; index < states.childCount; index++)
                    {
                        Transform branch = states.GetChild(index);
                        bool active = branch.gameObject.activeSelf;
                        if (active)
                        {
                            activeCount++;
                            if (!string.Equals(branch.name, selected.name, StringComparison.Ordinal))
                                throw new InvalidOperationException(
                                    "Variant instance activates the wrong state: " + branch.name +
                                    " instead of " + selected.name + " for " + instance.expectedPath);
                            if (branch.childCount != selected.expectation.childCount ||
                                CountRectTransforms(branch) != selected.expectation.rectTransformCount)
                            {
                                throw new InvalidOperationException(
                                    "Variant instance state content differs: " + instance.expectedPath);
                            }
                        }
                    }

                    if (activeCount != 1)
                        throw new InvalidOperationException(
                            "Variant instance must activate exactly one state: " + instance.expectedPath);
                }
            }
        }

        private static GameObject CreateVariantComponentPrefab(
            NativeVariantExtractionOperation operation,
            string assetPath)
        {
            var builder = new GameObject(operation.prefabName, typeof(RectTransform));
            try
            {
                CopyRectTransformGeometry(operation.template, builder.transform);
                builder.layer = operation.template.gameObject.layer;
                builder.tag = operation.template.gameObject.tag;

                var common = new GameObject(operation.commonName, typeof(RectTransform));
                common.transform.SetParent(builder.transform, false);
                CentreRect(common.GetComponent<RectTransform>());

                var statesContainer = new GameObject(operation.statesName, typeof(RectTransform));
                statesContainer.transform.SetParent(builder.transform, false);
                CentreRect(statesContainer.GetComponent<RectTransform>());

                for (int index = 0; index < operation.states.Count; index++)
                {
                    NativeVariantState state = operation.states[index];
                    var clone = (GameObject)Object.Instantiate(state.source.gameObject);
                    clone.name = state.name;
                    clone.transform.SetParent(statesContainer.transform, false);
                    CopyRectTransformGeometry(state.source, clone.transform);
                    if (clone.transform is RectTransform cloneRect && operation.template is RectTransform templateRect)
                    {
                        cloneRect.anchoredPosition = state.expectation.anchoredPosition;
                    }

                    clone.SetActive(index == operation.defaultStateIndex);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(builder, assetPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not create the variant component Prefab: " + assetPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(builder);
            }
        }

        private static void ReplaceVariantSourceWithInstance(
            NativeVariantExtractionOperation operation,
            NativeVariantInstance instance,
            GameObject sharedAsset,
            IDictionary<int, Object> remap)
        {
            Transform source = instance.source;
            Transform parent = source.parent;
            if (parent == null)
                throw new InvalidOperationException("Cannot replace the Prefab root with a variant instance.");

            int siblingIndex = source.GetSiblingIndex();
            bool sourceActive = source.gameObject.activeSelf;
            string sourceName = source.name;

            var node = (GameObject)PrefabUtility.InstantiatePrefab(sharedAsset, parent);
            if (node == null)
                throw new InvalidOperationException("Could not instantiate the variant component Prefab.");
            node.transform.SetSiblingIndex(siblingIndex);
            node.name = instance.name;
            node.layer = source.gameObject.layer;
            node.tag = source.gameObject.tag;
            CopyRectTransformGeometry(source, node.transform);
            node.SetActive(sourceActive);

            Transform states = node.transform.Find(operation.statesName);
            if (states == null)
                throw new InvalidOperationException("Variant component has no " + operation.statesName + " container.");
            NativeVariantState selected = operation.states
                .First(item => string.Equals(item.id, instance.stateId, StringComparison.Ordinal));
            Transform activeBranch = states.Find(selected.name);
            if (activeBranch == null)
                throw new InvalidOperationException("Variant component state was not found: " + selected.name);

            // 只把这一行的差异复制到它自己的状态分支上，然后把该分支设为唯一激活状态。
            var map = new Dictionary<Transform, Transform>();
            MapStructure(source, activeBranch, map);
            CopySourceValues(source, map, renameRoot: false);
            RegisterRemap(map, remap);
            for (int index = 0; index < states.childCount; index++)
            {
                states.GetChild(index).gameObject.SetActive(
                    string.Equals(states.GetChild(index).name, selected.name, StringComparison.Ordinal));
            }

            Object.DestroyImmediate(source.gameObject);
        }

        // ---------- 有状态重复项抽取（statefulComponentExtractions） ----------

        internal static IReadOnlyList<NativeStatefulExtractionOperation> BindStatefulExtractions(
            JObject plan,
            GameObject root,
            PsdHierarchyChatContext context,
            string targetPrefabAssetPath,
            bool replaceExistingTargets = false)
        {
            JArray extractions = plan["statefulComponentExtractions"] as JArray ?? new JArray();
            var operations = new List<NativeStatefulExtractionOperation>();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (JToken token in extractions)
            {
                if (!(token is JObject extraction))
                    throw new InvalidDataException("statefulComponentExtractions entries must be objects.");

                string id = ReadRequired(extraction, "id", "statefulComponentExtractions");
                if (!usedIds.Add(id))
                    throw new InvalidDataException("Duplicate statefulComponentExtractions id: " + id + ".");

                string label = "statefulComponentExtractions[" + id + "]";
                string assetPath = ReadRequired(extraction, "assetPath", label);
                ValidateExtractionAssetPath(assetPath, targetPrefabAssetPath);
                if (!replaceExistingTargets && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                string templatePath = ResolveNodePath(context, extraction, "template", label);
                Transform template = FindByPath(root, templatePath)?.transform;
                if (template == null)
                    throw new InvalidDataException(label + ".template was not found in the loaded Prefab: " + templatePath);
                if (template.parent == null)
                    throw new InvalidDataException("Cannot extract a stateful component from the Prefab root.");

                string defaultState = ReadRequired(extraction, "defaultState", label);
                string commonName = ReadOptional(extraction, "commonName");
                if (string.IsNullOrWhiteSpace(commonName))
                    commonName = "[Common]";
                string statesName = ReadOptional(extraction, "statesName");
                if (string.IsNullOrWhiteSpace(statesName))
                    statesName = "[States]";

                if (!(extraction["common"] is JObject common))
                    throw new InvalidDataException(label + ".common is required.");
                NativeStatefulBranch commonBranch = BindStatefulBranch(context, root, common, label + ".common");

                var states = new List<NativeStatefulState>();
                foreach (JToken stateToken in extraction["states"] as JArray ?? new JArray())
                {
                    if (!(stateToken is JObject state))
                        throw new InvalidDataException(label + ".states entries must be objects.");
                    string stateId = ReadRequired(state, "id", label + ".states");
                    if (states.Any(item => string.Equals(item.id, stateId, StringComparison.Ordinal)))
                        throw new InvalidDataException(label + " repeats the state id: " + stateId + ".");
                    string stateName = ReadRequired(state, "name", label + ".states[" + stateId + "]");
                    states.Add(new NativeStatefulState(
                        stateId,
                        stateName,
                        BindStatefulBranch(context, root, state, label + ".states[" + stateId + "]")));
                }

                if (states.Count == 0)
                    throw new InvalidDataException(label + ".states must not be empty.");
                int defaultStateIndex = states.FindIndex(item => string.Equals(item.id, defaultState, StringComparison.Ordinal));
                if (defaultStateIndex < 0)
                    throw new InvalidDataException(label + ".defaultState was not found in states: " + defaultState);

                var instances = new List<NativeStatefulInstance>();
                var instanceNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken instanceToken in extraction["instances"] as JArray ?? new JArray())
                {
                    if (!(instanceToken is JObject instance))
                        throw new InvalidDataException(label + ".instances entries must be objects.");
                    string sourcePath = ResolveNodePath(context, instance, "source", label + ".instances");
                    Transform source = FindByPath(root, sourcePath)?.transform;
                    if (source == null)
                        throw new InvalidDataException(label + ".instances source was not found: " + sourcePath);
                    if (source.parent != template.parent)
                        throw new InvalidDataException(
                            label + ".instances source must be a direct sibling of template: " + sourcePath);

                    string instanceName = ReadRequired(instance, "name", label + ".instances");
                    if (!instanceNames.Add(instanceName))
                        throw new InvalidDataException(label + " repeats an instance name: " + instanceName + ".");

                    string stateId = ReadRequired(instance, "state", label + ".instances[" + instanceName + "]");
                    NativeStatefulState selected = states
                        .FirstOrDefault(item => string.Equals(item.id, stateId, StringComparison.Ordinal));
                    if (selected == null)
                        throw new InvalidDataException(
                            label + ".instances[" + instanceName + "] selects an unknown state: " + stateId);

                    var commonSourceNames = ReadStringArray(instance, "commonSourceNames", label + ".instances[" + instanceName + "]");
                    var stateSourceNames = ReadStringArray(instance, "stateSourceNames", label + ".instances[" + instanceName + "]");

                    ValidateInstanceMemberMapping(
                        source, commonSourceNames, stateSourceNames, commonBranch, selected,
                        label + ".instances[" + instanceName + "]");

                    string parentPath = sourcePath.Substring(0, sourcePath.Length - source.name.Length);
                    instances.Add(new NativeStatefulInstance(
                        sourcePath, source, instanceName, stateId, parentPath + instanceName,
                        commonSourceNames, stateSourceNames));
                }

                if (instances.Count == 0)
                    throw new InvalidDataException(label + ".instances must contain every repeated item.");

                operations.Add(new NativeStatefulExtractionOperation(
                    id,
                    assetPath,
                    Path.GetFileNameWithoutExtension(assetPath),
                    templatePath,
                    template,
                    commonBranch,
                    states,
                    defaultStateIndex,
                    instances,
                    commonName,
                    statesName));
            }

            return operations;
        }

        private static NativeStatefulBranch BindStatefulBranch(
            PsdHierarchyChatContext context,
            GameObject root,
            JObject contract,
            string label)
        {
            string sourcePath = ResolveNodePath(context, contract, "source", label);
            Transform source = FindByPath(root, sourcePath)?.transform;
            if (source == null)
                throw new InvalidDataException(label + ".source was not found in the loaded Prefab: " + sourcePath);

            var members = new List<NativeStatefulMember>();
            foreach (JToken token in contract["members"] as JArray ?? new JArray())
            {
                if (!(token is JObject member))
                    throw new InvalidDataException(label + ".members entries must be objects.");
                string sourceName = ReadRequired(member, "sourceName", label + ".members");
                string targetName = ReadRequired(member, "name", label + ".members[" + sourceName + "]");
                if (members.Any(item => string.Equals(item.sourceName, sourceName, StringComparison.Ordinal)))
                    throw new InvalidDataException(label + " repeats a member source: " + sourceName + ".");
                if (FindDirectChild(source, sourceName) == null)
                    throw new InvalidDataException(
                        label + ".members references a missing direct child: " + sourceName + " in " + sourcePath);
                members.Add(new NativeStatefulMember(sourceName, targetName));
            }

            return new NativeStatefulBranch(sourcePath, source, members);
        }

        private static void ValidateInstanceMemberMapping(
            Transform source,
            IReadOnlyList<string> commonSourceNames,
            IReadOnlyList<string> stateSourceNames,
            NativeStatefulBranch commonBranch,
            NativeStatefulState selected,
            string label)
        {
            var mapped = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in commonSourceNames)
            {
                if (!mapped.Add(name))
                    throw new InvalidDataException(label + " maps a member twice: " + name);
                if (FindDirectChild(source, name) == null)
                    throw new InvalidDataException(label + " maps a missing member: " + name);
                if (!commonBranch.members.Any(item => string.Equals(item.sourceName, name, StringComparison.Ordinal)))
                    throw new InvalidDataException(label + " maps a member that the common contract does not declare: " + name);
            }

            foreach (string name in stateSourceNames)
            {
                if (!mapped.Add(name))
                    throw new InvalidDataException(label + " maps a member twice: " + name);
                if (FindDirectChild(source, name) == null)
                    throw new InvalidDataException(label + " maps a missing member: " + name);
                if (!selected.branch.members.Any(item => string.Equals(item.sourceName, name, StringComparison.Ordinal)))
                    throw new InvalidDataException(
                        label + " maps a member that state " + selected.id + " does not declare: " + name);
            }

            if (selected.branch.members.Count == 0 && stateSourceNames.Count != 0)
                throw new InvalidDataException(
                    label + " selects the all-common state " + selected.id + " and must not list stateSourceNames.");

            if (source.childCount != mapped.Count)
                throw new InvalidDataException(
                    label + " has an unmapped or duplicated direct child (" + source.childCount +
                    " children, " + mapped.Count + " mapped).");
            for (int index = 0; index < source.childCount; index++)
            {
                if (!mapped.Contains(source.GetChild(index).name))
                    throw new InvalidDataException(
                        label + " has an unmapped direct child: " + source.GetChild(index).name);
            }

            // 每个映射成员必须与声明的目标成员结构一致，否则差异无法安全保留。
            foreach (string name in commonSourceNames)
            {
                NativeStatefulMember member = commonBranch.members
                    .First(item => string.Equals(item.sourceName, name, StringComparison.Ordinal));
                Transform target = FindDirectChild(commonBranch.source, member.sourceName);
                if (!IsStructurallyIdentical(target, FindDirectChild(source, name)))
                    throw new InvalidDataException(
                        label + " member structure differs from the common contract: " + name);
            }

            foreach (string name in stateSourceNames)
            {
                NativeStatefulMember member = selected.branch.members
                    .First(item => string.Equals(item.sourceName, name, StringComparison.Ordinal));
                Transform target = FindDirectChild(selected.branch.source, member.sourceName);
                if (!IsStructurallyIdentical(target, FindDirectChild(source, name)))
                    throw new InvalidDataException(
                        label + " member structure differs from state " + selected.id + ": " + name);
            }
        }

        internal static void ApplyStatefulExtractions(
            GameObject root,
            IReadOnlyList<NativeStatefulExtractionOperation> operations,
            bool save,
            out IReadOnlyList<string> temporaryAssets,
            bool replaceExistingTargets = false,
            Action beforeBusinessWrite = null)
        {
            temporaryAssets = Array.Empty<string>();
            if (operations == null || operations.Count == 0)
            {
                return;
            }

            var created = new List<string>();
            foreach (NativeStatefulExtractionOperation operation in operations)
            {
                string assetPath = save ? operation.assetPath : BuildPreflightAssetPath(operation.assetPath, operation.prefabName);
                if (!save)
                {
                    created.Add(assetPath);
                }
                else
                {
                    beforeBusinessWrite?.Invoke();
                }

                EnsureAssetFolder(assetPath);
                if (!replaceExistingTargets && File.Exists(ToProjectFullPath(assetPath)))
                    throw new InvalidDataException("Extraction target already exists: " + assetPath);

                GameObject sharedAsset = CreateStatefulComponentPrefab(operation, assetPath);

                List<PendingReference> externalReferences = CollectExternalReferences(
                    root, operation.instances.Select(item => item.source).ToArray(), includeInternal: true);
                var remap = new Dictionary<int, Object>();
                foreach (NativeStatefulInstance instance in operation.instances)
                {
                    ReplaceStatefulSourceWithInstance(operation, instance, sharedAsset, remap);
                }

                ApplyExternalReferences(externalReferences, remap);
            }

            temporaryAssets = created;
        }

        internal static void VerifyStatefulExtractions(
            GameObject root,
            IReadOnlyList<NativeStatefulExtractionOperation> operations,
            bool persisted)
        {
            if (operations == null)
            {
                return;
            }

            foreach (NativeStatefulExtractionOperation operation in operations)
            {
                bool assetExists = AssetDatabase.LoadMainAssetAtPath(operation.assetPath) != null;
                if (persisted && !assetExists)
                    throw new InvalidOperationException("Extracted Prefab asset is missing: " + operation.assetPath);
                if (!persisted && assetExists)
                    throw new InvalidOperationException(
                        "Preflight must not create the declared extraction asset: " + operation.assetPath);

                foreach (NativeStatefulInstance instance in operation.instances)
                {
                    GameObject node = FindByPath(root, instance.expectedPath);
                    if (node == null)
                        throw new InvalidOperationException("Stateful instance disappeared: " + instance.expectedPath);
                    if (!string.Equals(node.name, instance.name, StringComparison.Ordinal))
                        throw new InvalidOperationException("Stateful instance name differs: " + node.name);

                    if (persisted)
                    {
                        string linked = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node);
                        if (!string.Equals(linked, operation.assetPath, StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "Stateful instance is not linked to the shared Prefab: " + linked);
                    }

                    Transform common = node.transform.Find(operation.commonName);
                    Transform states = node.transform.Find(operation.statesName);
                    if (common == null || states == null)
                        throw new InvalidOperationException(
                            "Stateful instance must contain " + operation.statesName + " and " + operation.commonName + ".");

                    // [States] 必须在 [Common] 之前创建，保证共享标签绘制在状态背景之上。
                    if (states.GetSiblingIndex() >= common.GetSiblingIndex())
                        throw new InvalidOperationException(
                            operation.statesName + " must be created before " + operation.commonName + ".");

                    if (common.childCount != operation.common.members.Count)
                        throw new InvalidOperationException("Stateful common member count differs: " + instance.expectedPath);
                    for (int index = 0; index < operation.common.members.Count; index++)
                    {
                        if (!string.Equals(common.GetChild(index).name, operation.common.members[index].targetName, StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "Stateful common member order differs: " + common.GetChild(index).name);
                    }

                    if (states.childCount != operation.states.Count)
                        throw new InvalidOperationException("Stateful state count differs: " + instance.expectedPath);
                    NativeStatefulState selected = operation.states
                        .First(item => string.Equals(item.id, instance.stateId, StringComparison.Ordinal));

                    int activeCount = 0;
                    for (int index = 0; index < states.childCount; index++)
                    {
                        Transform branch = states.GetChild(index);
                        NativeStatefulState state = operation.states[index];
                        if (!string.Equals(branch.name, state.name, StringComparison.Ordinal))
                            throw new InvalidOperationException("Stateful state order differs: " + branch.name);
                        if (branch.childCount != state.branch.members.Count)
                            throw new InvalidOperationException("Stateful state member count differs: " + branch.name);
                        for (int memberIndex = 0; memberIndex < state.branch.members.Count; memberIndex++)
                        {
                            if (!string.Equals(
                                    branch.GetChild(memberIndex).name,
                                    state.branch.members[memberIndex].targetName,
                                    StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException(
                                    "Stateful state member order differs: " + branch.name + "/" + branch.GetChild(memberIndex).name);
                            }
                        }

                        if (branch.gameObject.activeSelf)
                        {
                            activeCount++;
                            if (!string.Equals(branch.name, selected.name, StringComparison.Ordinal))
                                throw new InvalidOperationException(
                                    "Stateful instance activates the wrong state: " + branch.name +
                                    " instead of " + selected.name + " for " + instance.expectedPath);
                        }
                    }

                    if (activeCount != 1)
                        throw new InvalidOperationException(
                            "Stateful instance must activate exactly one state: " + instance.expectedPath);
                }
            }
        }

        private static GameObject CreateStatefulComponentPrefab(
            NativeStatefulExtractionOperation operation,
            string assetPath)
        {
            var builder = new GameObject(operation.prefabName, typeof(RectTransform));
            try
            {
                CopyRectTransformGeometry(operation.template, builder.transform);
                builder.layer = operation.template.gameObject.layer;
                builder.tag = operation.template.gameObject.tag;

                var statesContainer = new GameObject(operation.statesName, typeof(RectTransform));
                statesContainer.transform.SetParent(builder.transform, false);
                CentreRect(statesContainer.GetComponent<RectTransform>());

                var commonContainer = new GameObject(operation.commonName, typeof(RectTransform));
                commonContainer.transform.SetParent(builder.transform, false);
                CentreRect(commonContainer.GetComponent<RectTransform>());

                for (int index = 0; index < operation.common.members.Count; index++)
                {
                    CloneMember(
                        operation.common.source,
                        operation.common.members[index].sourceName,
                        operation.common.members[index].targetName,
                        commonContainer.transform);
                }

                for (int index = 0; index < operation.states.Count; index++)
                {
                    NativeStatefulState state = operation.states[index];
                    var stateContainer = new GameObject(state.name, typeof(RectTransform));
                    stateContainer.transform.SetParent(statesContainer.transform, false);
                    CentreRect(stateContainer.GetComponent<RectTransform>());
                    foreach (NativeStatefulMember member in state.branch.members)
                    {
                        CloneMember(state.branch.source, member.sourceName, member.targetName, stateContainer.transform);
                    }

                    stateContainer.SetActive(index == operation.defaultStateIndex);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(builder, assetPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not create the stateful component Prefab: " + assetPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(builder);
            }
        }

        private static void ReplaceStatefulSourceWithInstance(
            NativeStatefulExtractionOperation operation,
            NativeStatefulInstance instance,
            GameObject sharedAsset,
            IDictionary<int, Object> remap)
        {
            Transform source = instance.source;
            Transform parent = source.parent;
            if (parent == null)
                throw new InvalidOperationException("Cannot replace the Prefab root with a stateful instance.");

            int siblingIndex = source.GetSiblingIndex();
            bool sourceActive = source.gameObject.activeSelf;

            var node = (GameObject)PrefabUtility.InstantiatePrefab(sharedAsset, parent);
            if (node == null)
                throw new InvalidOperationException("Could not instantiate the stateful component Prefab.");
            node.transform.SetSiblingIndex(siblingIndex);
            node.name = instance.name;
            node.layer = source.gameObject.layer;
            node.tag = source.gameObject.tag;
            CopyRectTransformGeometry(source, node.transform);
            node.SetActive(sourceActive);

            Transform common = node.transform.Find(operation.commonName);
            Transform states = node.transform.Find(operation.statesName);
            if (common == null || states == null)
                throw new InvalidOperationException("Stateful component is missing its containers.");

            NativeStatefulState selected = operation.states
                .First(item => string.Equals(item.id, instance.stateId, StringComparison.Ordinal));
            Transform activeBranch = states.Find(selected.name);
            if (activeBranch == null)
                throw new InvalidOperationException("Stateful component state was not found: " + selected.name);

            // 公共成员：把这一行自己的取值复制到 [Common] 的对应成员上。
            foreach (string sourceName in instance.commonSourceNames)
            {
                NativeStatefulMember member = operation.common.members
                    .First(item => string.Equals(item.sourceName, sourceName, StringComparison.Ordinal));
                CopyMemberValues(
                    FindDirectChild(source, sourceName),
                    FindDirectChild(common, member.targetName),
                    remap);
            }

            // 状态成员：只复制到这一行选中的状态分支上。
            foreach (string sourceName in instance.stateSourceNames)
            {
                NativeStatefulMember member = selected.branch.members
                    .First(item => string.Equals(item.sourceName, sourceName, StringComparison.Ordinal));
                CopyMemberValues(
                    FindDirectChild(source, sourceName),
                    FindDirectChild(activeBranch, member.targetName),
                    remap);
            }

            for (int index = 0; index < states.childCount; index++)
            {
                states.GetChild(index).gameObject.SetActive(
                    string.Equals(states.GetChild(index).name, selected.name, StringComparison.Ordinal));
            }

            Object.DestroyImmediate(source.gameObject);
        }

        private static void CloneMember(
            Transform sourceParent,
            string sourceName,
            string targetName,
            Transform destinationParent)
        {
            Transform source = FindDirectChild(sourceParent, sourceName);
            if (source == null)
                throw new InvalidDataException("Member source was not found: " + sourceName);

            var clone = (GameObject)Object.Instantiate(source.gameObject);
            clone.name = targetName;
            clone.transform.SetParent(destinationParent, false);
            CopyRectTransformGeometry(source, clone.transform);
            clone.SetActive(source.gameObject.activeSelf);
        }

        private static void CopyMemberValues(
            Transform sourceChild,
            Transform targetMember,
            IDictionary<int, Object> remap)
        {
            if (sourceChild == null)
                throw new InvalidDataException("Member source child was not found.");
            if (targetMember == null)
                throw new InvalidDataException("Member target child was not found in the shared Prefab.");

            var map = new Dictionary<Transform, Transform>();
            MapStructure(sourceChild, targetMember, map);
            CopySourceValues(sourceChild, map, renameRoot: false);
            RegisterRemap(map, remap);
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;
            for (int index = 0; index < parent.childCount; index++)
            {
                if (string.Equals(parent.GetChild(index).name, name, StringComparison.Ordinal))
                    return parent.GetChild(index);
            }

            return null;
        }

        private static IReadOnlyList<string> ReadStringArray(JObject owner, string field, string label)
        {
            if (!(owner[field] is JArray array))
                throw new InvalidDataException(label + "." + field + " must be an array.");
            var values = new List<string>();
            foreach (JToken token in array)
            {
                if (token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>()))
                    throw new InvalidDataException(label + "." + field + " must contain non-empty strings.");
                values.Add(token.Value<string>());
            }

            return values;
        }

        // ---------- 局部选区抽取（selectedPrefabExtractions） ----------

        internal static IReadOnlyList<NativeSelectedExtractionOperation> BindSelectedExtractions(
            JObject plan,
            GameObject root,
            PsdHierarchyChatContext context,
            string targetPrefabAssetPath,
            bool replaceExistingTargets = false)
        {
            JArray extractions = plan["selectedPrefabExtractions"] as JArray ?? new JArray();
            var operations = new List<NativeSelectedExtractionOperation>();
            if (extractions.Count == 0)
            {
                return operations;
            }

            if (context.localRepairScope == null)
                throw new InvalidDataException(
                    "selectedPrefabExtractions can only be produced by a locked local repair selection.");
            if (extractions.Count != 1)
                throw new InvalidDataException("A local repair plan can contain exactly one selectedPrefabExtractions entry.");

            if (!(extractions[0] is JObject extraction))
                throw new InvalidDataException("selectedPrefabExtractions entries must be objects.");

            const string label = "selectedPrefabExtractions[0]";
            string id = ReadOptional(extraction, "id");
            string prefabName = ReadRequired(extraction, "name", label);
            if (!IsPascalCaseIdentifier(prefabName))
                throw new InvalidDataException(label + ".name must be a PascalCase identifier: " + prefabName);
            if (!string.IsNullOrEmpty(id) && !IsPascalCaseIdentifier(id) && id.IndexOf('_') < 0)
                throw new InvalidDataException(label + ".id must be a lower_snake_case identifier: " + id);

            string assetPath = ReadRequired(extraction, "assetPath", label);
            ValidateExtractionAssetPath(assetPath, targetPrefabAssetPath);
            if (!string.Equals(Path.GetFileNameWithoutExtension(assetPath), prefabName, StringComparison.Ordinal))
                throw new InvalidDataException(label + ".assetPath file name must equal .name: " + assetPath);
            if (!replaceExistingTargets && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new InvalidDataException("Extraction target already exists: " + assetPath);

            string parentPath = ResolveNodePath(context, extraction, "parent", label);
            Transform parent = FindByPath(root, parentPath)?.transform;
            if (parent == null)
                throw new InvalidDataException(label + ".parent was not found in the loaded Prefab: " + parentPath);
            if (!(parent is RectTransform))
                throw new InvalidDataException(label + ".parent must be a RectTransform.");

            JArray sources = extraction["sources"] as JArray;
            if (sources == null || sources.Count < 2)
                throw new InvalidDataException(label + " requires at least two selected direct children.");

            var sourcePaths = new List<string>();
            var sourceTransforms = new List<Transform>();
            foreach (JToken source in sources)
            {
                string sourcePath = ResolveNodePath(context, source, null, label + ".sources");
                if (sourcePaths.Contains(sourcePath, StringComparer.Ordinal))
                    throw new InvalidDataException(label + ".sources contains a duplicate node: " + sourcePath);
                Transform transform = FindByPath(root, sourcePath)?.transform;
                if (transform == null)
                    throw new InvalidDataException(label + ".sources was not found: " + sourcePath);
                if (transform.parent != parent)
                    throw new InvalidDataException(
                        label + ".sources must be direct children of " + parentPath + ": " + sourcePath);
                if (!(transform is RectTransform))
                    throw new InvalidDataException(label + ".sources must all use RectTransform: " + sourcePath);

                sourcePaths.Add(sourcePath);
                sourceTransforms.Add(transform);
            }

            // 折叠多个同级根会让外部引用变成歧义引用：必须在写入前拒绝。
            List<PendingReference> external = CollectExternalReferences(root, sourceTransforms);
            if (external.Count > 0)
                throw new InvalidDataException(
                    label + " sources are referenced from outside the selected nodes (" +
                    external.Count + " serialized reference(s)); collapsing them would break those references.");

            // 以原始同级顺序建立组件内容，实例落在最小同级位置。
            var orderedPairs = sourcePaths
                .Select((path, index) => new { path, transform = sourceTransforms[index] })
                .OrderBy(item => item.transform.GetSiblingIndex())
                .ToArray();

            operations.Add(new NativeSelectedExtractionOperation(
                string.IsNullOrEmpty(id) ? prefabName : id,
                assetPath,
                prefabName,
                parentPath,
                parent,
                orderedPairs.Select(item => item.path).ToArray(),
                orderedPairs.Select(item => item.transform).ToArray()));
            return operations;
        }

        /// <summary>局部选区抽取在预检阶段只做校验，不创建资产也不改动层级。</summary>
        internal static void ApplySelectedExtractions(
            GameObject root,
            IReadOnlyList<NativeSelectedExtractionOperation> operations,
            bool save,
            bool replaceExistingTargets = false,
            Action beforeBusinessWrite = null)
        {
            if (!save || operations == null || operations.Count == 0)
            {
                return;
            }

            foreach (NativeSelectedExtractionOperation operation in operations)
            {
                beforeBusinessWrite?.Invoke();
                EnsureAssetFolder(operation.assetPath);
                if (!replaceExistingTargets && File.Exists(ToProjectFullPath(operation.assetPath)))
                    throw new InvalidDataException("Extraction target already exists: " + operation.assetPath);

                var parentRect = operation.parent as RectTransform;
                int siblingIndex = operation.sourceTransforms.Min(source => source.GetSiblingIndex());

                var wrapper = new GameObject(operation.prefabName, typeof(RectTransform));
                try
                {
                    var wrapperRect = wrapper.GetComponent<RectTransform>();
                    wrapperRect.SetParent(operation.parent, false);
                    CentreRect(wrapperRect);
                    wrapperRect.SetSiblingIndex(siblingIndex);

                    foreach (Transform source in operation.sourceTransforms)
                    {
                        source.SetParent(wrapperRect, true);
                    }

                    TightenRectToChildren(wrapperRect, parentRect);

                    GameObject componentAsset = PrefabUtility.SaveAsPrefabAsset(wrapper, operation.assetPath);
                    if (componentAsset == null)
                        throw new InvalidOperationException(
                            "Could not create the selected Nested Prefab: " + operation.assetPath);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(componentAsset, operation.parent);
                    if (instance == null)
                        throw new InvalidOperationException(
                            "Could not instantiate the selected Nested Prefab: " + operation.assetPath);
                    instance.name = operation.prefabName;
                    CopyRectTransformGeometry(wrapperRect, instance.transform);
                    instance.transform.SetSiblingIndex(siblingIndex);
                }
                finally
                {
                    Object.DestroyImmediate(wrapper);
                }
            }
        }

        internal static void VerifySelectedExtractions(
            GameObject root,
            IReadOnlyList<NativeSelectedExtractionOperation> operations,
            bool persisted)
        {
            if (!persisted || operations == null)
            {
                return;
            }

            foreach (NativeSelectedExtractionOperation operation in operations)
            {
                if (AssetDatabase.LoadMainAssetAtPath(operation.assetPath) == null)
                    throw new InvalidOperationException("Extracted Prefab asset is missing: " + operation.assetPath);

                string instancePath = operation.parentPath.TrimEnd('/') + "/" + operation.prefabName;
                GameObject instance = FindByPath(root, instancePath);
                if (instance == null)
                    throw new InvalidOperationException("Selected extraction instance was not found: " + instancePath);

                string linked = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
                if (!string.Equals(linked, operation.assetPath, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Selected extraction instance is not linked to the shared Prefab: " + linked);

                if (instance.transform.childCount != operation.sourcePaths.Count)
                    throw new InvalidOperationException(
                        "Selected extraction instance child count differs: " + instance.transform.childCount);
                for (int index = 0; index < operation.sourcePaths.Count; index++)
                {
                    string expectedName = operation.sourcePaths[index]
                        .Substring(operation.sourcePaths[index].LastIndexOf('/') + 1);
                    if (!string.Equals(instance.transform.GetChild(index).name, expectedName, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Selected extraction instance order differs at " + index + ": " +
                            instance.transform.GetChild(index).name);
                }

                // 折叠后的容器必须覆盖被抽取节点的实际范围。
                var instanceRect = instance.transform as RectTransform;
                var parentRect = operation.parent as RectTransform;
                if (instanceRect != null && parentRect != null)
                {
                    Bounds bounds = default;
                    bool initialized = false;
                    for (int index = 0; index < instanceRect.childCount; index++)
                    {
                        if (!(instanceRect.GetChild(index) is RectTransform child))
                            continue;
                        var corners = new Vector3[4];
                        child.GetWorldCorners(corners);
                        for (int corner = 0; corner < corners.Length; corner++)
                        {
                            Vector3 point = parentRect.InverseTransformPoint(corners[corner]);
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
                        throw new InvalidOperationException("Selected extraction instance has no children: " + instancePath);
                    if (Vector2.Distance(instanceRect.sizeDelta, new Vector2(bounds.size.x, bounds.size.y)) > 0.5f ||
                        Vector2.Distance(instanceRect.anchoredPosition, new Vector2(bounds.center.x, bounds.center.y)) > 0.5f)
                    {
                        throw new InvalidOperationException(
                            "Selected extraction container does not cover its children: " + instancePath +
                            " size=" + instanceRect.sizeDelta + " expectedSize=" + new Vector2(bounds.size.x, bounds.size.y) +
                            " position=" + instanceRect.anchoredPosition + " expectedPosition=" +
                            new Vector2(bounds.center.x, bounds.center.y) +
                            " parentAnchors=" + parentRect.anchorMin + "/" + parentRect.anchorMax);
                    }
                }
            }
        }

        /// <summary>
        /// 让折叠容器正好覆盖子节点：先把子节点交还给父节点（保持世界位置），再调整容器矩形，
        /// 最后按世界位置把子节点放回容器——否则改变容器矩形会把子节点一起挪走。
        /// </summary>
        private static void TightenRectToChildren(RectTransform rect, RectTransform parent)
        {
            Bounds bounds = default;
            bool initialized = false;
            for (int index = 0; index < rect.childCount; index++)
            {
                if (!(rect.GetChild(index) is RectTransform child))
                    continue;
                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    Vector3 point = parent.InverseTransformPoint(corners[corner]);
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
                throw new InvalidOperationException("Cannot tighten an empty selected extraction wrapper.");

            var children = new List<Transform>();
            for (int index = 0; index < rect.childCount; index++)
            {
                children.Add(rect.GetChild(index));
            }

            foreach (Transform child in children)
            {
                child.SetParent(parent, true);
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
            rect.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);

            foreach (Transform child in children)
            {
                child.SetParent(rect, true);
            }
        }

        // ---------- 跨父级抽取（crossParentPrefabExtractions） ----------

        internal static IReadOnlyList<NativeCrossParentExtractionOperation> BindCrossParentExtractions(
            JObject plan,
            GameObject root,
            PsdHierarchyChatContext context,
            string targetPrefabAssetPath,
            bool replaceExistingTargets = false)
        {
            JArray extractions = plan["crossParentPrefabExtractions"] as JArray ?? new JArray();
            var operations = new List<NativeCrossParentExtractionOperation>();
            if (extractions.Count == 0)
            {
                return operations;
            }

            if (context.localRepairScope == null)
                throw new InvalidDataException(
                    "crossParentPrefabExtractions can only be produced by a locked local repair selection.");
            if (extractions.Count != 1)
                throw new InvalidDataException(
                    "A local repair plan can contain exactly one crossParentPrefabExtractions entry.");
            if (!(extractions[0] is JObject extraction))
                throw new InvalidDataException("crossParentPrefabExtractions entries must be objects.");

            const string label = "crossParentPrefabExtractions[0]";
            string id = ReadOptional(extraction, "id");
            string prefabName = ReadRequired(extraction, "name", label);
            if (!IsPascalCaseIdentifier(prefabName))
                throw new InvalidDataException(label + ".name must be a PascalCase identifier: " + prefabName);

            string assetPath = ReadRequired(extraction, "assetPath", label);
            ValidateExtractionAssetPath(assetPath, targetPrefabAssetPath);
            if (!string.Equals(Path.GetFileNameWithoutExtension(assetPath), prefabName, StringComparison.Ordinal))
                throw new InvalidDataException(label + ".assetPath file name must equal .name: " + assetPath);
            if (!replaceExistingTargets && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new InvalidDataException("Extraction target already exists: " + assetPath);

            string rootPath = ResolveNodePath(context, extraction, "root", label);
            Transform commonRoot = FindByPath(root, rootPath)?.transform;
            if (commonRoot == null)
                throw new InvalidDataException(label + ".root was not found in the loaded Prefab: " + rootPath);
            if (!(commonRoot is RectTransform))
                throw new InvalidDataException(label + ".root must be a RectTransform.");

            JArray templateSources = extraction["templateSources"] as JArray;
            if (templateSources == null || templateSources.Count < 2)
                throw new InvalidDataException(label + ".templateSources requires at least two nodes.");
            var templatePaths = new List<string>();
            var templateTransforms = new List<Transform>();
            foreach (JToken token in templateSources)
            {
                string path = ResolveNodePath(context, token, null, label + ".templateSources");
                if (templatePaths.Contains(path, StringComparer.Ordinal))
                    throw new InvalidDataException(label + ".templateSources repeats a node: " + path);
                Transform transform = FindByPath(root, path)?.transform;
                if (transform == null)
                    throw new InvalidDataException(label + ".templateSources was not found: " + path);
                if (transform == commonRoot || !transform.IsChildOf(commonRoot))
                    throw new InvalidDataException(
                        label + ".templateSources must be descendants of the common root: " + path);
                AssertCrossParentSourceIsSafe(commonRoot, transform, label);
                templatePaths.Add(path);
                templateTransforms.Add(transform);
            }

            if (!(extraction["instances"] is JArray instanceOperations) || instanceOperations.Count < 2)
                throw new InvalidDataException(label + ".instances requires at least two complete reviewed groups.");

            var groups = new List<NativeCrossParentGroup>();
            var claimed = new List<Transform>();
            bool includesTemplate = false;
            for (int index = 0; index < instanceOperations.Count; index++)
            {
                if (!(instanceOperations[index] is JObject group))
                    throw new InvalidDataException(label + ".instances entries must be objects.");
                string groupLabel = label + ".instances[" + index + "]";
                JArray groupSources = group["sources"] as JArray;
                if (groupSources == null || groupSources.Count != templatePaths.Count)
                    throw new InvalidDataException(groupLabel + " must contain the same member count as templateSources.");

                var paths = new List<string>();
                var transforms = new List<Transform>();
                foreach (JToken token in groupSources)
                {
                    string path = ResolveNodePath(context, token, null, groupLabel + ".sources");
                    if (paths.Contains(path, StringComparer.Ordinal))
                        throw new InvalidDataException(groupLabel + ".sources repeats a node: " + path);
                    Transform transform = FindByPath(root, path)?.transform;
                    if (transform == null)
                        throw new InvalidDataException(groupLabel + ".sources was not found: " + path);
                    if (transform == commonRoot || !transform.IsChildOf(commonRoot))
                        throw new InvalidDataException(
                            groupLabel + ".sources must be descendants of the common root: " + path);
                    if (claimed.Any(item => item == transform))
                        throw new InvalidDataException(groupLabel + " reuses a source from another group: " + path);
                    AssertCrossParentSourceIsSafe(commonRoot, transform, groupLabel);
                    paths.Add(path);
                    transforms.Add(transform);
                }

                includesTemplate |= transforms.Count == templateTransforms.Count &&
                                    !transforms.Where((item, memberIndex) => item != templateTransforms[memberIndex]).Any();
                claimed.AddRange(transforms);
                int sequence = (group["sequence"]?.Type == JTokenType.Integer)
                    ? group.Value<int>("sequence")
                    : index + 1;
                groups.Add(new NativeCrossParentGroup(sequence, paths, transforms));
            }

            if (!includesTemplate)
                throw new InvalidDataException(label + ".instances must include the templateSources group.");

            List<PendingReference> external = CollectExternalReferences(root, claimed);
            if (external.Count > 0)
                throw new InvalidDataException(
                    label + " sources are referenced from outside the reviewed groups (" +
                    external.Count + " serialized reference(s)); extracting them would break those references.");

            var unmatchedPaths = new List<string>();
            foreach (JToken token in extraction["unmatched"] as JArray ?? new JArray())
            {
                string path = ResolveNodePath(context, token, null, label + ".unmatched");
                Transform transform = FindByPath(root, path)?.transform;
                if (transform == null)
                    throw new InvalidDataException(label + ".unmatched was not found: " + path);
                if (claimed.Any(item => item == transform))
                    throw new InvalidDataException(label + ".unmatched must not belong to a reviewed group: " + path);
                unmatchedPaths.Add(path);
            }

            operations.Add(new NativeCrossParentExtractionOperation(
                string.IsNullOrEmpty(id) ? prefabName : id,
                assetPath,
                prefabName,
                rootPath,
                commonRoot,
                templatePaths,
                templateTransforms,
                groups.OrderBy(item => item.sequence).ToArray(),
                unmatchedPaths));
            return operations;
        }

        /// <summary>跨父级抽取在预检阶段只做校验，不创建资产也不改动层级。</summary>
        internal static void ApplyCrossParentExtractions(
            GameObject root,
            IReadOnlyList<NativeCrossParentExtractionOperation> operations,
            bool save,
            bool replaceExistingTargets = false,
            Action beforeBusinessWrite = null)
        {
            if (!save || operations == null || operations.Count == 0)
            {
                return;
            }

            foreach (NativeCrossParentExtractionOperation operation in operations)
            {
                beforeBusinessWrite?.Invoke();
                EnsureAssetFolder(operation.assetPath);
                if (!replaceExistingTargets && File.Exists(ToProjectFullPath(operation.assetPath)))
                    throw new InvalidDataException("Extraction target already exists: " + operation.assetPath);

                var wrapper = new GameObject(operation.prefabName, typeof(RectTransform));
                bool assetCreated = false;
                try
                {
                    var wrapperRect = wrapper.GetComponent<RectTransform>();
                    wrapperRect.SetParent(operation.root, false);
                    CentreRect(wrapperRect);
                    wrapperRect.SetSiblingIndex(operation.root.childCount);

                    foreach (Transform source in operation.templateTransforms)
                    {
                        source.SetParent(wrapperRect, true);
                    }

                    GameObject componentAsset = PrefabUtility.SaveAsPrefabAsset(wrapper, operation.assetPath);
                    if (componentAsset == null)
                        throw new InvalidOperationException(
                            "Could not create the cross-parent Prefab: " + operation.assetPath);
                    assetCreated = true;

                    foreach (NativeCrossParentGroup group in operation.groups)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(componentAsset, operation.root);
                        if (instance == null)
                            throw new InvalidOperationException(
                                "Could not instantiate the cross-parent Prefab: " + operation.assetPath);
                        CentreRect(instance.GetComponent<RectTransform>());

                        if (instance.transform.childCount != group.sources.Count)
                            throw new InvalidOperationException(
                                "Cross-parent Prefab member count changed after instantiation: " + operation.assetPath);

                        for (int index = 0; index < group.sources.Count; index++)
                        {
                            CopyCrossParentMemberState(
                                group.sources[index], instance.transform.GetChild(index));
                        }
                    }

                    foreach (NativeCrossParentGroup group in operation.groups)
                    {
                        foreach (Transform source in group.sources)
                        {
                            if (source != null)
                                Object.DestroyImmediate(source.gameObject);
                        }
                    }
                }
                catch (Exception)
                {
                    if (assetCreated)
                        DeleteAssetQuietly(operation.assetPath);
                    throw;
                }
                finally
                {
                    if (wrapper != null)
                        Object.DestroyImmediate(wrapper);
                }
            }
        }

        internal static void VerifyCrossParentExtractions(
            GameObject root,
            IReadOnlyList<NativeCrossParentExtractionOperation> operations,
            bool persisted)
        {
            if (!persisted || operations == null)
            {
                return;
            }

            foreach (NativeCrossParentExtractionOperation operation in operations)
            {
                if (AssetDatabase.LoadMainAssetAtPath(operation.assetPath) == null)
                    throw new InvalidOperationException("Extracted Prefab asset is missing: " + operation.assetPath);

                Transform container = FindByPath(root, operation.rootPath)?.transform;
                if (container == null)
                    throw new InvalidOperationException("Common root disappeared: " + operation.rootPath);

                var instances = new List<Transform>();
                for (int index = 0; index < container.childCount; index++)
                {
                    Transform child = container.GetChild(index);
                    if (string.Equals(
                            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject),
                            operation.assetPath,
                            StringComparison.Ordinal))
                    {
                        instances.Add(child);
                    }
                }

                if (instances.Count != operation.groups.Count)
                    throw new InvalidOperationException(
                        "Cross-parent instance count differs: " + instances.Count + " instead of " + operation.groups.Count);

                foreach (Transform instance in instances)
                {
                    if (instance.childCount != operation.templatePaths.Count)
                        throw new InvalidOperationException(
                            "Cross-parent instance member count differs: " + instance.name);
                    for (int index = 0; index < operation.templatePaths.Count; index++)
                    {
                        string expected = operation.templatePaths[index]
                            .Substring(operation.templatePaths[index].LastIndexOf('/') + 1);
                        if (!string.Equals(instance.GetChild(index).name, expected, StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "Cross-parent member order differs at " + index + ": " + instance.GetChild(index).name);
                    }
                }

                foreach (NativeCrossParentGroup group in operation.groups)
                {
                    foreach (string path in group.sourcePaths)
                    {
                        if (FindByPath(root, path) != null)
                            throw new InvalidOperationException(
                                "Cross-parent source was not extracted: " + path);
                    }
                }

                foreach (string path in operation.unmatchedPaths)
                {
                    if (FindByPath(root, path) == null)
                        throw new InvalidOperationException("Unmatched node disappeared: " + path);
                }
            }
        }

        private static void CopyCrossParentMemberState(Transform source, Transform destination)
        {
            var map = new Dictionary<Transform, Transform>();
            MapStructure(source, destination, map);
            CopySourceValues(source, map, renameRoot: false, renameChildren: false);

            // 成员来自不同父节点：按世界位置对齐，并把容器锚点归一，避免继承原父节点的坐标系。
            destination.position = source.position;
            destination.rotation = source.rotation;
            destination.localScale = source.localScale;
            if (destination is RectTransform destinationRect)
            {
                destinationRect.anchorMin = new Vector2(0.5f, 0.5f);
                destinationRect.anchorMax = new Vector2(0.5f, 0.5f);
                if (source is RectTransform sourceRect)
                {
                    destinationRect.pivot = sourceRect.pivot;
                    destinationRect.sizeDelta = sourceRect.rect.size;
                }
            }
        }

        private static void AssertCrossParentSourceIsSafe(Transform commonRoot, Transform source, string label)
        {
            for (Transform current = source.parent; current != null && current != commonRoot; current = current.parent)
            {
                if (current.localScale != Vector3.one ||
                    Quaternion.Angle(current.localRotation, Quaternion.identity) > 0.001f)
                {
                    throw new InvalidDataException(
                        label + " cannot cross a scaled or rotated UI parent: " + current.name + ".");
                }

                foreach (Component component in current.GetComponents<Component>())
                {
                    if (component == null || component is Transform)
                        continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "Canvas" || typeName == "CanvasGroup" || typeName == "Mask" ||
                        typeName == "RectMask2D" || typeName == "ScrollRect" ||
                        typeName.Contains("Layout", StringComparison.Ordinal) ||
                        typeName == "ContentSizeFitter" || typeName == "AspectRatioFitter")
                    {
                        throw new InvalidDataException(
                            label + " cannot cross UI behavior parent: " + current.name + " (" + typeName + ").");
                    }
                }
            }
        }

        internal static bool IsStructurallyIdentical(Transform template, Transform candidate)
        {
            if (template.childCount != candidate.childCount)
                return false;
            if (!ComponentSignature(template).SequenceEqual(ComponentSignature(candidate), StringComparer.Ordinal))
                return false;
            for (int index = 0; index < template.childCount; index++)
            {
                if (!IsStructurallyIdentical(template.GetChild(index), candidate.GetChild(index)))
                    return false;
            }

            return true;
        }

        /// <summary>只比较组件类型序列：节点名与子节点名都是逐实例覆盖，不参与结构判定。</summary>
        private static IEnumerable<string> ComponentSignature(Transform node)
        {
            foreach (Component component in node.GetComponents<Component>())
            {
                yield return component == null ? "<missing>" : component.GetType().FullName;
            }
        }

        private static void ValidateExtractionAssetPath(string assetPath, string targetPrefabAssetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                assetPath.Contains("..", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "componentExtractions assetPath must be a project-relative Assets/*.prefab path: " + assetPath);
            }

            if (string.Equals(assetPath, targetPrefabAssetPath, StringComparison.Ordinal))
                throw new InvalidDataException("An extraction cannot target the Prefab being cleaned: " + assetPath);
        }

        private static void ValidateNoMandatoryCandidateWithoutExtraction(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            IReadOnlyList<PsdHierarchySnapshotCandidate> candidates = ReadCandidates(context);
            if (candidates.Count == 0)
                return;
            ValidateMandatoryCandidates(plan, context);
        }

        private static void ValidateMandatoryCandidates(
            JObject plan,
            PsdHierarchyChatContext context)
        {
            IReadOnlyList<PsdHierarchySnapshotCandidate> candidates = ReadCandidates(context);
            if (candidates.Count == 0)
                return;

            JArray decisions = plan["componentFamilyDecisions"] as JArray ?? new JArray();
            foreach (PsdHierarchySnapshotCandidate candidate in candidates)
            {
                if (!candidate.requiresExtraction)
                    continue;

                JObject[] matchingDecisions = decisions
                    .OfType<JObject>()
                    .Where(item => string.Equals(
                        item.Value<string>("candidateId"), candidate.id, StringComparison.Ordinal))
                    .ToArray();
                if (matchingDecisions.Length == 0)
                    throw new InvalidDataException(
                        "componentFamilyDecisions must cover the mandatory candidate " + candidate.id +
                        " (requiresExtraction=true).");
                if (matchingDecisions.Length != 1)
                    throw new InvalidDataException(
                        "componentFamilyDecisions must cover the mandatory candidate exactly once: " + candidate.id + ".");

                JObject decision = matchingDecisions[0];
                if (!string.Equals(decision.Value<string>("parent"), candidate.parent, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "componentFamilyDecisions[" + candidate.id + "].parent does not match the snapshot candidate.");

                string[] decisionSources = ReadNodeReferences(decision["sources"] as JArray);
                if (!SameNodeSet(decisionSources, candidate.sources))
                    throw new InvalidDataException(
                        "componentFamilyDecisions[" + candidate.id + "].sources do not match the snapshot candidate.");

                string mode = decision.Value<string>("mode");
                if (!IsSupportedExtractionMode(mode))
                    throw new InvalidDataException(
                        "componentFamilyDecisions[" + candidate.id + "].mode=" + mode +
                        " is not executable; expected component, state, variant, or stateful.");
                string extractionId = decision.Value<string>("extractionId");
                JObject extraction = FindExtraction(plan, mode, extractionId);
                if (extraction == null && !IsDeferredToPostGroupingStage(plan, extractionId, mode))
                    throw new InvalidDataException(
                        "componentFamilyDecisions[" + candidate.id + "] must reference an existing " + mode +
                        " extraction id or a postGroupingExtractionIntents id with the same mode.");
                if (extraction != null)
                {
                    string[] extractionSources = ReadExtractionSources(extraction, mode);
                    if (!candidate.sources.All(source => extractionSources.Contains(source, StringComparer.Ordinal)))
                        throw new InvalidDataException(
                            "componentFamilyDecisions[" + candidate.id + "] references extraction " + extractionId +
                            " which does not cover every snapshot candidate source.");
                }
            }
        }

        private static bool IsSupportedExtractionMode(string mode)
        {
            return string.Equals(mode, "component", StringComparison.Ordinal) ||
                   string.Equals(mode, "state", StringComparison.Ordinal) ||
                   string.Equals(mode, "variant", StringComparison.Ordinal) ||
                   string.Equals(mode, "stateful", StringComparison.Ordinal);
        }

        private static JObject FindExtraction(JObject plan, string mode, string extractionId)
        {
            if (string.IsNullOrWhiteSpace(extractionId))
                return null;

            string listName = string.Equals(mode, "component", StringComparison.Ordinal)
                ? "componentExtractions"
                : string.Equals(mode, "state", StringComparison.Ordinal)
                    ? "stateComponentExtractions"
                    : string.Equals(mode, "variant", StringComparison.Ordinal)
                        ? "variantComponentExtractions"
                        : "statefulComponentExtractions";
            return (plan[listName] as JArray ?? new JArray())
                .OfType<JObject>()
                .FirstOrDefault(item => string.Equals(item.Value<string>("id"), extractionId, StringComparison.Ordinal));
        }

        private static string[] ReadExtractionSources(JObject extraction, string mode)
        {
            if (string.Equals(mode, "component", StringComparison.Ordinal))
                return ReadNodeReferences(extraction["instances"] as JArray);
            if (string.Equals(mode, "state", StringComparison.Ordinal))
                return ReadObjectNodeReferences(extraction["states"] as JArray, "source");
            return ReadObjectNodeReferences(extraction["instances"] as JArray, "source");
        }

        private static string[] ReadObjectNodeReferences(JArray values, string propertyName)
        {
            return (values ?? new JArray())
                .OfType<JObject>()
                .Select(item => item.Value<string>(propertyName))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        private static string[] ReadNodeReferences(JArray values)
        {
            return (values ?? new JArray())
                .Where(value => value.Type == JTokenType.String)
                .Select(value => value.Value<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        private static bool SameNodeSet(IEnumerable<string> left, IEnumerable<string> right)
        {
            return new HashSet<string>(left ?? Array.Empty<string>(), StringComparer.Ordinal)
                .SetEquals(right ?? Array.Empty<string>());
        }

        /// <summary>
        /// 整理动作先建立新的重复单元根节点时，本轮没有可用的抽取源：该家族由已审阅的分组后
        /// 抽取意图在第二阶段真正抽取。只接受声明了同名且同 mode 的意图延期，
        /// 不接受没有任何引用的“以后再处理”。
        /// </summary>
        internal static bool IsDeferredToPostGroupingStage(JObject plan, string extractionId, string mode)
        {
            if (string.IsNullOrWhiteSpace(extractionId) || !(plan["postGroupingExtractionIntents"] is JArray intents))
                return false;

            return intents
                .OfType<JObject>()
                .Any(intent =>
                    string.Equals(intent.Value<string>("id"), extractionId, StringComparison.Ordinal) &&
                    string.Equals(intent.Value<string>("mode"), mode, StringComparison.Ordinal));
        }

        private static IReadOnlyList<PsdHierarchySnapshotCandidate> ReadCandidates(PsdHierarchyChatContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.hierarchySnapshotJson))
                return Array.Empty<PsdHierarchySnapshotCandidate>();

            try
            {
                var snapshot = JObject.Parse(context.hierarchySnapshotJson);
                if (!(snapshot["componentFamilyCandidates"] is JArray candidates))
                    return Array.Empty<PsdHierarchySnapshotCandidate>();

                return candidates
                    .OfType<JObject>()
                    .Select(item => new PsdHierarchySnapshotCandidate(
                        item.Value<string>("id") ?? string.Empty,
                        item.Value<bool?>("requiresExtraction") ?? false,
                        item.Value<string>("parent") ?? string.Empty,
                        ReadNodeReferences(item["sources"] as JArray)))
                    .Where(item => !string.IsNullOrEmpty(item.id))
                    .ToArray();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return Array.Empty<PsdHierarchySnapshotCandidate>();
            }
        }

        private static string ResolveNodePath(
            PsdHierarchyChatContext context,
            JObject owner,
            string field,
            string label)
        {
            string reference = field == null
                ? owner.Value<string>() ?? string.Empty
                : owner.Value<string>(field) ?? string.Empty;
            return ResolveNodePath(context, reference, label + (field == null ? string.Empty : "." + field));
        }

        private static string ResolveNodePath(
            PsdHierarchyChatContext context,
            JToken token,
            string field,
            string label)
        {
            string reference = token == null ? string.Empty : token.Value<string>() ?? string.Empty;
            return ResolveNodePath(context, reference, label + (field == null ? string.Empty : "." + field));
        }

        private static string ResolveNodePath(PsdHierarchyChatContext context, string reference, string label)
        {
            const string prefix = "node:";
            if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException(label + " must use a node:<id> reference: " + reference);
            string nodeId = reference.Substring(prefix.Length);
            if (!context.TryGetNodePath(nodeId, out string path))
                throw new InvalidDataException(label + " references an unknown snapshot node: " + reference);
            return path;
        }

        private static string ReadRequired(JObject owner, string field, string label)
        {
            string value = owner.Value<string>(field);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException(label + "." + field + " must be a non-empty string.");
            return value;
        }

        private static string ReadOptional(JObject owner, string field)
        {
            return owner.Value<string>(field) ?? string.Empty;
        }

        private static bool IsPascalCaseIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsUpper(value[0]))
                return false;
            return value.All(character => char.IsLetterOrDigit(character) || character == '_');
        }

        private static string BuildPreflightAssetPath(NativeExtractionOperation operation)
        {
            return BuildPreflightAssetPath(operation.assetPath, operation.prefabName);
        }

        private static string BuildPreflightAssetPath(string declaredAssetPath, string prefabName)
        {
            return PreflightTempFolder + "/" + Guid.NewGuid().ToString("N") + "/" + prefabName + ".prefab";
        }

        private static string ToProjectFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                (assetPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
            if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory))
                return;

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void DeleteAssetQuietly(string assetPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
            }
            catch (Exception)
            {
                // 临时资产清理失败不影响真实结果。
            }
        }

        private static GameObject FindByPath(GameObject root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;
            string[] segments = path.Split('/');
            Transform current = root.transform;
            for (int index = 0; index < segments.Length; index++)
            {
                if (index == 0 && string.Equals(segments[0], root.name, StringComparison.Ordinal))
                    continue;
                Transform next = null;
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    if (string.Equals(current.GetChild(childIndex).name, segments[index], StringComparison.Ordinal))
                    {
                        next = current.GetChild(childIndex);
                        break;
                    }
                }

                if (next == null)
                    return null;
                current = next;
            }

            return current.gameObject;
        }

        private readonly struct PendingReference
        {
            internal PendingReference(Component component, string propertyPath, int targetId)
            {
                this.component = component;
                componentId = component == null ? 0 : component.GetInstanceID();
                this.propertyPath = propertyPath;
                this.targetId = targetId;
            }

            internal readonly Component component;
            internal readonly int componentId;
            internal readonly string propertyPath;
            internal readonly int targetId;
        }

        private readonly struct PsdHierarchySnapshotCandidate
        {
            internal PsdHierarchySnapshotCandidate(
                string id,
                bool requiresExtraction,
                string parent,
                IReadOnlyList<string> sources)
            {
                this.id = id;
                this.requiresExtraction = requiresExtraction;
                this.parent = parent;
                this.sources = sources;
            }

            internal readonly string id;
            internal readonly bool requiresExtraction;
            internal readonly string parent;
            internal readonly IReadOnlyList<string> sources;
        }
    }
}
