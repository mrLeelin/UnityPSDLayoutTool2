using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IAiCliProviderNamespace;
using AiJobFileStoreNamespace;
using AiJobActionNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using IAiJobListenerNamespace;
using AiJobStateNamespace;
using AiPathUtilityNamespace;
using VisibleCliTerminalLauncherNamespace;

namespace AiJobManagerNamespace
{
    internal sealed class AiJobManager
    {
        private sealed class AiJobRuntime
        {
            public AiJobContext _context;

            public IAiJobListener _listener;

            public Task _executionTask;

            public bool _isTerminalStateHandled;

            public bool _isCancellationRequested;

            public CancellationTokenSource _cancellationSource;

            public DateTime _startedAt;

            public VisibleCliTerminalLauncher.VisibleTerminalHandle _visibleTerminalHandle;

            private static AiJobRuntime s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static AiJobRuntime GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private readonly Dictionary<string, AiJobRuntime> _runningJobs = new Dictionary<string, AiJobRuntime>(StringComparer.OrdinalIgnoreCase);

        private bool _isEditorUpdateSubscribed;

        internal static AiJobManager s_ObfuscationSentinel;

        [SpecialName]
        internal bool HasActiveJobs()
        {
            return _runningJobs.Count > 0;
        }

        internal AiJobContext CreateJobContext(string text5, string text6, string text7)
        {
            string arg = (string.IsNullOrWhiteSpace(text5) ? "unknown" : text5.Replace("/", "_").Replace("\\", "_"));
            string text = $"{DateTime.Now:yyyyMMdd_HHmmss}_{arg}_{Guid.NewGuid():N}".Substring(0, 32);
            string text2 = CombineJobRootWithPsdName(text6, text7);
            string text3 = Path.Combine(text2, "request");
            string text4 = Path.Combine(text2, "response");
            AiJobFileStore.EnsureDirectory(text3);
            AiJobFileStore.EnsureDirectory(text4);
            AiJobContext obj = new AiJobContext
            {
                JobId = text,
                ProviderId = text5,
                JobDirectory = text2,
                RequestDirectory = text3,
                ResponseDirectory = text4,
                MetaPath = Path.Combine(text2, "meta.json"),
                AnalysisPackagePath = Path.Combine(text3, "analysis-package.json"),
                RecognitionCombinedPath = Path.Combine(text4, "combined-recognition.json"),
                RecognitionCombinedTempPath = Path.Combine(text4, "combined-recognition.json.tmp"),
                MainTypePath = Path.Combine(text4, "main-type.json"),
                MainTypeTempPath = Path.Combine(text4, "main-type.json.tmp"),
                ChildRelationPath = Path.Combine(text4, "child-relation.json"),
                ChildRelationTempPath = Path.Combine(text4, "child-relation.json.tmp"),
                StructuralPath = Path.Combine(text4, "structural.json"),
                StructuralTempPath = Path.Combine(text4, "structural.json.tmp"),
                OwnerScopeManifestPath = Path.Combine(text3, "owner-scope-manifest.json"),
                RecognitionInputManifestPath = Path.Combine(text3, "recognition-input", "manifest.json"),
                RecognitionNodeShardDirectory = Path.Combine(text3, "recognition-input", "nodes"),
                PromptPath = Path.Combine(text3, "prompt.md"),
                StatusPath = Path.Combine(text4, "status.json"),
                ResultPath = Path.Combine(text4, "result.json"),
                PatchPath = Path.Combine(text4, "patch.json"),
                PatchTempPath = Path.Combine(text4, "patch.json.tmp"),
                RawOutputPath = Path.Combine(text4, "raw-output.txt"),
                CompletedPath = Path.Combine(text4, "Completed"),
                StreamOutputPath = Path.Combine(text4, "stream-output.jsonl"),
                VisibleCliCompletionPath = Path.Combine(text4, "visible-cli-completed.json"),
                ErrorPath = Path.Combine(text4, "error.txt"),
                DebugLogPath = Path.Combine(text4, "debug-log.txt"),
                DebugConsoleCloseSignalPath = Path.Combine(text4, "debug-console-" + text + ".close")
            };
            PrepareJobDirectories(obj);
            AiJobFileStore.WriteJobStatus(obj, (AiJobState)0, "Job created.");
            AiJobFileStore.LogDebug(obj, "Job created. provider=" + text5 + ", jobDirectory=" + text2);
            return obj;
        }

