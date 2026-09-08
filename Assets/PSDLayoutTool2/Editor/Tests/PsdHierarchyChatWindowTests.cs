namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
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
        public void AutomaticPlanRepairKeepsTheLocalCliConversation()
        {
            string cliSessionId = "stale-cli-session";

            bool reset = PsdHierarchyChatWindow.TryResetCliSessionForAutomaticPlanRepair(
                PsdHierarchyAiConnectionMode.LocalCli,
                ref cliSessionId);

            Assert.That(reset, Is.False);
            Assert.That(cliSessionId, Is.EqualTo("stale-cli-session"));
        }

        [Test]
        public void AutomaticPlanRepairLeavesCustomApiSessionStateUntouched()
        {
            string cliSessionId = "not-a-cli-session";

            bool reset = PsdHierarchyChatWindow.TryResetCliSessionForAutomaticPlanRepair(
                PsdHierarchyAiConnectionMode.CustomApi,
                ref cliSessionId);

            Assert.That(reset, Is.False);
            Assert.That(cliSessionId, Is.EqualTo("not-a-cli-session"));
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

        [TestCase(false, true)]
        [TestCase(true, true)]
        public void RecoveryAfterAnIncompatibleReplayReplacesTheReplayProfile(
            bool hasAppliedCleanupStage,
            bool expected)
        {
            Assert.That(
                PsdHierarchyChatWindow.ShouldReplaceReplayProfile(
                    hasAppliedCleanupStage,
                    requiresReplayProfileReplacement: true),
                Is.EqualTo(expected));
        }

        [Test]
        public void AuthorizedComponentOnlyFollowUpIsAutoApplied()
        {
            string plan = CreateFollowUpPlan(includeExtraction: true, includeHierarchyChange: false);
            PsdHierarchyChatContext context = CreateFollowUpContext();

            Assert.That(
                PsdHierarchyChatWindow.ShouldAutoApplyComponentFollowUp(
                    true,
                    CreateConfirmedIntents(),
                    context,
                    plan,
                    out string error),
                Is.True);
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void AuthorizedFollowUpRejectsSameNamedNodeFromDifferentParent()
        {
            JObject plan = JObject.Parse(CreateFollowUpPlan(true, false));
            plan["componentExtractions"][0]["template"] = "node:n000004";
            plan["componentExtractions"][0]["instances"][0] = "node:n000004";

            Assert.That(
                PsdHierarchyChatWindow.ShouldAutoApplyComponentFollowUp(
                    true,
                    CreateConfirmedIntents(),
                    CreateFollowUpContext(),
                    plan.ToString(),
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("templatePath").Or.Contain("instances"));
        }

        [Test]
        public void SuccessfulHierarchyOnlyPlanWithoutConfirmedIntentsDoesNotStartAnotherAiTurn()
        {
            var result = new PsdHierarchyChatCleanupExecutionResult(true, "Prefab 已更新。");

            Assert.That(
                PsdHierarchyChatWindow.ShouldQueueComponentExtractionFollowUp(result, "[]"),
                Is.False);
        }

        [Test]
        public void VerificationWarningStopsAutomaticSecondStage()
        {
            var result = new PsdHierarchyChatCleanupExecutionResult(
                true,
                "Prefab 已更新。\nVERIFY_WARN issue=direct child mismatch");

            Assert.That(
                PsdHierarchyChatWindow.ShouldQueueComponentExtractionFollowUp(
                    result,
                    CreateConfirmedIntents()),
                Is.False);
        }

        [Test]
        public void MissingRefreshedExtractionCandidateReportsConfirmedWorkflowFailure()
        {
            Assert.That(
                PsdHierarchyChatWindow.CanGenerateConfirmedComponentFollowUp(
                    CreateConfirmedIntents(),
                    CreateFollowUpContext(),
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("已确认").And.Contain("候选"));
        }

        [Test]
        public void OrdinaryPlanStillRequiresExplicitConfirmation()
        {
            string plan = CreateFollowUpPlan(includeExtraction: true, includeHierarchyChange: false);

            Assert.That(
                PsdHierarchyChatWindow.ShouldAutoApplyComponentFollowUp(
                    false,
                    CreateConfirmedIntents(),
                    CreateFollowUpContext(),
                    plan,
                    out _),
                Is.False);
        }

        [Test]
        public void AuthorizedFollowUpCannotIncludeHierarchyChanges()
        {
            string plan = CreateFollowUpPlan(includeExtraction: true, includeHierarchyChange: true);

            Assert.That(
                PsdHierarchyChatWindow.ShouldAutoApplyComponentFollowUp(
                    true,
                    CreateConfirmedIntents(),
                    CreateFollowUpContext(),
                    plan,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("层级变更"));
        }

        [Test]
        public void AuthorizedFollowUpCannotChangeReviewedAssetPath()
        {
            JObject plan = JObject.Parse(CreateFollowUpPlan(true, false));
            plan["componentExtractions"][0]["assetPath"] = "Assets/UI/Common/Other.prefab";

            Assert.That(
                PsdHierarchyChatWindow.ShouldAutoApplyComponentFollowUp(
                    true,
                    CreateConfirmedIntents(),
                    CreateFollowUpContext(),
                    plan.ToString(),
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("assetPath"));
        }

        [TestCase("id")]
        [TestCase("mode")]
        [TestCase("instance")]
        [TestCase("state")]
        [TestCase("common")]
        public void AuthorizedFollowUpRejectsEveryUndisclosedManifestChange(string changedField)
        {
            JArray intents = JArray.Parse(CreateConfirmedIntents());
            JObject intent = (JObject)intents[0];
            switch (changedField)
            {
                case "id":
                    intent["id"] = "other_item";
                    break;
                case "mode":
                    intent["mode"] = "variant";
                    break;
                case "instance":
                    intent["instances"][0]["name"] = "[Other_1]";
                    break;
                case "state":
                    intent["states"] = new JArray(new JObject
                    {
                        ["id"] = "locked",
                        ["name"] = "[State_Locked]",
                        ["members"] = new JArray("LockIcon"),
                    });
                    break;
                case "common":
                    intent["commonMembers"] = new JArray("UnexpectedLabel");
                    break;
            }

            Assert.That(
                PsdHierarchyChatWindow.ShouldAutoApplyComponentFollowUp(
                    true,
                    intents.ToString(Newtonsoft.Json.Formatting.None),
                    CreateFollowUpContext(),
                    CreateFollowUpPlan(true, false),
                    out _),
                Is.False);
        }

        [Test]
        public void ConfirmedWorkflowManifestMismatchStopsInsteadOfRequestingSecondConfirmation()
        {
            JObject plan = JObject.Parse(CreateFollowUpPlan(true, false));
            plan["componentExtractions"][0]["assetPath"] = "Assets/UI/Common/Other.prefab";

            PsdHierarchyValidatedPlanDisposition disposition =
                PsdHierarchyChatWindow.ResolveValidatedPlanDisposition(
                    true,
                    CreateConfirmedIntents(),
                    CreateFollowUpContext(),
                    plan.ToString(),
                    out string error);

            Assert.That(disposition, Is.EqualTo(PsdHierarchyValidatedPlanDisposition.Reject));
            Assert.That(disposition, Is.Not.EqualTo(PsdHierarchyValidatedPlanDisposition.PendingConfirmation));
            Assert.That(error, Does.Contain("assetPath"));
        }

        [Test]
        public void OneConfirmationSequenceNeverLeavesTheValidatedFollowUpPending()
        {
            string plan = CreateFollowUpPlan(true, false);
            PsdHierarchyChatContext context = CreateFollowUpContext();

            PsdHierarchyValidatedPlanDisposition initialDisposition =
                PsdHierarchyChatWindow.ResolveValidatedPlanDisposition(
                    false,
                    string.Empty,
                    context,
                    plan,
                    out string initialError);
            PsdHierarchyValidatedPlanDisposition followUpDisposition =
                PsdHierarchyChatWindow.ResolveValidatedPlanDisposition(
                    true,
                    CreateConfirmedIntents(),
                    context,
                    plan,
                    out string followUpError);

            Assert.That(initialDisposition, Is.EqualTo(PsdHierarchyValidatedPlanDisposition.PendingConfirmation));
            Assert.That(initialError, Is.Empty);
            Assert.That(followUpDisposition, Is.EqualTo(PsdHierarchyValidatedPlanDisposition.AutoApply));
            Assert.That(followUpError, Is.Empty);
        }

        [Test]
        public void ComponentFollowUpPromptCarriesTheConfirmedReviewAndForbidsAnotherConfirmation()
        {
            const string confirmedReview = "| 子 Prefab | 路径 |\n| TaskItem | Assets/UI/Common/TaskItem.prefab |";

            string intents = CreateConfirmedIntents();
            string prompt = PsdHierarchyChatWindow.BuildComponentExtractionFollowUpPrompt(
                confirmedReview,
                intents);

            Assert.That(prompt, Does.Contain(confirmedReview));
            Assert.That(prompt, Does.Contain(intents));
            Assert.That(prompt, Does.Contain("首次确认已授权"));
            Assert.That(prompt, Does.Contain("不得请求第二次确认"));
            Assert.That(prompt, Does.Contain("只允许组件抽取"));
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
                    window.rootVisualElement.Q<Button>(PsdHierarchyChatWindow.OpenLocalRepairButtonName),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<VisualElement>(PsdHierarchyChatWindow.RecoverySectionName),
                    Is.Not.Null);
                Button recoveryButton = window.rootVisualElement.Q<Button>(
                    PsdHierarchyChatWindow.RegeneratePlanButtonName);
                Assert.That(recoveryButton, Is.Not.Null);
                Assert.That(recoveryButton.enabledSelf, Is.False);
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
                Object.DestroyImmediate(window);
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

        private static string CreateFollowUpPlan(bool includeExtraction, bool includeHierarchyChange)
        {
            return new JObject
            {
                ["wrappers"] = includeHierarchyChange
                    ? new JArray(new JObject { ["id"] = "unexpected_wrapper" })
                    : new JArray(),
                ["moves"] = new JArray(),
                ["renames"] = new JArray(),
                ["emptyContainerRemovals"] = new JArray(),
                ["tightBounds"] = new JArray(),
                ["textureRenames"] = new JArray(),
                ["spriteAtlasRenames"] = new JArray(),
                ["containmentResolutions"] = new JArray(),
                ["flatSiblingResolutions"] = new JArray(),
                ["componentExtractions"] = includeExtraction
                    ? new JArray(new JObject
                    {
                        ["id"] = "task_item",
                        ["template"] = "node:n000002",
                        ["assetPath"] = "Assets/UI/Common/TaskItem.prefab",
                        ["instances"] = new JArray("node:n000002", "node:n000003"),
                    })
                    : new JArray(),
                ["stateComponentExtractions"] = new JArray(),
                ["variantComponentExtractions"] = new JArray(),
                ["statefulComponentExtractions"] = new JArray(),
            }.ToString();
        }

        private static string CreateConfirmedIntents()
        {
            return new JArray(new JObject
            {
                ["id"] = "task_item",
                ["mode"] = "component",
                ["assetPath"] = "Assets/UI/Common/TaskItem.prefab",
                ["templatePath"] = "Root/[Items]/[Task_1]",
                ["commonMembers"] = new JArray(),
                ["instances"] = new JArray(
                    new JObject
                    {
                        ["path"] = "Root/[Items]/[Task_1]", ["state"] = string.Empty,
                        ["commonSourceNames"] = new JArray(), ["stateSourceNames"] = new JArray(),
                    },
                    new JObject
                    {
                        ["path"] = "Root/[Items]/[Task_2]", ["state"] = string.Empty,
                        ["commonSourceNames"] = new JArray(), ["stateSourceNames"] = new JArray(),
                    }),
                ["states"] = new JArray(),
                ["defaultState"] = string.Empty,
            }).ToString(Newtonsoft.Json.Formatting.None);
        }

        private static PsdHierarchyChatContext CreateFollowUpContext()
        {
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                new JObject
                {
                    ["fingerprint"] = "follow-up",
                    ["nodes"] = new JArray(
                        new JObject { ["id"] = "n000001", ["path"] = "Root/[Items]", ["name"] = "[Items]" },
                        new JObject { ["id"] = "n000002", ["path"] = "Root/[Items]/[Task_1]", ["name"] = "[Task_1]" },
                        new JObject { ["id"] = "n000003", ["path"] = "Root/[Items]/[Task_2]", ["name"] = "[Task_2]" },
                        new JObject { ["id"] = "n000004", ["path"] = "Root/[Other]/[Task_1]", ["name"] = "[Task_1]" }),
                    ["componentFamilyCandidates"] = new JArray(),
                }.ToString(Newtonsoft.Json.Formatting.None),
                "follow-up",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/follow-up.json");
        }
    }
}
