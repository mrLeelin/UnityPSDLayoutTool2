using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    public sealed class PsdCommonPrefabPlan
    {
        public string PrefabPath { get; }
        public string OutputPath { get; }
        public IReadOnlyList<string> Sources { get; }
        public IReadOnlyList<string> Names { get; }
        public bool UsesStates { get; }
        public IReadOnlyList<string> CommonMembers { get; }
        public IReadOnlyList<PsdCommonPrefabStatePlan> States { get; }
        public IReadOnlyList<PsdCommonPrefabSourceState> SourceStates { get; }
        internal string Fingerprint { get; }
        internal int[] CommonChildIndices { get; }
        internal int[][] StateChildIndices { get; }
        internal int[] SourceStateIndices { get; }
        internal bool SplitRoot { get; }

        internal PsdCommonPrefabPlan(string prefabPath, string outputPath, string[] sources, string[] names, string fingerprint)
            : this(prefabPath, outputPath, sources, names, fingerprint, false, Array.Empty<string>(),
                Array.Empty<PsdCommonPrefabStatePlan>(), Array.Empty<PsdCommonPrefabSourceState>(),
                Array.Empty<int>(), Array.Empty<int[]>(), Array.Empty<int>(), false)
        {
        }

        internal PsdCommonPrefabPlan(string prefabPath, string outputPath, string[] sources, string[] names, string fingerprint,
            bool usesStates, string[] commonMembers, PsdCommonPrefabStatePlan[] states,
            PsdCommonPrefabSourceState[] sourceStates, int[] commonChildIndices, int[][] stateChildIndices,
            int[] sourceStateIndices, bool splitRoot)
        {
            PrefabPath = prefabPath;
            OutputPath = outputPath;
            Sources = Array.AsReadOnly((string[])sources.Clone());
            Names = Array.AsReadOnly((string[])names.Clone());
            Fingerprint = fingerprint;
            UsesStates = usesStates;
            CommonMembers = Array.AsReadOnly((string[])commonMembers.Clone());
            States = Array.AsReadOnly((PsdCommonPrefabStatePlan[])states.Clone());
            SourceStates = Array.AsReadOnly((PsdCommonPrefabSourceState[])sourceStates.Clone());
            CommonChildIndices = (int[])commonChildIndices.Clone();
            StateChildIndices = stateChildIndices.Select(indices => (int[])indices.Clone()).ToArray();
            SourceStateIndices = (int[])sourceStateIndices.Clone();
            SplitRoot = splitRoot;
        }
    }

    public sealed class PsdCommonPrefabStatePlan
    {
        public string Name { get; }
        public IReadOnlyList<string> Members { get; }

        internal PsdCommonPrefabStatePlan(string name, string[] members)
        {
            Name = name;
            Members = Array.AsReadOnly((string[])members.Clone());
        }
    }

    public sealed class PsdCommonPrefabSourceState
    {
        public string Source { get; }
        public string State { get; }

        internal PsdCommonPrefabSourceState(string source, string state)
        {
            Source = source;
            State = state;
        }
    }

    /// <summary>Extract saved UI subtrees while retaining UI properties and internal bindings.</summary>
    public static class PsdCommonPrefabExtraction
    {
        public static PsdCommonPrefabPlan Preview(string prefabPath, string[] sources, string name)
        {
            return PreviewInternal(prefabPath, sources, name, false);
        }

        /// <summary>Preview an explicit state-based extraction for selected roots whose child structures may differ.</summary>
        public static PsdCommonPrefabPlan PreviewStates(string prefabPath, string[] sources, string name)
        {
            return PreviewInternal(prefabPath, sources, name, true);
        }

        static PsdCommonPrefabPlan PreviewInternal(string prefabPath, string[] sources, string name, bool useStates)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.EndsWith(".", StringComparison.Ordinal) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/") || name.Contains("\\") ||
                Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase))
                throw new InvalidOperationException("请输入有效的资产名称：不能包含路径分隔符、非法字符、末尾句点或系统保留名。");
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                prefabPath.Contains("..") || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || !File.Exists(prefabPath))
                throw new InvalidOperationException("请选择 Assets 中已保存的 UI Prefab。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == prefabPath && stage.scene.isDirty)
                throw new InvalidOperationException("请先保存当前 Prefab，再预览抽取。");
            string output = Path.GetDirectoryName(prefabPath).Replace('\\', '/') + "/Common/" + name + ".prefab";
            if (File.Exists(output) || File.Exists(output + ".meta") || AssetDatabase.LoadMainAssetAtPath(output) != null)
                throw new InvalidOperationException("公共 Prefab 已存在，请使用其他名称：" + output);
            if (sources == null || sources.Length < 2 || sources.Distinct().Count() != sources.Length)
                throw new InvalidOperationException("请选择至少两个不同的同结构组件根节点。");
            PsdCommonPrefabPersistence.ValidateOutput(prefabPath);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponentInChildren<PsdLayerNode>(true) != null)
                    throw new InvalidOperationException("此手动抽取入口用于生成后的 UI Prefab，不直接抽取 PSD 编辑节点。");
                if (PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)) != PrefabAssetType.Regular)
                    throw new InvalidOperationException("当前只支持普通 UI Prefab。");
                var selected = sources.Select(s => Resolve(root.transform, s)).ToArray();
                foreach (var item in selected)
                {
                    if (!(item is RectTransform) || item == root.transform || selected.Any(s => s != item && item.IsChildOf(s)))
                        throw new InvalidOperationException("不能抽取根节点、重叠子树或非 UI 节点。");
                    if (PrefabUtility.IsPartOfPrefabInstance(item.gameObject) ||
                        item.GetComponentsInChildren<Transform>(true).Any(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
                        throw new InvalidOperationException("选区包含已有嵌套 Prefab，请在其源资产中单独整理。");
                    if (!useStates) CheckStructure(selected[0], item);
                }
                CheckReferences(root, selected);
                if (!useStates)
                    return new PsdCommonPrefabPlan(prefabPath, output, sources, selected.Select(s => s.name).ToArray(), Hash(prefabPath));
                return BuildStatePlan(prefabPath, output, sources, selected);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Apply(PsdCommonPrefabPlan plan)
        {
            if (plan == null || !File.Exists(plan.PrefabPath) || Hash(plan.PrefabPath) != plan.Fingerprint)
                throw new InvalidOperationException("预览已过期，请重新分析。");
            var checkedPlan = plan.UsesStates
                ? PreviewStates(plan.PrefabPath, plan.Sources.ToArray(), Path.GetFileNameWithoutExtension(plan.OutputPath))
                : Preview(plan.PrefabPath, plan.Sources.ToArray(), Path.GetFileNameWithoutExtension(plan.OutputPath));
            if (checkedPlan.OutputPath != plan.OutputPath)
                throw new InvalidOperationException("输出路径与预览不一致。");
            byte[] original = File.ReadAllBytes(plan.PrefabPath);
            string folder = Path.GetDirectoryName(plan.OutputPath).Replace('\\', '/');
            bool createdFolder = !AssetDatabase.IsValidFolder(folder);
            bool outputCreated = false;
            bool targetSaveStarted = false;
            GameObject root = PrefabUtility.LoadPrefabContents(plan.PrefabPath);
            try
            {
                var sources = plan.Sources.Select(s => Resolve(root.transform, s)).ToArray();
                if (createdFolder)
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), "Common");
                outputCreated = true;
                GameObject template = null;
                GameObject asset;
                if (plan.UsesStates)
                {
                    try
                    {
                        template = BuildStateTemplate(plan, sources);
                        asset = PrefabUtility.SaveAsPrefabAsset(template, plan.OutputPath);
                    }
                    finally
                    {
                        if (template != null) Object.DestroyImmediate(template);
                    }
                }
                else asset = PrefabUtility.SaveAsPrefabAsset(sources[0].gameObject, plan.OutputPath);
                if (asset == null) throw new InvalidOperationException("保存公共 Prefab 失败。");
                for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                {
                    var source = sources[sourceIndex];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.scene);
                    instance.transform.SetParent(source.parent, false);
                    instance.transform.SetSiblingIndex(source.GetSiblingIndex());
                    if (plan.UsesStates) PopulateStateInstance(plan, sourceIndex, source, instance.transform);
                    else
                    {
                        var map = new Dictionary<Object, Object>();
                        BuildMap(source, instance.transform, map);
                        CopyTree(source, instance.transform, map);
                    }
                    Object.DestroyImmediate(source.gameObject);
                }
                targetSaveStarted = true;
                if (PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath) == null)
                    throw new InvalidOperationException("保存 UI Prefab 失败。");
                PsdCommonPrefabPersistence.RecordExtraction(plan.PrefabPath, plan.OutputPath, plan.Sources.ToArray());
            }
            catch
            {
                if (targetSaveStarted)
                {
                    File.WriteAllBytes(plan.PrefabPath, original);
                    AssetDatabase.ImportAsset(plan.PrefabPath, ImportAssetOptions.ForceUpdate);
                }
                if (outputCreated) AssetDatabase.DeleteAsset(plan.OutputPath);
                if (createdFolder && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    AssetDatabase.DeleteAsset(folder);
                throw;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static PsdCommonPrefabPlan BuildStatePlan(string prefabPath, string output, string[] sources, Transform[] selected)
        {
            foreach (var item in selected)
            {
                if (item.GetComponent<UnityEngine.UI.LayoutGroup>() != null ||
                    item.GetComponent<UnityEngine.UI.ContentSizeFitter>() != null ||
                    item.GetComponent<UnityEngine.UI.AspectRatioFitter>() != null ||
                    item.GetComponent<UnityEngine.UI.LayoutElement>() != null ||
                    item.parent.GetComponent<UnityEngine.UI.LayoutGroup>() != null)
                    throw new InvalidOperationException("选区根节点受自动布局控制，新增状态分组可能改变布局，请先单独整理：" + item.name);
            }
            bool splitRoot = selected.Skip(1).All(item => SameComponentTypes(selected[0], item));
            var commonIndices = new List<int>();
            if (splitRoot)
            {
                for (int childIndex = 0; childIndex < selected[0].childCount; childIndex++)
                {
                    Transform candidate = selected[0].GetChild(childIndex);
                    string signature = StructureSignature(candidate);
                    if (selected.Skip(1).All(item => childIndex < item.childCount &&
                        item.GetChild(childIndex).name == candidate.name &&
                        StructureSignature(item.GetChild(childIndex)) == signature))
                        commonIndices.Add(childIndex);
                    else break; // Only a shared prefix can move before all state branches without changing draw order.
                }
            }

            var stateKeys = new List<string>();
            var stateMembers = new List<string[]>();
            var stateChildIndices = new List<int[]>();
            var sourceStateIndices = new int[selected.Length];
            for (int sourceIndex = 0; sourceIndex < selected.Length; sourceIndex++)
            {
                int[] memberIndices = splitRoot
                    ? Enumerable.Range(0, selected[sourceIndex].childCount).Where(index => !commonIndices.Contains(index)).ToArray()
                    : Array.Empty<int>();
                string key = splitRoot
                    ? string.Join("|", memberIndices.Select(index => StructureSignature(selected[sourceIndex].GetChild(index))))
                    : StructureSignature(selected[sourceIndex]);
                int stateIndex = stateKeys.IndexOf(key);
                if (stateIndex < 0)
                {
                    stateIndex = stateKeys.Count;
                    stateKeys.Add(key);
                    stateChildIndices.Add(memberIndices);
                    stateMembers.Add(splitRoot
                        ? memberIndices.Select(index => selected[sourceIndex].GetChild(index).name).ToArray()
                        : new[] { selected[sourceIndex].name });
                }
                sourceStateIndices[sourceIndex] = stateIndex;
            }

            var states = stateMembers.Select((members, index) =>
                new PsdCommonPrefabStatePlan("State_" + (index + 1), members)).ToArray();
            var mappings = sources.Select((source, index) =>
                new PsdCommonPrefabSourceState(source, states[sourceStateIndices[index]].Name)).ToArray();
            return new PsdCommonPrefabPlan(prefabPath, output, sources, selected.Select(item => item.name).ToArray(), Hash(prefabPath),
                true, commonIndices.Select(index => selected[0].GetChild(index).name).ToArray(), states, mappings,
                commonIndices.ToArray(), stateChildIndices.ToArray(), sourceStateIndices, splitRoot);
        }

        static GameObject BuildStateTemplate(PsdCommonPrefabPlan plan, Transform[] sources)
        {
            string assetName = Path.GetFileNameWithoutExtension(plan.OutputPath);
            var template = new GameObject(assetName, typeof(RectTransform));
            try
            {
                if (plan.SplitRoot)
                {
                    foreach (var component in sources[0].GetComponents<Component>().Skip(1))
                        template.AddComponent(component.GetType());
                }
                var common = CreateContainer("[Common]", template.transform, sources[0] as RectTransform);
                var stateRoot = CreateContainer("[States]", template.transform, sources[0] as RectTransform);
                for (int stateIndex = 0; stateIndex < plan.States.Count; stateIndex++)
                {
                    int representative = Array.IndexOf(plan.SourceStateIndices, stateIndex);
                    var branch = CreateContainer("[" + plan.States[stateIndex].Name + "]", stateRoot, sources[representative] as RectTransform);
                    if (plan.SplitRoot)
                    {
                        foreach (int childIndex in plan.StateChildIndices[stateIndex])
                        {
                            var clone = Object.Instantiate(sources[representative].GetChild(childIndex).gameObject);
                            clone.name = sources[representative].GetChild(childIndex).name;
                            clone.transform.SetParent(branch, false);
                        }
                    }
                    else
                    {
                        var clone = Object.Instantiate(sources[representative].gameObject);
                        clone.name = sources[representative].name;
                        clone.transform.SetParent(branch, false);
                        NormalizeRect(clone.transform as RectTransform, sources[representative] as RectTransform);
                    }
                    branch.gameObject.SetActive(stateIndex == 0);
                }
                if (plan.SplitRoot)
                {
                    foreach (int childIndex in plan.CommonChildIndices)
                    {
                        var clone = Object.Instantiate(sources[0].GetChild(childIndex).gameObject);
                        clone.name = sources[0].GetChild(childIndex).name;
                        clone.transform.SetParent(common, false);
                    }
                }

                for (int stateIndex = 0; stateIndex < plan.States.Count; stateIndex++)
                {
                    int representative = Array.IndexOf(plan.SourceStateIndices, stateIndex);
                    PopulateStateInstance(plan, representative, sources[representative], template.transform);
                }
                PopulateStateInstance(plan, 0, sources[0], template.transform);
                template.name = assetName;
                return template;
            }
            catch
            {
                Object.DestroyImmediate(template);
                throw;
            }
        }

        static Transform CreateContainer(string name, Transform parent, RectTransform sourceRect)
        {
            var result = new GameObject(name, typeof(RectTransform)).transform;
            result.SetParent(parent, false);
            NormalizeRect(result as RectTransform, sourceRect);
            return result;
        }

        static void PopulateStateInstance(PsdCommonPrefabPlan plan, int sourceIndex, Transform source, Transform instance)
        {
            int stateIndex = plan.SourceStateIndices[sourceIndex];
            Transform common = instance.GetChild(0);
            Transform states = instance.GetChild(1);
            for (int index = 0; index < states.childCount; index++)
            {
                states.GetChild(index).gameObject.SetActive(index == stateIndex);
                PrefabUtility.RecordPrefabInstancePropertyModifications(states.GetChild(index).gameObject);
            }

            if (!plan.SplitRoot)
            {
                CopyWrapper(source, instance);
                Transform targetRoot = states.GetChild(stateIndex).GetChild(0);
                var map = new Dictionary<Object, Object>();
                BuildMap(source, targetRoot, map);
                CopyTree(source, targetRoot, map);
                NormalizeRect(targetRoot as RectTransform, source as RectTransform);
                return;
            }

            var splitMap = new Dictionary<Object, Object>();
            BuildObjectMap(source, instance, splitMap);
            for (int index = 0; index < plan.CommonChildIndices.Length; index++)
                BuildMap(source.GetChild(plan.CommonChildIndices[index]), common.GetChild(index), splitMap);
            int[] sourceMembers = Enumerable.Range(0, source.childCount)
                .Where(index => !plan.CommonChildIndices.Contains(index)).ToArray();
            Transform state = states.GetChild(stateIndex);
            if (sourceMembers.Length != state.childCount)
                throw new InvalidOperationException("状态结构已变化，请重新预览。");
            for (int index = 0; index < sourceMembers.Length; index++)
                BuildMap(source.GetChild(sourceMembers[index]), state.GetChild(index), splitMap);

            CopyGameObjectAndComponents(source, instance, splitMap);
            for (int index = 0; index < plan.CommonChildIndices.Length; index++)
                CopyTree(source.GetChild(plan.CommonChildIndices[index]), common.GetChild(index), splitMap);
            for (int index = 0; index < sourceMembers.Length; index++)
                CopyTree(source.GetChild(sourceMembers[index]), state.GetChild(index), splitMap);
        }

        static void CopyWrapper(Transform source, Transform target)
        {
            target.gameObject.name = source.name;
            target.gameObject.layer = source.gameObject.layer;
            target.gameObject.tag = source.gameObject.tag;
            target.gameObject.SetActive(source.gameObject.activeSelf);
            GameObjectUtility.SetStaticEditorFlags(target.gameObject, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
            var map = new Dictionary<Object, Object> { [source] = target };
            CopyObject(source, target, map);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
        }

        static void BuildObjectMap(Transform source, Transform target, Dictionary<Object, Object> map)
        {
            var sourceComponents = source.GetComponents<Component>();
            var targetComponents = target.GetComponents<Component>();
            if (sourceComponents.Length != targetComponents.Length ||
                !sourceComponents.Select(component => component.GetType()).SequenceEqual(targetComponents.Select(component => component.GetType())))
                throw new InvalidOperationException("状态根组件结构已变化，请重新预览。");
            map[source.gameObject] = target.gameObject;
            for (int index = 0; index < sourceComponents.Length; index++) map[sourceComponents[index]] = targetComponents[index];
        }

        static void CopyGameObjectAndComponents(Transform source, Transform target, Dictionary<Object, Object> map)
        {
            target.gameObject.name = source.name;
            target.gameObject.layer = source.gameObject.layer;
            target.gameObject.tag = source.gameObject.tag;
            target.gameObject.SetActive(source.gameObject.activeSelf);
            GameObjectUtility.SetStaticEditorFlags(target.gameObject, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
            var sourceComponents = source.GetComponents<Component>();
            var targetComponents = target.GetComponents<Component>();
            for (int index = 0; index < sourceComponents.Length; index++) CopyObject(sourceComponents[index], targetComponents[index], map);
        }

        static void NormalizeRect(RectTransform rect, RectTransform source)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = source != null ? source.pivot : new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var position = rect.localPosition;
            position.z = 0;
            rect.localPosition = position;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }

        static bool SameComponentTypes(Transform left, Transform right)
        {
            var leftComponents = left.GetComponents<Component>();
            var rightComponents = right.GetComponents<Component>();
            return leftComponents.All(component => component != null) && rightComponents.All(component => component != null) &&
                leftComponents.Select(component => component.GetType()).SequenceEqual(rightComponents.Select(component => component.GetType()));
        }

        static string StructureSignature(Transform node)
        {
            var components = node.GetComponents<Component>();
            if (components.Any(component => component == null))
                throw new InvalidOperationException("选区存在丢失脚本。");
            return "(" + string.Join(",", components.Select(component => component.GetType().AssemblyQualifiedName)) + ")" +
                "[" + string.Join(";", Enumerable.Range(0, node.childCount).Select(index => StructureSignature(node.GetChild(index)))) + "]";
        }

        public static string GetNodeAddress(Transform root, Transform node)
        {
            var indices = new List<int>();
            while (node != null && node != root) { indices.Add(node.GetSiblingIndex()); node = node.parent; }
            if (node != root || indices.Count == 0) throw new InvalidOperationException("请选择当前 Prefab 的子节点。");
            indices.Reverse();
            return string.Join("/", indices);
        }

        static Transform Resolve(Transform root, string address)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new InvalidOperationException("节点地址为空。");
            foreach (string part in address.Split('/'))
            {
                if (!int.TryParse(part, out int index) || index < 0 || index >= root.childCount)
                    throw new InvalidOperationException("节点地址无效：" + address);
                root = root.GetChild(index);
            }
            return root;
        }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        static void CheckStructure(Transform template, Transform other)
        {
            var left = template.GetComponents<Component>();
            var right = other.GetComponents<Component>();
            if (left.Any(c => c == null) || right.Any(c => c == null))
                throw new InvalidOperationException("选区存在丢失脚本。");
            if (template.childCount != other.childCount || !left.Select(c => c.GetType()).SequenceEqual(right.Select(c => c.GetType())))
                throw new InvalidOperationException("选区结构不同，当前手动抽取只支持同结构组件：" + other.name);
            for (int i = 0; i < template.childCount; i++) CheckStructure(template.GetChild(i), other.GetChild(i));
        }

        static void CheckReferences(GameObject root, Transform[] selected)
        {
            var owner = new Dictionary<Object, int>();
            for (int i = 0; i < selected.Length; i++)
                foreach (var t in selected[i].GetComponentsInChildren<Transform>(true))
                {
                    owner[t.gameObject] = i;
                    foreach (var c in t.GetComponents<Component>()) owner[c] = i;
                }
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) throw new InvalidOperationException("Prefab 存在丢失脚本。");
                var so = new SerializedObject(c);
                var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || IsInfrastructure(p.propertyPath)) continue;
                    var reference = p.objectReferenceValue;
                    if (reference == null || EditorUtility.IsPersistent(reference)) continue;
                    int sourceOwnerIndex = owner.TryGetValue(c, out int sourceOwner) ? sourceOwner : -1;
                    int referenceOwnerIndex = owner.TryGetValue(reference, out int referenceOwner) ? referenceOwner : -1;
                    if (sourceOwnerIndex != referenceOwnerIndex)
                        throw new InvalidOperationException("存在跨组件引用，不能安全抽取：" + c.name + "." + p.propertyPath);
                }
            }
        }

        static bool IsInfrastructure(string path)
        {
            string field = path.Split('.')[0];
            return field == "m_GameObject" || field == "m_Script" || field == "m_Father" || field == "m_Children" ||
                field == "m_RootOrder" || field == "m_CorrespondingSourceObject" || field == "m_PrefabInstance" || field == "m_PrefabAsset" || field == "m_ObjectHideFlags";
        }

        static void BuildMap(Transform source, Transform target, Dictionary<Object, Object> map)
        {
            map[source.gameObject] = target.gameObject;
            var sourceComponents = source.GetComponents<Component>();
            var targetComponents = target.GetComponents<Component>();
            for (int i = 0; i < sourceComponents.Length; i++) map[sourceComponents[i]] = targetComponents[i];
            for (int i = 0; i < source.childCount; i++) BuildMap(source.GetChild(i), target.GetChild(i), map);
        }

        static void CopyObject(Object source, Object target, Dictionary<Object, Object> map)
        {
            var from = new SerializedObject(source); var to = new SerializedObject(target);
            var p = from.GetIterator();
            bool enterChildren = true;
            while (p.Next(enterChildren))
            {
                enterChildren = false;
                if (!IsInfrastructure(p.propertyPath)) to.CopyFromSerializedProperty(p);
            }
            to.ApplyModifiedPropertiesWithoutUndo();
            to.Update(); p = to.GetIterator();
            while (p.Next(true))
                if (!IsInfrastructure(p.propertyPath) && p.propertyType == SerializedPropertyType.ObjectReference &&
                    p.objectReferenceValue != null && map.TryGetValue(p.objectReferenceValue, out var replacement))
                    p.objectReferenceValue = replacement;
            to.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        static void CopyTree(Transform source, Transform target, Dictionary<Object, Object> map)
        {
            target.gameObject.name = source.name;
            target.gameObject.layer = source.gameObject.layer;
            target.gameObject.tag = source.gameObject.tag;
            target.gameObject.SetActive(source.gameObject.activeSelf);
            GameObjectUtility.SetStaticEditorFlags(target.gameObject, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
            var sourceComponents = source.GetComponents<Component>();
            var targetComponents = target.GetComponents<Component>();
            for (int i = 0; i < sourceComponents.Length; i++) CopyObject(sourceComponents[i], targetComponents[i], map);
            for (int i = 0; i < source.childCount; i++) CopyTree(source.GetChild(i), target.GetChild(i), map);
        }
    }
}