        internal static string GetJobDirectoryPath(object value, object value2)
        {
            return CombineJobRootWithPsdName(value, value2);
        }

        internal static string CombineJobRootWithPsdName(object name, object value)
        {
            string path = AiPathUtility.SanitizePsdName(value);
            return Path.Combine((string)name, path);
        }

        internal void StartJob(AiJobContext aiJobContext, IAiCliProvider aiCliProvider, IAiJobListener aiJobListener, AiJobAction aiJobAction, bool showTerminal = true)
        {
            if (aiJobContext != null)
            {
                if (aiCliProvider == null)
                {
                    throw new ArgumentNullException("provider");
                }
                if (aiJobAction == null)
                {
                    throw new ArgumentNullException("execute");
                }
                if (_runningJobs.Count > 0)
                {
                    throw new InvalidOperationException("An AI job is already running.");
                }
                AiJobFileStore.WriteJobStatus(aiJobContext, (AiJobState)1, "Preparing provider execution.");
                AiJobFileStore.LogDebug(aiJobContext, "StartJob requested. provider=" + aiCliProvider.GetProviderId() + ", analysisPackage=" + aiJobContext.AnalysisPackagePath + ", mainTypePath=" + aiJobContext.MainTypePath + ", childRelationPath=" + aiJobContext.ChildRelationPath + ", structuralPath=" + aiJobContext.StructuralPath + ", patchPath=" + aiJobContext.PatchPath);
                VisibleCliTerminalLauncher.VisibleTerminalHandle value = null;
                if (showTerminal && aiCliProvider.GetCapabilities() != null && !aiCliProvider.GetCapabilities().UsesVisibleCliExecution)
                {
                    value = VisibleCliTerminalLauncher.LaunchForJob(aiJobContext, aiCliProvider.GetProviderId());
                }
                AiJobRuntime jobRuntime = new AiJobRuntime
                {
                    _context = aiJobContext,
                    _listener = aiJobListener,
                    _cancellationSource = new CancellationTokenSource(),
                    _startedAt = DateTime.Now,
                    _visibleTerminalHandle = value
                };
                jobRuntime._executionTask = Task.Run(delegate
                {
                    DateTime utcNow = DateTime.UtcNow;
                    AiJobFileStore.WriteJobStatus(aiJobContext, (AiJobState)2, "Provider execution started.");
                    AiJobFileStore.LogDebug(aiJobContext, "Background AI task entered running state.");
                    try
                    {
                        aiJobAction(aiJobContext, jobRuntime._cancellationSource.Token);
                        AiJobFileStore.LogDebug(aiJobContext, $"Workflow execution returned. patchExists={File.Exists(aiJobContext.PatchPath)}, rawOutputExists={File.Exists(aiJobContext.RawOutputPath)}");
                        if (!File.Exists(aiJobContext.PatchPath))
                        {
                            throw new FileNotFoundException("Provider finished without producing patch.json.", aiJobContext.PatchPath);
                        }
                        AiJobResultDocument aiJobResultDocument = new AiJobResultDocument
                        {
                            jobId = aiJobContext.JobId,
                            providerId = aiJobContext.ProviderId,
                            success = true,
                            completedAtUtc = DateTime.UtcNow.ToString("o"),
                            durationMs = (long)(DateTime.UtcNow - utcNow).TotalMilliseconds,
                            rawOutputFile = GetRelativePath(aiJobContext.JobDirectory, aiJobContext.RawOutputPath),
                            patchFile = GetRelativePath(aiJobContext.JobDirectory, aiJobContext.PatchPath),
                            errorFile = string.Empty
                        };
                        AiJobFileStore.WriteJsonAtomic(aiJobContext.ResultPath, aiJobResultDocument);
                        AiJobFileStore.WriteJobStatus(aiJobContext, (AiJobState)3, "Patch file generated successfully.");
                        AiJobFileStore.LogDebug(aiJobContext, $"AI task completed successfully. durationMs={aiJobResultDocument.durationMs}, patchPath={aiJobContext.PatchPath}");
                    }
                    catch (OperationCanceledException)
                    {
                        AiJobResultDocument aiJobResultDocument2 = new AiJobResultDocument
                        {
                            jobId = aiJobContext.JobId,
                            providerId = aiJobContext.ProviderId,
                            success = false,
                            completedAtUtc = DateTime.UtcNow.ToString("o"),
                            durationMs = (long)(DateTime.UtcNow - utcNow).TotalMilliseconds,
                            rawOutputFile = GetRelativePath(aiJobContext.JobDirectory, aiJobContext.RawOutputPath),
                            patchFile = string.Empty,
                            errorFile = string.Empty
                        };
                        AiJobFileStore.WriteJsonAtomic(aiJobContext.ResultPath, aiJobResultDocument2);
                        AiJobFileStore.WriteJobStatus(aiJobContext, (AiJobState)5, "AI task was cancelled by user.");
                        AiJobFileStore.LogDebug(aiJobContext, $"AI task cancelled. durationMs={aiJobResultDocument2.durationMs}");
                    }
                    catch (Exception ex2)
                    {
                        AiJobFileStore.WriteTextAtomic(aiJobContext.ErrorPath, ex2.ToString());
                        AiJobResultDocument value2 = new AiJobResultDocument
                        {
                            jobId = aiJobContext.JobId,
                            providerId = aiJobContext.ProviderId,
                            success = false,
                            completedAtUtc = DateTime.UtcNow.ToString("o"),
                            durationMs = (long)(DateTime.UtcNow - utcNow).TotalMilliseconds,
                            rawOutputFile = GetRelativePath(aiJobContext.JobDirectory, aiJobContext.RawOutputPath),
                            patchFile = string.Empty,
                            errorFile = GetRelativePath(aiJobContext.JobDirectory, aiJobContext.ErrorPath)
                        };
                        AiJobFileStore.WriteJsonAtomic(aiJobContext.ResultPath, value2);
                        AiJobFileStore.WriteJobStatus(aiJobContext, (AiJobState)4, ex2.Message);
                        AiJobFileStore.LogDebug(aiJobContext, $"AI task failed. exception={ex2}");
                    }
                });
                _runningJobs[aiJobContext.JobId] = jobRuntime;
                SubscribeEditorUpdate();
                return;
            }
            throw new ArgumentNullException("context");
        }

