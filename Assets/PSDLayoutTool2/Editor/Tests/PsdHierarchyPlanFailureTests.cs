namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using NUnit.Framework;

    public sealed class PsdHierarchyPlanFailureTests
    {
        [Test]
        public void PlanFailureClassifierExtractsCandidateAndNodeContext()
        {
            const string error =
                "Deterministic component-family repair failed: candidateId=family_002; asset=组 16; " +
                "recommendedMode=variant; sources=node:n000059,node:n000064,node:n000089; " +
                "reason=suggestedAssetName=组 16 cannot produce a bracketed English semantic item name.";

            PsdHierarchyPlanIssue issue = PsdHierarchyChatCleanupExecution.ClassifyPlanFailure(
                PsdHierarchyPlanIssueCategory.PlanPreparation,
                error);

            Assert.That(issue.category, Is.EqualTo(PsdHierarchyPlanIssueCategory.PlanPreparation));
            Assert.That(issue.technicalDetails, Is.EqualTo(error));
            Assert.That(issue.candidateIds, Is.EqualTo(new[] { "family_002" }));
            Assert.That(issue.affectedNodeIds, Is.EqualTo(new[] { "n000059", "n000064", "n000089" }));
            Assert.That(issue.summary, Does.Contain("结构操作"));
        }

        [Test]
        public void PlanFailureClassifierPreservesUnknownTopologyErrorsWithoutThrowing()
        {
            const string error =
                "variantComponentExtractions[1] variant sources must remain direct siblings after planned moves; " +
                "template=7日签到拆分/[Root]/组 16/[FlatSibling_flat_sibling_001]; " +
                "source=7日签到拆分/[Root]/组 16/[FlatSibling_flat_sibling_007]";

            PsdHierarchyPlanIssue issue = null;
            Assert.DoesNotThrow(() => issue = PsdHierarchyChatCleanupExecution.ClassifyPlanFailure(
                PsdHierarchyPlanIssueCategory.PlanPreparation,
                error));

            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.candidateIds, Is.Empty);
            Assert.That(issue.affectedNodeIds, Is.Empty);
            Assert.That(issue.technicalDetails, Is.EqualTo(error));
        }

        [Test]
        public void WorkspaceAdapterKeepsReadyPlanAndSnapshotFingerprint()
        {
            PsdHierarchyChatContext context = CreateContext();

            PsdHierarchyPlanWorkspace workspace = PsdHierarchyChatCleanupExecution.CreateReadyWorkspace(
                context,
                "review",
                "raw reply",
                "{\"version\":2}");

            Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.Ready));
            Assert.That(workspace.snapshotFingerprint, Is.EqualTo(context.hierarchySnapshotFingerprint));
            Assert.That(workspace.TryGetEnabledPlan(out string planJson), Is.True);
            Assert.That(planJson, Is.EqualTo("{\"version\":2}"));
        }

        [Test]
        public void WorkspaceAdapterReturnsIssueWorkspaceWithoutAContext()
        {
            PsdHierarchyPlanWorkspace workspace = null;

            Assert.DoesNotThrow(() => workspace = PsdHierarchyChatCleanupExecution.CreateIssueWorkspace(
                null,
                "review",
                "raw reply",
                PsdHierarchyPlanIssueCategory.PlanExtraction,
                "AI 未返回可执行的 JSON 计划代码块。"));

            Assert.That(workspace, Is.Not.Null);
            Assert.That(workspace.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.NoSafeChanges));
            Assert.That(workspace.snapshotFingerprint, Is.Empty);
            Assert.That(workspace.issues.Single().category, Is.EqualTo(PsdHierarchyPlanIssueCategory.PlanExtraction));
        }

        private static PsdHierarchyChatContext CreateContext()
        {
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"snapshot-123\",\"nodes\":[]}",
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }
    }
}
