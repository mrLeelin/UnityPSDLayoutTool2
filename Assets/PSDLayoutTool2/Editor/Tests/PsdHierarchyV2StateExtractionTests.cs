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
    /// 03a：状态抽取（stateComponentExtractions）。
    /// 同一视觉槽位上互斥的同级根折叠成一个 [States] 公共 Prefab，只保留默认状态激活。
    /// </summary>
    public sealed class PsdHierarchyV2StateExtractionTests
    {
        private const string Folder = "Assets/__PsdV2StateExtractionTests";
        private const string Target = Folder + "/InventoryPanelView.prefab";
        private const string ComponentPath = Folder + "/InventoryItem.prefab";
        private PsdHierarchyChatContext context;
        private bool ownsFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2StateExtractionTests");
            ownsFolder = true;
            BuildFixture(withExternalReference: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder)
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private void BuildFixture(bool withExternalReference)
        {
            var root = new GameObject("InventoryPanelView", typeof(RectTransform));
            try
            {
                var states = new GameObject("[ItemStates]", typeof(RectTransform));
                states.transform.SetParent(root.transform, false);

                CreateStateSource(states.transform, "Item_01", new Color(1f, 0f, 0f, 1f), "Locked", false);
                GameObject second = CreateStateSource(
                    states.transform, "Item_02", new Color(0f, 1f, 0f, 1f), "Available", false);
                CreateStateSource(states.transform, "Item_03", new Color(0f, 0f, 1f, 1f), "Done", true);

                if (withExternalReference)
                {
                    // 状态源被折叠成一个实例后，这种外部引用会变成歧义引用：必须在写入前拒绝。
                    var binder = root.AddComponent<ScrollRect>();
                    binder.enabled = false;
                    binder.content = (RectTransform)second.transform.GetChild(0);
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RefreshContext();
        }

        private static GameObject CreateStateSource(
            Transform parent,
            string name,
            Color color,
            string label,
            bool extraChild)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.anchoredPosition = new Vector2(name == "Item_02" ? 12f : 0f, 0f);
            rect.sizeDelta = new Vector2(96f, 48f);
            node.GetComponent<Image>().color = color;

            var text = new GameObject("Label", typeof(RectTransform), typeof(Text));
            text.transform.SetParent(node.transform, false);
            Text label2 = text.GetComponent<Text>();
            label2.text = label;
            label2.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (extraChild)
            {
                var tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
                tick.transform.SetParent(node.transform, false);
            }

            return node;
        }

        private void RefreshContext()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            context = new PsdHierarchyChatContext(project, "", Target, "", "", "",
                hierarchySnapshotJson: new JObject
                {
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "root", ["path"] = "InventoryPanelView" },
                        new JObject { ["id"] = "states", ["path"] = "InventoryPanelView/[ItemStates]" },
                        new JObject { ["id"] = "item1", ["path"] = "InventoryPanelView/[ItemStates]/Item_01" },
                        new JObject { ["id"] = "item2", ["path"] = "InventoryPanelView/[ItemStates]/Item_02" },
                        new JObject { ["id"] = "item3", ["path"] = "InventoryPanelView/[ItemStates]/Item_03" }),
                }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));
        }

        private JObject Plan(string[] stateSources = null, string defaultState = "available", string template = "node:item1")
        {
            stateSources = stateSources ?? new[] { "node:item1", "node:item2", "node:item3" };
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
                         "variantComponentExtractions", "statefulComponentExtractions", "postGroupingExtractionIntents",
                     })
            {
                plan[key] = new JArray();
            }

            plan["stateComponentExtractions"] = new JArray(new JObject
            {
                ["id"] = "inventory_item",
                ["template"] = template,
                ["assetPath"] = ComponentPath,
                ["defaultState"] = defaultState,
                ["states"] = new JArray(
                    new JObject { ["id"] = "locked", ["source"] = stateSources[0], ["name"] = "[Locked]" },
                    new JObject { ["id"] = "available", ["source"] = stateSources[1], ["name"] = "[Available]" },
                    new JObject { ["id"] = "completed", ["source"] = stateSources[2], ["name"] = "[Completed]" }),
            });
            return plan;
        }

        [Test]
        public async Task StateExtractionCollapsesSiblingStatesAndKeepsOnlyTheDefaultActive()
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
                Transform container = root.transform.Find("[ItemStates]");
                Assert.That(container, Is.Not.Null);
                Assert.That(container.childCount, Is.EqualTo(1), "三个互斥状态必须折叠成一个实例。");

                Transform instance = container.GetChild(0);
                Assert.That(instance.name, Is.EqualTo("Item_01"), "实例沿用模板节点名，路径保持稳定。");
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject),
                    Is.EqualTo(ComponentPath));
                Assert.That(((RectTransform)instance).sizeDelta, Is.EqualTo(new Vector2(96f, 48f)));

                Transform states = instance.Find("[States]");
                Assert.That(states, Is.Not.Null);
                Assert.That(states.childCount, Is.EqualTo(3));
                Assert.That(states.GetChild(0).name, Is.EqualTo("[Locked]"));
                Assert.That(states.GetChild(1).name, Is.EqualTo("[Available]"));
                Assert.That(states.GetChild(2).name, Is.EqualTo("[Completed]"));

                Assert.That(states.GetChild(0).gameObject.activeSelf, Is.False);
                Assert.That(states.GetChild(1).gameObject.activeSelf, Is.True, "defaultState=available 必须激活。");
                Assert.That(states.GetChild(2).gameObject.activeSelf, Is.False);

                // 每个分支保留自己来源的内容：颜色、文本、子节点数量。
                Assert.That(states.GetChild(0).GetComponent<Image>().color, Is.EqualTo(new Color(1f, 0f, 0f, 1f)));
                Assert.That(states.GetChild(0).GetChild(0).GetComponent<Text>().text, Is.EqualTo("Locked"));
                Assert.That(states.GetChild(1).GetComponent<Image>().color, Is.EqualTo(new Color(0f, 1f, 0f, 1f)));
                Assert.That(states.GetChild(1).GetChild(0).GetComponent<Text>().text, Is.EqualTo("Available"));
                Assert.That(states.GetChild(2).childCount, Is.EqualTo(2), "不同结构的状态分支必须原样保留。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task MandatoryStateCandidateAcceptsMatchingReviewedExtraction()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            JObject snapshot = JObject.Parse(context.hierarchySnapshotJson);
            snapshot["componentFamilyCandidates"] = new JArray(new JObject
            {
                ["id"] = "inventory_states",
                ["requiresExtraction"] = true,
                ["recommendedMode"] = "state",
                ["parent"] = "node:states",
                ["sources"] = new JArray("node:item1", "node:item2", "node:item3"),
            });
            context = new PsdHierarchyChatContext(
                project, "", Target, "", "", "",
                hierarchySnapshotJson: snapshot.ToString(),
                hierarchySnapshotFingerprint: context.hierarchySnapshotFingerprint);
            JObject plan = Plan();
            plan["componentFamilyDecisions"] = new JArray(new JObject
            {
                ["candidateId"] = "inventory_states",
                ["parent"] = "node:states",
                ["sources"] = new JArray("node:item1", "node:item2", "node:item3"),
                ["mode"] = "state",
                ["extractionId"] = "inventory_item",
            });

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.True, result.message);
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task ReusedStateSourceIsRejected()
        {
            JObject plan = Plan(stateSources: new[] { "node:item1", "node:item1", "node:item3" });
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task TemplateOutsideStatesIsRejected()
        {
            // template 指向一个不在 states[].source 里的兄弟节点。
            JObject plan = Plan(stateSources: new[] { "node:item2", "node:item2", "node:item3" });
            plan["stateComponentExtractions"][0]["states"] = new JArray(
                new JObject { ["id"] = "available", ["source"] = "node:item2", ["name"] = "[Available]" },
                new JObject { ["id"] = "completed", ["source"] = "node:item3", ["name"] = "[Completed]" });

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("template"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task UnknownDefaultStateIsRejected()
        {
            JObject plan = Plan(defaultState: "missing");
            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("defaultState"));
        }

        [Test]
        public async Task ExternalReferenceIntoStateSourcesIsRejectedBeforeAnyWrite()
        {
            AssetDatabase.DeleteAsset(Target);
            BuildFixture(withExternalReference: true);
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("referenced from outside"), result.message);
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task ExistingStateComponentAssetIsRejected()
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
