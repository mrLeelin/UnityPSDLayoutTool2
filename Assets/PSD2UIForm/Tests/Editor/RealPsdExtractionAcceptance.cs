using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Psd2UIForm.Tests
{
    /// <summary>
    /// Slow, repository-level acceptance coverage. The fixture is copied before import so the
    /// original PSD, its importer, generated examples, and project settings are never modified.
    /// </summary>
    public sealed class RealPsdExtractionAcceptance
    {
        private const string FixturePath = "Assets/PSDLayoutTool2/TestData/7日签到拆分.psd";
        private const string Folder = "Assets/__PsdRealExtractionAcceptance";
        private const string InputPath = Folder + "/Input.psd";
        private const string EditorPrefabPath = Folder + "/Input_UIFormEditor.prefab";
        private const string OutputFolder = Folder + "/Generated";
        private const string TargetPath = OutputFolder + "/Input.prefab";

        private bool _ownsFolder;
        private bool _hasSettingsSnapshot;
        private SettingsSnapshot _settings;

        [SetUp]
        public void SetUp()
        {
            _ownsFolder = false;
            _hasSettingsSnapshot = false;
            _settings = SettingsSnapshot.Capture();
            _hasSettingsSnapshot = true;
            Assert.That(AssetDatabase.LoadMainAssetAtPath(FixturePath), Is.Not.Null,
                "真实 PSD 验收素材缺失：" + FixturePath);
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False,
                "验收目录已存在；为避免覆盖用户资产，测试已停止：" + Folder);
            Assert.That(AssetDatabase.CreateFolder("Assets", "__PsdRealExtractionAcceptance"), Is.Not.Empty);
            _ownsFolder = true;

            Psd2UIFormSettings settings = UGF.EditorTools.Psd2UGUI.ScriptableSingleton<Psd2UIFormSettings>.Instance;
            settings.UIImagesOutputDir = Folder + "/Images";
            settings.UIFormOutputDir = OutputFolder;
            settings.UseUIFormOutputDir = true;
            settings.CompressImage = false;
            settings.AutoCropMinimalNineSlice = false;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            if (_hasSettingsSnapshot)
            {
                _settings.Restore();
            }
            if (_ownsFolder)
            {
                Assert.That(AssetDatabase.DeleteAsset(Folder), Is.True,
                    "真实 PSD 验收临时目录清理失败：" + Folder);
            }
        }

        [Test]
        [Explicit("Runs the complete PSD import and Prefab generation pipeline against a 10 MB fixture.")]
        [Category("RealPsdAcceptance")]
        [Timeout(300000)]
        public void FirstGeneration_ExtractReloadAndRepeatGeneration_PreservesPrefabGraph()
        {
            Assert.That(AssetDatabase.CopyAsset(FixturePath, InputPath), Is.True);
            AssetDatabase.ImportAsset(InputPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Assert.That(Psd2UIFormConverterEditor.CreateOrUpdateEditorPrefabFromPsd(
                InputPath, null, false, false), Is.True, "真实 PSD 未能生成编辑 Prefab");

            GenerateFromReloadedEditorPrefab("真实 PSD 首次生成 UI Prefab 失败");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath), Is.Not.Null);

            PsdCommonPrefabPlan extraction = FindExtractableImagePair(TargetPath);
            PsdCommonPrefabExtraction.Apply(extraction);
            AssetDatabase.SaveAssets();

            byte[] targetBytes = File.ReadAllBytes(TargetPath);
            byte[] commonBytes = File.ReadAllBytes(extraction.OutputPath);
            string targetGuid = AssetDatabase.AssetPathToGUID(TargetPath);
            string commonGuid = AssetDatabase.AssetPathToGUID(extraction.OutputPath);
            PsdCommonPrefabRules rules = PsdCommonPrefabPersistence.Find(TargetPath);
            Assert.That(rules, Is.Not.Null);
            Assert.That(rules.rules, Has.Count.EqualTo(1));
            Assert.That(rules.rules[0].instanceIds, Has.Count.EqualTo(2));

            // Release all loaded Prefab contents and re-import the durable assets. This exercises
            // the same serialized rule/source seam used after an Editor domain or project reload.
            EditorUtility.UnloadUnusedAssetsImmediate();
            AssetDatabase.ImportAsset(EditorPrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Path.ChangeExtension(TargetPath, ".extraction.asset"),
                ImportAssetOptions.ForceUpdate);

            GenerateFromReloadedEditorPrefab("重新载入编辑 Prefab 后首次重复生成失败");
            GenerateFromReloadedEditorPrefab("重新载入编辑 Prefab 后第二次重复生成失败");

            Assert.That(File.ReadAllBytes(TargetPath), Is.EqualTo(targetBytes));
            Assert.That(File.ReadAllBytes(extraction.OutputPath), Is.EqualTo(commonBytes));
            Assert.That(AssetDatabase.AssetPathToGUID(TargetPath), Is.EqualTo(targetGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(extraction.OutputPath), Is.EqualTo(commonGuid));

            rules = PsdCommonPrefabPersistence.Find(TargetPath);
            Assert.That(rules.rules[0].instanceIds, Has.Count.EqualTo(2));
            Assert.That(PsdCommonPrefabPersistence.TryReuse(
                AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefabPath), TargetPath, out GameObject reused),
                Is.True);
            Assert.That(reused, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath)));
        }

        internal static void GenerateFromReloadedEditorPrefab(string failureMessage)
        {
            GameObject source = PrefabUtility.LoadPrefabContents(EditorPrefabPath);
            try
            {
                Assert.That(source, Is.Not.Null);
                Psd2UIFormConverter converter = source.GetComponent<Psd2UIFormConverter>();
                Assert.That(converter, Is.Not.Null);
                Assert.That(Psd2UIFormConverterEditor.GetOrCreate(converter)
                    .GenerateAndSaveUIFormPrefab(source.transform, OutputFolder), Is.True, failureMessage);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        internal static PsdCommonPrefabPlan FindExtractableImagePair(string targetPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(targetPath);
            List<string> candidates;
            try
            {
                candidates = root.GetComponentsInChildren<Image>(true)
                    .Select(image => image.transform)
                    .Where(transform => transform != root.transform && transform.childCount == 0)
                    .Select(transform => SiblingAddress(root.transform, transform))
                    .OrderBy(address => address, StringComparer.Ordinal)
                    .ToList();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            for (int first = 0; first < candidates.Count; first++)
            {
                for (int second = first + 1; second < candidates.Count; second++)
                {
                    try
                    {
                        return PsdCommonPrefabExtraction.Preview(targetPath,
                            new[] { candidates[first], candidates[second] }, "RealPsdSharedImage");
                    }
                    catch (InvalidOperationException)
                    {
                        // Generated buttons can point at a child Image. Keep searching for a pair
                        // without cross-boundary references, which is the supported extraction seam.
                    }
                }
            }

            Assert.Fail("真实 PSD 生成结果中未找到两个可安全抽取的同结构 Image 叶节点。");
            return null;
        }

        private static string SiblingAddress(Transform root, Transform node)
        {
            var indexes = new Stack<int>();
            while (node != root)
            {
                indexes.Push(node.GetSiblingIndex());
                node = node.parent;
            }
            return string.Join("/", indexes);
        }

        private struct SettingsSnapshot
        {
            internal string Images;
            internal string Forms;
            internal string LastForms;
            internal bool UseForms;
            internal bool Compress;
            internal bool AutoCrop;

            internal static SettingsSnapshot Capture()
            {
                Psd2UIFormSettings settings = UGF.EditorTools.Psd2UGUI.ScriptableSingleton<Psd2UIFormSettings>.Instance;
                return new SettingsSnapshot
                {
                    Images = settings.UIImagesOutputDir,
                    Forms = settings.UIFormOutputDir,
                    LastForms = settings.LastUIFormOutputDir,
                    UseForms = settings.UseUIFormOutputDir,
                    Compress = settings.CompressImage,
                    AutoCrop = settings.AutoCropMinimalNineSlice
                };
            }

            internal void Restore()
            {
                Psd2UIFormSettings settings = UGF.EditorTools.Psd2UGUI.ScriptableSingleton<Psd2UIFormSettings>.Instance;
                settings.UIImagesOutputDir = Images;
                settings.UIFormOutputDir = Forms;
                settings.LastUIFormOutputDir = LastForms;
                settings.UseUIFormOutputDir = UseForms;
                settings.CompressImage = Compress;
                settings.AutoCropMinimalNineSlice = AutoCrop;
            }
        }
    }
}
