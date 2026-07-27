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
        public void CustomApiRequiresHttpEndpoint()
        {
            var settings = new PsdHierarchyAiSettings();

            Assert.Throws<ArgumentException>(() => settings.Set(
                PsdHierarchyAiProvider.Codex,
                PsdHierarchyAiConnectionMode.CustomApi,
                "not-a-url",
                "gpt-5"));
        }

        [Test]
        public void CustomApiUsesSelectedProviderDefaultsWhenFieldsAreBlank()
        {
            var settings = new PsdHierarchyAiSettings();
            Assert.That(settings.Set(
                PsdHierarchyAiProvider.Claude,
                PsdHierarchyAiConnectionMode.CustomApi,
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
            Assert.That(invocation.writePromptToStandardInput, Is.False);
        }
    }
}
