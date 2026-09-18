namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 07：v2 保存与重放。v2 快照的节点 ID 是位置编号，PSD 更新后的重放必须先在重新生成的
    /// 结果上证明每个被引用节点的唯一对应关系，再通过共享核心执行；否则停止并要求重新分析。
    /// </summary>
    public sealed class PsdHierarchyV2ReplayTests
    {
        private const string SourceGuid = "0123456789abcdef0123456789abcdef";
        private const string Folder = "Assets/__PsdV2ReplayTests";
        private const string Target = Folder + "/ReplayView.prefab";
        private const string RegeneratedFolder = Folder + "/Regenerated";
        private const string Regenerated = RegeneratedFolder + "/ReplayView.prefab";
        private PsdHierarchyCleanupReplayProfile profile;
        private string targetRootId;
        private string[] targetRowIds;
        private string targetSnapshotJson;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2ReplayTests");
            AssetDatabase.CreateFolder(Folder, "Regenerated");
            BuildPrefab(Target, extraHeaderChild: false, renamedRow: null);
            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    Target,
                    out targetSnapshotJson,
                    out _,
                    out string snapshotError),
                Is.True,
                snapshotError);
            targetRootId = ReadNodeId(targetSnapshotJson, "ReplayView");
            targetRowIds = new[]
            {
                ReadNodeId(targetSnapshotJson, "ReplayView/TaskItem_1"),
                ReadNodeId(targetSnapshotJson, "ReplayView/TaskItem_2"),
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (profile != null)
            {
                Object.DestroyImmediate(profile);
                profile = null;
            }

            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public async Task ReplayRebindsNodesByEvidenceAndAppliesTheStoredStageToTheRegeneratedPrefab()
        {
            JObject plan = CreatePlan();
            profile = CreateProfile(plan.ToString(Newtonsoft.Json.Formatting.None));
            BuildPrefab(Regenerated, extraHeaderChild: true, renamedRow: null);

            bool built = profile.TryBuildReplayPlans(
                SourceGuid,
                Target,
                Regenerated,
                out IReadOnlyList<string> stages,
                out string error);

            Assert.That(built, Is.True, error);
            Assert.That(stages.Count, Is.EqualTo(1));

            // 重新生成的树在 Header 下多了一个子节点：被引用节点的位置编号整体后移，
            // 重放计划必须使用新快照的编号，而不是沿用旧快照的编号。
            var replayPlan = JObject.Parse(stages[0]);
            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    Regenerated,
                    out string freshSnapshotJson,
                    out string freshFingerprint,
                    out string snapshotError),
                Is.True,
                snapshotError);
            string reboundRow = replayPlan["moves"][0]["source"].Value<string>();
            Assert.That(
                reboundRow,
                Is.Not.EqualTo("node:" + targetRowIds[0]),
                "重放必须按重新生成结果的编号重写被引用节点。");
            Assert.That(
                reboundRow,
                Is.EqualTo("node:" + ReadNodeId(freshSnapshotJson, "ReplayView/TaskItem_1")));
            Assert.That(
                replayPlan["wrappers"][0]["parent"].Value<string>(),
                Is.EqualTo("node:" + ReadNodeId(freshSnapshotJson, "ReplayView")));
            Assert.That(replayPlan.Value<string>("snapshotFingerprint"), Is.EqualTo(freshFingerprint));
            Assert.That(
                replayPlan["output"].Value<string>("assetPath"),
                Is.EqualTo(Regenerated));

            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            var result = await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                projectRoot,
                stages[0]);

            Assert.That(result.success, Is.True, result.message);

            GameObject root = PrefabUtility.LoadPrefabContents(Regenerated);
            try
            {
                Assert.That(root.transform.childCount, Is.EqualTo(2));
                Assert.That(root.transform.GetChild(0).name, Is.EqualTo("Header"));
                Assert.That(root.transform.GetChild(0).childCount, Is.EqualTo(1), "PSD 新增内容必须保留。");
                Assert.That(root.transform.GetChild(0).GetChild(0).name, Is.EqualTo("Subtitle"));

                Transform rows = root.transform.GetChild(1);
                Assert.That(rows.name, Is.EqualTo("[Rows]"));
                Assert.That(rows.childCount, Is.EqualTo(2));
                Assert.That(rows.GetChild(0).name, Is.EqualTo("TaskItem_1"));
                Assert.That(rows.GetChild(1).name, Is.EqualTo("TaskItem_2"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public async Task MultiStageReplayBuildsEachStageFromThePrefabSavedByThePreviousStage()
        {
            JObject firstPlan = CreatePlan();
            Assert.That(
                PsdHierarchyReplayBinding.TryBuildForPlan(
                    firstPlan,
                    targetSnapshotJson,
                    out string firstBinding,
                    out string firstBindingError),
                Is.True,
                firstBindingError);

            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            PsdHierarchyChatCleanupExecutionResult recordedFirstStage =
                await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                    projectRoot,
                    firstPlan.ToString(Newtonsoft.Json.Formatting.None));
            Assert.That(recordedFirstStage.success, Is.True, recordedFirstStage.message);
            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    Target,
                    out string afterFirstStageSnapshot,
                    out _,
                    out string afterFirstStageSnapshotError),
                Is.True,
                afterFirstStageSnapshotError);

            string recordedRowsId = ReadNodeId(afterFirstStageSnapshot, "ReplayView/[Rows]");
            var secondPlan = CreateEmptyPlan(afterFirstStageSnapshot);
            secondPlan["renames"] = new JArray(new JObject
            {
                ["target"] = "node:" + recordedRowsId,
                ["name"] = "[Tasks]",
            });
            Assert.That(
                PsdHierarchyReplayBinding.TryBuildForPlan(
                    secondPlan,
                    afterFirstStageSnapshot,
                    out string secondBinding,
                    out string secondBindingError),
                Is.True,
                secondBindingError);

            profile = ScriptableObject.CreateInstance<PsdHierarchyCleanupReplayProfile>();
            profile.Initialize(
                SourceGuid,
                Target,
                firstPlan.ToString(Newtonsoft.Json.Formatting.None),
                firstBinding);
            profile.AppendStage(
                SourceGuid,
                Target,
                secondPlan.ToString(Newtonsoft.Json.Formatting.None),
                secondBinding);
            BuildPrefab(Regenerated, extraHeaderChild: true, renamedRow: null);

            Assert.That(
                profile.TryBuildReplayStage(
                    SourceGuid,
                    Target,
                    Regenerated,
                    requireCurrentTargetGuid: true,
                    stageIndex: 0,
                    out string reboundFirstStage,
                    out string firstStageError),
                Is.True,
                firstStageError);
            PsdHierarchyChatCleanupExecutionResult firstReplay =
                await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                    projectRoot,
                    reboundFirstStage);
            Assert.That(firstReplay.success, Is.True, firstReplay.message);

            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    Regenerated,
                    out string beforeSecondStageSnapshot,
                    out string beforeSecondStageFingerprint,
                    out string beforeSecondStageSnapshotError),
                Is.True,
                beforeSecondStageSnapshotError);
            Assert.That(ReadNodeId(beforeSecondStageSnapshot, "ReplayView/[Rows]"), Is.Not.Empty);

            Assert.That(
                profile.TryBuildReplayStage(
                    SourceGuid,
                    Target,
                    Regenerated,
                    requireCurrentTargetGuid: true,
                    stageIndex: 1,
                    out string reboundSecondStage,
                    out string secondStageError),
                Is.True,
                secondStageError);
            JObject reboundSecondPlan = JObject.Parse(reboundSecondStage);
            Assert.That(
                reboundSecondPlan.Value<string>("snapshotFingerprint"),
                Is.EqualTo(beforeSecondStageFingerprint));
            Assert.That(
                reboundSecondPlan["renames"][0].Value<string>("target"),
                Is.EqualTo("node:" + ReadNodeId(beforeSecondStageSnapshot, "ReplayView/[Rows]")));
        }

        [Test]
        public void UnprovableNodeCorrespondenceStopsReplayAndKeepsTheGeneratedResultUntouched()
        {
            JObject plan = CreatePlan();
            profile = CreateProfile(plan.ToString(Newtonsoft.Json.Formatting.None));
            BuildPrefab(Regenerated, extraHeaderChild: false, renamedRow: "TaskItem_Two");
            byte[] before = System.IO.File.ReadAllBytes(ToFullPath(Regenerated));

            bool built = profile.TryBuildReplayPlans(
                SourceGuid,
                Target,
                Regenerated,
                out IReadOnlyList<string> stages,
                out string error);

            Assert.That(built, Is.False);
            Assert.That(stages, Is.Empty);
            Assert.That(
                error,
                Does.StartWith(PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage));
            Assert.That(error, Does.Contain("ReplayView/TaskItem_2"), error);
            Assert.That(
                System.IO.File.ReadAllBytes(ToFullPath(Regenerated)),
                Is.EqualTo(before),
                "对应关系无法证明时不得写入生成结果。");
        }

        [Test]
        public async Task VersionOneStageIsRefusedByTheSharedCoreReplayPath()
        {
            var legacy = new JObject
            {
                ["version"] = 1,
                ["prefabAssetPath"] = Regenerated,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Regenerated },
                ["wrapperCreations"] = new JArray(),
                ["nodeTransfers"] = new JArray(),
                ["nodeRenames"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["verify"] = new JObject(),
            };

            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            var result = await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                projectRoot,
                legacy.ToString(Newtonsoft.Json.Formatting.None));

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("重新分析"));
            Assert.That(
                PsdHierarchyCleanupReplayCoordinator.IsPermanentReplayFailure(result.message),
                Is.True);
        }

        [Test]
        public void VersionTwoStageWithoutNodeEvidenceCannotReplay()
        {
            JObject plan = CreatePlan();
            profile = CreateProfile(plan.ToString(Newtonsoft.Json.Formatting.None), bindingJson: string.Empty);
            BuildPrefab(Regenerated, extraHeaderChild: true, renamedRow: null);

            Assert.That(
                profile.TryBuildReplayPlans(
                    SourceGuid,
                    Target,
                    Regenerated,
                    out IReadOnlyList<string> stages,
                    out string error),
                Is.False);
            Assert.That(stages, Is.Empty);
            Assert.That(
                error,
                Does.StartWith(PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage));
        }

        [Test]
        public void BindingEvidenceRoundTripsThroughTheProfile()
        {
            JObject plan = CreatePlan();
            Assert.That(
                PsdHierarchyReplayBinding.TryBuildForPlan(
                    plan,
                    targetSnapshotJson,
                    out string bindingJson,
                    out string error),
                Is.True,
                error);

            var binding = JObject.Parse(bindingJson);
            Assert.That(binding.Value<int?>("schemaVersion"), Is.EqualTo(1));
            var nodes = (JObject)binding["nodes"];
            Assert.That(nodes.Count, Is.EqualTo(3), "只记录计划真正引用的节点。");
            foreach (string field in new[] { "path", "name", "siblingIndex", "components" })
            {
                Assert.That(
                    ((JObject)nodes[targetRootId])[field],
                    Is.Not.Null,
                    "绑定证据必须包含身份字段 " + field + "。");
            }
        }

        private JObject CreatePlan()
        {
            return new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] =
                    JObject.Parse(targetSnapshotJson).Value<string>("fingerprint"),
                ["prefabName"] = "ReplayView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["wrappers"] = new JArray(new JObject
                {
                    ["id"] = "rows",
                    ["parent"] = "node:" + targetRootId,
                    ["name"] = "[Rows]",
                    ["siblingIndex"] = 1,
                }),
                ["moves"] = new JArray(
                    new JObject { ["source"] = "node:" + targetRowIds[0], ["destination"] = "@rows", ["siblingIndex"] = 0 },
                    new JObject { ["source"] = "node:" + targetRowIds[1], ["destination"] = "@rows", ["siblingIndex"] = 1 }),
                ["renames"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["textureRenames"] = new JArray(),
                ["spriteAtlasRenames"] = new JArray(),
                ["componentFamilyDecisions"] = new JArray(),
                ["containmentResolutions"] = new JArray(),
                ["flatSiblingResolutions"] = new JArray(),
                ["componentExtractions"] = new JArray(),
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
                ["postGroupingExtractionIntents"] = new JArray(),
            };
        }

        private static JObject CreateEmptyPlan(string snapshotJson)
        {
            return new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = JObject.Parse(snapshotJson).Value<string>("fingerprint"),
                ["prefabName"] = "ReplayView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["wrappers"] = new JArray(),
                ["moves"] = new JArray(),
                ["renames"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["textureRenames"] = new JArray(),
                ["spriteAtlasRenames"] = new JArray(),
                ["componentFamilyDecisions"] = new JArray(),
                ["containmentResolutions"] = new JArray(),
                ["flatSiblingResolutions"] = new JArray(),
                ["componentExtractions"] = new JArray(),
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
                ["postGroupingExtractionIntents"] = new JArray(),
            };
        }

        private PsdHierarchyCleanupReplayProfile CreateProfile(string planJson, string bindingJson = null)
        {
            if (bindingJson == null)
            {
                Assert.That(
                    PsdHierarchyReplayBinding.TryBuildForPlan(
                        JObject.Parse(planJson),
                        targetSnapshotJson,
                        out bindingJson,
                        out string bindingError),
                    Is.True,
                    bindingError);
            }

            var created = ScriptableObject.CreateInstance<PsdHierarchyCleanupReplayProfile>();
            created.Initialize(SourceGuid, Target, planJson, bindingJson);
            return created;
        }

        private static void BuildPrefab(string assetPath, bool extraHeaderChild, string renamedRow)
        {
            var root = new GameObject("ReplayView", typeof(RectTransform));
            try
            {
                var header = new GameObject("Header", typeof(RectTransform), typeof(Text));
                header.transform.SetParent(root.transform, false);
                header.GetComponent<Text>().text = "Title";
                header.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                for (int index = 1; index <= 2; index++)
                {
                    string rowName = renamedRow != null && index == 2 ? renamedRow : "TaskItem_" + index;
                    var row = new GameObject(rowName, typeof(RectTransform), typeof(Text));
                    row.transform.SetParent(root.transform, false);
                    Text text = row.GetComponent<Text>();
                    text.text = "Row " + index;
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                    // 每行使用不同的子结构：避免被判定为强制抽取的重复组件家族。
                    var detail = new GameObject(index == 1 ? "Badge" : "Score", typeof(RectTransform));
                    detail.transform.SetParent(row.transform, false);
                }

                if (extraHeaderChild)
                {
                    var subtitle = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
                    subtitle.transform.SetParent(header.transform, false);
                    subtitle.GetComponent<Text>().text = "Subtitle";
                    subtitle.GetComponent<Text>().font =
                        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, assetPath), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static string ReadNodeId(string snapshotJson, string path)
        {
            foreach (JToken token in (JArray)JObject.Parse(snapshotJson)["nodes"])
            {
                if (token.Value<string>("path") == path)
                {
                    return token.Value<string>("id");
                }
            }

            Assert.Fail("快照中找不到节点路径：" + path);
            return string.Empty;
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            return System.IO.Path.Combine(
                projectRoot,
                assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }
    }
}
