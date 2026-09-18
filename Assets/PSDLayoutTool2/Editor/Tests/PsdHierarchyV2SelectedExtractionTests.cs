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
    /// 04a：局部选区抽取（selectedPrefabExtractions），只由已锁定的局部修复生成。
    /// 选中范围内的若干同级节点折叠成一个 Nested Prefab，范围外节点保持不变。
    /// </summary>
    public sealed class PsdHierarchyV2SelectedExtractionTests
    {
        private const string Folder = "Assets/__PsdV2SelectedExtractionTests";
        private const string Target = Folder + "/ExampleView.prefab";
        private const string ComponentPath = Folder + "/DaySignCard.prefab";
        private PsdHierarchyChatContext context;
        private PsdHierarchyLocalRepairScope scope;
        private bool ownsFolder;

        private static readonly string[] SelectedPaths =
        {
            "ExampleView/GiftBox4",
            "ExampleView/DateText4",
            "ExampleView/DateMarker5",
        };

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2SelectedExtractionTests");
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
            var root = new GameObject("ExampleView", typeof(RectTransform));
            try
            {
                CreateNode(root.transform, "KeepMe", new Vector2(-200f, 60f), 1);
                Transform gift = CreateNode(root.transform, "GiftBox4", new Vector2(0f, -42f), 2);
                Transform date = CreateNode(root.transform, "DateText4", new Vector2(0f, 2f), 3);
                CreateNode(root.transform, "DateMarker5", new Vector2(0f, 0f), 4);

                if (withExternalReference)
                {
                    var binder = root.AddComponent<ScrollRect>();
                    binder.enabled = false;
                    binder.content = (RectTransform)gift.GetChild(0);
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RefreshContext();
        }

        private static Transform CreateNode(Transform parent, string name, Vector2 position, int colorIndex)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(80f, 40f);
            node.GetComponent<Image>().color = new Color(colorIndex * 0.1f, 0.5f, 0.5f, 1f);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(node.transform, false);
            ((RectTransform)icon.transform).sizeDelta = new Vector2(16f, 16f);
            return node.transform;
        }

        private void RefreshContext()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            context = new PsdHierarchyChatContext(project, "", Target, "", "", "",
                hierarchySnapshotJson: new JObject
                {
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "root", ["path"] = "ExampleView" },
                        new JObject { ["id"] = "keep", ["path"] = "ExampleView/KeepMe", ["parentId"] = "root" },
                        new JObject { ["id"] = "gift", ["path"] = "ExampleView/GiftBox4", ["parentId"] = "root" },
                        new JObject { ["id"] = "date", ["path"] = "ExampleView/DateText4", ["parentId"] = "root" },
                        new JObject { ["id"] = "marker", ["path"] = "ExampleView/DateMarker5", ["parentId"] = "root" }),
                }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));

            Assert.That(
                PsdHierarchyLocalRepairScope.TryCreateFromSelectedPaths(
                    context,
                    SelectedPaths,
                    PsdHierarchyLocalRepairScopeMode.SelectedNodes,
                    out scope,
                    out string scopeError),
                Is.True,
                scopeError);
            context.localRepairScope = scope;
        }

        private JObject Plan(string[] sources = null, string assetPath = null, string[] extraSources = null)
        {
            sources = sources ?? new[] { "node:gift", "node:date", "node:marker" };
            var flags = new JArray(sources);
            if (extraSources != null)
            {
                foreach (string extra in extraSources)
                {
                    flags.Add(extra);
                }
            }

            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "ExampleView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["selectedPrefabExtractions"] = new JArray(new JObject
                {
                    ["id"] = "day_sign_card",
                    ["name"] = "DaySignCard",
                    ["assetPath"] = assetPath ?? ComponentPath,
                    ["parent"] = "node:root",
                    ["sources"] = flags,
                }),
            };
            foreach (string key in new[]
                     {
                         "wrappers", "moves", "renames", "tightBounds", "emptyContainerRemovals",
                         "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions",
                         "containmentResolutions", "flatSiblingResolutions", "componentExtractions",
                         "stateComponentExtractions", "variantComponentExtractions",
                         "statefulComponentExtractions", "crossParentPrefabExtractions",
                         "postGroupingExtractionIntents",
                     })
            {
                plan[key] = new JArray();
            }

            return plan;
        }

        [Test]
        public async Task SelectedExtractionCollapsesTheLockedSelectionAndLeavesTheRestUntouched()
        {
            JObject plan = Plan();
            byte[] before = File.ReadAllBytes(Target);

            var preflight = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(preflight.success, Is.True, preflight.message);
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before), "预检不得改动目标 Prefab。");
            Assert.That(File.Exists(ComponentPath), Is.False, "预检不得生成声明的抽取资产。");

            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());
            Assert.That(result.success, Is.True, result.message);

            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                // 范围内：三个同级节点折叠成一个实例；范围外：KeepMe 原样保留。
                Assert.That(root.transform.childCount, Is.EqualTo(2));
                Transform keep = root.transform.GetChild(0);
                Assert.That(keep.name, Is.EqualTo("KeepMe"));
                Assert.That(keep.childCount, Is.EqualTo(1));
                Assert.That(((RectTransform)keep).anchoredPosition, Is.EqualTo(new Vector2(-200f, 60f)));

                Transform instance = root.transform.GetChild(1);
                Assert.That(instance.name, Is.EqualTo("DaySignCard"));
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject),
                    Is.EqualTo(ComponentPath));
                Assert.That(instance.childCount, Is.EqualTo(3));
                Assert.That(instance.GetChild(0).name, Is.EqualTo("GiftBox4"));
                Assert.That(instance.GetChild(1).name, Is.EqualTo("DateText4"));
                Assert.That(instance.GetChild(2).name, Is.EqualTo("DateMarker5"));
                Assert.That(instance.GetChild(0).childCount, Is.EqualTo(1), "被抽取节点的子节点必须完整保留。");
                Assert.That(instance.GetChild(0).GetComponent<Image>().color, Is.EqualTo(new Color(0.2f, 0.5f, 0.5f, 1f)));

                // 折叠容器必须覆盖被抽取节点的实际范围。
                var instanceRect = (RectTransform)instance;
                var bounds = new Bounds();
                bool initialized = false;
                for (int index = 0; index < instanceRect.childCount; index++)
                {
                    var child = (RectTransform)instanceRect.GetChild(index);
                    var corners = new Vector3[4];
                    child.GetWorldCorners(corners);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector3 point = ((RectTransform)root.transform).InverseTransformPoint(corners[corner]);
                        if (!initialized)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(point);
                        }
                    }
                }

                Assert.That(instanceRect.sizeDelta.x, Is.EqualTo(bounds.size.x).Within(0.5f));
                Assert.That(instanceRect.sizeDelta.y, Is.EqualTo(bounds.size.y).Within(0.5f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task FailureAfterExtractionWriteReportsPartialAndPreservesTheOriginalTarget()
        {
            JObject plan = Plan();
            plan["verify"] = new JObject
            {
                ["hierarchy"] = new JArray(new JObject
                {
                    ["path"] = "ExampleView/GiftBox4",
                    ["childCount"] = 1,
                }),
            };
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyNativeCleanupExecutor.ApplyAsync(context, plan.ToString());

            Assert.That(result.success, Is.False, result.message);
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Partial), result.message);
            Assert.That(result.stage, Is.EqualTo("assets"));
            Assert.That(File.Exists(ComponentPath), Is.True, "The extraction asset was already written.");
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before), "The target was not saved yet.");
        }

        [Test]
        public async Task SourcesOutsideTheLockedSelectionAreRejectedBeforeAnyWrite()
        {
            AssetDatabase.DeleteAsset(Target);
            BuildFixture(withExternalReference: false);
            // 计划里额外加入一个没有锁定的节点。
            JObject plan = Plan(extraSources: new[] { "node:keep" });
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task DuplicateSelectedSourcesAreRejectedBeforeAnyWrite()
        {
            JObject plan = Plan(sources: new[] { "node:gift", "node:date", "node:date" });

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public async Task ExternalReferenceIntoTheSelectionIsRejectedBeforeAnyWrite()
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
        public async Task ExistingSelectedComponentAssetIsRejected()
        {
            var existing = new GameObject("DaySignCard", typeof(RectTransform));
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

        [Test]
        public async Task ContextFreeSelectedExtractionIsRejectedWithoutWrites()
        {
            var plainContext = new PsdHierarchyChatContext(
                context.projectRoot, "", Target, "", "", "",
                hierarchySnapshotJson: context.hierarchySnapshotJson,
                hierarchySnapshotFingerprint: context.hierarchySnapshotFingerprint);
            byte[] before = File.ReadAllBytes(Target);

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(plainContext, Plan().ToString());

            Assert.That(result.success, Is.False);
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }
    }
}