        internal void CancelJob(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !_runningJobs.TryGetValue(text, out var value) || value._isCancellationRequested)
            {
                return;
            }
            value._isCancellationRequested = true;
            AiJobFileStore.WriteJobStatus(value._context, (AiJobState)5, "AI task was cancelled by user.");
            AiJobFileStore.LogDebug(value._context, "Cancellation requested by user.");
            try
            {
                value._cancellationSource?.Cancel();
                DisposeVisibleTerminal(value);
            }
            catch
            {
            }
        }

        internal void PollRunningJobs()
        {
            if (_runningJobs.Count < 1)
            {
                UnsubscribeEditorUpdate();
                return;
            }
            UpdateProgressBar();
            List<string> list = new List<string>();
            foreach (KeyValuePair<string, AiJobRuntime> item in _runningJobs)
            {
                AiJobRuntime value = item.Value;
                if (value != null && !value._isTerminalStateHandled && TryGetTerminalState(value, out var aiJobState, out var text))
                {
                    value._isTerminalStateHandled = true;
                    ClearProgressBar();
                    DisposeVisibleTerminal(value);
                    if (aiJobState == (AiJobState)3)
                    {
                        AiJobFileStore.LogDebug(value._context, "PollRunningJobs observed completed state.");
                        value._listener?.OnJobCompleted(value._context);
                    }
                    else
                    {
                        AiJobFileStore.LogDebug(value._context, $"PollRunningJobs observed terminal state={aiJobState}, message={text}");
                        value._listener?.OnJobFailed(value._context, text);
                    }
                    list.Add(item.Key);
                }
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (_runningJobs.TryGetValue(list[i], out var value2))
                {
                    try
                    {
                        value2._cancellationSource?.Dispose();
                        DisposeVisibleTerminal(value2);
                    }
                    catch
                    {
                    }
                }
                _runningJobs.Remove(list[i]);
            }
            if (_runningJobs.Count < 1)
            {
                UnsubscribeEditorUpdate();
            }
        }

