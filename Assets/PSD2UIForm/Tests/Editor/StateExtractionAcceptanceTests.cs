using System.IO;
using System.Linq;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Psd2UIForm.Tests
{
    public class StateExtractionAcceptanceTests
    {
        const string Folder = "Assets/__PsdStateAcceptance";
        const string Target = Folder + "/Screen.prefab";
        bool _ownsFolder;

        [SetUp] public void SetUp()
        {
            _ownsFolder = false;
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            Assert.That(AssetDatabase.CreateFolder("Assets", "__PsdStateAcceptance"), Is.Not.Empty);
            _ownsFolder = true;
            var root = new GameObject("Screen", typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 800);
                for (int i = 0; i < 3; i++)
                {
                    var card = new GameObject("Card" + i, typeof(RectTransform));
                    card.transform.SetParent(root.transform, false);
                    var rect = card.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(150 + 25 * i, 200);
                    rect.anchoredPosition = new Vector2(i * 220 - 220, 40);
                    rect.pivot = new Vector2(.2f + .25f * i, .3f + .1f * i);
                    AddImage(card.transform, "Background", new Vector2(0, 0), new Vector2(120, 180), Color.gray);
                    AddImage(card.transform, "Reward", new Vector2(-25, 5), new Vector2(30, 40), i == 0 ? Color.red : Color.blue);
                    if (i == 2) AddImage(card.transform, "ExtraReward", new Vector2(30, 5), new Vector2(25, 35), Color.green);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TearDown] public void TearDown() { if (_ownsFolder) AssetDatabase.DeleteAsset(Folder); }

        [Test] public void DifferentStructures_PreserveVisibleGeometryAndRepeatGeneration()
        {
            AssetDatabase.CopyAsset(Target, Folder + "/Source.prefab");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Source.prefab");
            PsdCommonPrefabPersistence.RecordGeneration(source, Target);
            var before = PrefabUtility.LoadPrefabContents(Target);
            var expected = before.GetComponentsInChildren<Image>(false).Select(Snapshot).ToArray();
            PrefabUtility.UnloadPrefabContents(before);

            var plan = PsdCommonPrefabExtraction.PreviewStates(Target, new[] { "0", "1", "2" }, "Rewards");
            Assert.That(plan.UsesStates, Is.True);
            Assert.That(plan.States.Count, Is.EqualTo(2));
            Assert.That(plan.CommonMembers.Count, Is.GreaterThan(0), "Compatible common members should be shared");
            Assert.That(plan.SourceStates[0].State, Is.EqualTo(plan.SourceStates[1].State));
            Assert.That(plan.SourceStates[2].State, Is.Not.EqualTo(plan.SourceStates[0].State));
            PsdCommonPrefabExtraction.Apply(plan);

            var after = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Assert.That(after.transform.childCount, Is.EqualTo(3));
                Assert.That(after.GetComponentsInChildren<Image>(false).Select(Snapshot).ToArray(), Is.EqualTo(expected));
                for (int i = 0; i < 3; i++)
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(after.transform.GetChild(i).gameObject), Is.EqualTo(plan.OutputPath));
            }
            finally { PrefabUtility.UnloadPrefabContents(after); }
            byte[] saved = File.ReadAllBytes(Target);
            AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceUpdate);
            Assert.That(PsdCommonPrefabPersistence.TryReuse(source, Target, out var reused), Is.True);
            Assert.That(reused, Is.Not.Null);
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(saved));
        }

        [Test] public void SharedMembersAfterDifferentMembers_DoNotChangeDrawOrder()
        {
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var card = root.transform.GetChild(i);
                    card.GetChild(1).name = "Different" + i;
                    AddImage(card, "Overlay", Vector2.zero, new Vector2(10, 10), Color.white);
                    card.GetChild(card.childCount - 1).SetSiblingIndex(2);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            DifferentStructures_PreserveVisibleGeometryAndRepeatGeneration();
        }

        [Test] public void AutomaticLayoutRoot_IsRejectedBeforeWriting()
        {
            var root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                foreach (Transform card in root.transform) card.gameObject.AddComponent<HorizontalLayoutGroup>();
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            byte[] before = File.ReadAllBytes(Target);
            Assert.Throws<System.InvalidOperationException>(() => PsdCommonPrefabExtraction.PreviewStates(Target, new[] { "0", "1", "2" }, "Rewards"));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/Common"), Is.False);
        }

        [Test] public void DifferentRootComponents_RetainDepthAndInternalButtonBinding()
        {
            var root = PrefabUtility.LoadPrefabContents(Target);
            string[] expected;
            try
            {
                var first = root.transform.GetChild(0);
                var button = first.gameObject.AddComponent<Button>();
                button.targetGraphic = first.GetChild(1).GetComponent<Image>();
                first.localPosition += new Vector3(0, 0, 7);
                expected = root.GetComponentsInChildren<Image>(false).Select(Snapshot).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var plan = PsdCommonPrefabExtraction.PreviewStates(Target, new[] { "0", "1", "2" }, "RootStates");
            Assert.That(plan.CommonMembers, Is.Empty);
            PsdCommonPrefabExtraction.Apply(plan);
            var after = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Assert.That(after.GetComponentsInChildren<Image>(false).Select(Snapshot).ToArray(), Is.EqualTo(expected));
                var button = after.transform.GetChild(0).GetComponentInChildren<Button>();
                Assert.That(button.targetGraphic, Is.EqualTo(button.transform.GetChild(1).GetComponent<Image>()));
            }
            finally { PrefabUtility.UnloadPrefabContents(after); }
        }

        static string Snapshot(Image image)
        {
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            return image.name + "|" + image.color + "|" + AssetDatabase.GetAssetPath(image.sprite) + "|" +
                string.Join("|", corners.Select(c => c.ToString("F3")));
        }

        [Test, Explicit("Uses the repository seven-day sign-in fixture without modifying it.")]
        public void SevenDayFixture_PreservesVisibleRewardsAcrossStateExtraction()
        {
            const string fixture = "Assets/PSDLayoutTool2/TestData/7日签到拆分/Prefab/7日签到拆分.prefab";
            const string common = "Assets/PSDLayoutTool2/TestData/7日签到拆分/Prefab/Common/ReusableItemVariant.prefab";
            Assert.That(AssetDatabase.LoadMainAssetAtPath(fixture), Is.Not.Null);
            var root = PrefabUtility.LoadPrefabContents(fixture);
            string[] addresses;
            string[] expected;
            string[] expectedTexts;
            try
            {
                var items = root.GetComponentsInChildren<RectTransform>(true)
                    .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == common).ToArray();
                Assert.That(items.Length, Is.EqualTo(7));
                addresses = items.Select(t => PsdCommonPrefabExtraction.GetNodeAddress(root.transform, t)).ToArray();
                foreach (var item in items)
                {
                    PrefabUtility.UnpackPrefabInstance(item.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    // Recover the visible original subtree from the existing example's state container.
                    var branch = item.Find("[States]").Cast<Transform>().Single(t => t.gameObject.activeSelf);
                    var content = branch.GetChild(0);
                    content.SetParent(item.parent, true);
                    content.SetSiblingIndex(item.GetSiblingIndex());
                    Object.DestroyImmediate(item.gameObject);
                }
                expected = root.GetComponentsInChildren<Image>(false).Select(Snapshot).ToArray();
                expectedTexts = root.GetComponentsInChildren<TMPro.TMP_Text>(false).Select(t => t.text).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, Target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var plan = PsdCommonPrefabExtraction.PreviewStates(Target, addresses, "SevenDayRewards");
            Assert.That(plan.States.Count, Is.EqualTo(2));
            PsdCommonPrefabExtraction.Apply(plan);
            var result = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Assert.That(result.GetComponentsInChildren<Image>(false).Select(Snapshot).ToArray(), Is.EqualTo(expected));
                Assert.That(result.GetComponentsInChildren<TMPro.TMP_Text>(false).Select(t => t.text).ToArray(), Is.EqualTo(expectedTexts));
            }
            finally { PrefabUtility.UnloadPrefabContents(result); }
        }

        static void AddImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
        }
    }
}
