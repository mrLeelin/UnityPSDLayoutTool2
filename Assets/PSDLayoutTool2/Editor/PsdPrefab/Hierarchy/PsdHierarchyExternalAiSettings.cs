namespace PsdLayoutTool2
{
    using System;
    using UnityEngine;

    public enum PsdHierarchyAiTerminal
    {
        PowerShell,
        CommandPrompt,
        GitBash,
        MacTerminal,
    }

    internal readonly struct PsdHierarchyExternalAiSettingsSnapshot
    {
        internal PsdHierarchyExternalAiSettingsSnapshot(
            PsdHierarchyAiTerminal terminal,
            string terminalExecutablePath,
            string aiCommand,
            string aiArguments,
            string skillPath)
        {
            this.terminal = terminal;
            this.terminalExecutablePath = terminalExecutablePath ?? string.Empty;
            this.aiCommand = aiCommand ?? string.Empty;
            this.aiArguments = aiArguments ?? string.Empty;
            this.skillPath = skillPath ?? string.Empty;
        }

        internal readonly PsdHierarchyAiTerminal terminal;
        internal readonly string terminalExecutablePath;
        internal readonly string aiCommand;
        internal readonly string aiArguments;
        internal readonly string skillPath;

        internal bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(aiCommand))
            {
                error = "AI command is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(skillPath))
            {
                error = "AI organization skill path is required.";
                return false;
            }

            if (ContainsLineBreak(aiCommand) ||
                ContainsLineBreak(aiArguments) ||
                ContainsLineBreak(skillPath) ||
                ContainsLineBreak(terminalExecutablePath))
            {
                error = "Terminal and AI command settings must be single-line values.";
                return false;
            }

            if (terminal != PsdHierarchyAiTerminal.PowerShell &&
                terminal != PsdHierarchyAiTerminal.CommandPrompt &&
                terminal != PsdHierarchyAiTerminal.GitBash &&
                terminal != PsdHierarchyAiTerminal.MacTerminal)
            {
                error = "The configured terminal is unsupported.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ContainsLineBreak(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0);
        }
    }

    [Serializable]
    internal sealed class PsdHierarchyExternalAiSettings
    {
        internal const string DefaultSkillPath =
            "Assets/UnityPSDLayoutTool2/.agents/skills/prefab-hierarchy-cleanup/SKILL.md";

        [SerializeField]
        private PsdHierarchyAiTerminal terminal = PsdHierarchyAiTerminal.PowerShell;

        [SerializeField]
        private string terminalExecutablePath = string.Empty;

        [SerializeField]
        private string aiCommand = "codex";

        [SerializeField]
        private string aiArguments = string.Empty;

        [SerializeField]
        private string skillPath = DefaultSkillPath;

        internal PsdHierarchyExternalAiSettingsSnapshot Resolve()
        {
            return new PsdHierarchyExternalAiSettingsSnapshot(
                terminal,
                (terminalExecutablePath ?? string.Empty).Trim(),
                (aiCommand ?? string.Empty).Trim(),
                (aiArguments ?? string.Empty).Trim(),
                (skillPath ?? string.Empty).Trim());
        }

        internal bool Set(
            PsdHierarchyAiTerminal newTerminal,
            string newTerminalExecutablePath,
            string newAiCommand,
            string newAiArguments,
            string newSkillPath)
        {
            var candidate = new PsdHierarchyExternalAiSettingsSnapshot(
                newTerminal,
                (newTerminalExecutablePath ?? string.Empty).Trim(),
                (newAiCommand ?? string.Empty).Trim(),
                (newAiArguments ?? string.Empty).Trim(),
                (newSkillPath ?? string.Empty).Trim());
            if (!candidate.TryValidate(out string error))
            {
                throw new ArgumentException(error);
            }

            if (terminal == candidate.terminal &&
                string.Equals(terminalExecutablePath, candidate.terminalExecutablePath, StringComparison.Ordinal) &&
                string.Equals(aiCommand, candidate.aiCommand, StringComparison.Ordinal) &&
                string.Equals(aiArguments, candidate.aiArguments, StringComparison.Ordinal) &&
                string.Equals(skillPath, candidate.skillPath, StringComparison.Ordinal))
            {
                return false;
            }

            terminal = candidate.terminal;
            terminalExecutablePath = candidate.terminalExecutablePath;
            aiCommand = candidate.aiCommand;
            aiArguments = candidate.aiArguments;
            skillPath = candidate.skillPath;
            return true;
        }
    }
}
