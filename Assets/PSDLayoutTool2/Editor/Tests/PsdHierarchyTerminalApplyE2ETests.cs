namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.TestTools;
    using UnityEngine.UI;

    /// <summary>
    /// 08：真实终端会话 + 哨兵 + 回执的端到端验收（含占用失败与被中断请求的故障注入）。
    /// 同一份已接受请求不得因为重复轮询、占用失败或域重载被再次写入。
    /// </summary>
    public sealed class PsdHierarchyTerminalApplyE2ETests
    {
        private const string Folder = "Assets/__PsdTerminalApplyTests";
        private const string SourceAssetPath = Folder + "/Source.txt";
        private const string Target = Folder + "/TerminalView.prefab";
        private string sessionId;
        private string terminalDirectory;
        private string planJson;
        private string snapshotJson;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "__PsdTerminalApplyTests");
            File.WriteAllText(ToFullPath(SourceAssetPath), "source psd placeholder", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(SourceAssetPath, ImportAssetOptions.ForceSynchronousImport);
            BuildPrefab();

            Assert.That(
                PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                    Target,
                    out snapshotJson,
                    out _,
                    out string snapshotError),
                Is.True,
                snapshotError);
            planJson = BuildPlan().ToString(Formatting.None);

            terminalDirectory = PsdHierarchyTerminalApplyWatcher.TerminalDirectoryPath;
            Assert.That(terminalDirectory, Is.Not.Null.And.Not.Empty);
            ClearTerminalDirectory();
            Directory.CreateDirectory(terminalDirectory);
            sessionId = "e2e" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            ClearTerminalDirectory();
            PsdHierarchyCleanupReplayProfile.Remove(SourceAssetPath, Target);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public async Task ConfirmedSentinelIsAppliedOnceAndReceiptMatchesTheAcceptedRequest()
        {
            WriteSession(version: PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion);
            WritePlanFile();
            WriteApplySentinel();
            byte[] before = File.ReadAllBytes(ToFullPath(Target));

            Assert.That(
                await PsdHierarchyTerminalApplyWatcher.RunPendingApplyOnceForTestsAsync(),
                Is.True);

            JObject receipt = ReadReceipt();
            Assert.That(receipt.Value<bool>("success"), Is.True, receipt.ToString());
            Assert.That(
                receipt.Value<string>("status"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.StatusApplied));
            Assert.That(
                receipt.Value<int?>("version"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion));
            Assert.That(receipt.Value<string>("sessionId"), Is.EqualTo(sessionId));
            Assert.That(
                receipt.Value<string>("planHash"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.ComputePlanHash(planJson)));
            Assert.That(receipt.Value<string>("executionId"), Is.Not.Null.And.Not.Empty);
            Assert.That(receipt.Value<string>("targetPrefabPath"), Is.EqualTo(Target));

            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            try
            {
                Transform rows = root.transform.Find("[Rows]");
                Assert.That(rows, Is.Not.Null, "终端哨兵必须真正执行已接受计划。");
                Assert.That(rows.childCount, Is.EqualTo(2));
                Assert.That(rows.GetChild(0).name, Is.EqualTo("TaskItem_1"));
                Assert.That(rows.GetChild(1).name, Is.EqualTo("TaskItem_2"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 同一请求已完成：再次轮询不得重新执行，也不得再写入。
            byte[] afterFirstApply = File.ReadAllBytes(ToFullPath(Target));
            Assert.That(afterFirstApply, Is.Not.EqualTo(before));
            Assert.That(
                await PsdHierarchyTerminalApplyWatcher.RunPendingApplyOnceForTestsAsync(),
                Is.False);
            Assert.That(File.ReadAllBytes(ToFullPath(Target)), Is.EqualTo(afterFirstApply));
            Assert.That(PendingApplyFiles(), Is.Empty, "已完成的请求必须移出待执行集合。");
        }

        [Test]
        public async Task InterruptedClaimOnlyProducesAnUncertainReadOnlyReceipt()
        {
            WriteSession(version: PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion);
            WritePlanFile();
            byte[] before = File.ReadAllBytes(ToFullPath(Target));

            // 注入“占用后中断”：只剩 *.applying 抢占记录，没有终态回执。
            string applyPath = ApplyPath();
            var claim = new PsdHierarchyTerminalApplyWatcher.ApplyClaimRecord
            {
                version = PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion,
                sessionId = sessionId,
                executionId = Guid.NewGuid().ToString("N"),
                planPath = PlanPath(),
                planHash = PsdHierarchyTerminalApplyWatcher.ComputePlanHash(planJson),
                sourcePsdAssetPath = SourceAssetPath,
                targetPrefabPath = Target,
                targetFingerprintBefore = PsdHierarchyChatContextBuilder.ComputeFileFingerprint(
                    ToFullPath(Target)),
                claimedAtUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(
                PsdHierarchyTerminalApplyWatcher.BuildClaimPath(applyPath),
                JsonConvert.SerializeObject(claim, Formatting.Indented),
                new UTF8Encoding(false));

            LogAssert.Expect(LogType.Error, new Regex("检测到被中断的 Apply 请求"));

            Assert.That(
                await PsdHierarchyTerminalApplyWatcher.RunPendingApplyOnceForTestsAsync(),
                Is.False,
                "被中断的请求不能被再次执行。");

            JObject receipt = ReadReceipt();
            Assert.That(receipt.Value<string>("status"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.StatusUncertain));
            Assert.That(receipt.Value<bool>("success"), Is.False);
            Assert.That(receipt.Value<string>("stage"), Is.EqualTo("recovery"));
            Assert.That(receipt.Value<string>("message"), Does.Contain("中断"));
            Assert.That(receipt.Value<string>("executionId"), Is.EqualTo(claim.executionId));
            Assert.That(File.ReadAllBytes(ToFullPath(Target)), Is.EqualTo(before),
                "恢复只做只读核验，不得改动业务 Prefab。");
        }

        [Test]
        public async Task ClaimFailurePerformsNoWriteAndKeepsTheRequestPending()
        {
            WriteSession(version: PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion);
            WritePlanFile();
            WriteApplySentinel();
            byte[] before = File.ReadAllBytes(ToFullPath(Target));

            // 占用失败注入：抢占路径被一个目录挡住，File.Move 必然失败。
            Directory.CreateDirectory(PsdHierarchyTerminalApplyWatcher.BuildClaimPath(ApplyPath()));
            LogAssert.Expect(LogType.Warning, new Regex("无法占用 Apply 哨兵"));

            Assert.That(
                await PsdHierarchyTerminalApplyWatcher.RunPendingApplyOnceForTestsAsync(),
                Is.False);

            Assert.That(File.Exists(ResultPath()), Is.False, "占用失败不得写出任何回执。");
            Assert.That(File.Exists(ApplyPath()), Is.True, "占用失败时哨兵保持待执行。");
            Assert.That(File.ReadAllBytes(ToFullPath(Target)), Is.EqualTo(before));
        }

        [Test]
        public async Task VersionOneSessionIsRejectedBeforeAnyWrite()
        {
            WriteSession(version: 1);
            WritePlanFile();
            WriteApplySentinel();
            byte[] before = File.ReadAllBytes(ToFullPath(Target));

            LogAssert.Expect(LogType.Error, new Regex("自动应用 AI 计划未成功"));

            Assert.That(
                await PsdHierarchyTerminalApplyWatcher.RunPendingApplyOnceForTestsAsync(),
                Is.True);

            JObject receipt = ReadReceipt();
            Assert.That(receipt.Value<string>("status"),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.StatusRejected));
            Assert.That(receipt.Value<string>("stage"), Is.EqualTo("session"));
            Assert.That(receipt.Value<string>("message"), Does.Contain("版本"));
            Assert.That(File.ReadAllBytes(ToFullPath(Target)), Is.EqualTo(before));
            Assert.That(PendingApplyFiles(), Is.Empty, "被拒绝的请求必须移出待执行集合，不能重复执行。");
        }

        private string[] PendingApplyFiles()
        {
            if (!Directory.Exists(terminalDirectory))
            {
                return Array.Empty<string>();
            }

            return Array.FindAll(
                Directory.GetFiles(terminalDirectory),
                file => file.EndsWith(".apply", StringComparison.OrdinalIgnoreCase));
        }

        private void BuildPrefab()
        {
            var root = new GameObject("TerminalView", typeof(RectTransform));
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
            return new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = JObject.Parse(snapshotJson).Value<string>("fingerprint"),
                ["prefabName"] = "TerminalView",
                ["prefabAssetPath"] = Target,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = Target },
                ["verify"] = new JObject(),
                ["wrappers"] = new JArray(new JObject
                {
                    ["id"] = "rows",
                    ["parent"] = "node:" + ReadNodeId("TerminalView"),
                    ["name"] = "[Rows]",
                    ["siblingIndex"] = 0,
                }),
                ["moves"] = new JArray(
                    new JObject
                    {
                        ["source"] = "node:" + ReadNodeId("TerminalView/TaskItem_1"),
                        ["destination"] = "@rows",
                        ["siblingIndex"] = 0,
                    },
                    new JObject
                    {
                        ["source"] = "node:" + ReadNodeId("TerminalView/TaskItem_2"),
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

        private string ReadNodeId(string path)
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

        private string ApplyPath()
        {
            return Path.Combine(terminalDirectory, sessionId + ".apply");
        }

        private string PlanPath()
        {
            return Path.Combine(terminalDirectory, sessionId + ".plan.json");
        }

        private string ResultPath()
        {
            return Path.Combine(terminalDirectory, sessionId + ".apply-result.json");
        }

        private void WriteSession(int version)
        {
            var session = new PsdHierarchyTerminalApplyWatcher.SessionRecord
            {
                version = version,
                sessionId = sessionId,
                sourcePsdAssetPath = SourceAssetPath,
                targetPrefabPath = Target,
                planPath = PlanPath(),
                reviewPath = Path.Combine(terminalDirectory, sessionId + ".review.md"),
            };
            File.WriteAllText(
                PsdHierarchyTerminalApplyWatcher.BuildSessionPath(ApplyPath()),
                JsonConvert.SerializeObject(session, Formatting.Indented),
                new UTF8Encoding(false));
        }

        private void WritePlanFile()
        {
            File.WriteAllText(PlanPath(), planJson, new UTF8Encoding(false));
        }

        private void WriteApplySentinel()
        {
            File.WriteAllText(ApplyPath(), string.Empty, new UTF8Encoding(false));
        }

        private JObject ReadReceipt()
        {
            string path = ResultPath();
            Assert.That(File.Exists(path), Is.True, "终端必须写出回执：" + path);
            return JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        private void ClearTerminalDirectory()
        {
            if (string.IsNullOrEmpty(terminalDirectory) || !Directory.Exists(terminalDirectory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(terminalDirectory))
            {
                if (Path.GetFileName(file).StartsWith(sessionId ?? "e2e", StringComparison.Ordinal))
                {
                    File.Delete(file);
                }
            }

            foreach (string directory in Directory.GetDirectories(terminalDirectory))
            {
                if (Path.GetFileName(directory).StartsWith(sessionId ?? "e2e", StringComparison.Ordinal))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
