namespace PsdLayoutTool2.Tests
{
    using System.IO;
    using NUnit.Framework;

    public sealed class PsdHierarchyTerminalApplyWatcherTests
    {
        private const string TerminalDirectory = @"E:\Project\Demo\monsterhunter\Library\PsdHierarchyTerminal";

        [Test]
        public void BuildApplySentinelPathStripsPlanJsonSuffix()
        {
            string applyPath = PsdHierarchyOrganizerEntry.BuildApplySentinelPath(
                TerminalDirectory + @"\abc.plan.json");

            Assert.That(applyPath, Does.EndWith("abc.apply"));
            Assert.That(applyPath, Does.Not.Contain(".plan.json"));
        }

        [Test]
        public void BuildApplySentinelPathAppendsWhenNotPlanJson()
        {
            string applyPath = PsdHierarchyOrganizerEntry.BuildApplySentinelPath(
                TerminalDirectory + @"\abc");

            Assert.That(applyPath, Does.EndWith("abc.apply"));
        }

        [Test]
        public void BuildResultPathUsesApplyResultJsonSuffix()
        {
            string resultPath = PsdHierarchyTerminalApplyWatcher.BuildResultPath(
                TerminalDirectory + @"\abc.apply");

            Assert.That(resultPath, Does.EndWith("abc.apply-result.json"));
        }

        [Test]
        public void ClaimPathKeepsTheSessionPrefixAndUsesApplyingSuffix()
        {
            string claimPath = PsdHierarchyTerminalApplyWatcher.BuildClaimPath(
                TerminalDirectory + @"\abc.apply");

            Assert.That(claimPath, Does.EndWith("abc.applying"));
            // 抢占后必须还能找到同一个会话的回执路径。
            Assert.That(
                PsdHierarchyTerminalApplyWatcher.ResolveResultPath(claimPath),
                Does.EndWith("abc.apply-result.json"));
            Assert.That(
                PsdHierarchyTerminalApplyWatcher.ResolveResultPath(TerminalDirectory + @"\abc.applied"),
                Does.EndWith("abc.apply-result.json"));
            Assert.That(
                PsdHierarchyTerminalApplyWatcher.ResolveResultPath(TerminalDirectory + @"\abc.apply-uncertain"),
                Does.EndWith("abc.apply-result.json"));
        }

        [Test]
        public void SessionRecordRoundTripsThroughJson()
        {
            var record = new PsdHierarchyTerminalApplyWatcher.SessionRecord
            {
                sessionId = "abc",
                sourcePsdAssetPath = "Assets/UI/Source.psd",
                targetPrefabPath = "Assets/UI/Prefab/ExampleView.prefab",
                planPath = @"E:\tmp\abc.plan.json",
                reviewPath = @"E:\tmp\abc.review.md",
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(record);
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<PsdHierarchyTerminalApplyWatcher.SessionRecord>(json);

            Assert.That(loaded.sourcePsdAssetPath, Is.EqualTo("Assets/UI/Source.psd"));
            Assert.That(loaded.targetPrefabPath, Is.EqualTo("Assets/UI/Prefab/ExampleView.prefab"));
            Assert.That(loaded.planPath, Does.Contain("abc.plan.json"));
            Assert.That(loaded.version, Is.EqualTo(PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion));
            Assert.That(loaded.version, Is.EqualTo(2), "终端会话是独立协议，新写入必须是版本 2。");
        }

        [Test]
        public void ApplyReceiptsAndClaimsUseVersionTwo()
        {
            Assert.That(
                new PsdHierarchyTerminalApplyWatcher.ApplyResultRecord().version,
                Is.EqualTo(2));
            Assert.That(
                new PsdHierarchyTerminalApplyWatcher.ApplyClaimRecord().version,
                Is.EqualTo(2));
        }

        [Test]
        public void ReceiptStatusConstantsCoverEveryExecutionOutcome()
        {
            // 回执必须能区分写入前拒绝、完成、已写入但不完整、不确定（规格 A13）。
            Assert.That(PsdHierarchyTerminalApplyWatcher.StatusApplied, Is.EqualTo("applied"));
            Assert.That(PsdHierarchyTerminalApplyWatcher.StatusRejected, Is.EqualTo("rejected"));
            Assert.That(PsdHierarchyTerminalApplyWatcher.StatusPartial, Is.EqualTo("partial"));
            Assert.That(PsdHierarchyTerminalApplyWatcher.StatusUncertain, Is.EqualTo("uncertain"));
        }

        [Test]
        public void PlanHashIsStableAndContentSensitive()
        {
            string first = PsdHierarchyTerminalApplyWatcher.ComputePlanHash("{\"version\":2}");
            string again = PsdHierarchyTerminalApplyWatcher.ComputePlanHash("{\"version\":2}");
            string changed = PsdHierarchyTerminalApplyWatcher.ComputePlanHash("{\"version\":2,\"moves\":[]}");

            Assert.That(first, Is.EqualTo(again));
            Assert.That(first, Is.Not.EqualTo(changed));
            Assert.That(first.Length, Is.EqualTo(64));
            Assert.That(
                PsdHierarchyTerminalApplyWatcher.ComputePlanHash(null),
                Is.EqualTo(PsdHierarchyTerminalApplyWatcher.ComputePlanHash(string.Empty)));
        }

        [Test]
        public void SentinelAndClaimNamesShareOneSessionPrefix()
        {
            string plan = Path.Combine(TerminalDirectory, "session-1.plan.json");
            string apply = PsdHierarchyOrganizerEntry.BuildApplySentinelPath(plan);
            string claim = PsdHierarchyTerminalApplyWatcher.BuildClaimPath(apply);

            Assert.That(Path.GetFileName(apply), Is.EqualTo("session-1.apply"));
            Assert.That(Path.GetFileName(claim), Is.EqualTo("session-1.applying"));
            Assert.That(
                Path.GetFileName(PsdHierarchyTerminalApplyWatcher.BuildResultPath(apply)),
                Is.EqualTo("session-1.apply-result.json"));
        }
    }
}
