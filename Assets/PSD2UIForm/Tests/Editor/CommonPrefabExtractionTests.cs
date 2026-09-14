using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UGF.EditorTools.Psd2UGUI;
namespace Psd2UIForm.Tests
{
    public class CommonPrefabExtractionTests
    {
        const string Folder = "Assets/__PsdCommonExtractionTests";
        const string Target = Folder + "/Screen.prefab";
        bool _ownsFolder;
        [SetUp] public void SetUp()
        {
            _ownsFolder = false;
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "Do not overwrite existing assets");
            AssetDatabase.CreateFolder("Assets", "__PsdCommonExtractionTests");
            _ownsFolder = true;
            var root = new GameObject("Screen", typeof(RectTransform));
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var item = new GameObject("Item" + i, typeof(RectTransform), typeof(Image));
                    item.transform.SetParent(root.transform, false);
                    item.GetComponent<Image>().color = i == 0 ? Color.red : Color.blue;
                    item.GetComponent<RectTransform>().anchoredPosition = new Vector2(100 * i, -25);
                    var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    label.transform.SetParent(item.transform, false);
                    label.GetComponent<Text>().text = i == 0 ? "First" : "Second";
                }
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TearDown] public void TearDown() { if (_ownsFolder) AssetDatabase.DeleteAsset(Folder); }
        [Test] public void Extract_PreservesDistinctContentAndCreatesNestedInstances()
        {
            var sources = new[] { "0", "1" };
            var plan = PsdCommonPrefabExtraction.Preview(Target, sources, "RewardItem");
            sources[0] = "1";
            Assert.That(plan.Sources[0], Is.EqualTo("0"));
            Assert.Throws<System.NotSupportedException>(() => ((System.Collections.Generic.IList<string>)plan.Sources)[0] = "1");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Common/RewardItem.prefab"), Is.Null);
            PsdCommonPrefabExtraction.Apply(plan);
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Assert.That(root.transform.childCount, Is.EqualTo(2));
                for (int i = 0; i < 2; i++)
                {
                    var item = root.transform.GetChild(i);
                    Assert.That(PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject), Is.True);
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject), Is.EqualTo(plan.OutputPath));
                    Assert.That(item.name, Is.EqualTo("Item" + i));
                    Assert.That(item.GetComponent<Image>().color, Is.EqualTo(i == 0 ? Color.red : Color.blue));
                    Assert.That(item.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(100 * i, -25)));
                    Assert.That(item.GetChild(0).GetComponent<Text>().text, Is.EqualTo(i == 0 ? "First" : "Second"));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [Test] public void Preview_RejectsExternalBindingsWithoutWritingAssets()
        {
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                var button = root.AddComponent<Button>();
                button.targetGraphic = root.transform.GetChild(0).GetComponent<Image>();
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var before = System.IO.File.ReadAllBytes(Target);
            Assert.Throws<System.InvalidOperationException>(() => PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem"));
            Assert.That(System.IO.File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Common"), Is.False);
        }

        [Test] public void Apply_RejectsChangedPrefabAfterPreview()
        {
            var plan = PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem");
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                root.transform.GetChild(0).name = "Changed";
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var before = System.IO.File.ReadAllBytes(Target);
            Assert.Throws<System.InvalidOperationException>(() => PsdCommonPrefabExtraction.Apply(plan));
            Assert.That(System.IO.File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(plan.OutputPath), Is.Null);
        }

        [Test] public void Extract_RetainsInternalBindingsSpritesAndInactiveChildren()
        {
            var texture = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(texture, Folder + "/Texture.asset");
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            AssetDatabase.CreateAsset(sprite, Folder + "/Icon.asset");
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var item = root.transform.GetChild(i);
                    var button = item.gameObject.AddComponent<Button>();
                    button.targetGraphic = item.GetComponent<Image>();
                    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(button.onClick, item.GetChild(0).gameObject.SetActive, true);
                    if (i == 1)
                    {
                        item.GetComponent<Image>().sprite = sprite;
                        item.GetChild(0).gameObject.SetActive(false);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            PsdCommonPrefabExtraction.Apply(PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem"));
            root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var item = root.transform.GetChild(i);
                    Assert.That(item.GetComponent<Button>().targetGraphic, Is.EqualTo(item.GetComponent<Image>()));
                    Assert.That(item.GetComponent<Button>().onClick.GetPersistentTarget(0), Is.EqualTo(item.GetChild(0).gameObject));
                    Assert.That(item.GetChild(0).gameObject.activeSelf, Is.EqualTo(i == 0));
                    Assert.That(item.GetComponent<Image>().sprite, Is.EqualTo(i == 0 ? null : sprite));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test] public void Preview_RejectsDifferentStructures()
        {
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Object.DestroyImmediate(root.transform.GetChild(1).GetChild(0).gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Assert.Throws<System.InvalidOperationException>(() => PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem"));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Common"), Is.False);
        }
        [Test] public void Preview_RejectsExistingCommonAssetWithoutOverwritingIt()
        {
            AssetDatabase.CreateFolder(Folder, "Common");
            AssetDatabase.CopyAsset(Target, Folder + "/Common/RewardItem.prefab");
            var before = System.IO.File.ReadAllBytes(Folder + "/Common/RewardItem.prefab");
            Assert.Throws<System.InvalidOperationException>(() => PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem"));
            Assert.That(System.IO.File.ReadAllBytes(Folder + "/Common/RewardItem.prefab"), Is.EqualTo(before));
        }
        [Test] public void Extract_AcceptsChineseAssetNames()
        {
            var plan = PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "签到奖励项");
            PsdCommonPrefabExtraction.Apply(plan);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Common/签到奖励项.prefab"), Is.Not.Null);
        }

        [Test] public void RepeatGeneration_AfterReloadRetainsAssetAndInstanceIdentities()
        {
            const string sourcePath = Folder + "/Source.prefab";
            AssetDatabase.CopyAsset(Target, sourcePath);
            var editing = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                foreach (Transform child in editing.transform)
                {
                    var node = child.gameObject.AddComponent<PsdLayerNode>();
                    node.UIType = GUIType.Image;
                    node.markToExport = true;
                    child.gameObject.AddComponent<ImageHelper>();
                }
                PrefabUtility.SaveAsPrefabAsset(editing, sourcePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(editing); }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            PsdCommonPrefabPersistence.RecordGeneration(source, Target);
            var plan = PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem");
            PsdCommonPrefabExtraction.Apply(plan);
            var targetBytes = System.IO.File.ReadAllBytes(Target);
            var commonBytes = System.IO.File.ReadAllBytes(plan.OutputPath);
            var targetGuid = AssetDatabase.AssetPathToGUID(Target);
            var commonGuid = AssetDatabase.AssetPathToGUID(plan.OutputPath);
            AssetDatabase.ImportAsset(Folder + "/Screen.extraction.asset", ImportAssetOptions.ForceUpdate);
            source = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(PsdCommonPrefabPersistence.TryReuse(source, Target, out var reused), Is.True);
                    Assert.That(reused.transform.childCount, Is.EqualTo(2));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }
            Assert.That(System.IO.File.ReadAllBytes(Target), Is.EqualTo(targetBytes));
            Assert.That(System.IO.File.ReadAllBytes(plan.OutputPath), Is.EqualTo(commonBytes));
            Assert.That(AssetDatabase.AssetPathToGUID(Target), Is.EqualTo(targetGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(plan.OutputPath), Is.EqualTo(commonGuid));
        }

        [TestCase("rename")]
        [TestCase("reorder")]
        [TestCase("remove")]
        public void RepeatGeneration_RejectsChangedSourceWithoutWriting(string change)
        {
            const string sourcePath = Folder + "/Source.prefab";
            AssetDatabase.CopyAsset(Target, sourcePath);
            PsdCommonPrefabPersistence.RecordGeneration(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath), Target);
            var plan = PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem");
            PsdCommonPrefabExtraction.Apply(plan);
            var before = System.IO.File.ReadAllBytes(Target);
            var source = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                if (change == "rename") source.transform.GetChild(0).name = "Changed";
                if (change == "reorder") source.transform.GetChild(0).SetAsLastSibling();
                if (change == "remove") Object.DestroyImmediate(source.transform.GetChild(0).gameObject);
                StringAssert.Contains("已变化", Assert.Throws<System.InvalidOperationException>(() =>
                    PsdCommonPrefabPersistence.TryReuse(source, Target, out _)).Message);
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }
            Assert.That(System.IO.File.ReadAllBytes(Target), Is.EqualTo(before));
        }

        [Test] public void RepeatGeneration_MovedAssetsAndRenamedInstancesUsePersistentIds()
        {
            const string sourcePath = Folder + "/Source.prefab";
            AssetDatabase.CopyAsset(Target, sourcePath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            PsdCommonPrefabPersistence.RecordGeneration(source, Target);
            var plan = PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem");
            PsdCommonPrefabExtraction.Apply(plan);
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                root.transform.GetChild(0).name = "同名";
                root.transform.GetChild(1).name = "同名";
                root.transform.GetChild(0).SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Assert.That(AssetDatabase.MoveAsset(plan.OutputPath, Folder + "/Common/Renamed.prefab"), Is.Empty);
            Assert.That(AssetDatabase.MoveAsset(Target, Folder + "/Moved.prefab"), Is.Empty);
            Assert.That(PsdCommonPrefabPersistence.TryReuse(source, Folder + "/Moved.prefab", out _), Is.True);
        }

        [Test] public void RepeatGeneration_UsesConverterEntryBeforeDestructiveGeneration()
        {
            const string sourcePath = Folder + "/Source.prefab";
            var source = new GameObject("Source", typeof(RectTransform), typeof(Psd2UIFormConverter));
            try
            {
                source.GetComponent<Psd2UIFormConverter>().uiFormName = "Screen";
                PrefabUtility.SaveAsPrefabAsset(source, sourcePath);
            }
            finally { Object.DestroyImmediate(source); }
            source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            PsdCommonPrefabPersistence.RecordGeneration(source, Target);
            PsdCommonPrefabExtraction.Apply(PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem"));
            var before = System.IO.File.ReadAllBytes(Target);
            var editor = Psd2UIFormConverterEditor.GetOrCreate(source.GetComponent<Psd2UIFormConverter>());
            Assert.That(editor.GenerateAndSaveUIFormPrefab(source.transform, Folder), Is.True);
            Assert.That(System.IO.File.ReadAllBytes(Target), Is.EqualTo(before));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("公共 Prefab 生成冲突.*目标名称或输出目录已改变"));
            Assert.That(editor.GenerateAndSaveUIFormPrefab(source.transform, Folder + "/NewOutput"), Is.False);
            Assert.That(System.IO.Directory.Exists(Folder + "/NewOutput"), Is.False);
        }

        [Test] public void RepeatGeneration_RejectsLostTargetAndUnpackedInstances()
        {
            const string sourcePath = Folder + "/Source.prefab";
            AssetDatabase.CopyAsset(Target, sourcePath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            PsdCommonPrefabPersistence.RecordGeneration(source, Target);
            PsdCommonPrefabExtraction.Apply(PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem"));
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root.transform.GetChild(0).gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Assert.Throws<System.InvalidOperationException>(() => PsdCommonPrefabPersistence.TryReuse(source, Target, out _));
            AssetDatabase.DeleteAsset(Target);
            Assert.Throws<System.InvalidOperationException>(() => PsdCommonPrefabPersistence.TryReuse(source, Target, out _));
            Assert.That(System.IO.File.Exists(Target), Is.False);
        }

        [Test] public void Generation_RefreshesBaselineAfterExplicitUnextractedReplacement()
        {
            const string sourcePath = Folder + "/Source.prefab";
            AssetDatabase.CopyAsset(Target, sourcePath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            PsdCommonPrefabPersistence.RecordGeneration(source, Target);
            AssetDatabase.DeleteAsset(Target);
            Assert.That(PsdCommonPrefabPersistence.TryReuse(source, Target, out _), Is.False);
            AssetDatabase.CopyAsset(sourcePath, Target);
            PsdCommonPrefabPersistence.RecordGeneration(source, Target);
            Assert.That(PsdCommonPrefabPersistence.Find(Target).targetGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(Target)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Extraction_RestoresAssetsWhenRulesWriteFails(bool existingBaseline)
        {
            string rulesPath = Folder + "/Screen.extraction.asset";
            if (existingBaseline)
            {
                AssetDatabase.CopyAsset(Target, Folder + "/Source.prefab");
                PsdCommonPrefabPersistence.RecordGeneration(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Source.prefab"), Target);
            }
            byte[] before = System.IO.File.ReadAllBytes(Target);
            byte[] metaBefore = System.IO.File.ReadAllBytes(Target + ".meta");
            byte[] rulesBefore = existingBaseline ? System.IO.File.ReadAllBytes(rulesPath) : null;
            var plan = PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "RewardItem");
            var save = PsdCommonPrefabPersistence.SaveRules;
            try
            {
                PsdCommonPrefabPersistence.SaveRules = rules =>
                {
                    save(rules);
                    throw new System.IO.IOException("Injected failure after rules write");
                };
                Assert.Throws<System.IO.IOException>(() => PsdCommonPrefabExtraction.Apply(plan));
            }
            finally { PsdCommonPrefabPersistence.SaveRules = save; }
            Assert.That(System.IO.File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(System.IO.File.ReadAllBytes(Target + ".meta"), Is.EqualTo(metaBefore));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Common"), Is.False);
            if (existingBaseline)
            {
                Assert.That(System.IO.File.ReadAllBytes(rulesPath), Is.EqualTo(rulesBefore));
                Assert.That(PsdCommonPrefabPersistence.Find(Target).rules, Is.Empty);
            }
            else Assert.That(System.IO.File.Exists(rulesPath), Is.False);
        }
    }
}
