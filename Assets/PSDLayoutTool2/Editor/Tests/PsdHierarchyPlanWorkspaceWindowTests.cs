namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class PsdHierarchyPlanWorkspaceWindowTests
    {
        private static readonly string TemporaryProjectRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PsdHierarchyPlanWorkspaceWindowTests",
            System.Guid.NewGuid().ToString("N"));

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (System.IO.Directory.Exists(TemporaryProjectRoot))
            {
                System.IO.Directory.Delete(TemporaryProjectRoot, true);
            }
        }

        [Test]
        public void WorkspaceViewIsBuiltBeforeTheMessageHistory()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                VisualElement root = window.rootVisualElement.Q<VisualElement>(PsdHierarchyChatWindow.RootElementName);
                VisualElement workspaceView = root.Q<VisualElement>(PsdHierarchyPlanWorkspaceView.RootName);
                ScrollView messages = root.Q<ScrollView>(PsdHierarchyChatWindow.MessagesElementName);
                VisualElement[] children = root.Children().ToArray();

                Assert.That(workspaceView, Is.Not.Null);
                Assert.That(System.Array.IndexOf(children, workspaceView), Is.LessThan(System.Array.IndexOf(children, messages)));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReadyWorkspaceEnablesTheExistingExplicitApplyFlow()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                window.SetWorkspaceForTests(PsdHierarchyPlanWorkspace.CreateReady(
                    "snapshot-a",
                    "review",
                    "raw reply",
                    "{\"version\":2}"));

                Assert.That(window.CurrentWorkspaceForTests.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.Ready));
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.SendButtonName).text,
                    Is.EqualTo("确认并更新"));
                Assert.That(
                    window.rootVisualElement.Q<Label>(PsdHierarchyPlanWorkspaceView.TitleName).text,
                    Is.EqualTo("计划工作区"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BlockedWorkspaceKeepsApplyDisabledAndShowsHandlingActions()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                window.SetWorkspaceForTests(CreateBlockedWorkspace("snapshot-a"));

                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.SendButtonName).text,
                    Is.EqualTo("发送追问"));
                Assert.That(
                    window.rootVisualElement.Q<Label>(PsdHierarchyPlanWorkspaceView.TitleName).text,
                    Is.EqualTo("待处理问题"));
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyPlanWorkspaceView.KeepOriginalButtonName),
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void KeepOriginalActionResolvesTheCurrentWorkspaceWithoutCreatingAPlan()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                window.SetWorkspaceForTests(CreateBlockedWorkspace("snapshot-a"));

                Click(window.rootVisualElement.Q<Button>(PsdHierarchyPlanWorkspaceView.KeepOriginalButtonName));

                Assert.That(
                    window.CurrentWorkspaceForTests.issues.Single().state,
                    Is.EqualTo(PsdHierarchyPlanIssueState.KeptUnchanged));
                Assert.That(window.CurrentWorkspaceForTests.TryGetEnabledPlan(out _), Is.False);
                Assert.That(
                    window.rootVisualElement.Q<Label>(PsdHierarchyPlanWorkspaceView.IssueStateName).text,
                    Is.EqualTo("已保留原结构"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void InitializingANewSnapshotClearsThePreviousWorkspace()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                window.SetWorkspaceForTests(CreateBlockedWorkspace("snapshot-a"));

                window.InitializeForTests(CreateContext("snapshot-b"));

                Assert.That(window.CurrentWorkspaceForTests, Is.Null);
                Assert.That(
                    window.rootVisualElement.Q<VisualElement>(PsdHierarchyPlanWorkspaceView.RootName).style.display.value,
                    Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void CompletedPlanFailurePreservesReviewAndEnablesRecovery()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                window.HandleCompletedPlanFailure(
                    "评审结论仍然可用。",
                    "raw reply",
                    PsdHierarchyPlanIssueCategory.PlanPreparation,
                    "variant sources must remain direct siblings");

                Assert.That(window.CurrentWorkspaceForTests.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.NoSafeChanges));
                Assert.That(window.CurrentWorkspaceForTests.reviewText, Is.EqualTo("评审结论仍然可用。"));
                Assert.That(
                    window.rootVisualElement.Query<Label>(className: "psd-hierarchy-chat-message-content")
                        .ToList()
                        .Any(label => label.text.Contains("评审结论仍然可用。")),
                    Is.True);
                Assert.That(
                    window.rootVisualElement.Q<Label>("psd-hierarchy-chat-status").text,
                    Is.EqualTo("存在待处理问题"));
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.RegeneratePlanButtonName).enabledSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void InfrastructureFailureUsesTheSameReviewSurface()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                window.HandleCompletedPlanFailure(
                    string.Empty,
                    string.Empty,
                    PsdHierarchyPlanIssueCategory.Infrastructure,
                    "runner unavailable");

                Assert.That(
                    window.CurrentWorkspaceForTests.state,
                    Is.EqualTo(PsdHierarchyPlanWorkspaceState.InfrastructureBlocked));
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.SendButtonName).text,
                    Is.EqualTo("发送追问"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ApplyFailureQuarantinesTheOriginalPlan()
        {
            PsdHierarchyChatWindow window = CreateWindow("snapshot-a");
            try
            {
                window.HandleApplyFailure(
                    "{\"version\":2}",
                    "apply failed and rolled back",
                    infrastructureFailure: false);

                Assert.That(window.CurrentWorkspaceForTests.state, Is.EqualTo(PsdHierarchyPlanWorkspaceState.NoSafeChanges));
                Assert.That(window.CurrentWorkspaceForTests.batches.Single().enabled, Is.False);
                Assert.That(window.CurrentWorkspaceForTests.batches.Single().planJson, Is.EqualTo("{\"version\":2}"));
                Assert.That(
                    window.CurrentWorkspaceForTests.issues.Single().category,
                    Is.EqualTo(PsdHierarchyPlanIssueCategory.RunnerPreflight));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static PsdHierarchyChatWindow CreateWindow(string fingerprint)
        {
            PsdHierarchyChatWindow window = ScriptableObject.CreateInstance<PsdHierarchyChatWindow>();
            window.InitializeForTests(CreateContext(fingerprint));
            return window;
        }

        private static PsdHierarchyChatContext CreateContext(string fingerprint)
        {
            return new PsdHierarchyChatContext(
                TemporaryProjectRoot,
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"" + fingerprint + "\",\"nodes\":[]}",
                fingerprint,
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/" + fingerprint + ".json");
        }

        private static PsdHierarchyPlanWorkspace CreateBlockedWorkspace(string fingerprint)
        {
            return PsdHierarchyPlanWorkspace.CreateBlockedPlan(
                fingerprint,
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
