using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using IAiCliProviderNamespace;
using AiJobFileStoreNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using CliCommandResolverNamespace;
using AiCliEventKindNamespace;
using AiCliArtifactUtilityNamespace;
using VisibleCliTerminalLauncherNamespace;

namespace AiCliProviderBaseNamespace
{
    internal abstract class AiCliProviderBase : IAiCliProvider
    {
        private sealed class CliEventState
        {
            public AiCliEventKind _eventKind;

            public string _message;

            internal static CliEventState s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CliEventState GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class VisibleCliOutputCapture : IDisposable
        {
            public StreamWriter _streamWriter;

            public StreamWriter _displayLogWriter;

            public bool _isVisibleCliExecution;

            public readonly Dictionary<string, string> _dedupeValues = new Dictionary<string, string>(StringComparer.Ordinal);

            public readonly StringBuilder _thinkingText = new StringBuilder(512);

            public readonly StringBuilder _responseText = new StringBuilder(512);

            public string _currentContentBlockType = string.Empty;

            public bool _claudeThinkingHeaderWritten;

            public bool _claudeResponseHeaderWritten;

            public bool _openCodeThinkingHeaderWritten;

            public bool _openCodeResponseHeaderWritten;

            internal static VisibleCliOutputCapture s_ObfuscationSentinel;

            public void Dispose()
            {
                try
                {
                    if (_displayLogWriter != null)
                    {
                        _displayLogWriter.Flush();
                        _displayLogWriter.Dispose();
                    }
                }
                catch
                {
                }
                try
                {
                    if (_streamWriter != null)
                    {
                        _streamWriter.Flush();
                        _streamWriter.Dispose();
                    }
                }
                catch
                {
                }
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static VisibleCliOutputCapture GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class ClaudeVisibleEnvelope
        {
            public string type;

            public string subtype;

            public string model;

            public string[] tools;

            public ClaudeVisibleEventPayload @event;

            public ClaudeVisibleMessage message;

            public long duration_ms;

            internal static ClaudeVisibleEnvelope s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ClaudeVisibleEnvelope GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class ClaudeVisibleEventPayload
        {
            public string type;

            public ClaudeVisibleContentBlock content_block;

            public ClaudeVisibleDelta delta;

            internal static ClaudeVisibleEventPayload s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ClaudeVisibleEventPayload GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class ClaudeVisibleContentBlock
        {
            public string type;

            public string name;

            internal static ClaudeVisibleContentBlock s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ClaudeVisibleContentBlock GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class ClaudeVisibleDelta
        {
            public string type;

            public string thinking;

            public string text;

            public string partial_json;

            private static ClaudeVisibleDelta s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ClaudeVisibleDelta GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class ClaudeVisibleMessage
        {
            public ClaudeVisibleMessageContent[] content;

            internal static ClaudeVisibleMessage s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ClaudeVisibleMessage GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class ClaudeVisibleMessageContent
        {
            public string type;

            public string content;

            private static ClaudeVisibleMessageContent s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ClaudeVisibleMessageContent GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class OpenCodeVisibleEnvelope
        {
            public string type;

            public OpenCodeVisiblePart part;

            internal static OpenCodeVisibleEnvelope s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static OpenCodeVisibleEnvelope GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class OpenCodeVisiblePart
        {
            public string type;

            public string text;

            public string reason;

            public OpenCodeVisibleTokens tokens;

            private static OpenCodeVisiblePart s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static OpenCodeVisiblePart GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class OpenCodeVisibleTokens
        {
            public int output;

            public int reasoning;

            private static OpenCodeVisibleTokens s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static OpenCodeVisibleTokens GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class CodexVisibleEnvelope
        {
            public string type;

            public CodexVisibleItem item;

            public CodexVisibleUsage usage;

            public string message;

            public string summary;

            public string title;

            public string content;

            public CodexVisibleError error;

            internal static CodexVisibleEnvelope s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CodexVisibleEnvelope GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class CodexVisibleItem
        {
            public string type;

            public string status;

            public string command;

            public string tool;

            public string server;

            public CodexVisibleArguments arguments;

            public string text;

            public CodexVisibleItemResult result;

            public CodexVisibleError error;

            private static CodexVisibleItem s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CodexVisibleItem GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class CodexVisibleArguments
        {
            public string title;

            private static CodexVisibleArguments s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CodexVisibleArguments GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class CodexVisibleItemResult
        {
            public CodexVisibleResultContent[] content;

            internal static CodexVisibleItemResult s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CodexVisibleItemResult GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class CodexVisibleResultContent
        {
            public string text;

            public string content;

            internal static CodexVisibleResultContent s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CodexVisibleResultContent GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class CodexVisibleError
        {
            public string message;

            public string text;

            private static CodexVisibleError s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CodexVisibleError GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class CodexVisibleUsage
        {
            public int output_tokens;

            public int reasoning_output_tokens;

            internal static CodexVisibleUsage s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static CodexVisibleUsage GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static AiCliProviderBase s_ObfuscationSentinel;

        private readonly AiProviderKind providerKind;

        private readonly AiProviderConnectionSettings connectionSettings;

        protected AiCliProviderBase()
            : this(AiProviderKind.CodexCli, new AiProviderConnectionSettings())
        {
        }

        protected AiCliProviderBase(AiProviderKind providerKind, AiProviderConnectionSettings connectionSettings)
        {
            this.providerKind = providerKind;
            this.connectionSettings = connectionSettings ?? new AiProviderConnectionSettings();
        }

        [SpecialName]
        public abstract string GetProviderId();

        [SpecialName]
        public virtual AiProviderCapabilities GetCapabilities()
        {
            return new AiProviderCapabilities
            {
                SupportsImages = true,
                SupportsStrictJson = true
            };
        }

        [SpecialName]
        protected abstract string GetExecutableName();

        protected abstract string BuildCommandArguments(AiAnalysisRequest analysisRequest);

        protected virtual string BuildVisibleCommandArguments(AiAnalysisRequest analysisRequest)
        {
            return BuildCommandArguments(analysisRequest);
        }

        protected virtual string BuildVisibleRunnerInvocation(string commandArguments, AiJobContext jobContext)
        {
            return "& $cliPath " + commandArguments + " $bootstrapPrompt";
        }

        protected string BuildVisibleCliAdapterScript(string text, string text2, string text3, string text4 = null, string text5 = null)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("\nfunction Write-VisibleCliDisplayEvent {\n    param($Event)\n\n    if ($null -eq $Event) { return }\n\n    if ($Event -is [System.Collections.IEnumerable] -and -not ($Event -is [string]) -and -not $Event.PSObject.Properties['text']) {\n        foreach ($entry in $Event) {\n            Write-VisibleCliDisplayEvent $entry\n        }\n        return\n    }\n\n    $newlineOnly = $false\n    if ($Event.PSObject.Properties['newline']) {\n        $newlineOnly = [bool]$Event.newline\n    }\n    if ($newlineOnly) {\n        Write-Host ''\n        return\n    }\n\n    $text = [string]$Event\n    if ($Event.PSObject.Properties['text']) {\n        $text = [string]$Event.text\n    }\n    if ([string]::IsNullOrWhiteSpace($text)) { return }\n\n    $dedupeKey = ''\n    if ($Event.PSObject.Properties['dedupeKey']) {\n        $dedupeKey = [string]$Event.dedupeKey\n    }\n\n    $dedupeValue = $text\n    if ($Event.PSObject.Properties['dedupeValue']) {\n        $dedupeValue = [string]$Event.dedupeValue\n    }\n    if (-not [string]::IsNullOrWhiteSpace($dedupeKey)) {\n        $lastValue = $script:visibleCliEventDedupe[$dedupeKey]\n        if ($lastValue -eq $dedupeValue) {\n            return\n        }\n\n        $script:visibleCliEventDedupe[$dedupeKey] = $dedupeValue\n    }\n\n    $blankLineBefore = $false\n    if ($Event.PSObject.Properties['blankLineBefore']) {\n        $blankLineBefore = [bool]$Event.blankLineBefore\n    }\n    if ($blankLineBefore) {\n        Write-Host ''\n    }\n\n    $noNewline = $false\n    if ($Event.PSObject.Properties['noNewline']) {\n        $noNewline = [bool]$Event.noNewline\n    }\n\n    $color = 'White'\n    if ($Event.PSObject.Properties['color'] -and -not [string]::IsNullOrWhiteSpace([string]$Event.color)) {\n        $color = [string]$Event.color\n    }\n\n    if ($noNewline) {\n        Write-Host -NoNewline $text -ForegroundColor $color\n    } else {\n        Write-Host $text -ForegroundColor $color\n    }\n}\n\nfunction Invoke-VisibleCliStreamAdapter {\n    param([object]$Chunk, [string]$AdapterFunctionName)\n\n    if ($null -eq $Chunk) { return }\n\n    $line = [string]$Chunk\n    if ([string]::IsNullOrWhiteSpace($line)) { return }\n\n    if ($script:streamWriter -ne $null) {\n        $script:streamWriter.WriteLine($line)\n        $script:streamWriter.Flush()\n    }\n\n    try {\n        $events = & $AdapterFunctionName $line\n        Write-VisibleCliDisplayEvent $events\n    }\n    catch {\n        Write-Host $line -ForegroundColor DarkGray\n    }\n}\n");
            stringBuilder.AppendLine();
            if (!string.IsNullOrWhiteSpace(text2))
            {
                stringBuilder.Append(text2);
                stringBuilder.AppendLine();
            }
            stringBuilder.AppendLine("$script:streamWriter = [System.IO.StreamWriter]::new($streamPath, $false, $utf8NoBom)");
            stringBuilder.AppendLine("$script:visibleCliEventDedupe = @{}");
            if (!string.IsNullOrWhiteSpace(text4))
            {
                stringBuilder.AppendLine(text4);
            }
            stringBuilder.AppendLine("try {");
            stringBuilder.AppendLine("    $bootstrapPrompt | & $cliPath " + text + " 2>&1 | ForEach-Object { Invoke-VisibleCliStreamAdapter $_ '" + EscapePowerShellSingleQuotedString(text3) + "' }");
            stringBuilder.AppendLine("}");
            stringBuilder.AppendLine("finally {");
            stringBuilder.AppendLine("    if ($script:streamWriter -ne $null) { $script:streamWriter.Flush(); $script:streamWriter.Dispose() }");
            stringBuilder.AppendLine("}");
            if (!string.IsNullOrWhiteSpace(text5))
            {
                stringBuilder.AppendLine(text5);
            }
            return stringBuilder.ToString();
        }

        public void ExecuteJob(AiJobContext aiJobContext, AiAnalysisRequest aiAnalysisRequest)
        {
            if (aiJobContext == null)
            {
                throw new ArgumentNullException("context");
            }
            if (aiAnalysisRequest != null)
            {
                AiJobFileStore.LogDebug(aiJobContext, "CLI ExecuteJob begin. provider=" + GetProviderId());
                ValidateRequestPathsWithinWorkingDirectory(aiJobContext, aiAnalysisRequest);
                string text = AiCliArtifactUtility.BuildPrompt(aiAnalysisRequest.PromptTemplatePath, aiAnalysisRequest);
                if (string.IsNullOrWhiteSpace(text))
                {
                    string promptTemplatePath = string.IsNullOrWhiteSpace(aiAnalysisRequest.PromptTemplatePath) ? "<empty>" : aiAnalysisRequest.PromptTemplatePath;
                    throw new InvalidOperationException("AI prompt template is missing or empty: " + promptTemplatePath);
                }
                AiJobFileStore.WriteTextAtomic(aiJobContext.PromptPath, text);
                AiJobFileStore.LogDebug(aiJobContext, $"Prompt written. promptPath={aiJobContext.PromptPath}, promptLength={text?.Length ?? 0}");
                StringBuilder standardOutputBuilder = new StringBuilder(4096);
                StringBuilder builder = new StringBuilder(2048);
                object outputCaptureLock = new object();
                CliEventState cliEventState = new CliEventState();
                string text2 = null;
                int num = -1;
                bool flag;
                string text3 = ((flag = ShouldUseVisibleCliExecution(aiAnalysisRequest)) ? BuildVisibleCommandArguments(aiAnalysisRequest) : BuildCommandArguments(aiAnalysisRequest));
                string workingDirectory = GetWorkingDirectory(aiAnalysisRequest);
                DeletePreviousOutputArtifacts(aiJobContext, aiAnalysisRequest);
                LaunchVisibleCliLogViewer(aiJobContext, aiAnalysisRequest, flag);
                if (!CliCommandResolver.TryResolveLaunch(GetExecutableName(), text3, aiJobContext.PromptPath, out var value, out var text4))
                {
                    AiJobFileStore.LogDebug(aiJobContext, "CLI resolve failed. executable=" + GetExecutableName() + ", error=" + text4);
                    throw new InvalidOperationException(text4);
                }
                AiJobFileStore.LogDebug(aiJobContext, $"CLI resolved. executable={GetExecutableName()}, launchFile={value.ExecutablePath}, launchArguments={value.Arguments}, useStandardInput={value.RedirectStandardInput}");
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = value.ExecutablePath,
                    Arguments = value.Arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardInput = value.RedirectStandardInput,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = _utf8NoBom,
                    StandardErrorEncoding = _utf8NoBom
                };
                ApplyCustomApiEnvironment(processStartInfo);
                AiJobFileStore.LogDebug(aiJobContext, $"ProcessStartInfo prepared. workingDirectory={processStartInfo.WorkingDirectory}, redirectStdIn={processStartInfo.RedirectStandardInput}");
                try
                {
                    VisibleCliOutputCapture visibleOutputCapture = CreateVisibleCliOutputCapture(aiJobContext, aiAnalysisRequest, flag);
                    try
                    {
                        Process process = new Process
                        {
                            StartInfo = processStartInfo
                        };
                        try
                        {
                            process.OutputDataReceived += delegate(object _, DataReceivedEventArgs args)
                            {
                                CaptureProcessOutputLine(aiJobContext, aiAnalysisRequest, args?.Data, false, standardOutputBuilder, outputCaptureLock, cliEventState, visibleOutputCapture);
                            };
                            process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs args)
                            {
                                CaptureProcessOutputLine(aiJobContext, aiAnalysisRequest, args?.Data, true, builder, outputCaptureLock, cliEventState, visibleOutputCapture);
                            };
                            if (process.Start())
                            {
                                AiJobFileStore.LogDebug(aiJobContext, $"Process started. pid={process.Id}, fileName={processStartInfo.FileName}");
                                using (aiAnalysisRequest.CancellationToken.Register(delegate
                                {
                                    KillProcessTree(process, aiJobContext, "Cancellation token triggered");
                                }))
                                {
                                    if (value.RedirectStandardInput)
                                    {
                                        process.StandardInput.Write(text);
                                        process.StandardInput.Close();
                                        AiJobFileStore.LogDebug(aiJobContext, "Prompt piped to process stdin and stdin closed.");
                                    }
                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();
                                    AiJobFileStore.LogDebug(aiJobContext, "BeginOutputReadLine and BeginErrorReadLine called.");
                                    while (!process.HasExited)
                                    {
                                        if (!aiAnalysisRequest.CancellationToken.IsCancellationRequested)
                                        {
                                            UpdateUnifiedRecognitionStageProgress(aiJobContext, aiAnalysisRequest);
                                            if (!TryFinalizeStagedResult(aiJobContext, aiAnalysisRequest, out text2))
                                            {
                                                if (cliEventState._eventKind != (AiCliEventKind)2)
                                                {
                                                    if (!RequiresExplicitOutputFile(aiAnalysisRequest) && cliEventState._eventKind == (AiCliEventKind)1)
                                                    {
                                                        try
                                                        {
                                                            if (TryFinalizeResultArtifacts(aiJobContext, aiAnalysisRequest, true, out text2))
                                                            {
                                                                AiJobFileStore.LogDebug(aiJobContext, "CLI completion event observed and artifacts finalized. Stopping process tree.");
                                                                KillProcessTree(process, aiJobContext, "Completion artifacts ready");
                                                                return;
                                                            }
                                                            if (TryRecoverResultFromCapturedOutput(aiJobContext, aiAnalysisRequest, standardOutputBuilder, outputCaptureLock, ref num, out text2))
                                                            {
                                                                AiJobFileStore.LogDebug(aiJobContext, "CLI completion event observed and JSON synthesized from captured stdout. Stopping process tree.");
                                                                KillProcessTree(process, aiJobContext, "Completion artifacts ready from captured stdout");
                                                                return;
                                                            }
                                                        }
                                                        catch
                                                        {
                                                            KillProcessTree(process, aiJobContext, "Completion artifact validation failed");
                                                            throw;
                                                        }
                                                    }
                                                    process.WaitForExit(200);
                                                    continue;
                                                }
                                                string text5;
                                                lock (outputCaptureLock)
                                                {
                                                    text5 = builder.ToString();
                                                }
                                                KillProcessTree(process, aiJobContext, "Terminal failure event observed");
                                                throw new InvalidOperationException(BuildCliFailureMessage(aiJobContext, aiAnalysisRequest, text5, null, (!string.IsNullOrWhiteSpace(cliEventState._message)) ? cliEventState._message : "CLI provider reported a failure event."));
                                            }
                                            AiJobFileStore.LogDebug(aiJobContext, "Valid staged JSON artifact observed while CLI is still running. Stopping process tree immediately.");
                                            KillProcessTree(process, aiJobContext, "Staged JSON artifact committed");
                                            return;
                                        }
                                        throw new OperationCanceledException("AI job was cancelled by user.", aiAnalysisRequest.CancellationToken);
                                    }
                                    process.WaitForExit();
                                    AiJobFileStore.LogDebug(aiJobContext, $"Process exited. exitCode={process.ExitCode}");
                                    string text6;
                                    string text7;
                                    lock (outputCaptureLock)
                                    {
                                        text6 = standardOutputBuilder.ToString();
                                        text7 = builder.ToString();
                                    }
                                    AiJobFileStore.LogDebug(aiJobContext, $"Process output captured. stdoutLength={text6?.Length ?? 0}, stderrLength={text7?.Length ?? 0}");
                                    if (!File.Exists(aiAnalysisRequest.OutputRawTextPath))
                                    {
                                        StringBuilder stringBuilder = new StringBuilder();
                                        if (!string.IsNullOrWhiteSpace(text6))
                                        {
                                            stringBuilder.AppendLine(text6.Trim());
                                        }
                                        if (!string.IsNullOrWhiteSpace(text7))
                                        {
                                            stringBuilder.AppendLine(text7.Trim());
                                        }
                                        string text8 = ResolveRawOutputPath(aiJobContext, aiAnalysisRequest);
                                        if (!string.IsNullOrWhiteSpace(text8))
                                        {
                                            AiJobFileStore.WriteTextAtomic(text8, stringBuilder.ToString());
                                            AiJobFileStore.LogDebug(aiJobContext, "Final assistant message artifact created from captured streams. rawOutputPath=" + text8);
                                        }
                                    }
                                    if (aiAnalysisRequest.CancellationToken.IsCancellationRequested)
                                    {
                                        throw new OperationCanceledException("AI job was cancelled by user.", aiAnalysisRequest.CancellationToken);
                                    }
                                    if (!TryFinalizeResultArtifacts(aiJobContext, aiAnalysisRequest, true, out text2))
                                    {
                                        if (!TryRecoverResultFromCapturedOutput(aiJobContext, aiAnalysisRequest, standardOutputBuilder, outputCaptureLock, ref num, out text2))
                                        {
                                            if (cliEventState._eventKind == (AiCliEventKind)2)
                                            {
                                                throw new InvalidOperationException(BuildCliFailureMessage(aiJobContext, aiAnalysisRequest, text7, null, (!string.IsNullOrWhiteSpace(cliEventState._message)) ? cliEventState._message : "CLI provider reported a failure event."));
                                            }
                                            if (process.ExitCode != 0)
                                            {
                                                AiJobFileStore.LogDebug(aiJobContext, $"CLI process exited with non-zero code. exitCode={process.ExitCode}");
                                                throw new InvalidOperationException(BuildCliFailureMessage(aiJobContext, aiAnalysisRequest, text7, process.ExitCode));
                                            }
                                            if (!string.IsNullOrWhiteSpace(text2))
                                            {
                                                throw new InvalidOperationException(text2);
                                            }
                                            throw new InvalidOperationException("CLI process exited without producing a valid JSON result artifact.");
                                        }
                                        AiJobFileStore.LogDebug(aiJobContext, "CLI completion artifacts synthesized from captured stdout after process exit.");
                                    }
                                    else
                                    {
                                        AiJobFileStore.LogDebug(aiJobContext, "CLI completion artifacts extracted successfully after process exit.");
                                    }
                                    return;
                                }
                            }
                            AiJobFileStore.LogDebug(aiJobContext, "Process.Start returned false. fileName=" + processStartInfo.FileName);
                            throw new InvalidOperationException("Failed to start CLI provider '" + GetProviderId() + "'.");
                        }
                        finally
                        {
                            if (process != null)
                            {
                                ((IDisposable)process).Dispose();
                            }
                        }
                    }
                    finally
                    {
                        if (visibleOutputCapture != null)
                        {
                            ((IDisposable)visibleOutputCapture).Dispose();
                        }
                    }
                }
                finally
                {
                    SignalVisibleCliLogViewerClose(aiJobContext, flag);
                }
            }
            throw new ArgumentNullException("request");
        }

        private static bool TryRecoverResultFromCapturedOutput(object value, object value2, object value3, object value4, ref int value5, out string result)
        {
            result = null;
            if (value2 != null && value3 != null && value4 != null && !RequiresExplicitOutputFile(value2) && !((AiAnalysisRequest)value2).DisableOutputRecovery)
            {
                string text;
                lock (value4)
                {
                    if (((StringBuilder)value3).Length < 1 || ((StringBuilder)value3).Length == value5)
                    {
                        return false;
                    }
                    text = value3.ToString();
                    value5 = ((StringBuilder)value3).Length;
                }
                if (!AiCliArtifactUtility.TryRecoverResultJsonFromProviderOutput(text, value2, out result))
                {
                    return false;
                }
                EnsureCompletionMarker(value, value2);
                AiJobFileStore.LogDebug(value, "JSON synthesized directly from captured stdout. outputPath=" + ((AiAnalysisRequest)value2).OutputJsonPath, false);
                return true;
            }
            return false;
        }

        private bool ShouldUseVisibleCliExecution(AiAnalysisRequest value)
        {
            AiProviderCapabilities aiProviderCapabilities = GetCapabilities();
            if (!connectionSettings.useCustomApi && aiProviderCapabilities != null && value != null && value.AllowVisibleCliExecution && aiProviderCapabilities.UsesVisibleCliExecution)
            {
                if (!IsWindowsEditor())
                {
                    return IsMacTerminalAvailable();
                }
                return true;
            }
            return false;
        }

        private void ExecuteVisibleCliJob(AiJobContext value, AiAnalysisRequest value2, string text2, string text3)
        {
            ProcessStartInfo processStartInfo = BuildVisibleCliProcessStartInfo(value, value2, text3);
            DeletePreviousOutputArtifacts(value, value2);
            AiJobFileStore.LogDebug(value, "Starting visible CLI terminal. fileName=" + processStartInfo.FileName + ", arguments=" + processStartInfo.Arguments);
            using Process process = Process.Start(processStartInfo);
            if (process != null)
            {
                AiJobFileStore.UpdateJobDetail(value, "AI任务进程: CLI 运行中");
                AiJobFileStore.LogDebug(value, $"Visible CLI process started. pid={process.Id}");
                string text = null;
                bool flag = false;
                while (!process.HasExited)
                {
                    if (!value2.CancellationToken.IsCancellationRequested)
                    {
                        UpdateUnifiedRecognitionStageProgress(value, value2);
                        if (!TryFinalizeStagedResult(value, value2, out text))
                        {
                            if (ProcessLatestVisibleCliEvent(value, value2) == (AiCliEventKind)2)
                            {
                                flag = true;
                            }
                            if (!TryReadVisibleCliCompletion(value, value2, out var aiVisibleCliCompletionDocument))
                            {
                                process.WaitForExit(500);
                                continue;
                            }
                            AiJobFileStore.LogDebug(value, $"Visible CLI completion marker observed. exitCode={aiVisibleCliCompletionDocument.exitCode}, completedAtUtc={aiVisibleCliCompletionDocument.completedAtUtc}");
                            if (!TryFinalizeResultArtifacts(value, value2, true, out text))
                            {
                                if (aiVisibleCliCompletionDocument.exitCode != 0)
                                {
                                    throw new InvalidOperationException(BuildCliFailureMessage(value, value2, string.Empty, aiVisibleCliCompletionDocument.exitCode));
                                }
                                if (flag)
                                {
                                    throw new InvalidOperationException(BuildCliFailureMessage(value, value2, string.Empty, null, "CLI provider reported a failure event. See current stage stream output for details."));
                                }
                                throw new InvalidOperationException((!string.IsNullOrWhiteSpace(text)) ? text : "CLI process completed without producing a valid JSON result artifact.");
                            }
                            AiJobFileStore.LogDebug(value, "Visible CLI artifacts finalized from completion marker. Closing terminal window.");
                            KillProcessTree(process, value, "Completion artifacts ready");
                            return;
                        }
                        AiJobFileStore.LogDebug(value, "Valid staged JSON artifact observed while visible CLI is still running. Closing terminal window.");
                        KillProcessTree(process, value, "Staged JSON artifact committed");
                        return;
                    }
                    KillProcessTree(process, value, "Cancellation token triggered");
                    throw new OperationCanceledException("AI job was cancelled by user.", value2.CancellationToken);
                }
                AiJobFileStore.LogDebug(value, $"Visible CLI process exited. exitCode={process.ExitCode}");
                if (!TryFinalizeResultArtifacts(value, value2, true, out text))
                {
                    if (value2.CancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("AI job was cancelled by user.", value2.CancellationToken);
                    }
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException(BuildCliFailureMessage(value, value2, string.Empty, process.ExitCode));
                    }
                    if (flag)
                    {
                        throw new InvalidOperationException(BuildCliFailureMessage(value, value2, string.Empty, null, "CLI provider reported a failure event. See current stage stream output for details."));
                    }
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        throw new InvalidOperationException(text);
                    }
                    throw new InvalidOperationException("CLI process exited without producing a valid JSON result artifact.");
                }
                AiJobFileStore.LogDebug(value, "Visible CLI artifacts finalized after process exit.");
                return;
            }
            throw new InvalidOperationException("Failed to start visible CLI provider '" + GetProviderId() + "'.");
        }

        private static bool TryReadVisibleCliCompletion(object value, object value2, out AiVisibleCliCompletionDocument result)
        {
            result = null;
            string text = ResolveVisibleCliCompletionPath(value, value2);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            if (!AiJobFileStore.TryReadJson<AiVisibleCliCompletionDocument>(text, out result))
            {
                return false;
            }
            return result != null;
        }

        private static AiCliEventKind ProcessLatestVisibleCliEvent(object value, object value2)
        {
            string text = ReadLastNonEmptyLine(ResolveStreamOutputPath(value, value2));
            if (!string.IsNullOrWhiteSpace(text))
            {
                string text2 = AiCliArtifactUtility.ExtractVisibleDisplayText(text);
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    AiJobFileStore.UpdateJobDetail(value, text2);
                }
                if (AiCliArtifactUtility.TryParseCliEvent(text, out var aiCliEventKind, out var arg) && aiCliEventKind != 0)
                {
                    AiJobFileStore.LogDebug(value, $"Visible CLI stream event observed. kind={aiCliEventKind}, message={arg}", false);
                    return aiCliEventKind;
                }
                return (AiCliEventKind)0;
            }
            return (AiCliEventKind)0;
        }

        private static void UpdateUnifiedRecognitionStageProgress(object value, object value2)
        {
            if (value != null && value2 != null && string.Equals(((AiAnalysisRequest)value2).StageName, "recognition-orchestrator", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(((AiJobContext)value).MainTypeTempPath) && File.Exists(((AiJobContext)value).MainTypeTempPath) && (!AiJobFileStore.TryReadJson<AiJobStatusDocument>(((AiJobContext)value).StatusPath, out var aiJobStatusDocument) || aiJobStatusDocument == null || !string.Equals(aiJobStatusDocument.stage, "AI处理阶段 2/3：子控件归属识别", StringComparison.Ordinal)))
            {
                AiJobFileStore.UpdateJobStage(value, "AI处理阶段 2/3：子控件归属识别");
                AiJobFileStore.UpdateJobDetail(value, "AI任务进程: 第一阶段结果已写入，继续同会话进行子控件归属识别");
                AiJobFileStore.LogDebug(value, "Unified recognition progress advanced to child-relation stage.");
            }
        }

        private static string ReadLastNonEmptyLine(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && File.Exists((string)value))
            {
                try
                {
                    using FileStream fileStream = new FileStream((string)value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    int num = (int)Math.Min(16384L, fileStream.Length);
                    if (num < 1)
                    {
                        return string.Empty;
                    }
                    fileStream.Seek(-num, SeekOrigin.End);
                    byte[] array = new byte[num];
                    int count = fileStream.Read(array, 0, num);
                    string[] array2 = _utf8NoBom.GetString(array, 0, count).Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    int num2 = array2.Length - 1;
                    while (num2 >= 0)
                    {
                        string text = array2[num2].Trim();
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            num2--;
                            continue;
                        }
                        return text;
                    }
                }
                catch
                {
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private ProcessStartInfo BuildVisibleCliProcessStartInfo(AiJobContext value, AiAnalysisRequest value2, string text7)
        {
            if (!IsWindowsEditor())
            {
                throw new InvalidOperationException("Visible CLI execution is only supported on WindowsEditor.");
            }
            string text = ResolveVisibleCliCommandPath(GetExecutableName());
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("未找到 CLI 命令: " + GetExecutableName() + ".ps1");
            }
            string text2 = ResolveStreamOutputPath(value, value2);
            AiJobFileStore.EnsureDirectory((!string.IsNullOrWhiteSpace(text2)) ? Path.GetDirectoryName(text2) : value.ResponseDirectory);
            string text3 = Path.Combine(value.ResponseDirectory, "run-visible-cli.ps1");
            string text4 = Path.Combine(value.ResponseDirectory, "visible-cli-bootstrap-prompt.md");
            string text5 = BuildVisibleCommandArguments(value2);
            AiJobFileStore.WriteTextAtomic(text4, BuildVisibleCliBootstrapPrompt(value, value2));
            string text6 = BuildVisibleCliRunnerScript(value, text, text5, text4, value2);
            AiJobFileStore.WriteTextAtomic(text3, text6);
            AiJobFileStore.LogDebug(value, "Visible CLI runner written. runnerPath=" + text3 + ", commandPath=" + text + ", commandArguments=" + text5);
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"" + text3 + "\"",
                WorkingDirectory = text7,
                UseShellExecute = true,
                CreateNoWindow = false
            };
        }

        private void ApplyCustomApiEnvironment(ProcessStartInfo processStartInfo)
        {
            if (!connectionSettings.useCustomApi) return;

            Uri endpoint;
            if (!Uri.TryCreate(connectionSettings.customApiUrl, UriKind.Absolute, out endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
            {
                throw new InvalidOperationException("自定义 API 地址无效：请填写 HTTPS 地址；仅本机回环地址允许 HTTP。");
            }

            string apiKey = AiProviderSecretStore.ReadRequired(providerKind);
            if (providerKind == AiProviderKind.ClaudeCodeCli)
            {
                processStartInfo.EnvironmentVariables["ANTHROPIC_BASE_URL"] = endpoint.AbsoluteUri.TrimEnd('/');
                processStartInfo.EnvironmentVariables["ANTHROPIC_API_KEY"] = apiKey;
            }
            else
            {
                processStartInfo.EnvironmentVariables["OPENAI_BASE_URL"] = endpoint.AbsoluteUri.TrimEnd('/');
                processStartInfo.EnvironmentVariables["OPENAI_API_KEY"] = apiKey;
            }
        }

        private string BuildVisibleCliRunnerScript(AiJobContext value, string text2, string text3, string text4, AiAnalysisRequest value2)
        {
            StringBuilder stringBuilder = new StringBuilder(2048);
            string text = ((value2 == null || string.IsNullOrWhiteSpace(value2.StageDisplayName)) ? "AI Stage" : value2.StageDisplayName);
            stringBuilder.AppendLine("$ErrorActionPreference = 'Continue'");
            stringBuilder.AppendLine("$utf8NoBom = New-Object System.Text.UTF8Encoding($false)");
            stringBuilder.AppendLine("[Console]::InputEncoding = $utf8NoBom");
            stringBuilder.AppendLine("[Console]::OutputEncoding = $utf8NoBom");
            stringBuilder.AppendLine("$OutputEncoding = $utf8NoBom");
            stringBuilder.AppendLine("try { chcp 65001 > $null } catch { }");
            stringBuilder.AppendLine("$Host.UI.RawUI.WindowTitle = 'PSD2UIForm AI - " + EscapePowerShellSingleQuotedString(text) + " - " + EscapePowerShellSingleQuotedString(GetProviderId()) + " - " + EscapePowerShellSingleQuotedString(value.JobId) + "'");
            stringBuilder.AppendLine("$promptPath = '" + EscapePowerShellSingleQuotedString(value.PromptPath) + "'");
            stringBuilder.AppendLine("$streamPath = '" + EscapePowerShellSingleQuotedString(ResolveStreamOutputPath(value, value2)) + "'");
            stringBuilder.AppendLine("$rawOutputPath = '" + EscapePowerShellSingleQuotedString(ResolveRawOutputPath(value, value2)) + "'");
            stringBuilder.AppendLine("$completionPath = '" + EscapePowerShellSingleQuotedString(ResolveVisibleCliCompletionPath(value, value2)) + "'");
            stringBuilder.AppendLine("$bootstrapPromptPath = '" + EscapePowerShellSingleQuotedString(text4) + "'");
            stringBuilder.AppendLine("$cliPath = '" + EscapePowerShellSingleQuotedString(text2) + "'");
            stringBuilder.AppendLine("Write-Host '[PSD2UIForm.AI] Starting native CLI UI for " + EscapePowerShellSingleQuotedString(GetProviderId()) + "...'");
            stringBuilder.AppendLine("Write-Host ('[PSD2UIForm.AI] CLI: ' + $cliPath)");
            stringBuilder.AppendLine("Write-Host ('[PSD2UIForm.AI] Raw output: ' + $rawOutputPath)");
            stringBuilder.AppendLine("Write-Host ('[PSD2UIForm.AI] Completion marker: ' + $completionPath)");
            stringBuilder.AppendLine("Write-Host ('[PSD2UIForm.AI] Bootstrap prompt: ' + $bootstrapPromptPath)");
            stringBuilder.AppendLine("Write-Host ''");
            stringBuilder.AppendLine("$exitCode = 1");
            stringBuilder.AppendLine("if (Test-Path -LiteralPath $streamPath) { Remove-Item -LiteralPath $streamPath -Force }");
            stringBuilder.AppendLine("if (Test-Path -LiteralPath $completionPath) { Remove-Item -LiteralPath $completionPath -Force }");
            stringBuilder.AppendLine("$bootstrapPrompt = Get-Content -Raw -Encoding UTF8 -LiteralPath $bootstrapPromptPath");
            stringBuilder.AppendLine(BuildVisibleRunnerInvocation(text3, value));
            stringBuilder.AppendLine("$exitCode = $LASTEXITCODE");
            stringBuilder.AppendLine("$completion = [ordered]@{");
            stringBuilder.AppendLine("    exitCode = [int]$exitCode");
            stringBuilder.AppendLine("    completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')");
            stringBuilder.AppendLine("    rawOutputPath = $rawOutputPath");
            stringBuilder.AppendLine("    streamOutputPath = $streamPath");
            stringBuilder.AppendLine("}");
            stringBuilder.AppendLine("$completion | ConvertTo-Json -Compress | Set-Content -LiteralPath $completionPath -Encoding UTF8");
            stringBuilder.AppendLine("Write-Host ''");
            stringBuilder.AppendLine("Write-Host ('[PSD2UIForm.AI] CLI process exited. exitCode=' + $exitCode)");
            stringBuilder.AppendLine("Write-Host '[PSD2UIForm.AI] Unity is reading the result files now. This window will close automatically.'");
            stringBuilder.AppendLine("exit $exitCode");
            return stringBuilder.ToString();
        }

        private string BuildVisibleCliBootstrapPrompt(AiJobContext value, AiAnalysisRequest value2)
        {
            StringBuilder stringBuilder = new StringBuilder(2048);
            string text = ((value2 == null || string.IsNullOrWhiteSpace(value2.StageDisplayName)) ? "AI Stage" : value2.StageDisplayName);
            stringBuilder.AppendLine("You are running inside the native visible CLI for PSD2UIForm AI (" + GetProviderId() + ").");
            stringBuilder.AppendLine("Current task: " + text);
            stringBuilder.AppendLine("This visible CLI window belongs to the current PSD2UIForm AI task. It is not an automatic retry.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Integration task:");
            stringBuilder.AppendLine("1. Read this UTF-8 task prompt file:");
            stringBuilder.AppendLine(value.PromptPath);
            stringBuilder.AppendLine("2. Follow the task prompt as the source of truth for PSD2UIForm analysis.");
            stringBuilder.AppendLine("3. The task prompt may say not to read local files or write files; for this visible CLI integration, this bootstrap prompt overrides that only as follows:");
            stringBuilder.AppendLine("   - You must read the task prompt file above.");
            stringBuilder.AppendLine("   - You may read local files explicitly referenced by that prompt.");
            stringBuilder.AppendLine("   - You must write the final " + AiCliArtifactUtility.GetResultDocumentDescription(value2.ResultDocumentKind) + " to the staging result file below.");
            stringBuilder.AppendLine("   - The staging result file is the only success artifact Unity accepts. Assistant text and stream output are for display only.");
            stringBuilder.AppendLine("   - If this CLI exposes Bash, shell, or command execution tools, they remain forbidden unless the task prompt explicitly allows them.");
            stringBuilder.AppendLine("   - Do not write any other files unless the task prompt explicitly requires them.");
            stringBuilder.AppendLine("4. Before finishing, overwrite this UTF-8 staging result file with exactly one JSON object and nothing else:");
            stringBuilder.AppendLine((!string.IsNullOrWhiteSpace(value2.OutputTempJsonPath)) ? value2.OutputTempJsonPath : value2.OutputJsonPath);
            stringBuilder.AppendLine("5. Your final assistant response must repeat the same JSON object exactly, with no markdown fences and no surrounding prose.");
            stringBuilder.AppendLine("6. Do not emit planning/status text in assistant text responses. Keep intermediate progress in thinking/tool events only.");
            stringBuilder.AppendLine("7. Keep using the visible CLI normally so the user can see your live reasoning, tool calls, and progress in this terminal.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("The Unity editor treats the validated staging result file as the success signal and may stop this CLI as soon as that file is ready.");
            return stringBuilder.ToString();
        }

        private static string ResolveVisibleCliCommandPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            if (!Path.IsPathRooted((string)value))
            {
                List<string> list = GetExecutableSearchDirectories();
                for (int i = 0; i < list.Count; i++)
                {
                    string text = list[i];
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        string text2 = Path.Combine(text, (string)value + ".ps1");
                        if (File.Exists(text2))
                        {
                            return text2;
                        }
                    }
                }
                for (int j = 0; j < list.Count; j++)
                {
                    string text3 = list[j];
                    if (!string.IsNullOrWhiteSpace(text3))
                    {
                        string text4 = Path.Combine(text3, (string)value + ".cmd");
                        if (File.Exists(text4))
                        {
                            return text4;
                        }
                    }
                }
                return null;
            }
            if (!File.Exists((string)value))
            {
                return null;
            }
            return (string)value;
        }

        private static List<string> GetExecutableSearchDirectories()
        {
            List<string> list = new List<string>(32);
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddPathDirectories(list, hashSet, Environment.GetEnvironmentVariable("PATH"));
            AddPathDirectories(list, hashSet, GetPathEnvironmentVariable(EnvironmentVariableTarget.User));
            AddPathDirectories(list, hashSet, GetPathEnvironmentVariable(EnvironmentVariableTarget.Machine));
            return list;
        }

        private static void AddPathDirectories(List<string> texts, HashSet<string> texts2, object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return;
            }
            string[] array = ((string)value).Split(new char[1] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < array.Length; i++)
            {
                string text = array[i].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(text) && texts2.Add(text))
                {
                    texts.Add(text);
                }
            }
        }

        private static string GetPathEnvironmentVariable(EnvironmentVariableTarget value)
        {
            try
            {
                return Environment.GetEnvironmentVariable("PATH", value);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsWindowsEditor()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        protected static string EscapePowerShellSingleQuotedString(object text)
        {
            if (string.IsNullOrEmpty((string)text))
            {
                return string.Empty;
            }
            return ((string)text).Replace("'", "''");
        }

        protected virtual string GetWorkingDirectory(AiAnalysisRequest analysisRequest)
        {
            if (!string.IsNullOrWhiteSpace(analysisRequest.WorkingDirectory))
            {
                return analysisRequest.WorkingDirectory;
            }
            return Directory.GetCurrentDirectory();
        }

        protected static void AddQuotedArgument(List<string> texts, object text)
        {
            if (!string.IsNullOrWhiteSpace((string)text))
            {
                texts.Add("\"" + ((string)text).Replace("\"", "\\\"") + "\"");
            }
        }

        protected static string JoinArguments(List<string> texts)
        {
            if (texts != null && texts.Count >= 1)
            {
                return string.Join(" ", texts);
            }
            return string.Empty;
        }

        private static void ValidateRequestPathsWithinWorkingDirectory(object value, object value2)
        {
            string workingDirectory = ((AiAnalysisRequest)value2).WorkingDirectory;
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                throw new InvalidOperationException("CLI working directory is empty.");
            }
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ((AiAnalysisRequest)value2).PromptTemplatePath, "PromptTemplatePath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ((AiAnalysisRequest)value2).AnalysisPackageJsonPath, "AnalysisPackageJsonPath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ((AiAnalysisRequest)value2).OutputTempJsonPath, "OutputTempJsonPath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ((AiAnalysisRequest)value2).OutputJsonPath, "OutputJsonPath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ResolveRawOutputPath(value, value2), "OutputRawTextPath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ResolveCompletedMarkerPath(value, value2), "OutputCompletedPath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ResolveStreamOutputPath(value, value2), "StreamOutputPath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ResolveVisibleCliCompletionPath(value, value2), "VisibleCliCompletionPath");
            ValidatePathWithinWorkingDirectory(value, workingDirectory, ((AiJobContext)value).PromptPath, "PromptPath");
            if (((AiAnalysisRequest)value2).ImageInputPaths != null)
            {
                for (int i = 0; i < ((AiAnalysisRequest)value2).ImageInputPaths.Length; i++)
                {
                    ValidatePathWithinWorkingDirectory(value, workingDirectory, ((AiAnalysisRequest)value2).ImageInputPaths[i], $"request.ImageInputPaths[{i}]");
                }
                AiJobFileStore.LogDebug(value, "CLI path scope validation passed. workingDirectory=" + Path.GetFullPath(workingDirectory));
            }
        }

        private static void ValidatePathWithinWorkingDirectory(object value, object value2, object value3, object value4)
        {
            if (!string.IsNullOrWhiteSpace((string)value3))
            {
                string text = Path.GetFullPath((string)value2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath((string)value3);
                if (!fullPath.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    AiJobFileStore.LogDebug(value, "CLI path scope validation failed. label=" + (string)value4 + ", path=" + fullPath + ", workingDirectory=" + text);
                    throw new InvalidOperationException("CLI path is outside working directory: " + (string)value4 + " -> " + fullPath);
                }
            }
        }

        private static void KillProcessTree(object value, object value2, object value3)
        {
            if (value == null)
            {
                return;
            }
            try
            {
                if (!((Process)value).HasExited)
                {
                    AiJobFileStore.LogDebug(value2, $"{value3}. Killing process tree pid={((Process)value).Id}");
                    TryKillChildProcesses(((Process)value).Id, value2);
                    ((Process)value).Kill();
                }
            }
            catch (Exception ex)
            {
                AiJobFileStore.LogDebug(value2, $"KillProcessTree failed. pid={((Process)value)?.Id ?? 0}, error={ex.Message}");
            }
        }

        private static void TryKillChildProcesses(int value, object value2)
        {
            if (value <= 0)
            {
                return;
            }
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/PID {value} /T /F",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        process.Start();
                        process.WaitForExit(3000);
                        return;
                    }
                }
                using Process process2 = new Process();
                process2.StartInfo = new ProcessStartInfo
                {
                    FileName = "pkill",
                    Arguments = $"-TERM -P {value}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                process2.Start();
                process2.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                AiJobFileStore.LogDebug(value2, $"TryKillChildProcesses failed. pid={value}, error={ex.Message}");
            }
        }

        private static bool TryFinalizeResultArtifacts(object value, object value2, bool enabled, out string result)
        {
            result = null;
            if (value != null && value2 != null)
            {
                if (AiCliArtifactUtility.TryFinalizeResultFileWithRecoveryOption(value2, true, out result))
                {
                    EnsureCompletionMarker(value, value2);
                    AiJobFileStore.LogDebug(value, $"Detected valid JSON artifact. outputPath={((AiAnalysisRequest)value2).OutputJsonPath}, kind={((AiAnalysisRequest)value2).ResultDocumentKind}", false);
                    return true;
                }
                if (!RequiresExplicitOutputFile(value2) && !((AiAnalysisRequest)value2).DisableOutputRecovery)
                {
                    string text = ReadFileShared(((AiAnalysisRequest)value2).OutputRawTextPath);
                    string text2 = null;
                    string text3 = null;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (AiCliArtifactUtility.TryRecoverResultJsonFromProviderOutput(text, value2, out result))
                        {
                            EnsureCompletionMarker(value, value2);
                            AiJobFileStore.LogDebug(value, "JSON extracted successfully from final assistant message artifact. outputPath=" + ((AiAnalysisRequest)value2).OutputJsonPath, false);
                            return true;
                        }
                        text2 = result;
                    }
                    if (enabled && AiCliArtifactUtility.TryRecoverAssistantTextFromStreamFile(ResolveStreamOutputPath(value, value2), out var text4, out text3) && !string.IsNullOrWhiteSpace(text4))
                    {
                        if (!AiCliArtifactUtility.IsLikelyResultJson(((AiAnalysisRequest)value2).ResultDocumentKind, text4))
                        {
                            result = ((!string.IsNullOrWhiteSpace(text2)) ? text2 : null);
                            return false;
                        }
                        if (AiCliArtifactUtility.TryRecoverResultJsonFromProviderOutput(text4, value2, out result))
                        {
                            if (!string.IsNullOrWhiteSpace(((AiAnalysisRequest)value2).OutputRawTextPath))
                            {
                                AiJobFileStore.WriteTextAtomic(((AiAnalysisRequest)value2).OutputRawTextPath, text4);
                            }
                            EnsureCompletionMarker(value, value2);
                            AiJobFileStore.LogDebug(value, "JSON recovered successfully from stream output. outputPath=" + ((AiAnalysisRequest)value2).OutputJsonPath, false);
                            return true;
                        }
                    }
                    result = ((!string.IsNullOrWhiteSpace(text2)) ? text2 : (string.IsNullOrWhiteSpace(result) ? text3 : result));
                    return false;
                }
                return false;
            }
            return false;
        }

        private static bool TryFinalizeStagedResult(object value, object value2, out string result)
        {
            result = null;
            if (value == null || value2 == null)
            {
                return false;
            }
            if (!AiCliArtifactUtility.TryFinalizeResultFileWithRecoveryOption(value2, false, out result))
            {
                return false;
            }
            EnsureCompletionMarker(value, value2);
            AiJobFileStore.LogDebug(value, $"Detected valid staged JSON artifact while CLI is still running. outputPath={((AiAnalysisRequest)value2).OutputJsonPath}, kind={((AiAnalysisRequest)value2).ResultDocumentKind}", false);
            return true;
        }

        private static bool RequiresExplicitOutputFile(object value)
        {
            return ((AiAnalysisRequest)value)?.RequireExplicitOutputJsonFile ?? false;
        }

        private static void EnsureCompletionMarker(object value, object value2)
        {
            string text = ((!string.IsNullOrWhiteSpace(((AiAnalysisRequest)value2).OutputCompletedPath)) ? ((AiAnalysisRequest)value2).OutputCompletedPath : ((AiJobContext)value).CompletedPath);
            if (!string.IsNullOrWhiteSpace(text) && !File.Exists(text))
            {
                AiJobFileStore.WriteTextAtomic(text, string.Empty);
            }
        }

        private static string ReadFileShared(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && File.Exists((string)value))
            {
                try
                {
                    using FileStream stream = new FileStream((string)value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using StreamReader streamReader = new StreamReader(stream, _utf8NoBom, detectEncodingFromByteOrderMarks: true);
                    return streamReader.ReadToEnd();
                }
                catch
                {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        private string BuildCliFailureMessage(AiJobContext value, AiAnalysisRequest value2, string text2, int? value3, string text3 = null)
        {
            if (AiCliArtifactUtility.TryBuildFailureMessage(ResolveStreamOutputPath(value, value2), ResolveRawOutputPath(value, value2), text2, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
            if (!string.IsNullOrWhiteSpace(text3))
            {
                return text3;
            }
            if (!value3.HasValue)
            {
                return "CLI provider '" + GetProviderId() + "' failed.";
            }
            return $"CLI provider '{GetProviderId()}' exited with code {value3.Value}.";
        }

        private static string ResolveRawOutputPath(object value, object value2)
        {
            if (value2 != null && !string.IsNullOrWhiteSpace(((AiAnalysisRequest)value2).OutputRawTextPath))
            {
                return ((AiAnalysisRequest)value2).OutputRawTextPath;
            }
            return ((AiJobContext)value)?.RawOutputPath;
        }

        private static string ResolveStreamOutputPath(object value, object value2)
        {
            if (value2 != null && !string.IsNullOrWhiteSpace(((AiAnalysisRequest)value2).StreamOutputPath))
            {
                return ((AiAnalysisRequest)value2).StreamOutputPath;
            }
            return ((AiJobContext)value)?.StreamOutputPath;
        }

        private static string ResolveCompletedMarkerPath(object value, object value2)
        {
            if (value2 != null && !string.IsNullOrWhiteSpace(((AiAnalysisRequest)value2).OutputCompletedPath))
            {
                return ((AiAnalysisRequest)value2).OutputCompletedPath;
            }
            return ((AiJobContext)value)?.CompletedPath;
        }

        private static string ResolveVisibleCliCompletionPath(object value, object value2)
        {
            if (value2 != null && !string.IsNullOrWhiteSpace(((AiAnalysisRequest)value2).VisibleCliCompletionPath))
            {
                return ((AiAnalysisRequest)value2).VisibleCliCompletionPath;
            }
            return ((AiJobContext)value)?.VisibleCliCompletionPath;
        }

        private static void DeletePreviousOutputArtifacts(object value, object value2)
        {
            DeleteFileIfExistsQuietly(ResolveRawOutputPath(value, value2));
            DeleteFileIfExistsQuietly(ResolveStreamOutputPath(value, value2));
            DeleteFileIfExistsQuietly(ResolveVisibleCliCompletionPath(value, value2));
            DeleteFileIfExistsQuietly(ResolveCompletedMarkerPath(value, value2));
        }

        private static void DeleteFileIfExistsQuietly(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && File.Exists((string)value))
            {
                try
                {
                    AiJobFileStore.DeleteFileIfExists(value);
                }
                catch
                {
                }
            }
        }

        private void CaptureProcessOutputLine(AiJobContext value, AiAnalysisRequest value2, string text3, bool enabled, StringBuilder builder, object value3, CliEventState value4, VisibleCliOutputCapture value5)
        {
            if (text3 != null)
            {
                lock (value3)
                {
                    builder.AppendLine(text3);
                    WriteRawStreamLine(value5, text3);
                    WriteDisplayLogLines(value5, ConvertVisibleCliOutputLines(text3, value5));
                }
                string text = AiCliArtifactUtility.ExtractVisibleDisplayText(text3);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    AiJobFileStore.UpdateJobDetail(value, text);
                }
                if (!enabled && value4 != null && AiCliArtifactUtility.TryParseCliEvent(text3, out var aiCliEventKind, out var text2) && aiCliEventKind != 0)
                {
                    value4._eventKind = aiCliEventKind;
                    value4._message = text2 ?? string.Empty;
                    AiJobFileStore.LogDebug(value, $"Terminal stream event observed. kind={aiCliEventKind}, message={value4._message}", false);
                }
                LogProcessOutputLine(value, value5, text3, enabled);
            }
        }

        private static void LogProcessOutputLine(object value, object value2, object value3, bool enabled)
        {
            if (value != null && !string.IsNullOrWhiteSpace((string)value3) && (value2 == null || ((VisibleCliOutputCapture)value2)._streamWriter == null || enabled))
            {
                string text = ((string)value3).Trim();
                if (text.Length > 1200)
                {
                    text = text.Substring(0, 1200) + "...";
                }
                AiJobFileStore.LogDebug(value, ((!enabled) ? "STDOUT" : "STDERR") + ": " + text, enabled);
            }
        }

        private VisibleCliOutputCapture CreateVisibleCliOutputCapture(AiJobContext value, AiAnalysisRequest value2, bool enabled)
        {
            VisibleCliOutputCapture value3 = new VisibleCliOutputCapture
            {
                _isVisibleCliExecution = enabled
            };
            string text = ResolveStreamOutputPath(value, value2);
            if (!string.IsNullOrWhiteSpace(text))
            {
                AiJobFileStore.EnsureDirectory(Path.GetDirectoryName(text));
                DeleteFileIfExistsQuietly(text);
                value3._streamWriter = new StreamWriter(new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), _utf8NoBom)
                {
                    AutoFlush = true
                };
            }
            if (enabled)
            {
                string text2 = ResolveDisplayLogPath(value, value2);
                AiJobFileStore.EnsureDirectory(Path.GetDirectoryName(text2));
                DeleteFileIfExistsQuietly(text2);
                value3._displayLogWriter = new StreamWriter(new FileStream(text2, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), _utf8NoBom)
                {
                    AutoFlush = true
                };
                value3._displayLogWriter.WriteLine("[PSD2UIForm.AI] Visible CLI log mirror");
                value3._displayLogWriter.WriteLine("[PSD2UIForm.AI] Provider: " + GetProviderId());
                value3._displayLogWriter.WriteLine("[PSD2UIForm.AI] Stage: " + ((value2 == null) ? string.Empty : value2.StageDisplayName));
                value3._displayLogWriter.WriteLine(string.Empty);
            }
            return value3;
        }

        private static void WriteRawStreamLine(object value, object value2)
        {
            if (value != null && ((VisibleCliOutputCapture)value)._streamWriter != null && value2 != null)
            {
                ((VisibleCliOutputCapture)value)._streamWriter.WriteLine((string)value2);
            }
        }

        private static void WriteDisplayLogLines(object value2, List<string> texts)
        {
            if (value2 == null || !((VisibleCliOutputCapture)value2)._isVisibleCliExecution || ((VisibleCliOutputCapture)value2)._displayLogWriter == null || texts == null || texts.Count < 1)
            {
                return;
            }
            for (int i = 0; i < texts.Count; i++)
            {
                string value = texts[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ((VisibleCliOutputCapture)value2)._displayLogWriter.WriteLine(value);
                }
            }
        }

        private List<string> ConvertVisibleCliOutputLines(string text, VisibleCliOutputCapture value)
        {
            if (!string.IsNullOrWhiteSpace(text) && value != null && value._isVisibleCliExecution)
            {
                return GetProviderId() switch
                {
                    "codex-cli" => ConvertCodexVisibleCliEvent(text, value), 
                    "opencode-cli" => ConvertOpenCodeVisibleCliEvent(text, value), 
                    "claude-code-cli" => ConvertClaudeVisibleCliEvent(text, value), 
                    _ => WrapFallbackDisplayLine(text), 
                };
            }
            return null;
        }

        private static List<string> ConvertClaudeVisibleCliEvent(object value, object value2)
        {
            ClaudeVisibleEnvelope claudeVisibleEnvelope;
            try
            {
                claudeVisibleEnvelope = JsonUtility.FromJson<ClaudeVisibleEnvelope>((string)value);
            }
            catch
            {
                return WrapFallbackDisplayLine(value);
            }
            if (claudeVisibleEnvelope != null && !string.IsNullOrWhiteSpace(claudeVisibleEnvelope.type))
            {
                List<string> list = new List<string>(2);
                switch (claudeVisibleEnvelope.type)
                {
                case "result":
                    FlushClaudeContentBlock(value2, list);
                    list.Add("==> Done. Duration: " + claudeVisibleEnvelope.duration_ms + "ms");
                    break;
                case "user":
                {
                    if (claudeVisibleEnvelope.message == null || claudeVisibleEnvelope.message.content == null || claudeVisibleEnvelope.message.content.Length == 0)
                    {
                        break;
                    }
                    ClaudeVisibleMessageContent claudeVisibleMessageContent = claudeVisibleEnvelope.message.content[0];
                    if (claudeVisibleMessageContent != null && string.Equals(claudeVisibleMessageContent.type, "tool_result", StringComparison.Ordinal))
                    {
                        string text3 = NormalizeDisplayText(claudeVisibleMessageContent.content, 240);
                        if (!string.IsNullOrWhiteSpace(text3))
                        {
                            list.Add("  -> " + text3);
                        }
                    }
                    break;
                }
                case "stream_event":
                    if (claudeVisibleEnvelope.@event == null || string.IsNullOrWhiteSpace(claudeVisibleEnvelope.@event.type))
                    {
                        break;
                    }
                    if (string.Equals(claudeVisibleEnvelope.@event.type, "content_block_start", StringComparison.Ordinal))
                    {
                        string text = ((claudeVisibleEnvelope.@event.content_block != null) ? claudeVisibleEnvelope.@event.content_block.type : string.Empty);
                        ((VisibleCliOutputCapture)value2)._currentContentBlockType = text ?? string.Empty;
                        if (string.Equals(text, "thinking", StringComparison.Ordinal))
                        {
                            if (!((VisibleCliOutputCapture)value2)._claudeThinkingHeaderWritten)
                            {
                                list.Add("--- Thinking ---");
                                ((VisibleCliOutputCapture)value2)._claudeThinkingHeaderWritten = true;
                            }
                        }
                        else if (string.Equals(text, "tool_use", StringComparison.Ordinal))
                        {
                            string text2 = ((claudeVisibleEnvelope.@event.content_block != null) ? claudeVisibleEnvelope.@event.content_block.name : string.Empty);
                            list.Add("[Tool: " + text2 + "]");
                        }
                        else if (string.Equals(text, "text", StringComparison.Ordinal) && !((VisibleCliOutputCapture)value2)._claudeResponseHeaderWritten)
                        {
                            list.Add("--- Response ---");
                            ((VisibleCliOutputCapture)value2)._claudeResponseHeaderWritten = true;
                        }
                    }
                    else if (string.Equals(claudeVisibleEnvelope.@event.type, "content_block_delta", StringComparison.Ordinal))
                    {
                        string a = ((claudeVisibleEnvelope.@event.delta != null) ? claudeVisibleEnvelope.@event.delta.type : string.Empty);
                        if (string.Equals(a, "thinking_delta", StringComparison.Ordinal))
                        {
                            AppendNormalizedText(((VisibleCliOutputCapture)value2)._thinkingText, claudeVisibleEnvelope.@event.delta.thinking);
                        }
                        else if (string.Equals(a, "text_delta", StringComparison.Ordinal))
                        {
                            AppendNormalizedText(((VisibleCliOutputCapture)value2)._responseText, claudeVisibleEnvelope.@event.delta.text);
                        }
                    }
                    else if (string.Equals(claudeVisibleEnvelope.@event.type, "content_block_stop", StringComparison.Ordinal))
                    {
                        FlushClaudeContentBlock(value2, list);
                    }
                    break;
                case "system":
                    if (string.Equals(claudeVisibleEnvelope.subtype, "init", StringComparison.Ordinal))
                    {
                        int num = ((claudeVisibleEnvelope.tools != null) ? claudeVisibleEnvelope.tools.Length : 0);
                        list.Add("==> Claude session started (model: " + claudeVisibleEnvelope.model + ", tools: " + num + ")");
                    }
                    break;
                }
                if (list.Count <= 0)
                {
                    return null;
                }
                return list;
            }
            return null;
        }

        private static List<string> ConvertOpenCodeVisibleCliEvent(object value, object value2)
        {
            OpenCodeVisibleEnvelope openCodeVisibleEnvelope;
            try
            {
                openCodeVisibleEnvelope = JsonUtility.FromJson<OpenCodeVisibleEnvelope>((string)value);
            }
            catch
            {
                return WrapFallbackDisplayLine(value);
            }
            if (openCodeVisibleEnvelope != null && !string.IsNullOrWhiteSpace(openCodeVisibleEnvelope.type))
            {
                List<string> list = new List<string>(2);
                switch (openCodeVisibleEnvelope.type)
                {
                case "error":
                    list.Add("ERROR: " + NormalizeDisplayText((openCodeVisibleEnvelope.part != null) ? openCodeVisibleEnvelope.part.text : string.Empty, 320));
                    break;
                case "step_finish":
                {
                    int num = ((openCodeVisibleEnvelope.part != null && openCodeVisibleEnvelope.part.tokens != null) ? openCodeVisibleEnvelope.part.tokens.output : 0);
                    int num2 = ((openCodeVisibleEnvelope.part != null && openCodeVisibleEnvelope.part.tokens != null) ? openCodeVisibleEnvelope.part.tokens.reasoning : 0);
                    if (num > 0 && num2 > 0)
                    {
                        list.Add("==> Result: step completed (output=" + num + ", reasoning=" + num2 + ")");
                    }
                    else if (num > 0)
                    {
                        list.Add("==> Result: step completed (output=" + num + ")");
                    }
                    else
                    {
                        list.Add("==> Result: step completed");
                    }
                    break;
                }
                case "text":
                    if (!((VisibleCliOutputCapture)value2)._openCodeResponseHeaderWritten)
                    {
                        list.Add("--- Response ---");
                        ((VisibleCliOutputCapture)value2)._openCodeResponseHeaderWritten = true;
                    }
                    list.Add(NormalizeDisplayText((openCodeVisibleEnvelope.part != null) ? openCodeVisibleEnvelope.part.text : string.Empty, 600));
                    break;
                case "reasoning":
                    if (!((VisibleCliOutputCapture)value2)._openCodeThinkingHeaderWritten)
                    {
                        list.Add("--- Thinking ---");
                        ((VisibleCliOutputCapture)value2)._openCodeThinkingHeaderWritten = true;
                    }
                    list.Add(NormalizeDisplayText((openCodeVisibleEnvelope.part != null) ? openCodeVisibleEnvelope.part.text : string.Empty, 600));
                    break;
                case "step_start":
                    list.Add("==> Phase: running task");
                    break;
                }
                return NormalizeDisplayLines(list);
            }
            return null;
        }

        private static List<string> ConvertCodexVisibleCliEvent(object value, object value2)
        {
            CodexVisibleEnvelope codexVisibleEnvelope;
            try
            {
                codexVisibleEnvelope = JsonUtility.FromJson<CodexVisibleEnvelope>((string)value);
            }
            catch
            {
                return WrapFallbackDisplayLine(value);
            }
            if (codexVisibleEnvelope != null && !string.IsNullOrWhiteSpace(codexVisibleEnvelope.type))
            {
                List<string> list = new List<string>(2);
                switch (codexVisibleEnvelope.type)
                {
                case "turn.started":
                    list.Add("==> Phase: running task");
                    break;
                case "turn.completed":
                {
                    int num = ((codexVisibleEnvelope.usage != null) ? codexVisibleEnvelope.usage.output_tokens : 0);
                    int num2 = ((codexVisibleEnvelope.usage != null) ? codexVisibleEnvelope.usage.reasoning_output_tokens : 0);
                    if (num > 0 && num2 > 0)
                    {
                        list.Add("==> Result: task completed (output=" + num + ", reasoning=" + num2 + ")");
                    }
                    else if (num > 0)
                    {
                        list.Add("==> Result: task completed (output=" + num + ")");
                    }
                    else
                    {
                        list.Add("==> Result: task completed");
                    }
                    break;
                }
                case "session.started":
                case "thread.started":
                    list.Add("==> Codex session started");
                    break;
                case "error":
                case "turn.failed":
                case "session.failed":
                    list.Add("ERROR: " + GetCodexErrorText(codexVisibleEnvelope));
                    break;
                case "item.completed":
                case "item.started":
                    if (codexVisibleEnvelope.item != null)
                    {
                        string text = GetCodexItemPhase(codexVisibleEnvelope.item.type);
                        if (!string.IsNullOrWhiteSpace(text) && ShouldEmitDeduplicatedEvent(value2, "codex-phase", text))
                        {
                            list.Add("==> Phase: " + text);
                        }
                        string text2 = GetCodexItemLabel(codexVisibleEnvelope.item);
                        if (string.Equals(codexVisibleEnvelope.item.status, "failed", StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add("ERROR: " + text2);
                        }
                        else if (string.Equals(codexVisibleEnvelope.item.type, "agent_message", StringComparison.OrdinalIgnoreCase) && string.Equals(codexVisibleEnvelope.type, "item.completed", StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add("[Result] " + GetCodexResultSummary(codexVisibleEnvelope.item));
                        }
                        else if (string.Equals(codexVisibleEnvelope.type, "item.completed", StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add("[Item] Done: " + text2);
                        }
                        else
                        {
                            list.Add("[Item] " + text2);
                        }
                    }
                    break;
                }
                return NormalizeDisplayLines(list);
            }
            return null;
        }

        private static void FlushClaudeContentBlock(object value, List<string> texts)
        {
            if (value != null && texts != null)
            {
                if (string.Equals(((VisibleCliOutputCapture)value)._currentContentBlockType, "thinking", StringComparison.Ordinal) && ((VisibleCliOutputCapture)value)._thinkingText.Length > 0)
                {
                    texts.Add(NormalizeDisplayText(((VisibleCliOutputCapture)value)._thinkingText.ToString(), 600));
                    ((VisibleCliOutputCapture)value)._thinkingText.Length = 0;
                }
                else if (string.Equals(((VisibleCliOutputCapture)value)._currentContentBlockType, "text", StringComparison.Ordinal) && ((VisibleCliOutputCapture)value)._responseText.Length > 0)
                {
                    texts.Add(NormalizeDisplayText(((VisibleCliOutputCapture)value)._responseText.ToString(), 600));
                    ((VisibleCliOutputCapture)value)._responseText.Length = 0;
                }
                ((VisibleCliOutputCapture)value)._currentContentBlockType = string.Empty;
            }
        }

        private static void AppendNormalizedText(object value, object value2)
        {
            if (value != null && !string.IsNullOrWhiteSpace((string)value2))
            {
                if (((StringBuilder)value).Length > 0)
                {
                    ((StringBuilder)value).Append(' ');
                }
                ((StringBuilder)value).Append(NormalizeDisplayText(value2, 10000));
            }
        }

        private static bool ShouldEmitDeduplicatedEvent(object value2, object value3, object value4)
        {
            if (value2 != null && !string.IsNullOrWhiteSpace((string)value3))
            {
                if (((VisibleCliOutputCapture)value2)._dedupeValues.TryGetValue((string)value3, out var value) && string.Equals(value, (string)value4, StringComparison.Ordinal))
                {
                    return false;
                }
                ((VisibleCliOutputCapture)value2)._dedupeValues[(string)value3] = (string)(value4 ?? string.Empty);
                return true;
            }
            return true;
        }

        private static string GetCodexItemPhase(object value)
        {
            if (!((string)value == "mcp_tool_call") && !((string)value == "shell_command") && !((string)value == "function_call"))
            {
                if (!((string)value == "reasoning"))
                {
                    if (!((string)value == "agent_message"))
                    {
                        return string.Empty;
                    }
                    return "assembling result";
                }
                return "reasoning";
            }
            return "executing tools";
        }

        private static string GetCodexItemLabel(object value)
        {
            if (value != null)
            {
                if (string.IsNullOrWhiteSpace(((CodexVisibleItem)value).command))
                {
                    if (((CodexVisibleItem)value).arguments != null && !string.IsNullOrWhiteSpace(((CodexVisibleItem)value).arguments.title))
                    {
                        return NormalizeDisplayText(((CodexVisibleItem)value).arguments.title, 180);
                    }
                    if (!string.IsNullOrWhiteSpace(((CodexVisibleItem)value).server) && !string.IsNullOrWhiteSpace(((CodexVisibleItem)value).tool))
                    {
                        return NormalizeDisplayText(((CodexVisibleItem)value).server + "/" + ((CodexVisibleItem)value).tool, 180);
                    }
                    if (string.IsNullOrWhiteSpace(((CodexVisibleItem)value).tool))
                    {
                        if (string.IsNullOrWhiteSpace(((CodexVisibleItem)value).type))
                        {
                            return "item";
                        }
                        return NormalizeDisplayText(((CodexVisibleItem)value).type, 180);
                    }
                    return NormalizeDisplayText(((CodexVisibleItem)value).tool, 180);
                }
                return NormalizeDisplayText(((CodexVisibleItem)value).command, 180);
            }
            return "item";
        }

        private static string GetCodexResultSummary(object value)
        {
            if (value == null)
            {
                return "assistant response generated";
            }
            if (string.IsNullOrWhiteSpace(((CodexVisibleItem)value).text))
            {
                return "assistant response generated";
            }
            string text = ((CodexVisibleItem)value).text.TrimStart();
            if (!text.StartsWith("{", StringComparison.Ordinal) && !text.StartsWith("[", StringComparison.Ordinal))
            {
                return NormalizeDisplayText(((CodexVisibleItem)value).text, 240);
            }
            return "assistant response generated";
        }

        private static string GetCodexErrorText(object value)
        {
            if (value == null)
            {
                return "codex error";
            }
            if (string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).message))
            {
                if (!string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).summary))
                {
                    return NormalizeDisplayText(((CodexVisibleEnvelope)value).summary, 320);
                }
                if (!string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).title))
                {
                    return NormalizeDisplayText(((CodexVisibleEnvelope)value).title, 320);
                }
                if (!string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).content))
                {
                    return NormalizeDisplayText(((CodexVisibleEnvelope)value).content, 320);
                }
                if (((CodexVisibleEnvelope)value).error != null)
                {
                    if (!string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).error.message))
                    {
                        return NormalizeDisplayText(((CodexVisibleEnvelope)value).error.message, 320);
                    }
                    if (!string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).error.text))
                    {
                        return NormalizeDisplayText(((CodexVisibleEnvelope)value).error.text, 320);
                    }
                }
                if (((CodexVisibleEnvelope)value).item != null && ((CodexVisibleEnvelope)value).item.error != null)
                {
                    if (!string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).item.error.message))
                    {
                        return NormalizeDisplayText(((CodexVisibleEnvelope)value).item.error.message, 320);
                    }
                    if (!string.IsNullOrWhiteSpace(((CodexVisibleEnvelope)value).item.error.text))
                    {
                        return NormalizeDisplayText(((CodexVisibleEnvelope)value).item.error.text, 320);
                    }
                }
                return "codex error";
            }
            return NormalizeDisplayText(((CodexVisibleEnvelope)value).message, 320);
        }

        private static List<string> WrapFallbackDisplayLine(object value)
        {
            string text = NormalizeDisplayText(value, 600);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            return new List<string> { text };
        }

        private static List<string> NormalizeDisplayLines(List<string> ids)
        {
            if (ids != null && ids.Count >= 1)
            {
                List<string> list = new List<string>(ids.Count);
                for (int i = 0; i < ids.Count; i++)
                {
                    string text = NormalizeDisplayText(ids[i], 600);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        list.Add(text);
                    }
                }
                if (list.Count <= 0)
                {
                    return null;
                }
                return list;
            }
            return null;
        }

        private static string NormalizeDisplayText(object value, int value2)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            string text = ((string)value).Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("  ", " ");
            }
            if (value2 > 0 && text.Length > value2)
            {
                text = text.Substring(0, value2) + "...";
            }
            return text;
        }

