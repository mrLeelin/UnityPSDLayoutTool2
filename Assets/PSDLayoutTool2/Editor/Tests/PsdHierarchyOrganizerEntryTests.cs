namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 09：Inspector 保留的两个入口必须产出 v2 会话与自包含提示词，并真正连通 08 的哨兵/回执流程；
    /// 手动“应用AI计划”入口及其专属调用链不得存在。
    /// </summary>
    public sealed class PsdHierarchyOrganizerEntryTests
    {
        private const string Folder = "Assets/__PsdOrganizerEntryTests";
        private const string SourceAssetPath = Folder + "/Source.txt";
        private string terminalDirectory;
        private string sessionId;
        private string targetPrefabPath;
        private PsdImporter.OutputDirectoryMode originalOutputMode;
        private PsdImporter.PrefabOutputMode originalPrefabMode;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdOrganizerEntryTests");
            File.WriteAllText(ToFullPath(SourceAssetPath), "psd placeholder", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(SourceAssetPath, ImportAssetOptions.ForceSynchronousImport);

            // 让入口按项目设置解析目标：测试显式固定输出目录，再让解析器给出确切路径。
            originalOutputMode = PsdImporter.OutputMode;
            originalPrefabMode = PsdImporter.PrefabMode;
            PsdImporter.OutputMode = PsdImporter.OutputDirectoryMode.PsdDirectory;
            PsdImporter.PrefabMode = PsdImporter.PrefabOutputMode.SiblingToOutputFolder;
            Assert.That(
                PsdGeneratedPrefabPathResolver.TryResolve(
                    SourceAssetPath,
                    PsdImporter.OutputMode,
                    PsdImporter.OutputFolderName,
                    PsdImporter.FixedOutputPath,
                    PsdImporter.PrefabOutputPath,
                    PsdImporter.PrefabMode,
                    out targetPrefabPath),
                Is.True);
            EnsureAssetFolder(Path.GetDirectoryName(targetPrefabPath).Replace('\\', '/'));
            BuildPrefab(targetPrefabPath);

            terminalDirectory = PsdHierarchyTerminalApplyWatcher.TerminalDirectoryPath;
            Assert.That(terminalDirectory, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(terminalDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(sessionId) && Directory.Exists(terminalDirectory))
            {
                foreach (string file in Directory.GetFiles(terminalDirectory, sessionId + "*"))
                {
                    File.Delete(file);
                }
            }

            sessionId = null;
            // 复制提示词会写系统剪贴板；离开用例前清空，避免无头会话留下无法渲染的剪贴板数据。
            EditorGUIUtility.systemCopyBuffer = string.Empty;
            PsdImporter.OutputMode = originalOutputMode;
            PsdImporter.PrefabMode = originalPrefabMode;
            PsdHierarchyCleanupReplayProfile.Remove(SourceAssetPath, targetPrefabPath);
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [Test]
        public void RetainedAiEntriesAreExactlyTheTwoDocumentedLabels()
        {
            Assert.That(PsdHierarchyOrganizerEntry.AiButtonLabel, Is.EqualTo("AI整理"));
            Assert.That(PsdHierarchyOrganizerEntry.CopyPromptButtonLabel, Is.EqualTo("AI提示词复制"));

            Assert.That(typeof(PsdHierarchyOrganizerEntry).GetMethod("TryOpenChat"), Is.Not.Null);
            Assert.That(typeof(PsdHierarchyOrganizerEntry).GetMethod("TryCopyAiPrompt"), Is.Not.Null);
            foreach (System.Reflection.MethodInfo method in typeof(PsdHierarchyOrganizerEntry).GetMethods(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic |
                         System.Reflection.BindingFlags.Static |
                         System.Reflection.BindingFlags.Instance))
            {
                Assert.That(
                    method.Name.IndexOf("ApplyPlan", StringComparison.OrdinalIgnoreCase) < 0 &&
                    method.Name.IndexOf("ApplyAi", StringComparison.OrdinalIgnoreCase) < 0,
                    Is.True,
                    "不应存在手动应用入口：" + method.Name);
            }
        }

        [Test]
        public void TerminalSessionContractCoversReviewSubmitReceiptAndFailureStates()
        {
            string contract = PsdHierarchyOrganizerEntry.BuildTerminalSessionContract(
                "C:/Project/Library/PsdHierarchyTerminal/session.plan.json",
                "C:/Project/Library/PsdHierarchyTerminal/session.review.md",
                "C:/Project/Library/PsdHierarchyTerminal/session.apply",
                "C:/Project/Library/PsdHierarchyTerminal/session.apply-result.json");

            Assert.That(contract, Does.Contain("version 2"));
            Assert.That(contract, Does.Contain("session.plan.json"));
            Assert.That(contract, Does.Contain("session.review.md"));
            Assert.That(contract, Does.Contain("session.apply"));
            Assert.That(contract, Does.Contain("session.apply-result.json"));
            Assert.That(contract, Does.Contain("Do not write it before human approval"));
            Assert.That(contract, Does.Contain("status is one of applied, rejected, partial, uncertain"));
            Assert.That(
                contract,
                Does.Contain("JSON may be edited before approval"));
            Assert.That(contract, Does.Contain("If the plan or review is edited after approval"));
            Assert.That(contract, Does.Not.Contain("no extra human approval"));
            Assert.That(contract, Does.Not.Contain("Max 3 fix-and-reapply cycles"));
            Assert.That(contract, Does.Contain("partial or uncertain"));
            Assert.That(contract, Does.Contain("ask for a new review before any further apply"));
            Assert.That(contract, Does.Contain("postGroupingExtractionIntents"));
            Assert.That(contract, Does.Contain("recommendedMode is advisory only"));
            Assert.That(contract, Does.Contain("must match the actual extraction list or postGroupingExtractionIntents entry"));
            Assert.That(contract, Does.Contain("sources must fully cover the candidate"));
            Assert.That(contract, Does.Not.Contain("componentFamilyDecisions must use mode=component"));
            Assert.That(contract, Does.Contain("textureRenames"));
            // 旧写入脚本不得再被提示词要求执行。
        }

        [Test]
        public void FreshPrefabStartsFullReviewAndIncrementalPromptAsksBeforePlanning()
        {
            Assert.That(PsdHierarchyOrganizerEntry.TryResolveIncrementalReview(
                SourceAssetPath, targetPrefabPath, out bool incremental, out string error), Is.True, error);
            Assert.That(incremental, Is.False);

            var context = new PsdHierarchyChatContext(
                "C:/Project", SourceAssetPath, targetPrefabPath,
                "C:/Project/Assets/UnityPSDLayoutTool2/.agents/skills/prefab-hierarchy-cleanup/SKILL.md",
                string.Empty, string.Empty,
                hierarchySnapshotFullPath: "C:/Project/Library/current.snapshot.json");
            string prompt = PsdHierarchyOrganizerEntry.BuildIncrementalConversationPrompt(context);
            string contract = PsdHierarchyOrganizerEntry.BuildTerminalSessionContract(
                "plan.json", "review.md", "request.apply", "result.json",
                incrementalReview: true);

            Assert.That(prompt, Does.Contain("先只问用户"));
            Assert.That(prompt, Does.Contain("只由你一个主 Agent 完成"));
            Assert.That(prompt, Does.Contain("不要派生子 Agent"));
            Assert.That(prompt, Does.Contain("只包含本轮差异"));
            Assert.That(prompt, Does.Contain("operationScope.kind 必须是 incremental_adjustment"));
            Assert.That(contract, Does.Contain("First ask the user what to adjust"));
            Assert.That(contract, Does.Contain("Do not repeat the initial full cleanup"));
        }

        [Test]
        public void IncrementalPlanRequiresARequestedChangeAndRealOperations()
        {
            var plan = new JObject
            {
                ["operationScope"] = new JObject
                {
                    ["kind"] = "incremental_adjustment",
                    ["requestedChange"] = "Rename the selected diary icon",
                },
                ["selectionNodeIds"] = new JArray("node:n000001"),
                ["renames"] = new JArray(new JObject
                {
                    ["target"] = "node:n000001",
                    ["name"] = "DiaryIcon",
                }),
            };

            Assert.That(PsdHierarchyOrganizerEntry.TryValidateIncrementalPlan(
                plan.ToString(), out string validError), Is.True, validError);
            plan["selectionNodeIds"] = new JArray("node:n000002");
            Assert.That(PsdHierarchyOrganizerEntry.TryValidateIncrementalPlan(
                plan.ToString(), out string outsideScopeError), Is.False);
            Assert.That(outsideScopeError, Does.Contain("未列入 selectionNodeIds"));
            plan["selectionNodeIds"] = new JArray("node:n000001");
            plan["operationScope"]["requestedChange"] = string.Empty;
            Assert.That(PsdHierarchyOrganizerEntry.TryValidateIncrementalPlan(
                plan.ToString(), out _), Is.False);
            plan["operationScope"]["requestedChange"] = "Rename the selected diary icon";
            plan["renames"] = new JArray();
            Assert.That(PsdHierarchyOrganizerEntry.TryValidateIncrementalPlan(
                plan.ToString(), out _), Is.False);
        }

        [Test]
        public void ResumableLookupDoesNotMixFullAndIncrementalSessions()
        {
            string incrementalId = "incremental-" + Guid.NewGuid().ToString("N");
            string plan = Path.Combine(terminalDirectory, incrementalId + ".plan.json");
            string review = Path.Combine(terminalDirectory, incrementalId + ".review.md");
            File.WriteAllText(plan, "{}", new UTF8Encoding(false));
            File.WriteAllText(review, "saved review", new UTF8Encoding(false));
            PsdHierarchyOrganizerEntry.WriteTerminalSession(
                incrementalId, SourceAssetPath, targetPrefabPath, plan, review,
                "snapshot-a", incrementalReview: true);
            sessionId = incrementalId;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            Assert.That(PsdHierarchyOrganizerEntry.TryFindResumableTerminalSession(
                projectRoot, SourceAssetPath, targetPrefabPath,
                out string foundId, out _, out _, out _, out _, incrementalReview: true), Is.True);
            Assert.That(foundId, Is.EqualTo(incrementalId));
            Assert.That(PsdHierarchyOrganizerEntry.TryFindResumableTerminalSession(
                projectRoot, SourceAssetPath, targetPrefabPath,
                out _, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void ResumableSessionLookupRequiresTheSavedReviewAndPlan()
        {
            string resumableId = "resume-" + Guid.NewGuid().ToString("N");
            string plan = Path.Combine(terminalDirectory, resumableId + ".plan.json");
            string review = Path.Combine(terminalDirectory, resumableId + ".review.md");
            File.WriteAllText(plan, "{}", new UTF8Encoding(false));
            File.WriteAllText(review, "saved review", new UTF8Encoding(false));
            PsdHierarchyOrganizerEntry.WriteTerminalSession(
                resumableId, SourceAssetPath, targetPrefabPath, plan, review, "snapshot-a");
            sessionId = resumableId;

            Assert.That(PsdHierarchyOrganizerEntry.TryFindResumableTerminalSession(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")), SourceAssetPath, targetPrefabPath,
                out string foundId, out _, out string foundPlan, out string foundReview, out string foundFingerprint), Is.True);
            Assert.That(foundId, Is.EqualTo(resumableId));
            Assert.That(foundPlan, Is.EqualTo(plan));
            Assert.That(foundReview, Is.EqualTo(review));
            Assert.That(foundFingerprint, Is.EqualTo("snapshot-a"));
        }

        [Test]
        public void FindsLatestUnfinishedSessionForTheSamePrefab()
        {
            string resumableId = "resume-" + Guid.NewGuid().ToString("N");
            string plan = Path.Combine(terminalDirectory, resumableId + ".plan.json");
            string review = Path.Combine(terminalDirectory, resumableId + ".review.md");
            File.WriteAllText(plan, "{}", new UTF8Encoding(false));
            File.WriteAllText(review, "saved review", new UTF8Encoding(false));
            PsdHierarchyOrganizerEntry.WriteTerminalSession(
                resumableId,
                SourceAssetPath,
                targetPrefabPath,
                plan,
                review,
                "snapshot-a");
            sessionId = resumableId;

            Assert.That(
                PsdHierarchyOrganizerEntry.TryFindResumableTerminalSession(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    SourceAssetPath,
                    targetPrefabPath,
                    out string foundId,
                    out _, out string foundPlan, out string foundReview, out string foundFingerprint),
                Is.True);
            Assert.That(foundId, Is.EqualTo(resumableId));
            Assert.That(foundPlan, Is.EqualTo(plan));
            Assert.That(foundReview, Is.EqualTo(review));
            Assert.That(foundFingerprint, Is.EqualTo("snapshot-a"));
        }

        [Test]
        public void ApprovalOnlyRejectionKeepsTheExistingPlanResumable()
        {
            string resumableId = "approval-retry-" + Guid.NewGuid().ToString("N");
            string plan = Path.Combine(terminalDirectory, resumableId + ".plan.json");
            string review = Path.Combine(terminalDirectory, resumableId + ".review.md");
            File.WriteAllText(plan, "{}", new UTF8Encoding(false));
            File.WriteAllText(review, "saved review", new UTF8Encoding(false));
            PsdHierarchyOrganizerEntry.WriteTerminalSession(
                resumableId, SourceAssetPath, targetPrefabPath, plan, review, "snapshot-a");
            sessionId = resumableId;
            string applyPath = PsdHierarchyOrganizerEntry.BuildApplySentinelPath(plan);
            File.WriteAllText(
                applyPath + ".apply-failed",
                "{}",
                new UTF8Encoding(false));
            File.WriteAllText(
                PsdHierarchyTerminalApplyWatcher.BuildResultPath(applyPath),
                new JObject { ["status"] = "rejected", ["stage"] = "approval" }.ToString(),
                new UTF8Encoding(false));

            Assert.That(
                PsdHierarchyOrganizerEntry.TryFindResumableTerminalSession(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    SourceAssetPath,
                    targetPrefabPath,
                    out string foundId,
                    out _, out _, out _, out _),
                Is.True);
            Assert.That(foundId, Is.EqualTo(resumableId));
        }

        [Test]
        public async System.Threading.Tasks.Task CopiedPromptEntryProducesASessionWhoseSentinelIsAppliedAndReceipted()
        {
            Assert.That(
                PsdHierarchyOrganizerEntry.TryCopyAiPrompt(
                    SourceAssetPath,
                    out PsdHierarchyExternalPrompt prompt,
                    out string error),
                Is.True,
                error);

            Assert.That(prompt.text, Does.Contain("version 2"));
            Assert.That(prompt.text, Does.Contain(Path.GetFileName(prompt.applyFullPath)));
            Assert.That(
                prompt.text,
                Does.Contain("If the plan or review is edited after approval"));
            Assert.That(prompt.text, Does.Not.Contain("re-apply automatically"));
            Assert.That(File.Exists(prompt.promptFullPath), Is.True, "提示词必须落盘留档。");

            string sessionPath = PsdHierarchyTerminalApplyWatcher.BuildSessionPath(prompt.applyFullPath);
            sessionId = Path.GetFileName(sessionPath);
            sessionId = sessionId.Substring(0, sessionId.Length - ".session.json".Length);
            JObject session = JObject.Parse(File.ReadAllText(sessionPath));
            Assert.That(
                session.Value<int?>("version"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion));
            Assert.That(session.Value<string>("targetPrefabPath"), Is.EqualTo(targetPrefabPath));
            Assert.That(session.Value<string>("sourcePsdAssetPath"), Is.EqualTo(SourceAssetPath));

            // 入口 → 会话 → 哨兵 → 回执：用同一份 v2 计划走完整链路。
            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    targetPrefabPath,
                    out string snapshotJson,
                    out _,
                    out string snapshotError),
                Is.True,
                snapshotError);
            File.WriteAllText(
                prompt.planFullPath,
                BuildPlan(snapshotJson).ToString(Newtonsoft.Json.Formatting.None),
                new UTF8Encoding(false));
            File.WriteAllText(prompt.applyFullPath, string.Empty, new UTF8Encoding(false));

            Assert.That(
                await PsdHierarchyTerminalApplyWatcher.RunPendingApplyOnceForTestsAsync(),
                Is.True);

            string receiptPath = PsdHierarchyTerminalApplyWatcher.BuildResultPath(prompt.applyFullPath);
            Assert.That(File.Exists(receiptPath), Is.True, "入口链路必须写出回执：" + receiptPath);
            JObject receipt = JObject.Parse(File.ReadAllText(receiptPath));
            Assert.That(receipt.Value<bool>("success"), Is.True, receipt.ToString());
            Assert.That(
                receipt.Value<string>("status"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.StatusApplied));

            GameObject root = PrefabUtility.LoadPrefabContents(targetPrefabPath);
            try
            {
                Assert.That(root.transform.Find("[Rows]"), Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildPrefab(string assetPath)
        {
            var root = new GameObject("Source", typeof(RectTransform));
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

                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, assetPath), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private JObject BuildPlan(string snapshotJson)
        {
            var snapshot = JObject.Parse(snapshotJson);
            return new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = snapshot.Value<string>("fingerprint"),
                ["prefabName"] = "Source",
                ["prefabAssetPath"] = targetPrefabPath,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = targetPrefabPath },
                ["verify"] = new JObject(),
                ["wrappers"] = new JArray(new JObject
                {
                    ["id"] = "rows",
                    ["parent"] = "node:" + ReadNodeId(snapshot, "Source"),
                    ["name"] = "[Rows]",
                    ["siblingIndex"] = 0,
                }),
                ["moves"] = new JArray(
                    new JObject
                    {
                        ["source"] = "node:" + ReadNodeId(snapshot, "Source/TaskItem_1"),
                        ["destination"] = "@rows",
                        ["siblingIndex"] = 0,
                    },
                    new JObject
                    {
                        ["source"] = "node:" + ReadNodeId(snapshot, "Source/TaskItem_2"),
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

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder))
            {
                return;
            }

            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
