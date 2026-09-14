using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiPatchApplierNamespace;
using AiPatchValidatorNamespace;
using LayerNodeIdUtilityNamespace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>Builds a complete candidate in owned temporary assets; publishes only the reviewed candidate.</summary>
    internal sealed class AiOrganizerPreview : IDisposable
    {
        internal readonly List<PsdCommonPrefabPlan> Extractions = new List<PsdCommonPrefabPlan>();
        internal string TargetPath { get; private set; }
        internal string SourcePath { get; private set; }
        internal string PreviewPath { get; private set; }
        string _folder, _destination, _inputFingerprint, _candidateFingerprint;
        byte[] _sourceBytes;
        GameObject _authoring;
        Psd2UIFormConverterEditor _editor;
        bool _published;

        internal static AiOrganizerPreview Build(Psd2UIFormConverterEditor source, AiPatchDocument patch,
            AiAnalysisPackageDocument package, string destination, string expectedFingerprint = null, byte[] expectedSourceBytes = null)
        {
            if (source == null || patch == null || package == null || patch.treeHash != package.treeHash)
                throw new InvalidOperationException("整理方案与分析快照不匹配。");
            if (!new AiPatchValidator().ValidatePatch(patch, package, out string error))
                throw new InvalidOperationException(error);
            string sourcePath = PsdCommonPrefabPersistence.SourcePath(source.gameObject);
            if (string.IsNullOrEmpty(sourcePath)) throw new InvalidOperationException("请先保存 PSD 编辑 Prefab。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == sourcePath && stage.scene.isDirty)
                throw new InvalidOperationException("编辑树有未保存修改，请保存并重新分析。");
            ValidateDestination(destination);
            var preview = new AiOrganizerPreview { SourcePath = sourcePath, _destination = destination,
                _sourceBytes = File.ReadAllBytes(sourcePath), _inputFingerprint = PsdExtractionSourceFingerprint.Capture(source.gameObject) };
            if ((expectedFingerprint != null && expectedFingerprint != preview._inputFingerprint) ||
                (expectedSourceBytes != null && !expectedSourceBytes.SequenceEqual(preview._sourceBytes)))
                throw new InvalidOperationException("分析后的源文件已变化，请重新分析。");
            try
            {
                string folderName = "__PsdOrganizerPreview_" + Guid.NewGuid().ToString("N");
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", folderName))) throw new IOException("不能创建整理预览目录。");
                preview._folder = "Assets/" + folderName;
                preview._authoring = PrefabUtility.LoadPrefabContents(sourcePath);
                if (PsdExtractionSourceFingerprint.Capture(preview._authoring) != preview._inputFingerprint ||
                    !File.ReadAllBytes(sourcePath).SequenceEqual(preview._sourceBytes))
                    throw new InvalidOperationException("磁盘编辑树与分析输入不一致，请保存并重新分析。");
                preview._editor = Psd2UIFormConverterEditor.GetOrCreate(preview._authoring.GetComponent<Psd2UIFormConverter>());
                if (!new AiPatchApplier().ApplyPatch(preview._editor, patch, out error, true, false)) throw new InvalidOperationException(error);
                var settings = ScriptableSingleton<Psd2UIFormSettings>.Instance;
                string images = settings.UIImagesOutputDir;
                string lastForms = settings.LastUIFormOutputDir;
                try
                {
                    settings.UIImagesOutputDir = preview._folder + "/Images";
                    if (!preview._editor.GenerateAndSaveUIFormPrefab(preview._authoring.transform, preview._folder))
                        throw new InvalidOperationException("候选界面生成失败，原编辑树未改变。");
                }
                finally { settings.UIImagesOutputDir = images; settings.LastUIFormOutputDir = lastForms; }
                preview.PreviewPath = preview._folder + "/" + preview._authoring.GetComponent<Psd2UIFormConverter>().uiFormName + ".prefab";
                preview.TargetPath = destination + "/" + Path.GetFileName(preview.PreviewPath);
                preview.BuildExtractions(patch.components ?? new List<AiOrganizerComponent>());
                preview._candidateFingerprint = preview.CandidateFingerprint();
                return preview;
            }
            catch { preview.Dispose(); throw; }
        }

        void BuildExtractions(IReadOnlyList<AiOrganizerComponent> components)
        {
            var nodes = _authoring.GetComponentsInChildren<PsdLayerNode>(true)
                .ToDictionary(node => LayerNodeIdUtility.GetStableNodeId(_editor, node), StringComparer.Ordinal);
            var metadata = _editor.generatedMetadataEntries.Single(entry => entry.PrefabAssetPath == PreviewPath).Entries;
            var generated = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath);
            var selected = new List<Transform>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in components)
            {
                if (component == null || component.rootIds == null || component.rootIds.Length < 2 ||
                    component.rootIds.Distinct().Count() != component.rootIds.Length || !names.Add(component.name ?? "") ||
                    (component.mode != "same" && component.mode != "states"))
                    throw new InvalidOperationException("公共组件名称、模式或实例清单无效。");
                var addresses = new List<string>();
                foreach (string id in component.rootIds)
                {
                    if (id == null || !nodes.TryGetValue(id, out var node)) throw new InvalidOperationException("整理后的节点不存在：" + id);
                    string key = _editor.BuildNormalizedNodePath(node.gameObject, _authoring.transform);
                    var matches = metadata.Where(entry => entry.Key == key).ToArray();
                    if (matches.Length != 1 || !GlobalObjectId.TryParse(matches[0].GlobalObjectId, out var globalId) ||
                        !(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) is GameObject item))
                        throw new InvalidOperationException("节点没有唯一的生成结果，不能按名称猜测：" + id);
                    if (selected.Any(other => item.transform == other || item.transform.IsChildOf(other) || other.IsChildOf(item.transform)))
                        throw new InvalidOperationException("公共组件选区重复或互相包含。");
                    selected.Add(item.transform);
                    addresses.Add(PsdCommonPrefabExtraction.GetNodeAddress(generated.transform, item.transform));
                }
                Extractions.Add(component.mode == "states"
                    ? PsdCommonPrefabExtraction.PreviewStates(PreviewPath, addresses.ToArray(), component.name)
                    : PsdCommonPrefabExtraction.Preview(PreviewPath, addresses.ToArray(), component.name));
            }
            // All groups pass preflight before the first temporary extraction changes the candidate.
            foreach (var plan in Extractions)
                PsdCommonPrefabExtraction.Apply(plan.UsesStates
                    ? PsdCommonPrefabExtraction.PreviewStates(PreviewPath, plan.Sources.ToArray(), Path.GetFileNameWithoutExtension(plan.OutputPath))
                    : PsdCommonPrefabExtraction.Preview(PreviewPath, plan.Sources.ToArray(), Path.GetFileNameWithoutExtension(plan.OutputPath)));
        }

        internal void Publish(Psd2UIFormConverterEditor current)
        {
            if (_published || _authoring == null) throw new InvalidOperationException("预览已失效。");
            ValidateDestination(_destination);
            if (current == null || PsdCommonPrefabPersistence.SourcePath(current.gameObject) != SourcePath ||
                !File.ReadAllBytes(SourcePath).SequenceEqual(_sourceBytes) ||
                PsdExtractionSourceFingerprint.Capture(current.gameObject) != _inputFingerprint ||
                CandidateFingerprint() != _candidateFingerprint)
                throw new InvalidOperationException("编辑树、配置或候选资产已变化，请重新分析并预览。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == SourcePath)
            {
                if (stage.scene.isDirty) throw new InvalidOperationException("编辑树有未保存修改，请重新分析。");
                StageUtility.GoToMainStage();
            }
            bool moved = false, sourceWriteStarted = false;
            try
            {
                string error = AssetDatabase.MoveAsset(_folder, _destination);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                moved = true;
                foreach (var metadata in _editor.generatedMetadataEntries)
                    if (metadata.PrefabAssetPath.StartsWith(_folder + "/", StringComparison.Ordinal))
                        metadata.PrefabAssetPath = _destination + metadata.PrefabAssetPath.Substring(_folder.Length);
                sourceWriteStarted = true;
                if (PrefabUtility.SaveAsPrefabAsset(_authoring, SourcePath) == null) throw new IOException("保存编辑树失败。");
                var rules = PsdCommonPrefabPersistence.Find(TargetPath);
                if (rules != null)
                {
                    rules.targetPath = TargetPath;
                    rules.sourceGuid = AssetDatabase.AssetPathToGUID(SourcePath);
                    rules.sourceFingerprint = PsdExtractionSourceFingerprint.Capture(_authoring);
                    EditorUtility.SetDirty(rules);
                    AssetDatabase.SaveAssetIfDirty(rules);
                }
                _published = true;
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                if (sourceWriteStarted)
                {
                    try
                    {
                    File.WriteAllBytes(SourcePath, _sourceBytes);
                    AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceUpdate);
                    }
                    catch (Exception restoreError) { failures.Add(restoreError); }
                }
                if (moved)
                {
                    try
                    {
                    string rollback = AssetDatabase.MoveAsset(_destination, _folder);
                    if (!string.IsNullOrEmpty(rollback)) throw new IOException("发布失败，恢复预览目录也失败：" + rollback);
                    }
                    catch (Exception restoreError) { failures.Add(restoreError); }
                }
                throw new AggregateException("发布失败，已尝试恢复源文件和输出目录。", failures);
            }
        }

        static void ValidateDestination(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("..") || path.Contains('\\') || Path.GetInvalidPathChars().Any(path.Contains) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\', '/')) ||
                Directory.Exists(path) || File.Exists(path) || File.Exists(path + ".meta"))
                throw new InvalidOperationException("请选择 Assets 下的新输出文件夹（父目录必须存在）。已有结果的源更新合并尚未实现。");
        }

        string CandidateFingerprint()
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = new MemoryStream())
            {
                foreach (string file in Directory.GetFiles(_folder, "*", SearchOption.AllDirectories).OrderBy(file => file, StringComparer.Ordinal))
                {
                    byte[] name = System.Text.Encoding.UTF8.GetBytes(file.Substring(_folder.Length));
                    stream.Write(name, 0, name.Length);
                    byte[] bytes = File.ReadAllBytes(file); stream.Write(bytes, 0, bytes.Length);
                }
                return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));
            }
        }

        public void Dispose()
        {
            if (_authoring != null) PrefabUtility.UnloadPrefabContents(_authoring);
            _authoring = null;
            if (!_published && !string.IsNullOrEmpty(_folder) && AssetDatabase.IsValidFolder(_folder)) AssetDatabase.DeleteAsset(_folder);
        }
    }
}
