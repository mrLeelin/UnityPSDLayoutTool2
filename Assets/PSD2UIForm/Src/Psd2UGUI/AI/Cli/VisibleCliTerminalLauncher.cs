using System;
using System.Diagnostics;
using System.IO;
using AiJobFileStoreNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

using Object = UnityEngine.Object;
namespace VisibleCliTerminalLauncherNamespace
{
    internal sealed class VisibleCliTerminalLauncher
    {
        internal sealed class VisibleTerminalHandle
        {
            private readonly AiJobContext _context;

            private readonly Process _process;

            private readonly string _macWindowTitle;

            private bool _isClosed;

            private static VisibleTerminalHandle s_ObfuscationSentinel;

            internal VisibleTerminalHandle(AiJobContext aiJobContext, Process process, string text)
            {
                _context = aiJobContext;
                _process = process;
                _macWindowTitle = text;
            }

            internal void Close()
            {
                if (_isClosed)
                {
                    return;
                }
                _isClosed = true;
                try
                {
                    WriteCloseSignal(_context);
                    if ((int)Application.platform == 0)
                    {
                        CloseMacTerminalWindow(_context, _macWindowTitle);
                    }
                    if (_process != null && !_process.HasExited)
                    {
                        if (!_process.CloseMainWindow() || !_process.WaitForExit(800))
                        {
                            _process.Kill();
                        }
                        AiJobFileStore.LogDebug(_context, "Closed visible CLI debug console.");
                    }
                }
                catch (Exception ex)
                {
                    AiJobFileStore.LogDebug(_context, "Failed to close CLI debug console. error=" + ex.Message);
                }
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static VisibleTerminalHandle GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static VisibleCliTerminalLauncher s_ObfuscationSentinel;

        internal static VisibleTerminalHandle LaunchForJob(object aiJobContext, object value)
        {
            return Launch(aiJobContext, value, ((AiJobContext)aiJobContext)?.DebugLogPath, null);
        }

        internal static VisibleTerminalHandle Launch(object value, object value2, object text, object value3)
        {
            if (value != null && !string.IsNullOrWhiteSpace((string)text))
            {
                try
                {
                    DeleteStaleCloseSignal(value);
                    AiJobFileStore.EnsureDirectory(Path.GetDirectoryName((string)text));
                    if (!File.Exists((string)text))
                    {
                        AiJobFileStore.WriteTextAtomic(text, string.Empty);
                    }
                    RuntimePlatform platform = Application.platform;
                    if ((int)platform == 0)
                    {
                        return LaunchMacTerminal(value, value2, text, value3);
                    }
                    if ((int)platform == 7)
                    {
                        return LaunchWindowsTerminal(value, value2, text, value3);
                    }
                    if ((int)platform == 16)
                    {
                        return LaunchLinuxTerminal(value, value2, text, value3);
                    }
                }
                catch (Exception ex)
                {
                    AiJobFileStore.LogDebug(value, "Failed to launch CLI debug console. error=" + ex.Message);
                }
                return null;
            }
            return null;
        }

        private static VisibleTerminalHandle LaunchWindowsTerminal(object value, object value2, object value3, object value4)
        {
            string text = ((string)value3).Replace("'", "''");
            string text2 = ((AiJobContext)value).DebugConsoleCloseSignalPath.Replace("'", "''");
            string text3 = (string)((!string.IsNullOrWhiteSpace((string)value4)) ? ((string)value2 + " - " + (string)value4) : value2);
            string text4 = ("PSD2UIForm AI Debug - " + text3 + " - " + ((AiJobContext)value).JobId).Replace("'", "''");
            string text5 = "$Host.UI.RawUI.WindowTitle = '" + text4 + "'; Write-Host 'Tailing: " + text + "'; if (!(Test-Path -LiteralPath '" + text + "')) { New-Item -ItemType File -Path '" + text + "' -Force | Out-Null }; $tailJob = Start-Job -ScriptBlock { param($path) Get-Content -LiteralPath $path -Encoding UTF8 -Tail 40 -Wait } -ArgumentList '" + text + "'; try { while (!(Test-Path -LiteralPath '" + text2 + "')) { Receive-Job -Job $tailJob; Start-Sleep -Milliseconds 200; } } finally { Stop-Job -Job $tailJob -ErrorAction SilentlyContinue | Out-Null; Remove-Job -Job $tailJob -Force -ErrorAction SilentlyContinue | Out-Null; }";
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -ExecutionPolicy Bypass -Command \"" + text5 + "\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = Directory.GetParent(Application.dataPath).FullName
            });
            AiJobFileStore.LogDebug(value, "Launched visible Windows debug console for CLI output.");
            return new VisibleTerminalHandle((AiJobContext)value, process, null);
        }

