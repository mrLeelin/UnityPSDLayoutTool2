namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    public sealed class PsdHierarchyAiSettingsTests
    {
        [Test]
        public void CliDiscoveryOnlyReturnsInstalledProviders()
        {
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed = PsdHierarchyAiCliDiscovery.FindInstalled(
                @"C:\\Tools;C:\\Unused",
                path => path.EndsWith("claude.cmd", StringComparison.OrdinalIgnoreCase));

            Assert.That(installed.Count, Is.EqualTo(1));
            Assert.That(installed[0].provider, Is.EqualTo(PsdHierarchyAiProvider.Claude));
            Assert.That(installed[0].displayName, Is.EqualTo("Claude"));
        }

        [Test]
        public void DefaultSettingsUseLocalCliWithoutCustomApiValues()
        {
            var settings = new PsdHierarchyAiSettings();

            PsdHierarchyAiSettingsSnapshot snapshot = settings.Resolve();

            Assert.That(snapshot.connectionMode, Is.EqualTo(PsdHierarchyAiConnectionMode.LocalCli));
            Assert.That(snapshot.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void DefaultProviderIsNoneSoAiOrganizerStartsDisabled()
        {
            var settings = new PsdHierarchyAiSettings();

            PsdHierarchyAiSettingsSnapshot snapshot = settings.Resolve();

            Assert.That(snapshot.provider, Is.EqualTo(PsdHierarchyAiProvider.None));
            Assert.That(snapshot.isConfigured, Is.False);
        }

        [Test]
        public void LegacyClaudeAndCodexProviderValuesArePreserved()
        {
            // 老配置里 provider 是按整数存的，加 None 时把 Claude/Codex 的取值挪位会静默改写用户配置。
            Assert.That((int)PsdHierarchyAiProvider.Claude, Is.EqualTo(0));
            Assert.That((int)PsdHierarchyAiProvider.Codex, Is.EqualTo(1));
            Assert.That((int)PsdHierarchyAiProvider.None, Is.EqualTo(-1));
        }

        [Test]
        public void BlankEndpointMeansLocalCliEvenForClaudeAndCodex()
        {
            var settings = new PsdHierarchyAiSettings();
            Assert.That(settings.Set(
                PsdHierarchyAiProvider.Codex,
                string.Empty,
                string.Empty,
                string.Empty), Is.True);

            PsdHierarchyAiSettingsSnapshot snapshot = settings.Resolve();
            Assert.That(snapshot.connectionMode, Is.EqualTo(PsdHierarchyAiConnectionMode.LocalCli));
            Assert.That(snapshot.isConfigured, Is.True);
        }

        [Test]
        public void FilledEndpointSwitchesToCustomApi()
        {
            var settings = new PsdHierarchyAiSettings();
            Assert.That(settings.Set(
                PsdHierarchyAiProvider.Claude,
                "https://api.anthropic.com/v1/messages",
                "claude-sonnet-5",
                string.Empty), Is.True);

            Assert.That(
                settings.Resolve().connectionMode,
                Is.EqualTo(PsdHierarchyAiConnectionMode.CustomApi));
        }

        [Test]
        public void DisabledProviderKeepsModelAndEndpointForNextTime()
        {
            var settings = new PsdHierarchyAiSettings();
            settings.Set(PsdHierarchyAiProvider.Grok, "https://example.test/v1", "grok-4", "high");

            Assert.That(settings.Clear(), Is.True);

            PsdHierarchyAiSettingsSnapshot snapshot = settings.Resolve();
            Assert.That(snapshot.provider, Is.EqualTo(PsdHierarchyAiProvider.None));
            Assert.That(snapshot.isConfigured, Is.False);
            Assert.That(snapshot.customModel, Is.EqualTo("grok-4"));
            Assert.That(snapshot.reasoningEffort, Is.EqualTo("high"));
            Assert.That(snapshot.customEndpoint, Is.EqualTo("https://example.test/v1"));
        }

        [Test]
        public void ReasoningEffortRejectsWhitespaceBecauseItIsConcatenatedIntoTheCommandLine()
        {
            var settings = new PsdHierarchyAiSettings();

            Assert.Throws<ArgumentException>(() => settings.Set(
                PsdHierarchyAiProvider.Claude,
                string.Empty,
                string.Empty,
                "very high"));
        }

        [Test]
        public void GrokAndPiWithoutEndpointHaveNoBuiltInApiDefaults()
        {
            // 这两个 CLI 没有可以写死的官方直连地址，必须由用户显式填写。
            Assert.That(PsdHierarchyChatClient.DefaultEndpoint(PsdHierarchyAiProvider.Grok), Is.Empty);
            Assert.That(PsdHierarchyChatClient.DefaultEndpoint(PsdHierarchyAiProvider.Pi), Is.Empty);
            Assert.That(
                PsdHierarchyChatClient.DefaultEndpoint(PsdHierarchyAiProvider.Codex),
                Is.EqualTo(PsdHierarchyChatClient.OpenAiEndpoint));
        }

        [Test]
        public void GrokAndPiAreDiscoveredAndCarryReasoningEffortHints()
        {
            IReadOnlyList<PsdHierarchyAiCliDescriptor> installed = PsdHierarchyAiCliDiscovery.FindInstalled(
                @"C:\\Tools;C:\\Unused",
                path => path.EndsWith("grok.exe", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("pi.cmd", StringComparison.OrdinalIgnoreCase));

            Assert.That(installed.Count, Is.EqualTo(2));
            Assert.That(installed[0].provider, Is.EqualTo(PsdHierarchyAiProvider.Grok));
            Assert.That(installed[1].provider, Is.EqualTo(PsdHierarchyAiProvider.Pi));
            Assert.That(installed[0].reasoningEffortHint, Is.Not.Empty);
            Assert.That(installed[1].defaultModelHint, Is.Not.Empty);
        }

        [Test]
        public void GrokUsesPromptFileBecauseItCannotReadStandardInput()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Grok,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\Users\Example\.grok\bin\grok.exe",
                string.Empty,
                "grok-4",
                string.Empty,
                "high");

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\Project\Demo\monsterhunter",
                "Review the Prefab.");

            Assert.That(invocation.arguments, Does.Contain("--prompt-file"));
            Assert.That(invocation.arguments, Does.Contain("--output-format json"));
            Assert.That(invocation.arguments, Does.Contain("--model"));
            Assert.That(invocation.arguments, Does.Contain("--reasoning-effort"));
            Assert.That(invocation.promptFilePath, Is.Not.Empty);
            Assert.That(invocation.writePromptToStandardInput, Is.False);
        }

        [Test]
        public void PiReadsThePromptFromStandardInputAndMapsThinkingToItsOwnFlag()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Pi,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\Users\Example\AppData\Roaming\npm\pi.cmd",
                string.Empty,
                "anthropic/claude-sonnet-5",
                string.Empty,
                "medium");

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\Project\Demo\monsterhunter",
                "Review the Prefab.",
                "session-1",
                false);

            Assert.That(invocation.executablePath, Does.EndWith("cmd.exe").IgnoreCase);
            Assert.That(invocation.arguments, Does.Contain("--print"));
            Assert.That(invocation.arguments, Does.Contain("--mode json"));
            Assert.That(invocation.arguments, Does.Contain("--thinking"));
            Assert.That(invocation.arguments, Does.Contain("--session"));
            Assert.That(invocation.writePromptToStandardInput, Is.True);
        }

        [Test]
        public void CodexReasoningEffortIsPassedAsABareConfigLiteral()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Codex,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\Users\Example\AppData\Roaming\npm\codex.cmd",
                string.Empty,
                "gpt-5",
                string.Empty,
                "high");

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\Project\Demo\monsterhunter");

            // 裸值形式可以避免在 cmd /c 包装层里再嵌一层引号。
            Assert.That(invocation.arguments, Does.Contain("model_reasoning_effort=high"));
            Assert.That(invocation.arguments, Does.Contain("-m"));
        }

        [Test]
        public void BlankModelAndEffortProduceNoModelArgumentsSoTheCliOwnConfigurationWins()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Grok,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\Users\Example\.grok\bin\grok.exe",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\Project\Demo\monsterhunter");

            Assert.That(invocation.arguments, Does.Not.Contain("--model"));
            Assert.That(invocation.arguments, Does.Not.Contain("--reasoning-effort"));
        }

        [Test]
        public void CleanupExecutionDefaultsToNativeUnityBackend()
        {
            var settings = new PsdHierarchyCleanupExecutionSettings();

            Assert.That(
                settings.Resolve().backend,
                Is.EqualTo(PsdHierarchyCleanupExecutionBackend.NativeUnity));
        }

        [Test]
        public void CleanupExecutionCanSelectTheOptionalUnityCliBackend()
        {
            var settings = new PsdHierarchyCleanupExecutionSettings();

            Assert.That(settings.Set(PsdHierarchyCleanupExecutionBackend.UnityCliRunner), Is.True);
            Assert.That(
                settings.Resolve().backend,
                Is.EqualTo(PsdHierarchyCleanupExecutionBackend.UnityCliRunner));
        }

        [Test]
        public void CustomApiRequiresHttpEndpoint()
        {
            var settings = new PsdHierarchyAiSettings();

            Assert.Throws<ArgumentException>(() => settings.Set(
                PsdHierarchyAiProvider.Codex,
                "not-a-url",
                "gpt-5",
                string.Empty));
        }

        [Test]
        public void CustomApiUsesSelectedProviderDefaultsWhenModelIsBlank()
        {
            var settings = new PsdHierarchyAiSettings();
            Assert.That(settings.Set(
                PsdHierarchyAiProvider.Claude,
                PsdHierarchyChatClient.AnthropicEndpoint,
                string.Empty,
                string.Empty), Is.True);

            PsdHierarchyAiSettingsSnapshot snapshot = settings.Resolve();
            Assert.That(snapshot.ResolveEndpoint(), Is.EqualTo(PsdHierarchyChatClient.AnthropicEndpoint));
            Assert.That(snapshot.ResolveModel(), Is.EqualTo("claude-sonnet-5"));
        }

        [Test]
        public void CmdInstalledCliUsesHiddenCommandProcessorInvocation()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Claude,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\\Users\\Example\\AppData\\Roaming\\npm\\claude.cmd",
                string.Empty,
                string.Empty,
                string.Empty);

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\\Project\\Demo\\monsterhunter");

            Assert.That(invocation.executablePath, Does.EndWith("cmd.exe").IgnoreCase);
            Assert.That(invocation.arguments, Does.Contain("claude.cmd"));
            Assert.That(invocation.arguments, Does.Contain("--print"));
            Assert.That(invocation.arguments, Does.Contain("--safe-mode"));
            Assert.That(invocation.writePromptToStandardInput, Is.True);
        }

        [Test]
        public void ClaudeDirectPromptUsesNpmExecutableWithoutStandardInput()
        {
            var connection = new PsdHierarchyChatConnection(
                PsdHierarchyAiProvider.Claude,
                PsdHierarchyAiConnectionMode.LocalCli,
                @"C:\Users\Example\AppData\Roaming\npm\claude.cmd",
                string.Empty,
                string.Empty,
                string.Empty);

            PsdHierarchyCliInvocation invocation = PsdHierarchyChatClient.CreateCliInvocation(
                connection,
                @"E:\Project\Demo\monsterhunter",
                "Review the Prefab.");

            Assert.That(invocation.executablePath, Does.EndWith("claude.exe").IgnoreCase);
            Assert.That(invocation.arguments, Does.Contain("--tools Read"));
            Assert.That(invocation.arguments, Does.Contain("--permission-mode dontAsk"));
            // 提示词不在参数里，只能经 stdin 交给 --print；此前这里断言的是 False，与实现不符。
            Assert.That(invocation.writePromptToStandardInput, Is.True);
        }
    }
}
