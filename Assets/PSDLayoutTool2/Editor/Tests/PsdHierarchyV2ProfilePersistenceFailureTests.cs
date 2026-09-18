namespace PsdLayoutTool2.Tests
{
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// A16：Apply 已经改动业务 Prefab，但 v2 Replay Profile 持久化失败时，
    /// 必须同时报告“资源已变化”和“记录失败”，且不得再次 Apply、不得假报完整成功。
    /// </summary>
    public sealed class PsdHierarchyV2ProfilePersistenceFailureTests
    {
        private const string Folder = "Assets/__PsdV2ProfileFailureTests";
        private const string Target = Folder + "/ProfileFailureView.prefab";
        private PsdHierarchyChatContext context;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdV2ProfileFailureTests");
            BuildPrefab();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    Target,
                    out string snapshotJson,
                    out string fingerprint,
                    out string snapshotError),
                Is.True,
                snapshotError);
            // 源 PSD 路径留空：Profile 持久化必然失败，用来验证“资源已改但记录失败”的回执语义。
            context = new PsdHierarchyChatContext(
                projectRoot,
                string.Empty,
                Target,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                snapshotJson,
                fingerprint);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [Test]
        public void MissingReplayBindingReportsPartialBeforeReplacingAnyProfile()
        {
            JObject plan = BuildPlan();
            plan["moves"][0]["source"] = "node:missing";
            MethodInfo persist = typeof(PsdHierarchyChatCleanupExecution).GetMethod(
                "PersistCompletedReplayStage", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(persist, Is.Not.Null);

            var result = (PsdHierarchyChatCleanupExecutionResult)persist.Invoke(null, new object[]
            {
                context, plan.ToString(),
                new PsdHierarchyChatCleanupExecutionResult(PsdHierarchyCleanupExecutionState.Success, "verify", "VERIFY_OK"),
                true,
            });

            Assert.That(result.success, Is.False);
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Partial));
            Assert.That(result.stage, Is.EqualTo("replay-binding"));
            Assert.That(result.message, Does.Contain("Prefab 已更新"));
            Assert.That(result.message, Does.Contain("missing"));
        }

        [Test]
        public async Task AppliedPrefabWithFailedProfilePersistenceReportsBothStatesAndDoesNotReapply()
        {
            JObject plan = BuildPlan();
            byte[] before = File.ReadAllBytes(ToFullPath(Target));

            PsdHierarchyChatCleanupExecutionResult result =
                await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(
                    context,
                    plan.ToString(Newtonsoft.Json.Formatting.None));

            Assert.That(result.success, Is.False, result.message);
            Assert.That(result.state, Is.EqualTo(PsdHierarchyCleanupExecutionState.Partial));
            Assert.That(result.stage, Is.EqualTo("replay-profile"));
            Assert.That(result.message, Does.Contain("Prefab 已更新"));
            Assert.That(result.message, Does.Contain("整理重放 Profile 保存失败"));

            byte[] after = File.ReadAllBytes(ToFullPath(Target));
            Assert.That(after, Is.Not.EqualTo(before), "业务 Prefab 确实已经被改动。");

            // 记录失败不回滚、也不自动重放：同一份计划再次调用必须重新走完整校验，
            // 且不会因为上一次的 Profile 失败而跳过或重复写入。
            PsdHierarchyChatCleanupExecutionResult again =
                await PsdHierarchyChatCleanupExecution.ApplyConfirmedAsync(
                    context,
                    plan.ToString(Newtonsoft.Json.Formatting.None));
            Assert.That(again.success, Is.False, "文件内容已变化后，旧计划不能再通过快照校验。");
            Assert.That(again.message, Does.Contain("快照"));
            Assert.That(File.ReadAllBytes(ToFullPath(Target)), Is.EqualTo(after), "失败的计划不得再次写入。");
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("ProfileFailureView", typeof(RectTransform));
            try
            {
                for (int index = 1; index <= 2; index++)
                {
                    var row = new GameObject("TaskItem_" + index, typeof(RectTransform), typeof(Text));
                    row.transform.SetParent(root.transform, false);
                    Text text = row.GetComponent<Text>();
                    text.text = "Row " + index;
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    var detail = new GameObject(index == 1 ? "Badge" : "Score", typeof(RectTransform));
                    detail.transform.SetParent(row.transform, false);
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, Target), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private JObject BuildPlan()
        {
            var snapshot = JObject.Parse(context.hierarchySnapshotJson);
            return new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = context.hierarchySnapshotFingerprint,
                ["prefabName"] = "ProfileFailureView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["wrappers"] = new JArray(new JObject
                {
                    ["id"] = "rows",
                    ["parent"] = "node:" + ReadNodeId(snapshot, "ProfileFailureView"),
                    ["name"] = "[Rows]",
                    ["siblingIndex"] = 0,
                }),
                ["moves"] = new JArray(
                    new JObject
                    {
                        ["source"] = "node:" + ReadNodeId(snapshot, "ProfileFailureView/TaskItem_1"),
                        ["destination"] = "@rows",
                        ["siblingIndex"] = 0,
                    },
                    new JObject
                    {
                        ["source"] = "node:" + ReadNodeId(snapshot, "ProfileFailureView/TaskItem_2"),
                        ["destination"] = "@rows",
                        ["siblingIndex"] = 1,
                    }),
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

        private static string ReadNodeId(JObject snapshot, string path)
        {
            foreach (JToken token in (JArray)snapshot["nodes"])
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
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
