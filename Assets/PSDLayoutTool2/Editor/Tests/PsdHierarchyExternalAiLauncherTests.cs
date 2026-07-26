namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdHierarchyExternalAiLauncherTests
    {
        [Test]
        public void AvailabilityUsesOnlyTheConfiguredPrefabPath()
        {
            string target;
            string error;
            bool available = PsdHierarchyOrganizerEntry.TryResolvePrefabAvailability(
                "Assets/UI/Daily.psd",
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                "Generated",
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                path => path == "Assets/UI/Generated/Prefab/Daily.prefab",
                out target,
                out error);

            Assert.That(available, Is.True);
            Assert.That(target, Is.EqualTo("Assets/UI/Generated/Prefab/Daily.prefab"));
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void MissingPrefabProducesGenerateFirstMessage()
        {
            string target;
            string error;
            bool available = PsdHierarchyOrganizerEntry.TryResolvePrefabAvailability(
                "Assets/UI/Daily.psd",
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                "Generated",
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                path => false,
                out target,
                out error);

            Assert.That(available, Is.False);
            Assert.That(target, Is.EqualTo("Assets/UI/Generated/Prefab/Daily.prefab"));
            Assert.That(error, Does.Contain("Prefab不存在"));
        }

        [Test]
        public void UniqueSemanticPrefabInGeneratedFolderRemainsAvailable()
        {
            const string psdPath = "Assets/PSDLayoutTool2/TestData/7日任务拆分.psd";
            const string expectedPrefabPath =
                "Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab/SevenDayTaskView.prefab";

            bool available = PsdHierarchyOrganizerEntry.TryResolvePrefabAvailability(
                psdPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null,
                out string target,
                out string error);

            Assert.That(available, Is.True, error);
            Assert.That(target, Is.EqualTo(expectedPrefabPath));
        }

        [Test]
        public void UniqueDirectFallbackExcludesNestedCommonPrefab()
        {
            bool selected = PsdHierarchyOrganizerEntry.TrySelectUniqueDirectPrefabFallback(
                "Assets/UI/Daily/Prefab/Daily.prefab",
                new List<string>
                {
                    "Assets/UI/Daily/Prefab/SemanticDailyView.prefab",
                    "Assets/UI/Daily/Prefab/Common/DailyItem.prefab",
                },
                path => true,
                out string target);

            Assert.That(selected, Is.True);
            Assert.That(target, Is.EqualTo("Assets/UI/Daily/Prefab/SemanticDailyView.prefab"));
        }

        [Test]
        public void MultipleDirectFallbacksRemainUnavailable()
        {
            bool selected = PsdHierarchyOrganizerEntry.TrySelectUniqueDirectPrefabFallback(
                "Assets/UI/Daily/Prefab/Daily.prefab",
                new[]
                {
                    "Assets/UI/Daily/Prefab/SemanticDailyView.prefab",
                    "Assets/UI/Daily/Prefab/Daily.cleaned.prefab",
                },
                path => true,
                out string target);

            Assert.That(selected, Is.False);
            Assert.That(target, Is.Empty);
        }

        [Test]
        public void PromptContainsSkillAndPrefabPaths()
        {
            var request = new PsdHierarchyExternalAiLaunchRequest
            {
                sourcePsdAssetPath = "Assets/UI/Daily.psd",
                targetPrefabFullPath = "E:/Project/Assets/UI/Generated/Daily.prefab",
                skillFullPath = "E:/Project/.agents/skills/prefab-hierarchy-cleanup/SKILL.md"
            };

            string prompt = PsdHierarchyExternalAiLauncher.BuildPrompt(request);

            Assert.That(prompt, Does.Contain(request.targetPrefabFullPath));
            Assert.That(prompt, Does.Contain(request.skillFullPath));
            Assert.That(prompt, Does.Contain("$prefab-hierarchy-cleanup"));
            Assert.That(prompt, Does.Contain("Do not modify"));
            Assert.That(prompt, Does.Contain("in place"));
        }

        [TestCase(PsdHierarchyAiTerminal.PowerShell, "powershell.exe", "-NoExit")]
        [TestCase(PsdHierarchyAiTerminal.CommandPrompt, "cmd.exe", "/K")]
        [TestCase(PsdHierarchyAiTerminal.GitBash, "C:/Tools/bash.exe", "--login")]
        public void WindowsTerminalStartInfoUsesConfiguredTerminal(
            PsdHierarchyAiTerminal terminal,
            string expectedExecutable,
            string expectedArgument)
        {
            string terminalPath = terminal == PsdHierarchyAiTerminal.GitBash
                ? expectedExecutable
                : string.Empty;
            var settings = new PsdHierarchyExternalAiSettingsSnapshot(
                terminal,
                terminalPath,
                "codex",
                string.Empty,
                ".agents/skills/prefab-hierarchy-cleanup/SKILL.md");

            System.Diagnostics.ProcessStartInfo info =
                PsdHierarchyExternalAiLauncher.CreateTerminalStartInfo(
                    settings,
                    "E:/Project/Library/PSDLayoutTool2/AiOrganizer/task.ps1",
                    "E:/Project");

            Assert.That(info.FileName, Is.EqualTo(expectedExecutable));
            Assert.That(info.Arguments, Does.Contain(expectedArgument));
            Assert.That(info.UseShellExecute, Is.True);
        }

        [Test]
        public void ScriptKeepsConfiguredArgumentsAndPassesPrompt()
        {
            var settings = new PsdHierarchyExternalAiSettingsSnapshot(
                PsdHierarchyAiTerminal.CommandPrompt,
                string.Empty,
                "codex",
                "--model gpt-5.6",
                ".agents/skills/prefab-hierarchy-cleanup/SKILL.md");
            var request = new PsdHierarchyExternalAiLaunchRequest
            {
                projectRoot = "E:/Project",
                sourcePsdAssetPath = "Assets/UI/Daily.psd",
                targetPrefabFullPath = "E:/Project/Assets/UI/Daily.prefab",
                skillFullPath = "E:/Project/.agents/skills/prefab-hierarchy-cleanup/SKILL.md"
            };

            string script = PsdHierarchyExternalAiLauncher.BuildScriptContent(settings, request);

            Assert.That(script, Does.Contain("codex"));
            Assert.That(script, Does.Contain("--model gpt-5.6"));
            Assert.That(script, Does.Contain(request.targetPrefabFullPath));
            Assert.That(script, Does.Contain(request.skillFullPath));
        }

        [Test]
        public void MacTerminalUsesBashScriptWithTheSameTaskContext()
        {
            var settings = new PsdHierarchyExternalAiSettingsSnapshot(
                PsdHierarchyAiTerminal.MacTerminal,
                string.Empty,
                "codex",
                string.Empty,
                ".agents/skills/prefab-hierarchy-cleanup/SKILL.md");
            var request = new PsdHierarchyExternalAiLaunchRequest
            {
                projectRoot = "/Users/test/Project",
                sourcePsdAssetPath = "Assets/UI/Daily.psd",
                targetPrefabFullPath = "/Users/test/Project/Assets/UI/Daily.prefab",
                skillFullPath = "/Users/test/Project/.agents/skills/prefab-hierarchy-cleanup/SKILL.md"
            };

            string script = PsdHierarchyExternalAiLauncher.BuildScriptContent(settings, request);

            Assert.That(script, Does.StartWith("#!/usr/bin/env bash"));
            Assert.That(script, Does.Contain(request.targetPrefabFullPath));
            Assert.That(script, Does.Contain(request.skillFullPath));
        }

        [Test]
        public void ProjectSettingsPersistExternalTerminalConfiguration()
        {
            PsdLayoutProjectSettings projectSettings =
                UnityEngine.ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            try
            {
                projectSettings.SetExternalAiSettings(
                    PsdHierarchyAiTerminal.GitBash,
                    "C:/Tools/Git/bin/bash.exe",
                    "claude",
                    "--dangerously-skip-permissions",
                    "Assets/UnityPSDLayoutTool2/.agents/skills/prefab-hierarchy-cleanup/SKILL.md");

                PsdHierarchyExternalAiSettingsSnapshot snapshot =
                    projectSettings.ResolveExternalAiSettings();
                Assert.That(snapshot.terminal, Is.EqualTo(PsdHierarchyAiTerminal.GitBash));
                Assert.That(snapshot.terminalExecutablePath, Is.EqualTo("C:/Tools/Git/bin/bash.exe"));
                Assert.That(snapshot.aiCommand, Is.EqualTo("claude"));
                Assert.That(snapshot.aiArguments, Is.EqualTo("--dangerously-skip-permissions"));
                Assert.That(snapshot.skillPath, Does.EndWith("prefab-hierarchy-cleanup/SKILL.md"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectSettings);
            }
        }

        [Test]
        public void ExternalAiSettingsRejectLineBreaks()
        {
            var settings = new PsdHierarchyExternalAiSettingsSnapshot(
                PsdHierarchyAiTerminal.PowerShell,
                string.Empty,
                "codex\nmalicious",
                string.Empty,
                ".agents/skills/prefab-hierarchy-cleanup/SKILL.md");

            Assert.That(settings.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("single-line"));
        }
    }
}
