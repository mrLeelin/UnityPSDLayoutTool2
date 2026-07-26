namespace PsdLayoutTool2
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using UnityEditor;

    internal sealed class PsdHierarchyExternalAiLaunchRequest
    {
        internal string projectRoot;
        internal string sourcePsdAssetPath;
        internal string targetPrefabAssetPath;
        internal string targetPrefabFullPath;
        internal string skillFullPath;
    }

    internal static class PsdHierarchyExternalAiLauncher
    {
        internal static bool TryLaunch(
            string sourcePsdAssetPath,
            string targetPrefabAssetPath,
            PsdHierarchyExternalAiSettingsSnapshot settings,
            out string error)
        {
            PsdHierarchyExternalAiLaunchRequest request;
            if (!TryCreateRequest(sourcePsdAssetPath, targetPrefabAssetPath, settings, out request, out error))
            {
                return false;
            }

            try
            {
                string scriptPath = WriteLaunchScript(request, settings);
                Process.Start(CreateTerminalStartInfo(settings, scriptPath, request.projectRoot));
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Unable to start the configured terminal: " + exception.Message;
                return false;
            }
        }

        internal static bool TryCreateRequest(
            string sourcePsdAssetPath,
            string targetPrefabAssetPath,
            PsdHierarchyExternalAiSettingsSnapshot settings,
            out PsdHierarchyExternalAiLaunchRequest request,
            out string error)
        {
            request = null;
            if (!settings.TryValidate(out error))
            {
                return false;
            }

            DirectoryInfo projectDirectory = Directory.GetParent(UnityEngine.Application.dataPath);
            if (projectDirectory == null)
            {
                error = "Unable to resolve the Unity project root.";
                return false;
            }

            string projectRoot = projectDirectory.FullName;
            string prefabFullPath = ToFullPath(projectRoot, targetPrefabAssetPath);
            if (!File.Exists(prefabFullPath))
            {
                error = "Prefab不存在，请先生成Prefab。";
                return false;
            }

            string skillFullPath = ToFullPath(projectRoot, settings.skillPath);
            if (!File.Exists(skillFullPath))
            {
                error = "AI整理技能不存在：" + skillFullPath;
                return false;
            }

            request = new PsdHierarchyExternalAiLaunchRequest
            {
                projectRoot = projectRoot,
                sourcePsdAssetPath = NormalizeAssetPath(sourcePsdAssetPath),
                targetPrefabAssetPath = NormalizeAssetPath(targetPrefabAssetPath),
                targetPrefabFullPath = prefabFullPath,
                skillFullPath = skillFullPath
            };
            error = string.Empty;
            return true;
        }

        internal static string BuildPrompt(PsdHierarchyExternalAiLaunchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return
                "Use $prefab-hierarchy-cleanup. Read and follow the hierarchy organization skill before making any change.\n" +
                "Skill: " + request.skillFullPath + "\n" +
                "Target Prefab: " + request.targetPrefabFullPath + "\n" +
                "Source PSD: " + request.sourcePsdAssetPath + "\n\n" +
                "First inspect the existing Target Prefab and present a complete cleanup plan. " +
                "Do not modify the Prefab until the user explicitly confirms that plan in this terminal. " +
                "When confirmed, organize the existing Target Prefab in place. Do not create a replacement Prefab or move it. " +
                "Preserve its visual layout, generated assets, bindings, and unrelated components. " +
                "Apply the skill rules to the target and report the files changed when finished.";
        }

        internal static string BuildScriptContent(
            PsdHierarchyExternalAiSettingsSnapshot settings,
            PsdHierarchyExternalAiLaunchRequest request)
        {
            string prompt = BuildPrompt(request);
            switch (settings.terminal)
            {
                case PsdHierarchyAiTerminal.PowerShell:
                    return BuildPowerShellScript(settings, request.projectRoot, prompt);
                case PsdHierarchyAiTerminal.CommandPrompt:
                    return BuildCommandPromptScript(settings, request.projectRoot, prompt);
                case PsdHierarchyAiTerminal.GitBash:
                case PsdHierarchyAiTerminal.MacTerminal:
                    return BuildBashScript(settings, request.projectRoot, prompt);
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.terminal), settings.terminal,
                        "Unsupported terminal.");
            }
        }

        internal static ProcessStartInfo CreateTerminalStartInfo(
            PsdHierarchyExternalAiSettingsSnapshot settings,
            string scriptPath,
            string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(scriptPath)) throw new ArgumentException("Script path is required.", nameof(scriptPath));
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));

            switch (settings.terminal)
            {
                case PsdHierarchyAiTerminal.PowerShell:
                    return new ProcessStartInfo
                    {
                        FileName = ResolveTerminalExecutable(settings, "powershell.exe"),
                        Arguments = "-NoExit -ExecutionPolicy Bypass -File " + QuoteProcessArgument(scriptPath),
                        WorkingDirectory = projectRoot,
                        UseShellExecute = true
                    };
                case PsdHierarchyAiTerminal.CommandPrompt:
                    return new ProcessStartInfo
                    {
                        FileName = ResolveTerminalExecutable(settings, "cmd.exe"),
                        Arguments = "/K " + QuoteProcessArgument(scriptPath),
                        WorkingDirectory = projectRoot,
                        UseShellExecute = true
                    };
                case PsdHierarchyAiTerminal.GitBash:
                    return new ProcessStartInfo
                    {
                        FileName = ResolveTerminalExecutable(settings, DefaultGitBashPath()),
                        Arguments = "--login -i " + QuoteProcessArgument(scriptPath),
                        WorkingDirectory = projectRoot,
                        UseShellExecute = true
                    };
                case PsdHierarchyAiTerminal.MacTerminal:
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        throw new PlatformNotSupportedException("macOS Terminal can only be used on macOS.");
                    }

                    string command = "bash " + QuoteBashLiteral(scriptPath);
                    string runScript = "tell application \"Terminal\" to do script " + QuoteAppleScriptString(command);
                    return new ProcessStartInfo
                    {
                        FileName = ResolveTerminalExecutable(settings, "/usr/bin/osascript"),
                        Arguments = "-e " + QuoteProcessArgument(runScript) +
                                    " -e " + QuoteProcessArgument("tell application \"Terminal\" to activate"),
                        WorkingDirectory = projectRoot,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.terminal), settings.terminal,
                        "Unsupported terminal.");
            }
        }

        private static string WriteLaunchScript(
            PsdHierarchyExternalAiLaunchRequest request,
            PsdHierarchyExternalAiSettingsSnapshot settings)
        {
            string folder = Path.Combine(request.projectRoot, "Library", "PSDLayoutTool2", "AiOrganizer");
            Directory.CreateDirectory(folder);
            string extension = settings.terminal == PsdHierarchyAiTerminal.PowerShell
                ? ".ps1"
                : settings.terminal == PsdHierarchyAiTerminal.CommandPrompt
                    ? ".cmd"
                    : ".sh";
            string path = Path.Combine(folder, "ai-organize-" + Guid.NewGuid().ToString("N") + extension);
            File.WriteAllText(path, BuildScriptContent(settings, request), new UTF8Encoding(false));
            return path;
        }

        private static string BuildPowerShellScript(
            PsdHierarchyExternalAiSettingsSnapshot settings,
            string projectRoot,
            string prompt)
        {
            var builder = new StringBuilder();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine("Set-Location -LiteralPath " + QuotePowerShellLiteral(projectRoot));
            builder.Append("& " + QuotePowerShellLiteral(settings.aiCommand));
            AppendRawArguments(builder, settings.aiArguments);
            builder.AppendLine(" " + QuotePowerShellLiteral(prompt));
            builder.AppendLine("if ($LASTEXITCODE -ne 0) { Write-Host ('AI command exited with code ' + $LASTEXITCODE) }");
            builder.AppendLine("Read-Host 'Press Enter to close'");
            return builder.ToString();
        }

        private static string BuildCommandPromptScript(
            PsdHierarchyExternalAiSettingsSnapshot settings,
            string projectRoot,
            string prompt)
        {
            var builder = new StringBuilder();
            builder.AppendLine("@echo off");
            builder.AppendLine("cd /d " + QuoteCommandPromptArgument(projectRoot));
            builder.Append("call " + QuoteCommandPromptArgument(settings.aiCommand));
            AppendRawArguments(builder, settings.aiArguments);
            builder.AppendLine(" " + QuoteCommandPromptArgument(prompt));
            builder.AppendLine("if errorlevel 1 echo AI command exited with code %errorlevel%");
            builder.AppendLine("pause");
            return builder.ToString();
        }

        private static string BuildBashScript(
            PsdHierarchyExternalAiSettingsSnapshot settings,
            string projectRoot,
            string prompt)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#!/usr/bin/env bash");
            builder.AppendLine("cd -- " + QuoteBashLiteral(projectRoot));
            builder.Append(QuoteBashLiteral(settings.aiCommand));
            AppendRawArguments(builder, settings.aiArguments);
            builder.AppendLine(" " + QuoteBashLiteral(prompt));
            builder.AppendLine("status=$?");
            builder.AppendLine("if [ $status -ne 0 ]; then printf 'AI command exited with code %s\\n' $status; fi");
            builder.AppendLine("exec bash -i");
            return builder.ToString();
        }

        private static void AppendRawArguments(StringBuilder builder, string arguments)
        {
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                builder.Append(" ");
                builder.Append(arguments.Trim());
            }
        }

        private static string ResolveTerminalExecutable(
            PsdHierarchyExternalAiSettingsSnapshot settings,
            string defaultExecutable)
        {
            return string.IsNullOrWhiteSpace(settings.terminalExecutablePath)
                ? defaultExecutable
                : settings.terminalExecutablePath;
        }

        private static string DefaultGitBashPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Git",
                "bin",
                "bash.exe");
        }

        private static string ToFullPath(string projectRoot, string path)
        {
            string candidate = path ?? string.Empty;
            if (!Path.IsPathRooted(candidate))
            {
                candidate = Path.Combine(projectRoot, candidate.Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.GetFullPath(candidate);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string QuotePowerShellLiteral(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        private static string QuoteCommandPromptArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteBashLiteral(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\\\"'\\\"'") + "'";
        }

        private static string QuoteAppleScriptString(string value)
        {
            return "\"" + (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteProcessArgument(string value)
        {
            value = value ?? string.Empty;
            if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '\"' }) < 0)
            {
                return value;
            }

            var result = new StringBuilder("\"");
            int slashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    slashCount++;
                    continue;
                }

                if (character == '\"')
                {
                    result.Append('\\', slashCount * 2 + 1);
                    result.Append(character);
                    slashCount = 0;
                    continue;
                }

                result.Append('\\', slashCount);
                result.Append(character);
                slashCount = 0;
            }

            result.Append('\\', slashCount * 2);
            result.Append('\"');
            return result.ToString();
        }
    }
}
