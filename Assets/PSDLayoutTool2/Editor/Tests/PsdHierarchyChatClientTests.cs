namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    public sealed class PsdHierarchyChatClientTests
    {
        private const string SourcePsdPath = "Assets/PSDLayoutTool2/TestData/跳格子切图.psd";
        private const string TargetPrefabPath =
            "Assets/PSDLayoutTool2/TestData/跳格子切图/Prefab/跳格子切图.prefab";

        [Test]
        public void ContextBuilderReadsTheCleanupSkillAndActualTargetPrefab()
        {
            bool created = PsdHierarchyChatContextBuilder.TryCreate(
                SourcePsdPath,
                TargetPrefabPath,
                out PsdHierarchyChatContext context,
                out string error);

            Assert.That(created, Is.True, error);
            Assert.That(context.sourcePsdAssetPath, Is.EqualTo(SourcePsdPath));
            Assert.That(context.targetPrefabAssetPath, Is.EqualTo(TargetPrefabPath));
            Assert.That(context.skillContent, Does.Contain("prefab-hierarchy-cleanup"));
            Assert.That(context.prefabContent, Does.Contain("%YAML"));
            Assert.That(context.BuildInstructions(), Does.Contain("===== BEGIN TARGET PREFAB NODE SNAPSHOT ====="));
            Assert.That(context.BuildInstructions(), Does.Contain("跳格子切图"));
        }

        [Test]
        public void ContextBuilderCreatesAnAuthoritativeNodeSnapshotForTheAi()
        {
            bool created = PsdHierarchyChatContextBuilder.TryCreate(
                SourcePsdPath,
                TargetPrefabPath,
                out PsdHierarchyChatContext context,
                out string error);

            Assert.That(created, Is.True, error);
            Assert.That(context.hierarchySnapshotJson, Does.Contain("\"nodes\"").And.Contain("\"id\":\"n"));
            Assert.That(context.hierarchySnapshotJson, Does.Contain("跳格子切图"));
            Assert.That(context.hierarchySnapshotFingerprint, Is.Not.Empty);
            Assert.That(File.Exists(context.hierarchySnapshotFullPath), Is.True);
            Assert.That(context.BuildInstructions(), Does.Contain("TARGET PREFAB NODE SNAPSHOT"));
            Assert.That(context.BuildInstructions(), Does.Contain("node:<id>"));
            Assert.That(context.BuildInstructions(), Does.Not.Contain("BEGIN TARGET PREFAB YAML"));
        }

        [Test]
        public void NumberedRepeatedUnitsWithoutCommonMembersAreNotMandatoryStatefulCandidates()
        {
            var nodes = new JArray
            {
                CreateCandidateNode("n000001", string.Empty, "ExampleView", 0, 1),
                CreateCandidateNode("n000002", "n000001", "[TaskList]", 0, 3),
                CreateCandidateNode("n000003", "n000002", "[TaskItem_1]", 0, 1, 830f, 173f),
                CreateCandidateNode("n000004", "n000002", "[TaskItem_2]", 1, 1, 830f, 177f),
                CreateCandidateNode("n000005", "n000002", "[TaskItem_3]", 2, 1, 830f, 173f),
                CreateCandidateNode("n000006", "n000003", "Background", 0, 0),
                CreateCandidateNode("n000007", "n000004", "Background", 0, 0),
                CreateCandidateNode("n000008", "n000005", "LockIcon", 0, 1),
                CreateCandidateNode("n000009", "n000008", "LockOverlay", 0, 0),
            };

            JArray candidates = PsdHierarchyChatContextBuilder.BuildComponentFamilyCandidates(nodes);

            Assert.That(candidates.Count, Is.EqualTo(1));
            JObject candidate = (JObject)candidates[0];
            Assert.That(candidate.Value<string>("id"), Is.EqualTo("family_001"));
            Assert.That(candidate.Value<string>("suggestedAssetName"), Is.EqualTo("TaskItem"));
            Assert.That(candidate.Value<bool>("requiresExtraction"), Is.False);
            Assert.That(candidate.Value<string>("recommendedMode"), Is.EqualTo("variant"));
            Assert.That(candidate["parent"].Value<string>(), Is.EqualTo("node:n000002"));
            Assert.That(((JArray)candidate["sources"]).Values<string>(), Is.EqualTo(new[]
            {
                "node:n000003", "node:n000004", "node:n000005",
            }));
        }

        [Test]
        public void NumberedRepeatedUnitsWithCommonMembersRemainStatefulCandidates()
        {
            var nodes = new JArray
            {
                CreateCandidateNode("n000010", string.Empty, "ExampleView", 0, 1),
                CreateCandidateNode("n000011", "n000010", "[TaskList]", 0, 3),
                CreateCandidateNode("n000012", "n000011", "[TaskItem_1]", 0, 2),
                CreateCandidateNode("n000013", "n000011", "[TaskItem_2]", 1, 3),
                CreateCandidateNode("n000014", "n000011", "[TaskItem_3]", 2, 2),
                CreateCandidateNode("n000015", "n000012", "Background", 0, 0),
                CreateCandidateNode("n000016", "n000013", "Background", 0, 0),
                CreateCandidateNode("n000017", "n000014", "Background", 0, 0),
                CreateCandidateNode("n000018", "n000013", "RewardIcon", 1, 0),
            };

            JObject candidate = (JObject)PsdHierarchyChatContextBuilder.BuildComponentFamilyCandidates(nodes)[0];

            Assert.That(candidate.Value<bool>("requiresExtraction"), Is.True);
            Assert.That(candidate.Value<string>("recommendedMode"), Is.EqualTo("stateful"));
        }

        [Test]
        public void NumberedRepeatedUnitsIncludeMatchingBareIndexSibling()
        {
            var nodes = new JArray
            {
                CreateCandidateNode("n000020", string.Empty, "ExampleView", 0, 1),
                CreateCandidateNode("n000021", "n000020", "[Tasks]", 0, 5),
                CreateCandidateNode("n000022", "n000021", "[Task_5]", 0, 1),
                CreateCandidateNode("n000023", "n000021", "[Task_4]", 1, 1),
                CreateCandidateNode("n000024", "n000021", "[Task_3]", 2, 1),
                CreateCandidateNode("n000025", "n000021", "[Task_2]", 3, 1),
                CreateCandidateNode("n000026", "n000021", "1", 4, 1),
                CreateCandidateNode("n000027", "n000022", "State5", 0, 0),
                CreateCandidateNode("n000028", "n000023", "State4", 0, 0),
                CreateCandidateNode("n000029", "n000024", "State3", 0, 0),
                CreateCandidateNode("n000030", "n000025", "State2", 0, 0),
                CreateCandidateNode("n000031", "n000026", "State1", 0, 0),
            };

            JObject candidate = (JObject)PsdHierarchyChatContextBuilder.BuildComponentFamilyCandidates(nodes)[0];

            Assert.That(candidate.Value<string>("suggestedAssetName"), Is.EqualTo("Task"));
            Assert.That(candidate.Value<int>("instanceCount"), Is.EqualTo(5));
            Assert.That(((JArray)candidate["sources"]).Values<string>(), Is.EqualTo(new[]
            {
                "node:n000022", "node:n000023", "node:n000024", "node:n000025", "node:n000026",
            }));
        }

        [Test]
        public void CodexCustomApiRequestContainsContextAndBearerKey()
        {
            PsdHierarchyChatHttpRequest request = PsdHierarchyChatClient.BuildRequest(
                CreateSmallContext(),
                CreateCustomApiConnection(PsdHierarchyAiProvider.Codex, "gpt-5", "openai-key"),
                new List<PsdHierarchyChatMessage> { new PsdHierarchyChatMessage("user", "检查层级") });

            Assert.That(request.url, Is.EqualTo(PsdHierarchyChatClient.OpenAiEndpoint));
            Assert.That(request.GetHeader("Authorization"), Is.EqualTo("Bearer openai-key"));
            Assert.That(request.GetHeader("Content-Type"), Is.EqualTo("application/json"));
            Assert.That(request.body, Does.Contain("Skill Body"));
            Assert.That(request.body, Does.Contain("n000001").And.Contain("node:<id>"));
            Assert.That(request.body, Does.Contain("检查层级"));
            Assert.That(request.body, Does.Contain("\"store\":false"));
        }

        [Test]
        public void ClaudeCustomApiRequestContainsContextAndApiVersionHeaders()
        {
            PsdHierarchyChatHttpRequest request = PsdHierarchyChatClient.BuildRequest(
                CreateSmallContext(),
                CreateCustomApiConnection(PsdHierarchyAiProvider.Claude, "claude-sonnet-5", "anthropic-key"),
                new List<PsdHierarchyChatMessage> { new PsdHierarchyChatMessage("user", "检查层级") });

            Assert.That(request.url, Is.EqualTo(PsdHierarchyChatClient.AnthropicEndpoint));
            Assert.That(request.GetHeader("x-api-key"), Is.EqualTo("anthropic-key"));
            Assert.That(request.GetHeader("anthropic-version"), Is.EqualTo("2023-06-01"));
            Assert.That(request.body, Does.Contain("\"max_tokens\":4096"));
            Assert.That(request.body, Does.Contain("Skill Body"));
            Assert.That(request.body, Does.Contain("n000001").And.Contain("node:<id>"));
        }

        [Test]
        public async Task MissingApiKeyFailsBeforeTransportIsCalled()
        {
            var transport = new FakeTransport(new PsdHierarchyChatHttpResponse(true, 200, "{}", string.Empty));

            PsdHierarchyChatSendResult result = await PsdHierarchyChatClient.SendAsync(
                CreateSmallContext(),
                CreateCustomApiConnection(PsdHierarchyAiProvider.Codex, "gpt-5", string.Empty),
                null,
                transport);

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("API Key"));
            Assert.That(transport.called, Is.False);
        }

        [Test]
        public async Task SendParsesCodexApiTextWithoutExposingKey()
        {
            var transport = new FakeTransport(new PsdHierarchyChatHttpResponse(
                true,
                200,
                "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"完整整理方案\"}]}]}",
                string.Empty));

            PsdHierarchyChatSendResult result = await PsdHierarchyChatClient.SendAsync(
                CreateSmallContext(),
                CreateCustomApiConnection(PsdHierarchyAiProvider.Codex, "gpt-5", "secret-key"),
                null,
                transport);

            Assert.That(result.success, Is.True);
            Assert.That(result.message, Is.EqualTo("完整整理方案"));
            Assert.That(transport.request.GetHeader("Authorization"), Is.EqualTo("Bearer secret-key"));
            Assert.That(result.message, Does.Not.Contain("secret-key"));
        }

        [Test]
        public void ErrorResponseUsesProviderMessage()
        {
            PsdHierarchyChatSendResult result = PsdHierarchyChatClient.ParseResponse(
                PsdHierarchyAiProvider.Claude,
                new PsdHierarchyChatHttpResponse(
                    false,
                    401,
                    "{\"error\":{\"message\":\"invalid key\"}}",
                    "Unauthorized"));

            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("invalid key"));
        }

        [Test]
        public void BlankPromptUsesCleanupPlanDefault()
        {
            Assert.That(
                PsdHierarchyChatClient.ResolveUserPrompt(" \t\r\n "),
                Is.EqualTo(PsdHierarchyChatClient.DefaultUserPrompt));
        }

        [Test]
        public void DefaultPromptRequestsAReviewablePlan()
        {
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("完整、可确认的层级整理方案"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("完整层级、节点几何、组件、同级顺序和重复结构"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("完整的原地整理后树形结构"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("应用前必须验证"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("可审查的分析摘要"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("分组依据"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("不要输出原始内部推理"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("```json 计划代码块"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("Unity 窗口会直接更新 Prefab"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("不要声称已经修改本地文件"));
        }

        [Test]
        public void ChatContextKeepsTheExistingPrefabInPlaceAndReviewsComponentFamilies()
        {
            string instructions = CreateSmallContext().BuildInstructions();

            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("本次主界面只原地更新当前目标 Prefab"));
            Assert.That(instructions, Does.Contain("The only allowed output mode is in_place"));
            Assert.That(instructions, Does.Contain("already confirmed for in-place cleanup"));
            Assert.That(instructions, Does.Contain(".cleaned.prefab"));
            Assert.That(instructions, Does.Contain("include the complete reviewed extraction contract"));
            Assert.That(instructions, Does.Contain("Return an auditable analysis summary"));
            Assert.That(instructions, Does.Contain("风险与保留项"));
        }

        [Test]
        public void ChatContextUsesAReviewedJsonPlanForTheSecondTurnExecution()
        {
            string instructions = CreateSmallContext().BuildInstructions();

            Assert.That(instructions, Does.Contain("Do not invoke PowerShell, Python, Unity runners"));
            Assert.That(instructions, Does.Contain("exactly one complete UTF-8 JSON plan"));
            Assert.That(instructions, Does.Contain("the Unity chat window validates the JSON"));
        }

        [Test]
        public void PlanRepairPromptRequestsJsonOnlyReplacementToAvoidTruncation()
        {
            const string validationError = "计划 version 必须为 2。";

            string prompt = PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt(validationError);

            Assert.That(prompt, Does.Contain(validationError));
            Assert.That(prompt, Does.Contain("exactly one complete UTF-8 JSON plan"));
            Assert.That(prompt, Does.Contain("Do not output prose"));
            Assert.That(prompt, Does.Contain("\"version\": 2"));
            Assert.That(prompt, Does.Contain("snapshotFingerprint"));
            Assert.That(prompt, Does.Contain("```json"));
            Assert.That(prompt, Does.Contain("must be exactly @wrapperId"));
            Assert.That(prompt, Does.Contain("node:<id>"));
            Assert.That(prompt, Does.Contain("stateSourceNames").And.Contain("direct children"));
            Assert.That(prompt, Does.Contain("commonSourceNames").And.Contain("derive the other"));
            Assert.That(prompt, Does.Not.Contain("original pre-apply full path"));
        }

        [Test]
        public void PlanRepairPromptReplaysMandatoryComponentFamilyRecordsExactly()
        {
            var context = new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000010\",\"path\":\"Root/Milestones\",\"name\":\"Milestones\"}," +
                "{\"id\":\"n000011\",\"path\":\"Root/Milestones/[Milestone_0]\",\"name\":\"[Milestone_0]\",\"parentId\":\"n000010\"}," +
                "{\"id\":\"n000012\",\"path\":\"Root/Milestones/[Milestone_1]\",\"name\":\"[Milestone_1]\",\"parentId\":\"n000010\"}," +
                "{\"id\":\"n000013\",\"path\":\"Root/Milestones/[Milestone_2]\",\"name\":\"[Milestone_2]\",\"parentId\":\"n000010\"}," +
                "{\"id\":\"n000020\",\"path\":\"Root/Milestones/[Milestone_0]/Icon\",\"name\":\"Icon\",\"parentId\":\"n000011\",\"siblingIndex\":0}," +
                "{\"id\":\"n000021\",\"path\":\"Root/Milestones/[Milestone_1]/Icon\",\"name\":\"Icon\",\"parentId\":\"n000012\",\"siblingIndex\":0}," +
                "{\"id\":\"n000022\",\"path\":\"Root/Milestones/[Milestone_2]/Icon\",\"name\":\"Icon\",\"parentId\":\"n000013\",\"siblingIndex\":0}]," +
                "\"componentFamilyCandidates\":[{\"id\":\"family_002\",\"suggestedAssetName\":\"Milestone\",\"recommendedMode\":\"stateful\",\"requiresExtraction\":true,\"parent\":\"node:n000010\",\"sources\":[\"node:n000011\",\"node:n000012\",\"node:n000013\"]}]}",
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");

            string prompt = PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt(
                "componentFamilyDecisions[0] must cover family_002",
                context);

            Assert.That(prompt, Does.Contain("BEGIN REQUIRED COMPONENT FAMILIES"));
            Assert.That(prompt, Does.Contain("family_002").And.Contain("Milestone"));
            Assert.That(prompt, Does.Contain("node:n000010"));
            Assert.That(prompt, Does.Contain("node:n000011").And.Contain("node:n000012").And.Contain("node:n000013"));
            Assert.That(prompt, Does.Contain("stateful"));
            Assert.That(prompt, Does.Contain("mode must not be skip"));
            Assert.That(prompt, Does.Contain("lower_snake_case extractionId"));
        }

        [Test]
        public void PlanRepairPromptIncludesDirectChildMatrixForMandatoryStatefulFamilies()
        {
            const string snapshot =
                "{\"fingerprint\":\"snapshot-123\",\"nodes\":[" +
                "{\"id\":\"n000010\",\"path\":\"Root/Milestones\",\"name\":\"Milestones\"}," +
                "{\"id\":\"n000011\",\"path\":\"Root/Milestones/[Milestone_0]\",\"name\":\"[Milestone_0]\",\"parentId\":\"n000010\"}," +
                "{\"id\":\"n000012\",\"path\":\"Root/Milestones/[Milestone_0]/MilestoneIcon\",\"name\":\"MilestoneIcon\",\"parentId\":\"n000011\",\"siblingIndex\":0}," +
                "{\"id\":\"n000013\",\"path\":\"Root/Milestones/[Milestone_0]/MilestoneFrame\",\"name\":\"MilestoneFrame\",\"parentId\":\"n000011\",\"siblingIndex\":1}," +
                "{\"id\":\"n000014\",\"path\":\"Root/Milestones/[Milestone_0]/MilestoneValue\",\"name\":\"MilestoneValue\",\"parentId\":\"n000011\",\"siblingIndex\":2}," +
                "{\"id\":\"n000015\",\"path\":\"Root/Milestones/[Milestone_1]\",\"name\":\"[Milestone_1]\",\"parentId\":\"n000010\"}," +
                "{\"id\":\"n000016\",\"path\":\"Root/Milestones/[Milestone_1]/RewardIcon\",\"name\":\"RewardIcon\",\"parentId\":\"n000015\",\"siblingIndex\":0}," +
                "{\"id\":\"n000017\",\"path\":\"Root/Milestones/[Milestone_1]/RequiredScore\",\"name\":\"RequiredScore\",\"parentId\":\"n000015\",\"siblingIndex\":1}]," +
                "\"componentFamilyCandidates\":[{" +
                "\"id\":\"family_002\",\"suggestedAssetName\":\"Milestone\",\"recommendedMode\":\"stateful\"," +
                "\"requiresExtraction\":true,\"parent\":\"node:n000010\",\"sources\":[\"node:n000011\",\"node:n000015\"]}]}";
            var context = new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                snapshot,
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");

            string prompt = PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt(
                "snapshot has 3 direct children but the contracts require 1 Common plus 3 selected-state members",
                context);

            Assert.That(prompt, Does.Contain("directChildCount == common.members.Count + selectedState.members.Count"));
            int recordsStart = prompt.IndexOf("===== BEGIN REQUIRED COMPONENT FAMILIES =====", StringComparison.Ordinal);
            int jsonStart = prompt.IndexOf('[', recordsStart);
            int jsonEnd = prompt.IndexOf("===== END REQUIRED COMPONENT FAMILIES =====", jsonStart, StringComparison.Ordinal);
            var records = JArray.Parse(prompt.Substring(jsonStart, jsonEnd - jsonStart).Trim());
            JArray structures = (JArray)records[0]["sourceStructures"];
            Assert.That(structures.Count, Is.EqualTo(2));
            Assert.That(structures.Select(row => row.Value<string>("source")), Is.EqualTo(new[]
            {
                "node:n000011", "node:n000015",
            }));
            Assert.That(
                structures[0]["directChildren"].Values<string>(),
                Is.EqualTo(new[] { "MilestoneIcon", "MilestoneFrame", "MilestoneValue" }));
            Assert.That(
                structures[1]["directChildren"].Values<string>(),
                Is.EqualTo(new[] { "RewardIcon", "RequiredScore" }));
        }

        [Test]
        public void PlanRepairPromptRejectsMissingMandatorySourceEvidence()
        {
            var context = new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"snapshot-123\",\"nodes\":[],\"componentFamilyCandidates\":[{" +
                "\"id\":\"family_002\",\"suggestedAssetName\":\"Milestone\",\"recommendedMode\":\"stateful\"," +
                "\"requiresExtraction\":true,\"parent\":\"node:n000010\",\"sources\":[\"node:n000011\",\"node:n000015\"]}]}",
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt("invalid stateful mapping", context));

            Assert.That(exception.Message, Does.Contain("family_002").And.Contain("node:n000011"));
        }

        [Test]
        public void InitialAndRepairPromptsRequireLowerSnakeCaseWrapperIds()
        {
            string instructions = CreateSmallContext().BuildInstructions();
            string repairPrompt = PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt(
                "wrappers[1].id must be lower snake_case");

            Assert.That(instructions, Does.Contain("wrappers[].id must use lower snake_case"));
            Assert.That(instructions, Does.Contain("screen_root").And.Contain("[a-z][a-z0-9_]*"));
            Assert.That(repairPrompt, Does.Contain("wrappers[].id must use lower snake_case"));
            Assert.That(repairPrompt, Does.Contain("screen_root").And.Contain("[a-z][a-z0-9_]*"));
        }

        [Test]
        public void InvalidNodeRepairPromptForbidsInventingAnotherIdentifier()
        {
            string prompt = PsdHierarchyChatClient.BuildJsonOnlyPlanRepairPrompt(
                "moves[18].source 引用的节点 n999999 在当前快照中不存在。");

            Assert.That(prompt, Does.Contain("node IDs listed in the authoritative snapshot"));
            Assert.That(prompt, Does.Contain("Remove an operation"));
            Assert.That(prompt, Does.Contain("never invent a node ID"));
        }

        [Test]
        public void ClaudeDirectPromptSuppliesTheCanonicalExecutablePlanContract()
        {
            const string planFormat =
                "{\n" +
                "  \"version\": 2,\n" +
                "  \"snapshotFingerprint\": \"snapshot-123\",\n" +
                "  \"prefabName\": \"ExampleView\",\n" +
                "  \"wrappers\": [],\n" +
                "  \"moves\": [],\n" +
                "  \"renames\": []\n" +
                "}";
            var context = new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                planFormat,
                "{\"fingerprint\":\"snapshot-123\",\"nodes\":[{\"id\":\"n000001\",\"path\":\"Root\"}]}",
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");

            string prompt = PsdHierarchyChatClient.BuildClaudeDirectPrompt(
                context,
                new List<PsdHierarchyChatMessage>
                {
                    new PsdHierarchyChatMessage("user", "整理目标 Prefab"),
                });

            Assert.That(prompt, Does.Contain("exactly one complete UTF-8 JSON plan"));
            Assert.That(prompt, Does.Contain("\"prefabName\""));
            Assert.That(prompt, Does.Contain("\"wrappers\""));
            Assert.That(prompt, Does.Contain("\"moves\""));
            Assert.That(prompt, Does.Contain("\"renames\""));
            Assert.That(prompt, Does.Contain("wrapperCreations").And.Contain("Do not use"));
            Assert.That(prompt, Does.Contain("snapshot-123.json"));
            Assert.That(prompt, Does.Contain("node:<id>"));
            Assert.That(prompt, Does.Contain("wrappers[].id must use lower snake_case").And.Contain("screen_root"));
            Assert.That(prompt, Does.Not.Contain("Target Prefab: E:/Project"));
        }

        [Test]
        public void ConsecutiveUserMessagesAreMergedForProviderCompatibleConversation()
        {
            PsdHierarchyChatHttpRequest request = PsdHierarchyChatClient.BuildRequest(
                CreateSmallContext(),
                CreateCustomApiConnection(PsdHierarchyAiProvider.Claude, "claude-sonnet-5", "key"),
                new List<PsdHierarchyChatMessage>
                {
                    new PsdHierarchyChatMessage("user", "first"),
                    new PsdHierarchyChatMessage("user", "second"),
                });

            Assert.That(request.body, Does.Contain("first\\n\\nsecond"));
        }

        [Test]
        public void LocalCliPresentationIdentifiesTheAgentWithoutInventingAModel()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Claude,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\\Tools\\claude.cmd",
                string.Empty,
                string.Empty,
                string.Empty);

            Assert.That(PsdHierarchyChatClient.GetProviderDisplayName(connection.provider), Is.EqualTo("Claude"));
            Assert.That(PsdHierarchyChatClient.GetModelDisplayName(connection), Is.EqualTo("本地 CLI 默认"));
        }

        [Test]
        public void InteractiveClaudeInvocationResumesTheSameSession()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Claude,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\\Tools\\claude.cmd",
                string.Empty,
                string.Empty,
                string.Empty);

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateInteractiveCliInvocation(
                connection,
                @"E:\\Project\\Demo\\monsterhunter",
                "2f9f4162-1029-4c4b-9e9e-0e9627063a4b");

            Assert.That(invocation.executablePath, Does.EndWith("cmd.exe").IgnoreCase);
            Assert.That(invocation.arguments, Does.Contain("/k"));
            Assert.That(invocation.arguments, Does.Contain("--resume"));
            Assert.That(invocation.arguments, Does.Contain("2f9f4162-1029-4c4b-9e9e-0e9627063a4b"));
            Assert.That(invocation.arguments, Does.Contain("--permission-mode plan"));
        }

        [Test]
        public void ClaudeDirectInvocationStreamsRepairPromptThroughStandardInput()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Claude,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\\Tools\\claude.exe",
                string.Empty,
                string.Empty,
                string.Empty);
            string prompt = new string('x', 40000);

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\\Project\\Demo\\monsterhunter",
                prompt,
                "2f9f4162-1029-4c4b-9e9e-0e9627063a4b",
                true);

            Assert.That(invocation.arguments, Does.Contain("--resume"));
            Assert.That(invocation.arguments, Does.Contain("--tools Read"));
            Assert.That(invocation.arguments, Does.Not.Contain(prompt));
            Assert.That(invocation.writePromptToStandardInput, Is.True);
        }

        [Test]
        public async Task LocalCliSendForwardsAndRetainsTheCliSessionId()
        {
            string cliPath = Path.GetTempFileName();
            try
            {
                var transport = new FakeCliTransport(new PsdHierarchyChatSendResult(
                    true,
                    "分析摘要",
                    "2f9f4162-1029-4c4b-9e9e-0e9627063a4b"));
                PsdHierarchyChatSendResult result = await PsdHierarchyChatClient.SendWithCliSessionAsync(
                    CreateSmallContext(),
                    new PsdHierarchyChatConnection(
                        PsdHierarchyAiProvider.Codex,
                        PsdHierarchyAiConnectionMode.LocalCli,
                        cliPath,
                        string.Empty,
                        string.Empty,
                        string.Empty),
                    new List<PsdHierarchyChatMessage>
                    {
                        new PsdHierarchyChatMessage("user", "请继续分析"),
                    },
                    "c0000000-1029-4c4b-9e9e-0e9627063a4b",
                    null,
                    transport);

                Assert.That(transport.receivedSessionId, Is.EqualTo("c0000000-1029-4c4b-9e9e-0e9627063a4b"));
                Assert.That(result.success, Is.True);
                Assert.That(result.cliSessionId, Is.EqualTo("2f9f4162-1029-4c4b-9e9e-0e9627063a4b"));
            }
            finally
            {
                File.Delete(cliPath);
            }
        }

        [Test]
        public void CodexResumedInvocationUsesTheRecordedSessionId()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Codex,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\\Tools\\codex.cmd",
                string.Empty,
                string.Empty,
                string.Empty);

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\\Project\\Demo\\monsterhunter",
                "继续分析",
                "2f9f4162-1029-4c4b-9e9e-0e9627063a4b",
                true);

            Assert.That(invocation.arguments, Does.Contain("exec resume --json"));
            Assert.That(invocation.arguments, Does.Contain("2f9f4162-1029-4c4b-9e9e-0e9627063a4b"));
            Assert.That(invocation.writePromptToStandardInput, Is.True);
        }

        private sealed class FakeCliTransport : IPsdHierarchyCliChatTransport
        {
            private readonly PsdHierarchyChatSendResult response;

            internal FakeCliTransport(PsdHierarchyChatSendResult response)
            {
                this.response = response;
            }

            internal string receivedSessionId;

            public System.Threading.Tasks.Task<PsdHierarchyChatSendResult> SendAsync(
                PsdHierarchyChatContext context,
                PsdHierarchyChatConnection connection,
                IReadOnlyList<PsdHierarchyChatMessage> messages,
                string cliSessionId)
            {
                receivedSessionId = cliSessionId;
                return System.Threading.Tasks.Task.FromResult(response);
            }
        }

        private static PsdHierarchyChatContext CreateSmallContext()
        {
            return new PsdHierarchyChatContext(
                "E:/Project/Demo/monsterhunter",
                "Assets/UI/Source.psd",
                "Assets/UI/Prefab/ExampleView.prefab",
                "E:/Project/Demo/monsterhunter/Skill.md",
                "Skill Body",
                "Prefab Body",
                "Plan Format",
                "{\"fingerprint\":\"snapshot-123\",\"nodes\":[{\"id\":\"n000001\",\"path\":\"Root\",\"name\":\"Root\"}]}",
                "snapshot-123",
                "E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/HierarchySnapshots/snapshot-123.json");
        }

        private static JObject CreateCandidateNode(
            string id,
            string parentId,
            string name,
            int siblingIndex,
            int childCount,
            float width = 100f,
            float height = 100f)
        {
            return new JObject
            {
                ["id"] = id,
                ["parentId"] = parentId,
                ["name"] = name,
                ["siblingIndex"] = siblingIndex,
                ["childCount"] = childCount,
                ["components"] = new JArray("UnityEngine.RectTransform"),
                ["rect"] = new JObject
                {
                    ["anchorMin"] = new JArray(0.5f, 0.5f),
                    ["anchorMax"] = new JArray(0.5f, 0.5f),
                    ["pivot"] = new JArray(0.5f, 0.5f),
                    ["sizeDelta"] = new JArray(width, height),
                },
            };
        }

        private static PsdHierarchyChatConnection CreateCustomApiConnection(
            PsdHierarchyAiProvider provider,
            string model,
            string apiKey)
        {
            return new PsdHierarchyChatConnection(
                provider,
                PsdHierarchyAiConnectionMode.CustomApi,
                string.Empty,
                PsdHierarchyChatClient.DefaultEndpoint(provider),
                model,
                apiKey);
        }

        private sealed class FakeTransport : IPsdHierarchyChatTransport
        {
            private readonly PsdHierarchyChatHttpResponse response;

            internal FakeTransport(PsdHierarchyChatHttpResponse response)
            {
                this.response = response;
            }

            internal bool called;
            internal PsdHierarchyChatHttpRequest request;

            public Task<PsdHierarchyChatHttpResponse> SendAsync(PsdHierarchyChatHttpRequest nextRequest)
            {
                called = true;
                request = nextRequest;
                return Task.FromResult(response);
            }
        }
    }
}
