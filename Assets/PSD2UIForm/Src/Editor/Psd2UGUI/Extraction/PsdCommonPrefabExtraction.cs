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
        internal string Fingerprint { get; }

        internal PsdCommonPrefabPlan(string prefabPath, string outputPath, string[] sources, string[] names, string fingerprint)
        {
            PrefabPath = prefabPath;
            OutputPath = outputPath;
            Sources = Array.AsReadOnly((string[])sources.Clone());
            Names = Array.AsReadOnly((string[])names.Clone());
            Fingerprint = fingerprint;
        }
    }

    /// <summary>Extract saved UI subtrees while retaining UI properties and internal bindings.</summary>
    public static class PsdCommonPrefabExtraction
    {
        public static PsdCommonPrefabPlan Preview(string prefabPath, string[] sources, string name)
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
                    CheckStructure(selected[0], item);
                }
                CheckReferences(root, selected);
                return new PsdCommonPrefabPlan(prefabPath, output, sources, selected.Select(s => s.name).ToArray(), Hash(prefabPath));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Apply(PsdCommonPrefabPlan plan)
        {
            if (plan == null || !File.Exists(plan.PrefabPath) || Hash(plan.PrefabPath) != plan.Fingerprint)
                throw new InvalidOperationException("预览已过期，请重新分析。");
            var checkedPlan = Preview(plan.PrefabPath, plan.Sources.ToArray(), Path.GetFileNameWithoutExtension(plan.OutputPath));
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
                var asset = PrefabUtility.SaveAsPrefabAsset(sources[0].gameObject, plan.OutputPath);
                if (asset == null) throw new InvalidOperationException("保存公共 Prefab 失败。");
                foreach (var source in sources)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.scene);
                    instance.transform.SetParent(source.parent, false);
                    instance.transform.SetSiblingIndex(source.GetSiblingIndex());
                    var map = new Dictionary<Object, Object>();
                    BuildMap(source, instance.transform, map);
                    CopyTree(source, instance.transform, map);
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
