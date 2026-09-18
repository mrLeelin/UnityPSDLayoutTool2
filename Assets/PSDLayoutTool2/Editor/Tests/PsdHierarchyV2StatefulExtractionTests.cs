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
    /// 03c：有状态重复项抽取（statefulComponentExtractions）。
    /// 一个共享 Prefab，[States]（先）+ [Common]（后），逐实例完整映射并只激活自己的状态。
    /// </summary>
    public sealed class PsdHierarchyV2StatefulExtractionTests
    {
        private const string Folder = "Assets/__PsdV2StatefulExtractionTests";
        private const string Target = Folder + "/InventoryPanelView.prefab";
        private const string ComponentPath = Folder + "/InventoryItem.prefab";
        private PsdHierarchyChatContext context;
        private bool ownsFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2StatefulExtractionTests");
            ownsFolder = true;
            BuildFixture(item4BackgroundExtraChild: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder)
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private void BuildFixture(bool item4BackgroundExtraChild)
        {
            var root = new GameObject("InventoryPanelView", typeof(RectTransform));
            try
            {
                var list = new GameObject("[ItemList]", typeof(RectTransform));
                list.transform.SetParent(root.transform, false);

                CreateRow(list.transform, "[Item_01]", "Label1", "10", new Color(0f, 1f, 0f, 1f), withLock: false, backgroundExtraChild: false);
                CreateRow(list.transform, "[Item_02]", "Label2", "20", new Color(0f, 1f, 0f, 1f), withLock: false, backgroundExtraChild: false);
                CreateRow(list.transform, "[Item_03]", "Label3", "30", new Color(0.5f, 0.5f, 0.5f, 1f), withLock: true, backgroundExtraChild: false);
                CreateRow(list.transform, "[Item_04]", "Label4", "40", new Color(0.5f, 0.5f, 0.5f, 1f), withLock: true, backgroundExtraChild: item4BackgroundExtraChild);

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RefreshContext();
        }

        private static void CreateRow(
            Transform parent,
            string name,
            string label,
            string value,
            Color background,
            bool withLock,
            bool backgroundExtraChild)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            ((RectTransform)row.transform).sizeDelta = new Vector2(120f, 48f);

            CreateChild(row.transform, "ItemLabel", typeof(Text), label, background, false);
            CreateChild(row.transform, "ItemValue", typeof(Text), value, background, false);
            CreateChild(row.transform, "ItemBackground", typeof(Image), string.Empty, background, backgroundExtraChild);
            if (withLock)
            {
                CreateChild(row.transform, "ItemLock", typeof(Image), string.Empty, background, false);
            }
        }

        private static void CreateChild(
            Transform parent,
            string name,
            System.Type componentType,
            string text,
            Color color,
            bool extraChild)
        {
            var node = new GameObject(name, typeof(RectTransform), componentType);
            node.transform.SetParent(parent, false);
            if (node.GetComponent<Text>() != null)
            {
                Text label = node.GetComponent<Text>();
                label.text = text;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (node.GetComponent<Image>() != null)
            {
                node.GetComponent<Image>().color = color;
            }

            if (extraChild)
            {
                var extra = new GameObject("Glow", typeof(RectTransform), typeof(Image));
                extra.transform.SetParent(node.transform, false);
            }
        }

        private void RefreshContext()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            context = new PsdHierarchyChatContext(project, "", Target, "", "", "",
                hierarchySnapshotJson: new JObject
                {
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "root", ["path"] = "InventoryPanelView" },
                        new JObject { ["id"] = "list", ["path"] = "InventoryPanelView/[ItemList]" },
                        new JObject { ["id"] = "item1", ["path"] = "InventoryPanelView/[ItemList]/[Item_01]" },
                        new JObject { ["id"] = "item2", ["path"] = "InventoryPanelView/[ItemList]/[Item_02]" },
                        new JObject { ["id"] = "item3", ["path"] = "InventoryPanelView/[ItemList]/[Item_03]" },
                        new JObject { ["id"] = "item4", ["path"] = "InventoryPanelView/[ItemList]/[Item_04]" }),
                }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));
        }

        private JObject Plan(
            string instance4State = "locked",
            string[] instance4CommonNames = null,
            string[] instance4StateNames = null)
        {
            instance4CommonNames = instance4CommonNames ?? new[] { "ItemLabel", "ItemValue" };
            instance4StateNames = instance4StateNames ?? new[] { "ItemBackground", "ItemLock" };

            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "InventoryPanelView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
            };
            foreach (string key in new[]
                     {
                         "wrappers", "moves", "renames", "tightBounds", "emptyContainerRemovals",
                         "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions",
                         "containmentResolutions", "flatSiblingResolutions", "componentExtractions",
                         "stateComponentExtractions", "variantComponentExtractions", "postGroupingExtractionIntents",
                     })
            {
                plan[key] = new JArray();
            }

            plan["statefulComponentExtractions"] = new JArray(new JObject
            {
                ["id"] = "inventory_item",
                ["template"] = "node:item1",
                ["assetPath"] = ComponentPath,
                ["commonName"] = "[Common]",
                ["statesName"] = "[States]",
                ["defaultState"] = "available",
                ["common"] = new JObject
                {
                    ["source"] = "node:item1",
                    ["members"] = new JArray(
                        new JObject { ["sourceName"] = "ItemLabel", ["name"] = "ItemLabel" },
                        new JObject { ["sourceName"] = "ItemValue", ["name"] = "ItemValue" }),
                },
                ["states"] = new JArray(
                    new JObject
                    {
                        ["id"] = "available",
                        ["source"] = "node:item1",
                        ["name"] = "[State_Available]",
                        ["members"] = new JArray(
                            new JObject { ["sourceName"] = "ItemBackground", ["name"] = "AvailableBackground" }),
                    },
                    new JObject
                    {
                        ["id"] = "locked",
                        ["source"] = "node:item3",
                        ["name"] = "[State_Locked]",
                        ["members"] = new JArray(
                            new JObject { ["sourceName"] = "ItemBackground", ["name"] = "LockedBackground" },
                            new JObject { ["sourceName"] = "ItemLock", ["name"] = "LockIcon" }),
                    }),
                ["instances"] = new JArray(
                    new JObject
                    {
                        ["source"] = "node:item1",
                        ["name"] = "[Item_01]",
                        ["state"] = "available",
                        ["commonSourceNames"] = new JArray("ItemLabel", "ItemValue"),
                        ["stateSourceNames"] = new JArray("ItemBackground"),
                    },
                    new JObject
                    {
                        ["source"] = "node:item4",
                        ["name"] = "[Item_04]",
                        ["state"] = instance4State,
                        ["commonSourceNames"] = new JArray(instance4CommonNames),
                        ["stateSourceNames"] = new JArray(instance4StateNames),
                    }),
            });
            return plan;
        }

        [Test]
        public async Task StatefulExtractionPartitionsCommonAndStateMembersPerInstance()
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
                Transform list = root.transform.Find("[ItemList]");
                Assert.That(list, Is.Not.Null);
                // 计划只抽取 [Item_01] 与 [Item_04]；未列入 instances 的行保持原样。
                Assert.That(list.childCount, Is.EqualTo(4));
                Assert.That(list.GetChild(1).name, Is.EqualTo("[Item_02]"));
                Assert.That(list.GetChild(1).Find("[Common]"), Is.Null);
                Assert.That(list.GetChild(2).name, Is.EqualTo("[Item_03]"));
                Assert.That(list.GetChild(2).Find("[Common]"), Is.Null);

                Transform available = list.GetChild(0);
                Transform locked = list.GetChild(3);
                Assert.That(available.name, Is.EqualTo("[Item_01]"));
                Assert.That(locked.name, Is.EqualTo("[Item_04]"));
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(available.gameObject),
                    Is.EqualTo(ComponentPath));
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(locked.gameObject),
                    Is.EqualTo(ComponentPath));

                foreach (Transform instance in new[] { available, locked })
                {
                    Transform common = instance.Find("[Common]");
                    Transform states = instance.Find("[States]");
                    Assert.That(common, Is.Not.Null);
                    Assert.That(states, Is.Not.Null);
                    Assert.That(
                        states.GetSiblingIndex(),
                        Is.LessThan(common.GetSiblingIndex()),
                        "[States] 必须先于 [Common] 创建，保证共享内容绘制在状态背景之上。");
                    Assert.That(common.childCount, Is.EqualTo(2));
                    Assert.That(common.GetChild(0).name, Is.EqualTo("ItemLabel"));
                    Assert.That(common.GetChild(1).name, Is.EqualTo("ItemValue"));
                    Assert.That(states.childCount, Is.EqualTo(2));
                    Assert.That(states.GetChild(0).name, Is.EqualTo("[State_Available]"));
                    Assert.That(states.GetChild(1).name, Is.EqualTo("[State_Locked]"));
                    Assert.That(states.GetChild(0).childCount, Is.EqualTo(1));
                    Assert.That(states.GetChild(1).childCount, Is.EqualTo(2));
                }

                // 实例 1：available 状态，保留自己的文本与背景。
                Assert.That(available.Find("[States]/[State_Available]").gameObject.activeSelf, Is.True);
                Assert.That(available.Find("[States]/[State_Locked]").gameObject.activeSelf, Is.False);
                Assert.That(available.Find("[Common]/ItemLabel").GetComponent<Text>().text, Is.EqualTo("Label1"));
                Assert.That(available.Find("[Common]/ItemValue").GetComponent<Text>().text, Is.EqualTo("10"));
                Assert.That(
                    available.Find("[States]/[State_Available]/AvailableBackground").GetComponent<Image>().color,
                    Is.EqualTo(new Color(0f, 1f, 0f, 1f)));

                // 实例 4：locked 状态，自己的文本、背景与锁图标都保留。
                Assert.That(locked.Find("[States]/[State_Locked]").gameObject.activeSelf, Is.True);
                Assert.That(locked.Find("[States]/[State_Available]").gameObject.activeSelf, Is.False);
                Assert.That(locked.Find("[Common]/ItemLabel").GetComponent<Text>().text, Is.EqualTo("Label4"));
                Assert.That(locked.Find("[Common]/ItemValue").GetComponent<Text>().text, Is.EqualTo("40"));
                Assert.That(
                    locked.Find("[States]/[State_Locked]/LockedBackground").GetComponent<Image>().color,
                    Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1f)));
                Assert.That(locked.Find("[States]/[State_Locked]/LockIcon"), Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task MandatoryStatefulCandidateAcceptsMatchingReviewedExtraction()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            JObject snapshot = JObject.Parse(context.hierarchySnapshotJson);
            snapshot["componentFamilyCandidates"] = new JArray(new JObject
            {
                ["id"] = "inventory_family",
                ["requiresExtraction"] = true,
                ["recommendedMode"] = "stateful",
                ["parent"] = "node:list",
                ["sources"] = new JArray("node:item1", "node:item4"),
            });
            context = new PsdHierarchyChatContext(
                project, "", Target, "", "", "",
                hierarchySnapshotJson: snapshot.ToString(),
                hierarchySnapshotFingerprint: context.hierarchySnapshotFingerprint);
            JObject plan = Plan();
            plan["componentFamilyDecisions"] = new JArray(new JObject
            {
                ["candidateId"] = "inventory_family",
                ["parent"] = "node:list",
                ["sources"] = new JArray("node:item1", "node:item4"),
                ["mode"] = "stateful",
                ["extractionId"] = "inventory_item",
            });

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.True, result.message);
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task UnmappedMemberIsRejectedBeforeAnyWrite()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(instance4StateNames: new[] { "ItemBackground" }).ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("unmapped"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task DuplicatedMemberMappingIsRejectedBeforeAnyWrite()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context,
                Plan(instance4StateNames: new[] { "ItemBackground", "ItemBackground", "ItemLock" }).ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("twice"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task UnknownInstanceStateIsRejectedBeforeAnyWrite()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(instance4State: "missing").ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("unknown state"));
        }

        [Test]
        public async Task MemberStructureMismatchIsRejectedBeforeAnyWrite()
        {
            AssetDatabase.DeleteAsset(Target);
            BuildFixture(item4BackgroundExtraChild: true);
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("structure differs"));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task ExistingStatefulComponentAssetIsRejected()
        {
            var existing = new GameObject("InventoryItem", typeof(RectTransform));
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
