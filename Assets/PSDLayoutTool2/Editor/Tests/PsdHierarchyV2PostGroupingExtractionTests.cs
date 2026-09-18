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
    /// 06：分组后抽取（postGroupingExtractionIntents）。
    /// 首阶段先整理层级，再刷新权威快照，把已审阅的后续意图解析成新指纹/新节点 ID 的第二阶段并执行。
    /// </summary>
    public sealed class PsdHierarchyV2PostGroupingExtractionTests
    {
        private const string Folder = "Assets/__PsdV2PostGroupingTests";
        private const string Target = Folder + "/TaskView.prefab";
        private const string ComponentPath = Folder + "/TaskItem.prefab";
        private PsdHierarchyChatContext context;
        private bool ownsFolder;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2PostGroupingTests");
            ownsFolder = true;
            BuildFixture();
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFolder)
            {
                PsdHierarchyCleanupReplayProfile.Remove(Target, Target);
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private void BuildFixture()
        {
            var root = new GameObject("TaskView", typeof(RectTransform));
            try
            {
                for (int index = 1; index <= 4; index++)
                {
                    var row = new GameObject("TaskItem_" + index, typeof(RectTransform));
                    row.transform.SetParent(root.transform, false);
                    ((RectTransform)row.transform).sizeDelta = new Vector2(120f, 40f);

                    var label = new GameObject("TaskLabel", typeof(RectTransform), typeof(Text));
                    label.transform.SetParent(row.transform, false);
                    Text text = label.GetComponent<Text>();
                    text.text = "Task " + index;
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                    var background = new GameObject("ProgressBackground", typeof(RectTransform), typeof(Image));
                    background.transform.SetParent(row.transform, false);
                    background.GetComponent<Image>().color = new Color(index * 0.1f, 0.4f, 0.4f, 1f);
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            RefreshContext();
        }

        private void RefreshContext()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            var nodes = new JArray(new JObject { ["id"] = "root", ["path"] = "TaskView" });
            for (int index = 1; index <= 4; index++)
            {
                nodes.Add(new JObject
                {
                    ["id"] = "row" + index,
                    ["path"] = "TaskView/TaskItem_" + index,
                    ["parentId"] = "root",
                    ["siblingIndex"] = index - 1,
                });
            }

            context = new PsdHierarchyChatContext(project, Target, Target, "", "", "",
                hierarchySnapshotJson: new JObject { ["nodes"] = nodes }.ToString(),
                hierarchySnapshotFingerprint: PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    Path.Combine(project, Target)));
        }

        private JObject Plan(string templatePath = "TaskView/[TaskList]/TaskItem_1", string mode = "component")
        {
            var instances = new JArray();
            for (int index = 1; index <= 4; index++)
            {
                instances.Add(new JObject
                {
                    ["path"] = "TaskView/[TaskList]/TaskItem_" + index,
                    ["state"] = string.Empty,
                    ["commonSourceNames"] = new JArray(),
                    ["stateSourceNames"] = new JArray(),
                });
            }

            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "TaskView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["wrappers"] = new JArray(new JObject
                {
                    ["id"] = "task_list",
                    ["parent"] = "node:root",
                    ["name"] = "[TaskList]",
                    ["siblingIndex"] = 0,
                }),
                ["moves"] = new JArray(
                    new JObject { ["source"] = "node:row1", ["destination"] = "@task_list", ["siblingIndex"] = 0 },
                    new JObject { ["source"] = "node:row2", ["destination"] = "@task_list", ["siblingIndex"] = 1 },
                    new JObject { ["source"] = "node:row3", ["destination"] = "@task_list", ["siblingIndex"] = 2 },
                    new JObject { ["source"] = "node:row4", ["destination"] = "@task_list", ["siblingIndex"] = 3 }),
                ["postGroupingExtractionIntents"] = new JArray(new JObject
                {
                    ["id"] = "task_item",
                    ["mode"] = mode,
                    ["assetPath"] = ComponentPath,
                    ["templatePath"] = templatePath,
                    ["commonMembers"] = new JArray(),
                    ["instances"] = instances,
                    ["states"] = new JArray(),
                    ["defaultState"] = string.Empty,
                }),
            };
            foreach (string key in new[]
                     {
                         "renames", "tightBounds", "emptyContainerRemovals", "textureRenames",
                         "spriteAtlasRenames", "componentFamilyDecisions", "containmentResolutions",
                         "flatSiblingResolutions", "componentExtractions", "stateComponentExtractions",
                         "variantComponentExtractions", "statefulComponentExtractions",
                         "selectedPrefabExtractions", "crossParentPrefabExtractions",
                     })
            {
                plan[key] = new JArray();
            }

            return plan;
        }

        [Test]
        public async Task HierarchyStageIsFollowedByExtractionOnTheRefreshedSnapshot()
        {
            JObject plan = Plan();
            byte[] before = File.ReadAllBytes(Target);

            var preflight = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());
            Assert.That(preflight.success, Is.True, preflight.message);
            Assert.That(File.Exists(ComponentPath), Is.False, "预检不得生成组件资产。");
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(before), "预检不得改动目标 Prefab。");

            var result = await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(context, plan.ToString());
            Assert.That(result.success, Is.True, result.message);

            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Transform list = root.transform.Find("[TaskList]");
                Assert.That(list, Is.Not.Null, "首阶段必须建立分组容器。");
                Assert.That(list.childCount, Is.EqualTo(4));

                for (int index = 0; index < 4; index++)
                {
                    Transform row = list.GetChild(index);
                    Assert.That(row.name, Is.EqualTo("TaskItem_" + (index + 1)));
                    Assert.That(
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(row.gameObject),
                        Is.EqualTo(ComponentPath),
                        "第二阶段必须把分组后的行替换成组件实例。");
                    Assert.That(row.Find("TaskLabel").GetComponent<Text>().text, Is.EqualTo("Task " + (index + 1)));
                    Assert.That(
                        row.Find("ProgressBackground").GetComponent<Image>().color,
                        Is.EqualTo(new Color((index + 1) * 0.1f, 0.4f, 0.4f, 1f)));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task UnresolvableIntentStopsAfterStageOneAndReportsPartial()
        {
            JObject plan = Plan(templatePath: "TaskView/[TaskList]/MissingRow");

            var result = await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Partial));
            Assert.That(result.stage, Is.EqualTo("post-grouping"));
            Assert.That(result.message, Does.Contain("refreshed snapshot"), result.message);
            Assert.That(File.Exists(ComponentPath), Is.False, "解析失败时不得生成组件资产。");

            // 首阶段已经写入并保存：分组容器必须在，且不被重跑或回滚。
            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Transform list = root.transform.Find("[TaskList]");
                Assert.That(list, Is.Not.Null);
                Assert.That(list.childCount, Is.EqualTo(4));
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(list.GetChild(0).gameObject),
                    Is.Empty);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task UnsupportedIntentModeIsRejectedBeforeAnyWrite()
        {
            JObject plan = Plan(mode: "unknown_mode");

            var result = await PsdHierarchyChatCleanupExecution.ValidatePlanAsync(context, plan.ToString());

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("mode"));
            Assert.That(File.Exists(ComponentPath), Is.False);
        }

        [Test]
        public void RefreshedMandatoryVariantCandidateBuildsVariantDecision()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            var refreshed = new PsdHierarchyChatContext(
                project,
                "",
                Target,
                "",
                "",
                "",
                hierarchySnapshotJson: new JObject
                {
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "root", ["path"] = "TaskView" },
                        new JObject { ["id"] = "list", ["path"] = "TaskView/[TaskList]" },
                        new JObject { ["id"] = "row1", ["path"] = "TaskView/[TaskList]/TaskItem_1" },
                        new JObject { ["id"] = "row2", ["path"] = "TaskView/[TaskList]/TaskItem_2" }),
                    ["componentFamilyCandidates"] = new JArray(new JObject
                    {
                        ["id"] = "task_family",
                        ["requiresExtraction"] = true,
                        ["recommendedMode"] = "variant",
                        ["parent"] = "node:list",
                        ["sources"] = new JArray("node:row1", "node:row2"),
                    }),
                }.ToString(),
                hierarchySnapshotFingerprint: "refreshed-fingerprint");
            var intents = new JArray(new JObject
            {
                ["id"] = "task_item",
                ["mode"] = "variant",
                ["assetPath"] = ComponentPath,
                ["templatePath"] = "TaskView/[TaskList]/TaskItem_1",
                ["commonMembers"] = new JArray(),
                ["states"] = new JArray(
                    new JObject
                    {
                        ["id"] = "normal", ["name"] = "[State_Normal]",
                        ["sourcePath"] = "TaskView/[TaskList]/TaskItem_1", ["members"] = new JArray(),
                    },
                    new JObject
                    {
                        ["id"] = "complete", ["name"] = "[State_Complete]",
                        ["sourcePath"] = "TaskView/[TaskList]/TaskItem_2", ["members"] = new JArray(),
                    }),
                ["defaultState"] = "normal",
                ["instances"] = new JArray(
                    new JObject
                    {
                        ["path"] = "TaskView/[TaskList]/TaskItem_1", ["state"] = "normal",
                        ["commonSourceNames"] = new JArray(), ["stateSourceNames"] = new JArray(),
                    },
                    new JObject
                    {
                        ["path"] = "TaskView/[TaskList]/TaskItem_2", ["state"] = "complete",
                        ["commonSourceNames"] = new JArray(), ["stateSourceNames"] = new JArray(),
                    }),
            });

            JObject stageTwo = PsdHierarchyPostGroupingExtraction.BuildStageTwoPlan(
                refreshed,
                PsdHierarchyPostGroupingExtraction.ValidateIntents(
                    new JObject { ["postGroupingExtractionIntents"] = intents }),
                Target,
                out string error);

            Assert.That(stageTwo, Is.Not.Null, error);
            Assert.That(stageTwo["componentFamilyDecisions"][0]["mode"].Value<string>(), Is.EqualTo("variant"));
            Assert.That(stageTwo["componentFamilyDecisions"][0]["extractionId"].Value<string>(),
                Is.EqualTo("task_item"));
            Assert.That(((JArray)stageTwo["variantComponentExtractions"]).Count, Is.EqualTo(1));
        }
    }
}