        private void SubscribeEditorUpdate()
        {
            if (!_isEditorUpdateSubscribed)
            {
                EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine((Delegate)(object)EditorApplication.update, (Delegate)new EditorApplication.CallbackFunction(PollRunningJobs));
                _isEditorUpdateSubscribed = true;
            }
        }

        private void UnsubscribeEditorUpdate()
        {
            if (_isEditorUpdateSubscribed)
            {
                EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Remove((Delegate)(object)EditorApplication.update, (Delegate)new EditorApplication.CallbackFunction(PollRunningJobs));
                _isEditorUpdateSubscribed = false;
                ClearProgressBar();
            }
        }

        private void UpdateProgressBar()
        {
            AiJobRuntime aiJobRuntime = null;
            foreach (KeyValuePair<string, AiJobRuntime> item in _runningJobs)
            {
                AiJobRuntime value = item.Value;
                if (value != null && !value._isTerminalStateHandled)
                {
                    aiJobRuntime = value;
                    break;
                }
            }
            if (aiJobRuntime != null)
            {
                string text = string.Empty;
                string text2 = string.Empty;
                if (AiJobFileStore.TryReadJson<AiJobStatusDocument>(aiJobRuntime._context.StatusPath, out var aiJobStatusDocument) && aiJobStatusDocument != null)
                {
                    text2 = aiJobStatusDocument.stage ?? string.Empty;
                    text = aiJobStatusDocument.detail ?? string.Empty;
                }
                double num = Math.Max(0.0, (DateTime.Now - aiJobRuntime._startedAt).TotalSeconds);
                float num2 = 0.08f + 0.87f * (1f - (float)Math.Exp((0.0 - num) / 12.0));
                num2 = Mathf.Clamp(num2, 0.08f, 0.95f);
                string text3 = BuildProgressMessage(text2, text);
                if (EditorUtility.DisplayCancelableProgressBar("AI 深度思考中...", text3, num2))
                {
                    CancelJob(aiJobRuntime._context.JobId);
                    ClearProgressBar();
                }
            }
            else
            {
                ClearProgressBar();
            }
        }

