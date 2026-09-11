using System;
using System.IO;
using System.Text;
using System.Threading;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using AiJobStateNamespace;

namespace AiJobFileStoreNamespace
{
    internal sealed class AiJobFileStore
    {
        private static readonly UTF8Encoding s_Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly object s_DebugLogLock = new object();

        private static readonly object s_FileAccessLock = new object();

        private static AiJobFileStore s_ObfuscationSentinel;

        internal static void EnsureDirectory(object text)
        {
            if (!string.IsNullOrWhiteSpace((string)text))
            {
                Directory.CreateDirectory((string)text);
            }
        }

        internal static void WriteJsonAtomic<TDocument>(object value, TDocument tDocument)
        {
            lock (s_FileAccessLock)
            {
                WriteTextAtomicCore(value, JsonUtility.ToJson((object)tDocument, true));
            }
        }

        internal static bool TryReadJson<TDocument>(object json, out TDocument result)
        {
            result = default(TDocument);
            if (!string.IsNullOrWhiteSpace((string)json) && File.Exists((string)json))
            {
                try
                {
                    lock (s_FileAccessLock)
                    {
                        string text = ReadTextWithRetry(json);
                        result = JsonUtility.FromJson<TDocument>(text);
                    }
                    return result != null;
                }
                catch
                {
                    result = default(TDocument);
                    return false;
                }
            }
            return false;
        }

        internal static void WriteTextAtomic(object value, object value2)
        {
            WriteTextAtomicCore(value, value2 ?? string.Empty);
        }

        internal static void LogDebug(object aiJobContext, object text2, bool enabled = true)
        {
            if (aiJobContext != null && !string.IsNullOrWhiteSpace(((AiJobContext)aiJobContext).DebugLogPath) && !string.IsNullOrWhiteSpace((string)text2))
            {
                string text = $"[{DateTime.Now:HH:mm:ss.fff}] {text2}";
                lock (s_DebugLogLock)
                {
                    AppendTextWithRetry(((AiJobContext)aiJobContext).DebugLogPath, text + Environment.NewLine);
                }
                if (enabled)
                {
                    Debug.Log((object)("[PSD2UIForm.AI] " + text));
                }
            }
        }

        internal static void WriteJobStatus(object aiJobContext, AiJobState aiJobState, object text3, string text4 = null)
        {
            if (aiJobContext == null || string.IsNullOrWhiteSpace(((AiJobContext)aiJobContext).StatusPath))
            {
                return;
            }
            lock (s_FileAccessLock)
            {
                string text = text4;
                string text2 = null;
                if (text == null && TryReadJson<AiJobStatusDocument>(((AiJobContext)aiJobContext).StatusPath, out var aiJobStatusDocument))
                {
                    text = ((aiJobStatusDocument != null) ? aiJobStatusDocument.detail : string.Empty);
                    text2 = ((aiJobStatusDocument != null) ? aiJobStatusDocument.stage : string.Empty);
                }
                AiJobStatusDocument value = new AiJobStatusDocument
                {
                    jobId = ((AiJobContext)aiJobContext).JobId,
                    providerId = ((AiJobContext)aiJobContext).ProviderId,
                    state = GetJobStateName(aiJobState),
                    updatedAtUtc = DateTime.UtcNow.ToString("o"),
                    message = (string)(text3 ?? string.Empty),
                    stage = (text2 ?? string.Empty),
                    detail = (text ?? string.Empty)
                };
                WriteJsonAtomic(((AiJobContext)aiJobContext).StatusPath, value);
            }
        }

        internal static void UpdateJobStage(object aiJobContext, object text)
        {
            if (aiJobContext == null || string.IsNullOrWhiteSpace(((AiJobContext)aiJobContext).StatusPath))
            {
                return;
            }
            lock (s_FileAccessLock)
            {
                AiJobStatusDocument aiJobStatusDocument = null;
                if (!TryReadJson<AiJobStatusDocument>(((AiJobContext)aiJobContext).StatusPath, out aiJobStatusDocument) || aiJobStatusDocument == null)
                {
                    aiJobStatusDocument = new AiJobStatusDocument
                    {
                        jobId = ((AiJobContext)aiJobContext).JobId,
                        providerId = ((AiJobContext)aiJobContext).ProviderId,
                        state = GetJobStateName((AiJobState)2),
                        message = string.Empty,
                        stage = string.Empty,
                        detail = string.Empty
                    };
                }
                aiJobStatusDocument.updatedAtUtc = DateTime.UtcNow.ToString("o");
                aiJobStatusDocument.stage = (string)(text ?? string.Empty);
                WriteJsonAtomic(((AiJobContext)aiJobContext).StatusPath, aiJobStatusDocument);
            }
        }

