namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using NUnit.Framework;

    public sealed class PsdHierarchyPlanWorkspaceTests
    {
        [Test]
        public void ReadyWorkspaceExposesItsSingleValidatedBatch()
        {
            PsdHierarchyPlanWorkspace workspace = PsdHierarchyPlanWorkspace.CreateReady(
                "snapshot-a",
                "review",
                "raw reply",
                "{\"version\":2}");

            Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.Ready));
            Assert.That(workspace.batches, Has.Count.EqualTo(1));
            Assert.That(workspace.issues, Is.Empty);
            Assert.That(workspace.TryGetEnabledPlan(out string planJson), Is.True);
            Assert.That(planJson, Is.EqualTo("{\"version\":2}"));
        }

        [Test]
        public void FailedCompatibilityBatchReturnsNoSafeChangesInsteadOfNull()
        {
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace();

            Assert.That(workspace, Is.Not.Null);
            Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.NoSafeChanges));
            Assert.That(workspace.TryGetEnabledPlan(out _), Is.False);
            Assert.That(workspace.issues.Single().state, Is.EqualTo(PsdHierarchyPlanIssueState.Open));
            Assert.That(workspace.rawAssistantReply, Is.EqualTo("raw reply"));
        }

        [Test]
        public void KeepOriginalStructureRecordsAnExplicitResolution()
        {
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace();

            bool resolved = workspace.TryKeepOriginalStructure(workspace.issues[0].id);

            Assert.That(resolved, Is.True);
            Assert.That(workspace.issues[0].state, Is.EqualTo(PsdHierarchyPlanIssueState.KeptUnchanged));
            Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.NoSafeChanges));
            Assert.That(workspace.OpenIssueCount, Is.Zero);
        }

        [Test]
        public void KeepOriginalStructureIsIdempotentAndRejectsUnknownIssue()
        {
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace();
            string issueId = workspace.issues[0].id;

            Assert.That(workspace.TryKeepOriginalStructure(issueId), Is.True);
            Assert.That(workspace.TryKeepOriginalStructure(issueId), Is.True);
            Assert.That(workspace.TryKeepOriginalStructure("missing"), Is.False);
        }

        [Test]
        public void BlockedWorkspaceCopiesCandidateAndNodeArrays()
        {
            string[] candidateIds = { "family_002" };
            string[] nodeIds = { "n000059", "n000089" };
            PsdHierarchyPlanWorkspace workspace = PsdHierarchyPlanWorkspace.CreateBlockedPlan(
                "snapshot-a",
                "review",
                "raw reply",
                PsdHierarchyPlanIssueCategory.PlanPreparation,
                "summary",
                "variant sources must remain direct siblings",
                "保持原结构，或重新分析此项。",
                candidateIds,
                nodeIds);

            candidateIds[0] = "changed";
            nodeIds[0] = "changed";

            Assert.That(workspace.issues[0].candidateIds, Is.EqualTo(new[] { "family_002" }));
            Assert.That(workspace.issues[0].affectedNodeIds, Is.EqualTo(new[] { "n000059", "n000089" }));
            Assert.That(workspace.AffectedNodeCount, Is.EqualTo(2));
            Assert.That(workspace.QuarantinedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void IssueIdsAreStableForEquivalentWorkspaces()
        {
            PsdHierarchyPlanWorkspace first = CreateBlockedWorkspace();
            PsdHierarchyPlanWorkspace second = CreateBlockedWorkspace();

            Assert.That(first.issues[0].id, Is.EqualTo("issue_plan_preparation_001"));
            Assert.That(second.issues[0].id, Is.EqualTo(first.issues[0].id));
        }

        [Test]
        public void InfrastructureFailureRemainsReviewable()
        {
            PsdHierarchyPlanWorkspace workspace = PsdHierarchyPlanWorkspace.CreateInfrastructureBlocked(
                "snapshot-a",
                "review",
                "raw reply",
                "找不到 Prefab 整理计划预检器。",
                "请重试或刷新快照。");

            Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.InfrastructureBlocked));
            Assert.That(workspace.batches, Is.Empty);
            Assert.That(workspace.issues.Single().category, Is.EqualTo(PsdHierarchyPlanIssueCategory.Infrastructure));
            Assert.That(workspace.issues.Single().severity, Is.EqualTo(PsdHierarchyPlanIssueSeverity.Blocked));
            Assert.That(workspace.TryGetEnabledPlan(out _), Is.False);
        }

        private static PsdHierarchyPlanWorkspace CreateBlockedWorkspace()
        {
            return PsdHierarchyPlanWorkspace.CreateBlockedPlan(
                "snapshot-a",
                "review",
                "raw reply",
                PsdHierarchyPlanIssueCategory.PlanPreparation,
                "计划包含相互冲突或无法确定的结构操作。",
                "variant sources must remain direct siblings",
                "保持原结构，或重新分析此项。",
                new[] { "family_002" },
                new[] { "n000059", "n000089" });
        }
    }
}
