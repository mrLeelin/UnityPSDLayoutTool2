using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>Unchanged-input regeneration reuses the saved graph; changed inputs require a later merge decision.</summary>
    internal static class PsdCommonPrefabPersistence
    {
        internal static PsdCommonPrefabRules Find(string targetPath, bool allowUnextractedReplacement = false)
        {
            string guid = AssetDatabase.AssetPathToGUID(targetPath);
            var matches = AssetDatabase.FindAssets("t:PsdCommonPrefabRules")
                .Select(id => AssetDatabase.LoadAssetAtPath<PsdCommonPrefabRules>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(asset => asset != null && ((!string.IsNullOrEmpty(guid) && asset.targetGuid == guid) ||
                    asset.targetPath == targetPath)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("当前界面存在重复的公共 Prefab 规则资产。");
            var result = matches.SingleOrDefault();
            if (result != null && result.version != 1) throw new InvalidOperationException("不支持的公共 Prefab 规则版本。");
            if (result != null && (result.rules == null || result.rules.Any(rule => rule == null || rule.instanceIds == null)))
                throw new InvalidOperationException("公共 Prefab 规则内容不完整。");
            if (result != null && result.rules.Count == 0 && !allowUnextractedReplacement && result.targetGuid != guid)
                return null;
            if (result != null && (string.IsNullOrEmpty(guid) || result.targetGuid != guid) &&
                !(allowUnextractedReplacement && result.rules.Count == 0))
                throw new InvalidOperationException("规则对应的 UI Prefab 已移动、丢失或身份改变，不能按同名资产重新创建。");
            return result;
        }

        internal static void ValidateOutput(string targetPath)
        {
            var rules = Find(targetPath);
            if (rules != null)
            {
                ValidateInstances(rules, AssetDatabase.LoadAssetAtPath<GameObject>(targetPath));
                return;
            }
            string path = RulesPath(targetPath);
            if (File.Exists(path) || File.Exists(path + ".meta"))
                throw new InvalidOperationException("抽取规则输出位置已被占用：" + path);
        }

        internal static void RecordGeneration(GameObject source, string targetPath)
        {
            string sourcePath = SourcePath(source);
            if (string.IsNullOrEmpty(sourcePath)) return;
            var rules = Find(targetPath, true);
            if (rules != null && rules.rules.Count != 0)
                throw new InvalidOperationException("已有抽取规则，不能重置其来源基线。");
            string fingerprint = PsdExtractionSourceFingerprint.Capture(source);
            bool created = rules == null;
            rules = rules != null ? rules : Create(targetPath);
            string backup = EditorJsonUtility.ToJson(rules);
            try
            {
                rules.targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
                rules.targetPath = targetPath;
                rules.sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                rules.sourceFingerprint = fingerprint;
                EditorUtility.SetDirty(rules);
                AssetDatabase.SaveAssetIfDirty(rules);
            }
            catch
            {
                RestoreRules(rules, created, backup);
                throw;
            }
        }

        internal static void RecordExtraction(string targetPath, string commonPath, string[] addresses)
        {
            var rules = Find(targetPath);
            bool created = rules == null;
            if (created) rules = Create(targetPath);
            string backup = EditorJsonUtility.ToJson(rules);
            try
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
                var rule = new PsdCommonPrefabRule { commonGuid = AssetDatabase.AssetPathToGUID(commonPath) };
                foreach (string address in addresses)
                {
                    Transform node = root.transform;
                    foreach (string index in address.Split('/')) node = node.GetChild(int.Parse(index));
                    rule.instanceIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(node.gameObject).ToString());
                }
                rules.rules.Add(rule);
                ValidateInstances(rules, root);
                EditorUtility.SetDirty(rules);
                AssetDatabase.SaveAssetIfDirty(rules);
            }
            catch
            {
                RestoreRules(rules, created, backup);
                throw;
            }
        }

        static void RestoreRules(PsdCommonPrefabRules rules, bool created, string backup)
        {
            if (created) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(rules));
            else
            {
                EditorJsonUtility.FromJsonOverwrite(backup, rules);
                EditorUtility.SetDirty(rules);
                AssetDatabase.SaveAssetIfDirty(rules);
            }
        }

        internal static bool TryReuse(GameObject source, string targetPath, out GameObject asset)
        {
            asset = null;
            var rules = Find(targetPath);
            string sourcePath = SourcePath(source);
            if (rules == null || rules.rules.Count == 0)
            {
                string sourceGuid = string.IsNullOrEmpty(sourcePath) ? null : AssetDatabase.AssetPathToGUID(sourcePath);
                if (!string.IsNullOrEmpty(sourceGuid) && AssetDatabase.FindAssets("t:PsdCommonPrefabRules")
                    .Select(id => AssetDatabase.LoadAssetAtPath<PsdCommonPrefabRules>(AssetDatabase.GUIDToAssetPath(id)))
                    .Any(existing => existing != null && existing.sourceGuid == sourceGuid && existing.rules != null && existing.rules.Count > 0))
                    throw new InvalidOperationException("当前来源已有公共 Prefab 抽取结果，目标名称或输出目录已改变；已停止生成，请先解决目标迁移冲突。");
                return false;
            }
            if (string.IsNullOrEmpty(rules.sourceGuid) || string.IsNullOrEmpty(sourcePath) ||
                AssetDatabase.AssetPathToGUID(sourcePath) != rules.sourceGuid)
                throw new InvalidOperationException("抽取规则缺少有效来源，或当前来源身份已改变；已停止生成，保留现有 Prefab。");
            if (PsdExtractionSourceFingerprint.Capture(source) != rules.sourceFingerprint)
                throw new InvalidOperationException("PSD、编辑树或生成配置已变化。当前尚不支持合并更新；已停止生成，保留公共 Prefab 与实例，请先解决来源冲突。");
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            ValidateInstances(rules, asset);
            return true;
        }

        internal static void ValidateGeneration(GameObject source, string targetPath)
        {
            // A transient scene remains usable by the legacy generator, but cannot promise durable extraction provenance.
            if (string.IsNullOrEmpty(SourcePath(source))) return;
            var rules = Find(targetPath, true);
            if (rules == null) ValidateOutput(targetPath);
            PsdExtractionSourceFingerprint.Capture(source);
        }

        static void ValidateInstances(PsdCommonPrefabRules rules, GameObject root)
        {
            if (root == null) throw new InvalidOperationException("规则对应的 UI Prefab 已丢失。");
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var rule in rules.rules)
            {
                string commonPath = AssetDatabase.GUIDToAssetPath(rule.commonGuid);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(commonPath) == null || rule.instanceIds.Count < 2)
                    throw new InvalidOperationException("公共 Prefab 丢失或实例映射不完整。");
                foreach (string id in rule.instanceIds)
                {
                    if (!seen.Add(id) || !GlobalObjectId.TryParse(id, out var globalId))
                        throw new InvalidOperationException("公共 Prefab 实例身份重复或无效。");
                    var instance = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                    if (instance == null || !instance.transform.IsChildOf(root.transform) ||
                        !PrefabUtility.IsAnyPrefabInstanceRoot(instance) ||
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) != commonPath)
                        throw new InvalidOperationException("公共 Prefab 实例身份失效，不能按名称或排序猜测匹配：" + id);
                }
            }
        }

        static PsdCommonPrefabRules Create(string targetPath)
        {
            string path = RulesPath(targetPath);
            if (File.Exists(path) || File.Exists(path + ".meta"))
                throw new InvalidOperationException("抽取规则输出位置已被占用：" + path);
            var asset = ScriptableObject.CreateInstance<PsdCommonPrefabRules>();
            asset.targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
            asset.targetPath = targetPath;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static string RulesPath(string targetPath) => Path.ChangeExtension(targetPath, ".extraction.asset").Replace('\\', '/');

        static string SourcePath(GameObject source)
        {
            if (source == null) return null;
            string path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path)) return path;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && source.scene == stage.scene) return stage.assetPath;
            // LoadPrefabContents uses the prefab path as its isolated scene path.
            path = source.scene.path;
            return !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? path : null;
        }
    }
}
