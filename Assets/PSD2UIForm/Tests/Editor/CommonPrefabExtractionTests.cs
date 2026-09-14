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
    }
}