        private static VisibleTerminalHandle LaunchMacTerminal(object value, object value2, object value3, object value4)
        {
            string text = ((string)value3).Replace("\\", "\\\\").Replace("\"", "\\\"");
            string text2 = ((AiJobContext)value).DebugConsoleCloseSignalPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string text3 = (string)((!string.IsNullOrWhiteSpace((string)value4)) ? ((string)value2 + " - " + (string)value4) : value2);
            string text4 = ("PSD2UIForm AI Debug - " + text3 + " - " + ((AiJobContext)value).JobId).Replace("\"", "\\\"");
            string text5 = "tell application \"Terminal\" to do script \"printf '\\\\e]1;" + text4 + "\\\\a'; touch \\\"" + text + "\\\"; printf 'Tailing: " + text + "\\\\n'; tail -n 40 -f \\\"" + text + "\\\" & TAIL_PID=$!; while [ ! -f \\\"" + text2 + "\\\" ]; do sleep 1; done; kill $TAIL_PID >/dev/null 2>&1; wait $TAIL_PID 2>/dev/null; exit\"";
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = "-e \"" + text5 + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            AiJobFileStore.LogDebug(value, "Launched visible macOS Terminal debug console for CLI output.");
            return new VisibleTerminalHandle((AiJobContext)value, process, text4);
        }

        private static VisibleTerminalHandle LaunchLinuxTerminal(object value, object value2, object value3, object value4)
        {
            string text = ((string)value3).Replace("\"", "\\\"");
            string text2 = ((AiJobContext)value).DebugConsoleCloseSignalPath.Replace("\"", "\\\"");
            string text3 = (string)(string.IsNullOrWhiteSpace((string)value4) ? value2 : ((string)value2 + " - " + (string)value4));
            string text4 = ("PSD2UIForm AI Debug - " + text3 + " - " + ((AiJobContext)value).JobId).Replace("\"", "\\\"");
            string text5 = "touch \"" + text + "\"; printf 'Tailing: " + text + "\\n'; tail -n 40 -f \"" + text + "\" & TAIL_PID=$!; while [ ! -f \"" + text2 + "\" ]; do sleep 1; done; kill $TAIL_PID >/dev/null 2>&1; wait $TAIL_PID 2>/dev/null";
            string[] array = new string[5] { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm" };
            foreach (string text6 in array)
            {
                try
                {
                    Process process = Process.Start(new ProcessStartInfo
                    {
                        FileName = text6,
                        Arguments = BuildLinuxTerminalArguments(text6, text4, text5),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    AiJobFileStore.LogDebug(value, "Launched visible Linux debug console using " + text6 + ".");
                    return new VisibleTerminalHandle((AiJobContext)value, process, null);
                }
                catch
                {
                }
            }
            AiJobFileStore.LogDebug(value, "No supported Linux terminal emulator found for visible CLI debug console.");
            return null;
        }

        private static string BuildLinuxTerminalArguments(object value, object value2, object value3)
        {
            if (!((string)value == "gnome-terminal"))
            {
                if ((string)value == "konsole")
                {
                    return "--new-tab -p tabtitle=\"" + (string)value2 + "\" -e bash -lc \"" + (string)value3 + "\"";
                }
                if (!((string)value == "xfce4-terminal"))
                {
                    if (!((string)value == "xterm"))
                    {
                        return "-T \"" + (string)value2 + "\" -e bash -lc \"" + (string)value3 + "\"";
                    }
                    return "-T \"" + (string)value2 + "\" -e bash -lc \"" + (string)value3 + "\"";
                }
                return "--title=\"" + (string)value2 + "\" -e \"bash -lc '" + (string)value3 + "'\"";
            }
            return "--title=\"" + (string)value2 + "\" -- bash -lc \"" + (string)value3 + "\"";
        }

        private static void WriteCloseSignal(object value)
        {
            if (value == null || string.IsNullOrWhiteSpace(((AiJobContext)value).DebugConsoleCloseSignalPath))
            {
                return;
            }
            try
            {
                AiJobFileStore.WriteTextAtomic(((AiJobContext)value).DebugConsoleCloseSignalPath, DateTime.UtcNow.ToString("o"));
            }
            catch (Exception ex)
            {
                AiJobFileStore.LogDebug(value, "Failed to write debug console close signal. error=" + ex.Message);
            }
        }

        private static void DeleteStaleCloseSignal(object value)
        {
            if (value != null && !string.IsNullOrWhiteSpace(((AiJobContext)value).DebugConsoleCloseSignalPath) && File.Exists(((AiJobContext)value).DebugConsoleCloseSignalPath))
            {
                try
                {
                    AiJobFileStore.DeleteFileIfExists(((AiJobContext)value).DebugConsoleCloseSignalPath);
                }
                catch (Exception ex)
                {
                    AiJobFileStore.LogDebug(value, "Failed to delete stale debug console close signal. error=" + ex.Message);
                }
            }
        }

        private static void CloseMacTerminalWindow(object value, object value2)
        {
            if (string.IsNullOrWhiteSpace((string)value2))
            {
                return;
            }
            string text = ((string)value2).Replace("\\", "\\\\").Replace("\"", "\\\"");
            string text2 = "tell application \"Terminal\" to close (every window whose name contains \"" + text + "\")";
            try
            {
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = "-e \"" + text2 + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                AiJobFileStore.LogDebug(value, "Failed to close macOS Terminal window. error=" + ex.Message);
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static VisibleCliTerminalLauncher GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
