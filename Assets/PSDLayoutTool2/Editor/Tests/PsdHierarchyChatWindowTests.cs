namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class PsdHierarchyChatWindowTests
    {
        [Test]
        public void FreshSnapshotClearsRequestHistoryAndCliSession()
        {
            var conversation = new List<PsdHierarchyChatMessage>
            {
                new PsdHierarchyChatMessage("user", "Old snapshot request"),
                new PsdHierarchyChatMessage("assistant", "Old snapshot plan"),
            };
            string cliSessionId = "old-cli-session";

            PsdHierarchyChatWindow.ResetConversationForFreshSnapshot(
                conversation,
                ref cliSessionId);

            Assert.That(conversation, Is.Empty);
            Assert.That(cliSessionId, Is.Empty);
        }

        [Test]
        public void SamePrefabFingerprintWithChangedCandidateAnalysisRequiresConversationReset()
        {
            const string fingerprint = "same-prefab-fingerprint";
            var staleContext = new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"same-prefab-fingerprint\",\"componentFamilyCandidates\":[{\"id\":\"family_001\",\"sources\":[\"node:n1\",\"node:n2\",\"node:n3\",\"node:n4\"]}]}",
                fingerprint,
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/same-prefab-fingerprint.json");
            var refreshedContext = new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"same-prefab-fingerprint\",\"componentFamilyCandidates\":[{\"id\":\"family_001\",\"sources\":[\"node:n1\",\"node:n2\",\"node:n3\",\"node:n4\",\"node:n5\"]}]}",
                fingerprint,
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/same-prefab-fingerprint.json");

            Assert.That(
                PsdHierarchyChatWindow.HasAuthoritativeAnalysisChanged(staleContext, refreshedContext),
                Is.True);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void FirstSuccessfulApplyReplacesTheReplayProfile(bool hasAppliedCleanupStage, bool expected)
        {
            Assert.That(
                PsdHierarchyChatWindow.ShouldReplaceReplayProfile(hasAppliedCleanupStage),
                Is.EqualTo(expected));
        }

        [Test]
        public void WindowBuildsOnlyChatControls()
        {
            PsdHierarchyChatWindow window = ScriptableObject.CreateInstance<PsdHierarchyChatWindow>();
            try
            {
                window.InitializeForTests(new PsdHierarchyChatContext(
                    "E:/Project/Demo/monsterhunter",
                    "Assets/UI/Source.psd",
                    "Assets/UI/Prefab/ExampleView.prefab",
                    "E:/Project/Demo/monsterhunter/Skill.md",
                    "Skill Body",
                    "Prefab Body"));

                VisualElement root = window.rootVisualElement.Q<VisualElement>(PsdHierarchyChatWindow.RootElementName);
                Assert.That(root, Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<ScrollView>(PsdHierarchyChatWindow.MessagesElementName), Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<VisualElement>(PsdHierarchyChatWindow.ThinkingIndicatorElementName),
                    Is.Null);
                Assert.That(window.rootVisualElement.Q<TextField>(PsdHierarchyChatWindow.DraftFieldName), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.SendButtonName), Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<Label>(PsdHierarchyChatWindow.AgentInfoElementName).text,
                    Does.Contain("Agent"));
                Button openCliButton = window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.OpenCliButtonName);
                Assert.That(openCliButton, Is.Not.Null);
                Assert.That(openCliButton.enabledSelf, Is.False);
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.PingPsdButtonName).text,
                    Is.EqualTo("Source.psd"));
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.PingPrefabButtonName).text,
                    Is.EqualTo("ExampleView.prefab"));
                Assert.That(window.rootVisualElement.Q<VisualElement>("psd-hierarchy-chat-provider"), Is.Null);
                Assert.That(window.rootVisualElement.Q<TextField>("psd-hierarchy-chat-api-key"), Is.Null);
                Assert.That(
                    window.rootVisualElement.Q<Label>("psd-hierarchy-chat-status").text,
                    Is.EqualTo("准备分析"));

                window.ShowThinkingIndicator();
                VisualElement thinkingIndicator = window.rootVisualElement.Q<VisualElement>(
                    PsdHierarchyChatWindow.ThinkingIndicatorElementName);
                Assert.That(thinkingIndicator, Is.Not.Null);
                Assert.That(thinkingIndicator.Q<Label>().text, Is.EqualTo("AI"));
                Assert.That(
                    thinkingIndicator.Q<Label>(className: "psd-hierarchy-chat-thinking-content").text,
                    Does.Contain("正在分析"));

                window.HideThinkingIndicator();
                Assert.That(
                    window.rootVisualElement.Q<VisualElement>(PsdHierarchyChatWindow.ThinkingIndicatorElementName),
                    Is.Null);
            }
            finally
            {
                window.Close();
            }
        }

        [TestCase("user")]
        [TestCase("assistant")]
        [TestCase("system")]
        public void MessageElementProvidesCopyButtonForEveryRole(string role)
        {
            VisualElement message = PsdHierarchyChatWindow.CreateMessageElement(role, "完整错误内容");

            Button copyButton = message.Q<Button>(className: PsdHierarchyChatWindow.CopyMessageButtonClassName);
            Assert.That(copyButton, Is.Not.Null);
            Assert.That(copyButton.tooltip, Is.EqualTo("复制完整消息"));
        }

        [Test]
        public void CopyMessageToClipboardPreservesTheCompleteErrorMessage()
        {
            const string errorMessage = "Prefab 更新失败：Path was not found: 跳格子切图/1/122M";

            PsdHierarchyChatWindow.CopyMessageToClipboard(errorMessage);

            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(errorMessage));
        }

        [Test]
        public void ScrollToIfAttachedIgnoresMissingOrDetachedElement()
        {
            var scrollView = new ScrollView();

            Assert.DoesNotThrow(() => PsdHierarchyChatWindow.ScrollToIfAttached(scrollView, null));
            Assert.DoesNotThrow(
                () => PsdHierarchyChatWindow.ScrollToIfAttached(scrollView, new VisualElement()));
        }
    }
}
