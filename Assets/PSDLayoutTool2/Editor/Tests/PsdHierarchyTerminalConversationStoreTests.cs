namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using PsdLayoutTool2;

    public sealed class PsdHierarchyTerminalConversationStoreTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PsdHierarchyConversationTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void AppendEventWritesUtf8JsonLinesAndReadsTheLastCompleteEvent()
        {
            string path = PsdHierarchyTerminalConversationStore.BuildConversationPath(directory, "session-1");
            PsdHierarchyTerminalConversationStore.AppendEvent(path, "user_message", "user", "中文问题");
            PsdHierarchyTerminalConversationStore.AppendEvent(path, "assistant_message", "assistant", "已保存");
            File.AppendAllText(path, "{\"type\":\"truncated", new UTF8Encoding(false));

            Assert.That(
                PsdHierarchyTerminalConversationStore.TryReadLastValidEvent(path, out JObject last),
                Is.True);
            Assert.That(last.Value<string>("type"), Is.EqualTo("assistant_message"));
            Assert.That(last.Value<string>("content"), Is.EqualTo("已保存"));
        }

        [Test]
        public void RecoveryPromptPinsAllDurableArtifactsAndRequiresSummaryUpdates()
        {
            string prompt = PsdHierarchyTerminalConversationStore.BuildRecoveryPrompt(
                @"E:\Project\Library\PsdHierarchyTerminal\abc.conversation.jsonl",
                @"E:\Project\Library\PsdHierarchyTerminal\abc.terminal.log",
                @"E:\Project\Library\PsdHierarchyTerminal\abc.summary.md",
                true);

            Assert.That(prompt, Does.Contain("abc.conversation.jsonl"));
            Assert.That(prompt, Does.Contain("abc.terminal.log"));
            Assert.That(prompt, Does.Contain("abc.summary.md"));
            Assert.That(prompt, Does.Contain("terminal was reopened"));
            Assert.That(prompt, Does.Contain("update the summary file atomically"));
        }

        [Test]
        public void MissingConversationIsNotReportedAsRecoverable()
        {
            Assert.That(
                PsdHierarchyTerminalConversationStore.HasRecoverableArtifacts(
                    Path.Combine(directory, "missing.conversation.jsonl"),
                    Path.Combine(directory, "missing.terminal.log"),
                    Path.Combine(directory, "missing.summary.md")),
                Is.False);
        }
    }
}