        internal static void UpdateJobDetail(object aiJobContext, object text)
        {
            if (aiJobContext == null || string.IsNullOrWhiteSpace(((AiJobContext)aiJobContext).StatusPath))
            {
                return;
            }
            lock (s_FileAccessLock)
            {
                AiJobStatusDocument aiJobStatusDocument = null;
                if (!TryReadJson<AiJobStatusDocument>(((AiJobContext)aiJobContext).StatusPath, out aiJobStatusDocument) || aiJobStatusDocument == null)
                {
                    aiJobStatusDocument = new AiJobStatusDocument
                    {
                        jobId = ((AiJobContext)aiJobContext).JobId,
                        providerId = ((AiJobContext)aiJobContext).ProviderId,
                        state = GetJobStateName((AiJobState)2),
                        message = string.Empty,
                        stage = string.Empty,
                        detail = string.Empty
                    };
                }
                aiJobStatusDocument.updatedAtUtc = DateTime.UtcNow.ToString("o");
                aiJobStatusDocument.detail = (string)(text ?? string.Empty);
                WriteJsonAtomic(((AiJobContext)aiJobContext).StatusPath, aiJobStatusDocument);
            }
        }

        internal static string ReadTextIfExists(object text)
        {
            if (string.IsNullOrWhiteSpace((string)text) || !File.Exists((string)text))
            {
                return string.Empty;
            }
            return ReadTextWithRetry(text);
        }

        internal static void DeleteFileIfExists(object text)
        {
            if (!string.IsNullOrWhiteSpace((string)text) && File.Exists((string)text))
            {
                DeleteFileWithRetry(text);
            }
        }

        internal static string GetJobStateName(AiJobState aiJobState)
        {
            return aiJobState switch
            {
                (AiJobState)0 => "created", 
                (AiJobState)1 => "preparing", 
                (AiJobState)2 => "running", 
                (AiJobState)3 => "completed", 
                (AiJobState)4 => "failed", 
                (AiJobState)5 => "cancelled", 
                _ => "unknown", 
            };
        }

        private static string ReadTextWithRetry(object value)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    using FileStream stream = new FileStream((string)value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using StreamReader streamReader = new StreamReader(stream, s_Utf8NoBom, detectEncodingFromByteOrderMarks: true);
                    return streamReader.ReadToEnd();
                }
                catch (Exception obj) when (obj is IOException && i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException) when (i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
            }
            using FileStream stream2 = new FileStream((string)value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader streamReader2 = new StreamReader(stream2, s_Utf8NoBom, detectEncodingFromByteOrderMarks: true);
            return streamReader2.ReadToEnd();
        }

        private static void WriteTextAtomicCore(object value, object value2)
        {
            EnsureDirectory(Path.GetDirectoryName((string)value));
            string text = (string)value + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    using StreamWriter streamWriter = new StreamWriter(stream, s_Utf8NoBom, 4096, leaveOpen: false);
                    streamWriter.Write((string)(value2 ?? string.Empty));
                    streamWriter.Flush();
                }
                ReplaceFileWithRetry(text, value);
            }
            finally
            {
                if (File.Exists(text))
                {
                    DeleteFileWithRetry(text);
                }
            }
        }

        private static void AppendTextWithRetry(object value, object value2)
        {
            EnsureDirectory(Path.GetDirectoryName((string)value));
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    using FileStream stream = new FileStream((string)value, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                    using StreamWriter streamWriter = new StreamWriter(stream, s_Utf8NoBom, 4096, leaveOpen: false);
                    streamWriter.Write((string)(value2 ?? string.Empty));
                    streamWriter.Flush();
                    return;
                }
                catch (Exception obj) when (obj is IOException && i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
                catch (Exception obj2) when (obj2 is UnauthorizedAccessException && i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
            }
            using FileStream stream2 = new FileStream((string)value, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            using StreamWriter streamWriter2 = new StreamWriter(stream2, s_Utf8NoBom, 4096, leaveOpen: false);
            streamWriter2.Write((string)(value2 ?? string.Empty));
            streamWriter2.Flush();
        }

        private static void ReplaceFileWithRetry(object value, object value2)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    if (File.Exists((string)value2))
                    {
                        DeleteFileWithRetry(value2);
                    }
                    File.Move((string)value, (string)value2);
                    return;
                }
                catch (Exception obj) when (obj is IOException && i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException) when (i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
            }
            if (File.Exists((string)value2))
            {
                DeleteFileWithRetry(value2);
            }
            File.Move((string)value, (string)value2);
        }

        private static void DeleteFileWithRetry(object value)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    File.Delete((string)value);
                    return;
                }
                catch (Exception obj) when (obj is IOException && i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
                catch (Exception obj2) when (obj2 is UnauthorizedAccessException && i + 1 < 20)
                {
                    Thread.Sleep(50);
                }
            }
            File.Delete((string)value);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiJobFileStore GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
