namespace PsdLayoutTool2.Tests
{
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine.UIElements;

    public sealed class PsdHierarchyPlanWorkspaceViewTests
    {
        [Test]
        public void ViewIsHiddenBeforeAWorkspaceIsBound()
        {
            var view = new PsdHierarchyPlanWorkspaceView();

            Assert.That(view.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void BlockedWorkspaceShowsIssueActionsWithoutAnApplySafeButton()
        {
            var view = new PsdHierarchyPlanWorkspaceView();

            view.Bind(CreateBlockedWorkspace(), new PsdHierarchyPlanWorkspaceActions());

            Assert.That(view.Q<Label>(PsdHierarchyPlanWorkspaceView.TitleName).text, Is.EqualTo("待处理问题"));
            Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.KeepOriginalButtonName), Is.Not.Null);
            Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ShowDetailsButtonName), Is.Not.Null);
            Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.RetryButtonName), Is.Not.Null);
            Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ExportButtonName), Is.Not.Null);
            Assert.That(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ApplySafeButtonName), Is.Null);
        }

        [Test]
        public void DetailsAreCollapsedUntilRequested()
        {
            var view = new PsdHierarchyPlanWorkspaceView();
            view.Bind(CreateBlockedWorkspace(), new PsdHierarchyPlanWorkspaceActions());
            Label details = view.Q<Label>(PsdHierarchyPlanWorkspaceView.TechnicalDetailsName);

            Assert.That(details.style.display.value, Is.EqualTo(DisplayStyle.None));

            Click(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ShowDetailsButtonName));

            Assert.That(details.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(details.text, Does.Contain("variant sources must remain direct siblings"));
        }

        [Test]
        public void LocateIsDisabledWhenTheIssueHasNoAuthoritativeNodeIds()
        {
            var workspace = PsdHierarchyPlanWorkspace.CreateBlockedPlan(
                "snapshot-a",
                "review",
                "raw reply",
                PsdHierarchyPlanIssueCategory.PlanPreparation,
                "计划包含相互冲突或无法确定的结构操作。",
                "topology error",
                "保持原结构，或重新分析此项。",
                new string[0],
                new string[0]);
            var view = new PsdHierarchyPlanWorkspaceView();

            view.Bind(workspace, new PsdHierarchyPlanWorkspaceActions());

            Assert.That(
                view.Q<Button>(PsdHierarchyPlanWorkspaceView.LocateNodesButtonName).enabledSelf,
                Is.False);
        }

        [Test]
        public void IssueButtonsInvokeTheCurrentBindingOnlyOnce()
        {
            int keepCount = 0;
            int retryCount = 0;
            int exportCount = 0;
            string keptIssueId = string.Empty;
            string[] locatedNodeIds = null;
            var actions = new PsdHierarchyPlanWorkspaceActions
            {
                keepOriginalStructure = issueId =>
                {
                    keepCount++;
                    keptIssueId = issueId;
                },
                locateNodes = nodeIds => locatedNodeIds = nodeIds,
                retryAnalysis = () => retryCount++,
                exportDiagnostics = () => exportCount++,
            };
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace();
            var view = new PsdHierarchyPlanWorkspaceView();
            view.Bind(workspace, actions);
            view.Bind(workspace, actions);

            Click(view.Q<Button>(PsdHierarchyPlanWorkspaceView.KeepOriginalButtonName));
            Click(view.Q<Button>(PsdHierarchyPlanWorkspaceView.LocateNodesButtonName));
            Click(view.Q<Button>(PsdHierarchyPlanWorkspaceView.RetryButtonName));
            Click(view.Q<Button>(PsdHierarchyPlanWorkspaceView.ExportButtonName));

            Assert.That(keepCount, Is.EqualTo(1));
            Assert.That(keptIssueId, Is.EqualTo(workspace.issues[0].id));
            Assert.That(locatedNodeIds, Is.EqualTo(new[] { "n000059", "n000089" }));
            Assert.That(retryCount, Is.EqualTo(1));
            Assert.That(exportCount, Is.EqualTo(1));
        }

        [Test]
        public void RebindingResolvedIssueShowsKeptOriginalState()
        {
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace();
            workspace.TryKeepOriginalStructure(workspace.issues[0].id);
            var view = new PsdHierarchyPlanWorkspaceView();

            view.Bind(workspace, new PsdHierarchyPlanWorkspaceActions());

            Assert.That(
                view.Q<Label>(PsdHierarchyPlanWorkspaceView.IssueStateName).text,
                Is.EqualTo("已保留原结构"));
            Assert.That(
                view.Q<Button>(PsdHierarchyPlanWorkspaceView.KeepOriginalButtonName).enabledSelf,
                Is.False);
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

        private static void Click(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
