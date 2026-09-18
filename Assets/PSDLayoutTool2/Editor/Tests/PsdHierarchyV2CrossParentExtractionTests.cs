namespace PsdLayoutTool2.Tests
{
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 04b：跨父级抽取（crossParentPrefabExtractions），同样只由已锁定的局部修复生成。
    /// 模板成员来自不同父节点，每个审核过的组各建一个实例并保留自己的差异，unmatched 原样不动。
    /// </summary>
    public sealed class PsdHierarchyV2CrossParentExtractionTests
    {
        private const string Folder = "Assets/__PsdV2CrossParentExtractionTests";
        private const string Target = Folder + "/ExampleView.prefab";
        private const string ComponentPath = Folder + "/DaySignRewardItem.prefab";
        private PsdHierarchyChatContext context;
        private PsdHierarchyLocalRepairScope scope;
        private bool ownsFolder;

        private static readonly string[] TemplatePaths =
        {
            "ExampleView/Screen/Progress/DateMarker5",
            "ExampleView/Screen/Progress/DateText4",
            "ExampleView/Screen/Reward/GiftBox4",
        };

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2CrossParentExtractionTests");
            ownsFolder = true;
            BuildFixture(rotatedProgress: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder)
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private void BuildFixture(bool rotatedProgress)
        {
            var root = new GameObject("ExampleView", typeof(RectTransform));
            try
            {
                Transform screen = CreateRect(root.transform, "Screen", Vector2.zero);
                Transform reward = CreateRect(screen, "Reward", Vector2.zero);
                Transform progress = CreateRect(screen, "Progress", Vector2.zero);
                if (rotatedProgress)
                {
                    progress.localRotation = Quaternion.Euler(0f, 0f, 15f);
                }

                for (int index = 1; index <= 4; index++)
                {
                    CreateImage(reward, "GiftBox" + index, new Vector2(index * 100f, -42f), index);
                    CreateImage(progress, "DateText" + index, new Vector2(index * 100f, 2f), index + 10);
                }

                for (int index = 1; index <= 5; index++)
                {
                    CreateImage(progress, "DateMarker" + index, new Vector2((index - 1) * 100f, 0f), index + 20);
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RefreshContext();
        }

        private static Transform CreateRect(Transform parent, string name, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            ((RectTransform)node.transform).anchoredPosition = position;
            return node.transform;
        }

        private static void CreateImage(Transform parent, string name, Vector2 position, int colorIndex)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(80f, 40f);
            node.GetComponent<Image>().color = ColorFor(colorIndex);
        }

        private static Color ColorFor(int index)
        {
            return new Color(index / 100f, 0.5f, 0.5f, 1f);
        }

        private void RefreshContext()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            context = new PsdHierarchyChatContext(project, "", Target, "", "", "",
                hierarchySnapshotJson: new JObject
                {
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "root", ["path"] = "ExampleView" },
                        new JObject { ["id"] = "screen", ["path"] = "ExampleView/Screen", ["parentId"] = "root" },
                        new JObject { ["id"] = "reward", ["path"] = "ExampleView/Screen/Reward", ["parentId"] = "screen" },
                        new JObject { ["id"] = "progress", ["path"] = "ExampleView/Screen/Progress", ["parentId"] = "screen" },
                        Marker("m1", "DateMarker1"), Marker("m2", "DateMarker2"), Marker("m3", "DateMarker3"),
                        Marker("m4", "DateMarker4"), Marker("m5", "DateMarker5"),
                        TextNode("t1", "DateText1"), TextNode("t2", "DateText2"),
                        TextNode("t3", "DateText3"), TextNode("t4", "DateText4"),
                        Gift("g1", "GiftBox1"), Gift("g2", "GiftBox2"), Gift("g3", "GiftBox3"), Gift("g4", "GiftBox4")),
                }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));

            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    TemplatePaths,
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out scope,
                    out string scopeError),
                Is.True,
                scopeError);
            context.localRepairScope = scope;
        }

        private static JObject Marker(string id, string name)
        {
            return new JObject
            {
                ["id"] = id, ["path"] = "ExampleView/Screen/Progress/" + name, ["parentId"] = "progress",
            };
        }

        private static JObject TextNode(string id, string name)
        {
            return new JObject
            {
                ["id"] = id, ["path"] = "ExampleView/Screen/Progress/" + name, ["parentId"] = "progress",
            };
        }

        private static JObject Gift(string id, string name)
        {
            return new JObject
            {
                ["id"] = id, ["path"] = "ExampleView/Screen/Reward/" + name, ["parentId"] = "reward",
            };
        }

        private JObject Plan(
            string[] groupOneSources = null,
            string[] unmatched = null,
            string assetPath = null,
            bool includeTemplateGroup = true)
        {
            groupOneSources = groupOneSources ?? new[] { "node:m2", "node:t1", "node:g1" };
            unmatched = unmatched ?? new[] { "node:m1" };

            var instances = new JArray(
                new JObject { ["sequence"] = 1, ["sources"] = new JArray(groupOneSources) },
                new JObject { ["sequence"] = 2, ["sources"] = new JArray("node:m3", "node:t2", "node:g2") },
                new JObject { ["sequence"] = 3, ["sources"] = new JArray("node:m4", "node:t3", "node:g3") });
            if (includeTemplateGroup)
            {
                instances.Add(new JObject
                {
                    ["sequence"] = 4, ["sources"] = new JArray("node:m5", "node:t4", "node:g4"),
                });
            }

            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "ExampleView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["crossParentPrefabExtractions"] = new JArray(new JObject
                {
                    ["id"] = "day_sign_reward_item",
                    ["name"] = "DaySignRewardItem",
                    ["assetPath"] = assetPath ?? ComponentPath,
                    ["root"] = "node:screen",
                    ["templateSources"] = new JArray("node:m5", "node:t4", "node:g4"),
                    ["instances"] = instances,
                    ["unmatched"] = new JArray(unmatched),
                }),
            };
            foreach (string key in new[]
                     {
                         "wrappers", "moves", "renames", "tightBounds", "emptyContainerRemovals",
                         "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions",
                         "containmentResolutions", "flatSiblingResolutions", "componentExtractions",
                         "stateComponentExtractions", "variantComponentExtractions",
                         "statefulComponentExtractions", "selectedPrefabExtractions",
                         "postGroupingExtractionIntents",
                     })
            {
                plan[key] = new JArray();
            }

            return plan;
        }

        [Test]
        public async Task CrossParentExtractionReusesOnePrefabAndKeepsEachGroupState()
        {
            JObject plan = Plan();
            byte[] before = File.ReadAllBytes(Target);

            var preflight = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(preflight.success, Is.True, preflight.message);
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False, "预检不得生成声明的抽取资产。");

            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());
            Assert.That(result.success, Is.True, result.message);

            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Transform screen = root.transform.Find("Screen");
                Assert.That(screen, Is.Not.Null);

                var instances = new System.Collections.Generic.List<Transform>();
                for (int index = 0; index < screen.childCount; index++)
                {
                    Transform child = screen.GetChild(index);
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == ComponentPath)
                    {
                        instances.Add(child);
                    }
                }

                Assert.That(instances.Count, Is.EqualTo(4), "四个已审核的组必须各有一个实例。");
                for (int index = 0; index < instances.Count; index++)
                {
                    Transform instance = instances[index];
                    Assert.That(instance.childCount, Is.EqualTo(3));
                    Assert.That(instance.GetChild(0).name, Is.EqualTo("DateMarker5"));
                    Assert.That(instance.GetChild(1).name, Is.EqualTo("DateText4"));
                    Assert.That(instance.GetChild(2).name, Is.EqualTo("GiftBox4"));
                    Assert.That(instance.GetChild(0).GetComponent<Image>().color, Is.EqualTo(ColorFor(index + 22)));
                    Assert.That(instance.GetChild(1).GetComponent<Image>().color, Is.EqualTo(ColorFor(index + 11)));
                    Assert.That(((RectTransform)instance.GetChild(2)).anchoredPosition.x,
                        Is.EqualTo((index + 1) * 100f).Within(0.01f));
                }

                // unmatched 节点必须原样保留。
                Assert.That(screen.Find("Progress/DateMarker1"), Is.Not.Null);
                // 被抽取的源节点必须已经被替换。
                Assert.That(screen.Find("Progress/DateMarker2"), Is.Null);
                Assert.That(screen.Find("Reward/GiftBox1"), Is.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task GroupWithoutTheTemplateIsRejectedBeforeAnyWrite()
        {
            JObject plan = Plan(includeTemplateGroup: false);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task IncompleteGroupIsRejectedBeforeAnyWrite()
        {
            JObject plan = Plan(groupOneSources: new[] { "node:m2", "node:t1" });

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task UnmatchedNodeInsideAGroupIsRejectedBeforeAnyWrite()
        {
            JObject plan = Plan(unmatched: new[] { "node:m2" });

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("Unmatched").Or.Contain("unmatched"));
        }

        [Test]
        public async Task ScaledOrRotatedParentIsRejectedBeforeAnyWrite()
        {
            AssetDatabase.DeleteAsset(Target);
            BuildFixture(rotatedProgress: true);
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("scaled or rotated"));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task ExistingCrossParentComponentAssetIsRejected()
        {
            var existing = new GameObject("DaySignRewardItem", typeof(RectTransform));
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(existing, ComponentPath), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(existing);
            }

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("already exists"));
        }
    }
}
