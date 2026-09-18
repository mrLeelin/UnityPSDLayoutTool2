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
    /// 03b：变体列表抽取（variantComponentExtractions）。
    /// 一行一个实例，每个实例只激活自己的状态分支，并保留该行的差异。
    /// </summary>
    public sealed class PsdHierarchyV2VariantExtractionTests
    {
        private const string Folder = "Assets/__PsdV2VariantExtractionTests";
        private const string Target = Folder + "/InventoryPanelView.prefab";
        private const string ComponentPath = Folder + "/InventoryItem.prefab";
        private PsdHierarchyChatContext context;
        private bool ownsFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2VariantExtractionTests");
            ownsFolder = true;
            BuildFixture(item4ExtraChild: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder)
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private void BuildFixture(bool item4ExtraChild)
        {
            var root = new GameObject("InventoryPanelView", typeof(RectTransform));
            try
            {
                var list = new GameObject("[ItemList]", typeof(RectTransform));
                list.transform.SetParent(root.transform, false);

                CreateRow(list.transform, "[Item_01]", new Color(1f, 0f, 0f, 1f), "InProgress", "1", false);
                CreateRow(list.transform, "[Item_02]", new Color(1f, 1f, 0f, 1f), "Claimable", "2", false);
                CreateRow(list.transform, "[Item_03]", new Color(0.5f, 0.5f, 0.5f, 1f), "Locked", "3", false);
                CreateRow(list.transform, "[Item_04]", new Color(1f, 1f, 0f, 1f), "Claimable", "4", item4ExtraChild);

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RefreshContext();
        }

        private static GameObject CreateRow(
            Transform parent,
            string name,
            Color color,
            string label,
            string value,
            bool extraChild)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            var rect = (RectTransform)row.transform;
            rect.sizeDelta = new Vector2(100f, 40f);
            row.GetComponent<Image>().color = color;

            var labelNode = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelNode.transform.SetParent(row.transform, false);
            Text labelText = labelNode.GetComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var valueNode = new GameObject("Value", typeof(RectTransform), typeof(Text));
            valueNode.transform.SetParent(row.transform, false);
            Text valueText = valueNode.GetComponent<Text>();
            valueText.text = value;
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (extraChild)
            {
                var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
                badge.transform.SetParent(row.transform, false);
            }

            return row;
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
                        new JObject { ["id"] = "item4", ["path"] = "InventoryPanelView/[ItemList]/[Item_04]" },
                        new JObject { ["id"] = "label1", ["path"] = "InventoryPanelView/[ItemList]/[Item_01]/Label" }),
                }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));
        }

        private JObject Plan(
            string labelStateSource = "node:item1",
            string instance4State = "claimable",
            bool omitThirdStateRepresentative = false)
        {
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
                         "stateComponentExtractions", "statefulComponentExtractions", "postGroupingExtractionIntents",
                     })
            {
                plan[key] = new JArray();
            }

            var instances = new JArray(
                new JObject { ["source"] = "node:item1", ["name"] = "[Item_01]", ["state"] = "in_progress" },
                new JObject { ["source"] = "node:item2", ["name"] = "[Item_02]", ["state"] = "claimable" },
                new JObject { ["source"] = "node:item4", ["name"] = "[Item_04]", ["state"] = instance4State });
            if (!omitThirdStateRepresentative)
            {
                instances.Insert(2, new JObject
                {
                    ["source"] = "node:item3", ["name"] = "[Item_03]", ["state"] = "locked",
                });
            }

            plan["variantComponentExtractions"] = new JArray(new JObject
            {
                ["id"] = "inventory_item",
                ["template"] = "node:item1",
                ["assetPath"] = ComponentPath,
                ["commonName"] = "[Common]",
                ["statesName"] = "[States]",
                ["defaultState"] = "in_progress",
                ["states"] = new JArray(
                    new JObject { ["id"] = "in_progress", ["source"] = labelStateSource, ["name"] = "[State_InProgress]" },
                    new JObject { ["id"] = "claimable", ["source"] = "node:item2", ["name"] = "[State_Claimable]" },
                    new JObject { ["id"] = "locked", ["source"] = "node:item3", ["name"] = "[State_Locked]" }),
                ["instances"] = instances,
            });
            return plan;
        }

        [Test]
        public async Task VariantExtractionKeepsOneInstancePerRowWithItsOwnActiveState()
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
                Assert.That(list.childCount, Is.EqualTo(4), "每一行都必须保留自己的实例。");

                string[] names = { "[Item_01]", "[Item_02]", "[Item_03]", "[Item_04]" };
                string[] expectedStates = { "[State_InProgress]", "[State_Claimable]", "[State_Locked]", "[State_Claimable]" };
                string[] expectedLabels = { "InProgress", "Claimable", "Locked", "Claimable" };
                string[] expectedValues = { "1", "2", "3", "4" };

                for (int index = 0; index < names.Length; index++)
                {
                    Transform instance = list.GetChild(index);
                    Assert.That(instance.name, Is.EqualTo(names[index]), "列表位置与名称必须保持。");
                    Assert.That(
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject),
                        Is.EqualTo(ComponentPath));

                    Transform common = instance.Find("[Common]");
                    Assert.That(common, Is.Not.Null);
                    Assert.That(common.childCount, Is.EqualTo(0), "[Common] 在未证明公共成员时保持为空。");

                    Transform states = instance.Find("[States]");
                    Assert.That(states, Is.Not.Null);
                    Assert.That(states.childCount, Is.EqualTo(3));

                    int activeCount = 0;
                    Transform active = null;
                    for (int branchIndex = 0; branchIndex < states.childCount; branchIndex++)
                    {
                        if (states.GetChild(branchIndex).gameObject.activeSelf)
                        {
                            activeCount++;
                            active = states.GetChild(branchIndex);
                        }
                    }

                    Assert.That(activeCount, Is.EqualTo(1), "每个实例只能激活一个状态分支。");
                    Assert.That(active.name, Is.EqualTo(expectedStates[index]));
                    Assert.That(active.GetChild(0).GetComponent<Text>().text, Is.EqualTo(expectedLabels[index]));
                    Assert.That(active.GetChild(1).GetComponent<Text>().text, Is.EqualTo(expectedValues[index]));
                }

                // 第 4 行复用 claimable 状态，但自己的图标/数值差异必须保留。
                Transform fourth = list.GetChild(3).Find("[States]/[State_Claimable]");
                Assert.That(fourth.GetComponent<Image>().color, Is.EqualTo(new Color(1f, 1f, 0f, 1f)));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task MandatoryCandidateAcceptsReviewedVariantInsteadOfRecommendation()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            JObject snapshot = JObject.Parse(context.hierarchySnapshotJson);
            snapshot["componentFamilyCandidates"] = new JArray(new JObject
            {
                ["id"] = "inventory_family",
                ["requiresExtraction"] = true,
                ["recommendedMode"] = "component",
                ["parent"] = "node:list",
                ["sources"] = new JArray("node:item1", "node:item2", "node:item3", "node:item4"),
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
                ["sources"] = new JArray("node:item1", "node:item2", "node:item3", "node:item4"),
                ["mode"] = "variant",
                ["extractionId"] = "inventory_item",
            });

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.True, result.message);
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task UnknownInstanceStateIsRejectedBeforeAnyWrite()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(instance4State: "missing").ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("unknown state"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task StateSourceOutsideTheTemplateParentIsRejected()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(labelStateSource: "node:label1").ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("direct sibling"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task StateRepresentativeMissingFromInstancesIsRejected()
        {
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(
                context, Plan(omitThirdStateRepresentative: true).ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("state representative"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task InstanceStructureMismatchIsRejectedBeforeAnyWrite()
        {
            AssetDatabase.DeleteAsset(Target);
            BuildFixture(item4ExtraChild: true);
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("structure differs"));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task ExistingVariantComponentAssetIsRejected()
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