        private void LaunchVisibleCliLogViewer(AiJobContext value, AiAnalysisRequest value2, bool enabled)
        {
            if (enabled)
            {
                try
                {
                    string text = ResolveDisplayLogPath(value, value2);
                    VisibleCliTerminalLauncher.Launch(value, GetProviderId(), text, value2?.StageDisplayName);
                }
                catch (Exception ex)
                {
                    AiJobFileStore.LogDebug(value, "Visible CLI log viewer launch failed. error=" + ex.Message);
                }
            }
        }

        private static void SignalVisibleCliLogViewerClose(object value, bool enabled)
        {
            if (enabled && value != null && !string.IsNullOrWhiteSpace(((AiJobContext)value).DebugConsoleCloseSignalPath))
            {
                try
                {
                    AiJobFileStore.WriteTextAtomic(((AiJobContext)value).DebugConsoleCloseSignalPath, DateTime.UtcNow.ToString("o"));
                }
                catch (Exception ex)
                {
                    AiJobFileStore.LogDebug(value, "Visible CLI log viewer close signal failed. error=" + ex.Message);
                }
            }
        }

        private static string ResolveDisplayLogPath(object value, object value2)
        {
            string text = ResolveStreamOutputPath(value, value2);
            if (!string.IsNullOrWhiteSpace(text))
            {
                string directoryName = Path.GetDirectoryName(text);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
                return Path.Combine(directoryName ?? string.Empty, fileNameWithoutExtension + ".display.log");
            }
            return Path.Combine((value == null) ? Directory.GetCurrentDirectory() : ((AiJobContext)value).ResponseDirectory, "visible-cli.display.log");
        }

        private static bool IsMacTerminalAvailable()
        {
            if (Environment.OSVersion.Platform != PlatformID.MacOSX)
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    return Directory.Exists("/Applications/Terminal.app");
                }
                return false;
            }
            return true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiCliProviderBase GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
