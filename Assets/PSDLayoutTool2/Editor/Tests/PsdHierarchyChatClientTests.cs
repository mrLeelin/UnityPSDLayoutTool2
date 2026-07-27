namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using NUnit.Framework;

    public sealed class PsdHierarchyChatClientTests
    {
        private const string SourcePsdPath = "Assets/PSDLayoutTool2/TestData/7日任务拆分.psd";
        private const string TargetPrefabPath =
            "Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab/SevenDayTaskView.prefab";

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
            Assert.That(context.BuildInstructions(), Does.Contain("===== BEGIN TARGET PREFAB YAML ====="));
            Assert.That(context.BuildInstructions(), Does.Contain("SevenDayTaskView"));
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
            Assert.That(request.body, Does.Contain("Prefab Body"));
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
            Assert.That(request.body, Does.Contain("Prefab Body"));
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
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Not.Contain("已经修改本地文件"));
        }

        [Test]
        public void ChatContextRequiresTheExistingPrefabToBeOrganizedInPlace()
        {
            string instructions = CreateSmallContext().BuildInstructions();

            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("仅修改当前目标 Prefab"));
            Assert.That(PsdHierarchyChatClient.DefaultUserPrompt, Does.Contain("本次不要新建、复制、抽取、嵌套或另存为任何 Prefab"));
            Assert.That(instructions, Does.Contain("The only allowed output mode is in_place"));
            Assert.That(instructions, Does.Contain("already confirmed for in-place cleanup"));
            Assert.That(instructions, Does.Contain(".cleaned.prefab"));
            Assert.That(instructions, Does.Contain("This chat request does not authorize component extraction"));
            Assert.That(instructions, Does.Contain("Return an auditable analysis summary"));
            Assert.That(instructions, Does.Contain("风险与保留项"));
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
                "Prefab Body");
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
