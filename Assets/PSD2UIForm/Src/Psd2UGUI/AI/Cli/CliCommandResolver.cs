using System;
using System.Collections.Generic;
using System.IO;

namespace CliCommandResolverNamespace
{
    internal static class CliCommandResolver
    {
        internal readonly struct CliLaunchInfo
        {
            internal readonly string ExecutablePath;

            internal readonly string Arguments;

            internal readonly bool RedirectStandardInput;

            internal static object s_ObfuscationSentinel;

            internal CliLaunchInfo(string text, string text2, bool enabled)
            {
                ExecutablePath = text;
                Arguments = text2 ?? string.Empty;
                RedirectStandardInput = enabled;
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static object GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }
internal static bool TryResolveLaunch(object text3, object text4, object text5, out CliLaunchInfo result, out string result2)
        {
            result = default(CliLaunchInfo);
            result2 = null;
            if (!string.IsNullOrWhiteSpace((string)text3))
            {
                string text = ResolveExecutablePath(text3);
                if (string.IsNullOrWhiteSpace(text))
                {
                    result2 = "未找到 CLI 命令: " + (string)text3;
                    return false;
                }
                string text2 = Path.GetExtension(text).ToLowerInvariant();
                if (IsWindows())
                {
                    switch (text2)
                    {
                    case ".ps1":
                        result = new CliLaunchInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Get-Content -Raw -LiteralPath '" + EscapePowerShellSingleQuotedString(text5) + "' | & '" + EscapePowerShellSingleQuotedString(text) + "' " + (string)text4 + "\"", false);
                        return true;
                    case ".cmd":
                    case ".bat":
                        result = new CliLaunchInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", "/d /s /c \"\"" + text + "\" " + (string)text4 + " < \"" + (string)text5 + "\"\"", false);
                        return true;
                    }
                }
                result = new CliLaunchInfo(text, (string)text4, true);
                return true;
            }
            result2 = "CLI command name is empty.";
            return false;
        }

        private static string ResolveExecutablePath(object value)
        {
            if (!Path.IsPathRooted((string)value) || !File.Exists((string)value))
            {
                string[] array = BuildCandidateFileNames(value);
                foreach (string item in EnumeratePathDirectories())
                {
                    if (string.IsNullOrWhiteSpace(item) || !Directory.Exists(item))
                    {
                        continue;
                    }
                    for (int i = 0; i < array.Length; i++)
                    {
                        string text = Path.Combine(item, array[i]);
                        if (File.Exists(text))
                        {
                            return text;
                        }
                    }
                }
                return null;
            }
            return (string)value;
        }

        private static string[] BuildCandidateFileNames(object value)
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension((string)value)))
            {
                if (!IsWindows())
                {
                    return new string[2]
                    {
                        (string)value,
                        (string)value + ".sh"
                    };
                }
                return new string[5]
                {
                    (string)value + ".cmd",
                    (string)value + ".exe",
                    (string)value + ".bat",
                    (string)value + ".ps1",
                    (string)value
                };
            }
            return new string[1] { (string)value };
        }

        private static IEnumerable<string> EnumeratePathDirectories()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string item in SplitPathVariable(Environment.GetEnvironmentVariable("PATH")))
            {
                if (seen.Add(item))
                {
                    yield return item;
                }
            }
            if (!IsWindows())
            {
                yield break;
            }
            foreach (string item2 in SplitPathVariable(GetEnvironmentVariableSafe("PATH", EnvironmentVariableTarget.User)))
            {
                if (seen.Add(item2))
                {
                    yield return item2;
                }
            }
            foreach (string item3 in SplitPathVariable(GetEnvironmentVariableSafe("PATH", EnvironmentVariableTarget.Machine)))
            {
                if (seen.Add(item3))
                {
                    yield return item3;
                }
            }
        }

        private static IEnumerable<string> SplitPathVariable(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                yield break;
            }
            string[] parts = ((string)value).Split(new char[1] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string text = parts[i].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }

        private static string EscapePowerShellSingleQuotedString(object value)
        {
            if (!string.IsNullOrEmpty((string)value))
            {
                return ((string)value).Replace("'", "''");
            }
            return string.Empty;
        }

        private static string GetEnvironmentVariableSafe(object value, EnvironmentVariableTarget value2)
        {
            try
            {
                return Environment.GetEnvironmentVariable((string)value, value2);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        internal static bool IsResolverAvailable()

        {

            return true;

        }
}
}