        private static bool TryGetTerminalState(object value, out AiJobState result, out string result2)
        {
            result = (AiJobState)2;
            result2 = null;
            if (value != null && ((AiJobRuntime)value)._context != null)
            {
                if (((AiJobRuntime)value)._isCancellationRequested)
                {
                    result = (AiJobState)5;
                    result2 = "AI task was cancelled by user.";
                    return true;
                }
                if (AiJobFileStore.TryReadJson<AiJobStatusDocument>(((AiJobRuntime)value)._context.StatusPath, out var aiJobStatusDocument) && aiJobStatusDocument != null)
                {
                    if (string.Equals(aiJobStatusDocument.state, "completed", StringComparison.OrdinalIgnoreCase))
                    {
                        result = (AiJobState)3;
                        result2 = aiJobStatusDocument.message;
                        return true;
                    }
                    if (string.Equals(aiJobStatusDocument.state, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        result = (AiJobState)4;
                        result2 = aiJobStatusDocument.message;
                        return true;
                    }
                    if (string.Equals(aiJobStatusDocument.state, "cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        result = (AiJobState)5;
                        result2 = aiJobStatusDocument.message;
                        return true;
                    }
                }
                if (((AiJobRuntime)value)._executionTask == null || !((AiJobRuntime)value)._executionTask.IsCompleted)
                {
                    return false;
                }
                if (((AiJobRuntime)value)._executionTask.IsFaulted)
                {
                    result2 = ((((AiJobRuntime)value)._executionTask.Exception == null) ? "AI task failed." : ((AiJobRuntime)value)._executionTask.Exception.GetBaseException().Message);
                }
                else if (File.Exists(((AiJobRuntime)value)._context.ErrorPath))
                {
                    result2 = AiJobFileStore.ReadTextIfExists(((AiJobRuntime)value)._context.ErrorPath);
                }
                else
                {
                    result2 = "AI task finished unexpectedly.";
                }
                result = (AiJobState)4;
                AiJobFileStore.WriteJobStatus(((AiJobRuntime)value)._context, result, result2);
                return true;
            }
            result = (AiJobState)4;
            result2 = "AI 任务上下文无效。";
            return true;
        }

        private static string BuildProgressMessage(object value, object value2)
        {
            string text = NormalizeProgressText(value);
            string text2 = NormalizeProgressText(value2);
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(text2))
            {
                return "AI 正在分析当前 PSD 节点树...";
            }
            string text3 = (string.IsNullOrWhiteSpace(text) ? text2 : ((!string.IsNullOrWhiteSpace(text2)) ? (text + " | " + text2) : text));
            if (text3.Length > 120)
            {
                text3 = text3.Substring(0, 117) + "...";
            }
            return text3;
        }

        private static string NormalizeProgressText(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            string text = ((string)value).Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }
            return text;
        }

        private static void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }

        private static string GetRelativePath(object value, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && !string.IsNullOrWhiteSpace((string)value2))
            {
                try
                {
                    return Path.GetRelativePath((string)value, (string)value2).Replace("\\", "/");
                }
                catch
                {
                    return ((string)value2).Replace("\\", "/");
                }
            }
            return string.Empty;
        }

        private static void PrepareJobDirectories(object value)
        {
            if (value != null)
            {
                AiJobFileStore.EnsureDirectory(((AiJobContext)value).JobDirectory);
                AiJobFileStore.EnsureDirectory(((AiJobContext)value).RequestDirectory);
                AiJobFileStore.EnsureDirectory(((AiJobContext)value).ResponseDirectory);
                DeleteDirectoryContents(((AiJobContext)value).RequestDirectory);
                DeleteDirectoryContents(((AiJobContext)value).ResponseDirectory);
                DeleteOrClearFile(((AiJobContext)value).MetaPath);
                DeleteOrClearFile(Path.Combine(((AiJobContext)value).JobDirectory, "latest-job.txt"));
                DeleteOrClearFile(((AiJobContext)value).DebugConsoleCloseSignalPath);
            }
        }

        private static void DisposeVisibleTerminal(object value)
        {
            if (value != null && ((AiJobRuntime)value)._visibleTerminalHandle != null)
            {
                ((AiJobRuntime)value)._visibleTerminalHandle.Close();
                ((AiJobRuntime)value)._visibleTerminalHandle = null;
            }
        }

        private static void DeleteDirectoryContents(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && Directory.Exists((string)value))
            {
                string[] files = Directory.GetFiles((string)value, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    DeleteOrClearFile(files[i]);
                }
            }
        }

        private static void DeleteOrClearFile(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value) || !File.Exists((string)value))
            {
                return;
            }
            try
            {
                AiJobFileStore.DeleteFileIfExists(value);
            }
            catch
            {
                try
                {
                    AiJobFileStore.WriteTextAtomic(value, string.Empty);
                }
                catch
                {
                }
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiJobManager GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
