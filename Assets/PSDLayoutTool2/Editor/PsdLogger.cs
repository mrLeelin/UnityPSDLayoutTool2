namespace PsdLayoutTool2
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Writes PSDLayoutTool2 diagnostic logs to a project-local file.
    /// </summary>
    internal static class PsdLogger
    {
        private const int MaxLogFilesToKeep = 50;
        private const long MaxLogBytes = 100L * 1024L * 1024L;
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly Encoding LogEncoding = new UTF8Encoding(false);
        private static Stopwatch sessionStopwatch;
        private static string currentLogPath;
        private static long currentLogBytes;
        private static bool hasError;
        private static bool writeFailureReported;
        private static bool logSizeLimitReached;
        private static long processMemoryAtStart;
        private static long managedMemoryAtStart;
        private static long unityAllocatedMemoryAtStart;
        private static long unityReservedMemoryAtStart;
        private static int[] gcCollectionsAtStart;
        private static string activeStep;
        private static Stopwatch activeStepStopwatch;
        private static readonly System.Collections.Generic.Dictionary<string, PhaseSummary> phaseSummaries =
            new System.Collections.Generic.Dictionary<string, PhaseSummary>(StringComparer.Ordinal);
        private static readonly System.Collections.Generic.Dictionary<string, string> sessionMetrics =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        private sealed class PhaseSummary
        {
            public int Count;
            public long TotalMilliseconds;
            public long MaximumMilliseconds;
        }

        /// <summary>
        /// Gets the directory that stores diagnostic logs.
        /// </summary>
        public static string LogDirectory
        {
            get
            {
                return Path.Combine(GetProjectRootPath(), "Library", "PSDLayoutTool2", "Logs");
            }
        }

        /// <summary>
        /// Starts a new import logging session.
        /// </summary>
        /// <param name="assetPath">PSD asset path.</param>
        /// <param name="mode">Import mode.</param>
        /// <param name="skipConflictPrompt">Whether the conflict prompt is skipped.</param>
        public static void BeginImportSession(string assetPath, string mode, bool skipConflictPrompt)
        {
            if (sessionStopwatch != null)
            {
                EndImportSession("Interrupted by a new session");
            }

            Directory.CreateDirectory(LogDirectory);
            CleanupOldLogs();

            string safeAssetName = MakeSafeFileName(string.IsNullOrEmpty(assetPath) ? "UnknownPSD" : assetPath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            currentLogPath = Path.Combine(LogDirectory, timestamp + "_" + safeAssetName + ".log");
            currentLogBytes = 0;
            sessionStopwatch = Stopwatch.StartNew();
            hasError = false;
            writeFailureReported = false;
            logSizeLimitReached = false;
            processMemoryAtStart = GetProcessMemoryBytes();
            managedMemoryAtStart = GC.GetTotalMemory(false);
            unityAllocatedMemoryAtStart = GetUnityMemoryBytes(false);
            unityReservedMemoryAtStart = GetUnityMemoryBytes(true);
            gcCollectionsAtStart = CaptureGcCollectionCounts();
            activeStep = null;
            activeStepStopwatch = null;
            phaseSummaries.Clear();
            sessionMetrics.Clear();

            WriteRawLine("============================================================");
            WriteRawLine("PSDLayoutTool2 Import Log");
            WriteRawLine("Started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            WriteRawLine("Unity: " + Application.unityVersion);
            WriteRawLine("Project: " + GetProjectRootPath());
            WriteRawLine("Asset: " + assetPath);
            WriteRawLine("Mode: " + mode);
            WriteRawLine("Skip conflict prompt: " + skipConflictPrompt);
            WriteRawLine("Baseline process memory: " + FormatBytes(processMemoryAtStart));
            WriteRawLine("Baseline managed memory: " + FormatBytes(managedMemoryAtStart));
            WriteRawLine("Max log file size: " + FormatBytes(MaxLogBytes));
            WriteRawLine("Max log folder size: " + FormatBytes(MaxLogBytes));
            WriteRawLine("============================================================");
        }

        /// <summary>
        /// Ends the active import logging session.
        /// </summary>
        /// <param name="result">Short result text.</param>
        public static void EndImportSession(string result)
        {
            if (sessionStopwatch == null)
            {
                return;
            }

            string status = hasError ? "FAILED" : "FINISHED";
            FinishActiveStep();
            Info("Session " + status + ": " + result);
            WritePerformanceSummary();
            WriteRawLine("Ended: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            WriteRawLine("Elapsed: " + sessionStopwatch.Elapsed);
            WriteRawLine("Log file: " + currentLogPath);
            WriteRawLine("============================================================");
            Debug.Log("[PSDLayoutTool2] Import log " + status + ": " + currentLogPath);
            CleanupOldLogs();

            sessionStopwatch = null;
            currentLogPath = null;
            currentLogBytes = 0;
            unityAllocatedMemoryAtStart = 0;
            unityReservedMemoryAtStart = 0;
            activeStep = null;
            activeStepStopwatch = null;
            gcCollectionsAtStart = null;
            phaseSummaries.Clear();
            sessionMetrics.Clear();
        }

        /// <summary>
        /// Writes an informational log entry.
        /// </summary>
        /// <param name="message">Message to write.</param>
        public static void Info(string message)
        {
            Write("INFO", message);
        }

        /// <summary>
        /// Writes a step log entry.
        /// </summary>
        /// <param name="message">Message to write.</param>
        public static void Step(string message)
        {
            FinishActiveStep();
            activeStep = string.IsNullOrEmpty(message) ? "(unnamed)" : message;
            activeStepStopwatch = Stopwatch.StartNew();
            Write("STEP", message);
        }

        /// <summary>
        /// Records a low-cost key/value metric in the current import session.
        /// Metrics are emitted once at the end of the session to avoid adding
        /// file I/O to the hot import path.
        /// </summary>
        public static void Metric(string name, object value)
        {
            if (sessionStopwatch == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            sessionMetrics[name] = value == null ? "null" : value.ToString();
        }

        /// <summary>
        /// Writes a warning log entry.
        /// </summary>
        /// <param name="message">Message to write.</param>
        public static void Warning(string message)
        {
            Write("WARN", message);
            Debug.LogWarning("[PSDLayoutTool2] " + message);
        }

        /// <summary>
        /// Writes an exception log entry.
        /// </summary>
        /// <param name="message">Context message.</param>
        /// <param name="exception">Exception to record.</param>
        public static void Exception(string message, Exception exception)
        {
            hasError = true;
            Write("ERROR", message);
            Write("ERROR", exception != null ? exception.ToString() : "Unknown exception");
            Debug.LogError("[PSDLayoutTool2] " + message + "\n" + exception);
        }

        /// <summary>
        /// Writes a standalone diagnostic record for opt-in tools that do not run
        /// through the PSD import session.
        /// </summary>
        public static string WriteStandalone(string title, string details)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                CleanupOldLogs();
                string safeTitle = MakeSafeFileName(string.IsNullOrEmpty(title) ? "Diagnostic" : title);
                string path = Path.Combine(LogDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + safeTitle + ".log");
                File.WriteAllText(path,
                    "PSDLayoutTool2 Diagnostic Log" + Environment.NewLine +
                    "Started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + Environment.NewLine +
                    "Title: " + title + Environment.NewLine +
                    (details ?? string.Empty) + Environment.NewLine,
                    LogEncoding);
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PSDLayoutTool2] Failed to write standalone diagnostic log: " + exception.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Reveals the log directory in the OS file manager.
        /// </summary>
        public static void RevealLogFolder()
        {
            Directory.CreateDirectory(LogDirectory);
            EditorUtility.RevealInFinder(LogDirectory);
        }

        /// <summary>
        /// Reveals the latest log file in the OS file manager.
        /// </summary>
        public static void RevealLatestLog()
        {
            string latestLog = GetLatestLogPath();
            if (string.IsNullOrEmpty(latestLog))
            {
                EditorUtility.DisplayDialog(
                    "PSDLayoutTool2",
                    "No PSDLayoutTool2 log file has been created yet.",
                    "OK");
                return;
            }

            EditorUtility.RevealInFinder(latestLog);
        }

        /// <summary>
        /// Menu entry for revealing the log directory.
        /// </summary>
        [MenuItem("Tools/PSD Layout Tool 2/Open Log Folder")]
        private static void OpenLogFolder()
        {
            RevealLogFolder();
        }

        /// <summary>
        /// Menu entry for revealing the latest log file.
        /// </summary>
        [MenuItem("Tools/PSD Layout Tool 2/Open Latest Log")]
        private static void OpenLatestLog()
        {
            RevealLatestLog();
        }

        private static void Write(string level, string message)
        {
            string elapsed = sessionStopwatch != null ? sessionStopwatch.Elapsed.ToString(@"hh\:mm\:ss\.fff") : "--:--:--.---";
            string prefix = DateTime.Now.ToString("HH:mm:ss.fff") + " +" + elapsed + " [" + level + "] ";
            string[] lines = SplitLines(message);
            foreach (string line in lines)
            {
                WriteRawLine(prefix + line);
            }
        }

        private static void FinishActiveStep()
        {
            if (string.IsNullOrEmpty(activeStep) || activeStepStopwatch == null)
            {
                return;
            }

            long elapsedMilliseconds = Math.Max(0L, activeStepStopwatch.ElapsedMilliseconds);
            PhaseSummary summary;
            if (!phaseSummaries.TryGetValue(activeStep, out summary))
            {
                summary = new PhaseSummary();
                phaseSummaries.Add(activeStep, summary);
            }

            summary.Count++;
            summary.TotalMilliseconds += elapsedMilliseconds;
            summary.MaximumMilliseconds = Math.Max(summary.MaximumMilliseconds, elapsedMilliseconds);
            activeStep = null;
            activeStepStopwatch = null;
        }

        private static void WritePerformanceSummary()
        {
            long processMemoryAtEnd = GetProcessMemoryBytes();
            long managedMemoryAtEnd = GC.GetTotalMemory(false);
            long unityAllocatedMemoryAtEnd = GetUnityMemoryBytes(false);
            long unityReservedMemoryAtEnd = GetUnityMemoryBytes(true);
            int[] gcCollectionsAtEnd = CaptureGcCollectionCounts();
            WriteRawLine("PERFORMANCE_BASELINE totalElapsedMs=" + sessionStopwatch.ElapsedMilliseconds +
                         " processMemoryStartBytes=" + processMemoryAtStart +
                         " processMemoryEndBytes=" + processMemoryAtEnd +
                         " processMemoryDeltaBytes=" + FormatDeltaBytes(processMemoryAtStart, processMemoryAtEnd) +
                         " managedMemoryStartBytes=" + managedMemoryAtStart +
                         " managedMemoryEndBytes=" + managedMemoryAtEnd +
                         " managedMemoryDeltaBytes=" + (managedMemoryAtEnd - managedMemoryAtStart) +
                         " unityAllocatedMemoryStartBytes=" + unityAllocatedMemoryAtStart +
                         " unityAllocatedMemoryEndBytes=" + unityAllocatedMemoryAtEnd +
                         " unityAllocatedMemoryDeltaBytes=" + FormatDeltaBytes(unityAllocatedMemoryAtStart, unityAllocatedMemoryAtEnd) +
                         " unityReservedMemoryStartBytes=" + unityReservedMemoryAtStart +
                         " unityReservedMemoryEndBytes=" + unityReservedMemoryAtEnd +
                         " unityReservedMemoryDeltaBytes=" + FormatDeltaBytes(unityReservedMemoryAtStart, unityReservedMemoryAtEnd) +
                         " gcGen0Delta=" + GetGcDelta(gcCollectionsAtStart, gcCollectionsAtEnd, 0) +
                         " gcGen1Delta=" + GetGcDelta(gcCollectionsAtStart, gcCollectionsAtEnd, 1) +
                         " gcGen2Delta=" + GetGcDelta(gcCollectionsAtStart, gcCollectionsAtEnd, 2));

            foreach (var metric in sessionMetrics.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                WriteRawLine("METRIC " + metric.Key + "=" + metric.Value);
            }

            foreach (var phase in phaseSummaries
                         .OrderByDescending(pair => pair.Value.TotalMilliseconds)
                         .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                         .Take(20))
            {
                WriteRawLine("PHASE_SUMMARY name=" + phase.Key +
                             " count=" + phase.Value.Count +
                             " totalMs=" + phase.Value.TotalMilliseconds +
                             " maxMs=" + phase.Value.MaximumMilliseconds);
            }
        }

        private static long GetProcessMemoryBytes()
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    long privateMemorySize = process.PrivateMemorySize64;
                    return privateMemorySize > 0 ? privateMemorySize : -1L;
                }
            }
            catch
            {
                return -1L;
            }
        }

        private static int[] CaptureGcCollectionCounts()
        {
            return new[]
            {
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2)
            };
        }

        private static long GetUnityMemoryBytes(bool reserved)
        {
            try
            {
                long value = reserved
                    ? UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()
                    : UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                return value > 0 ? value : -1L;
            }
            catch
            {
                return -1L;
            }
        }

        private static string FormatDeltaBytes(long start, long end)
        {
            return start < 0 || end < 0 ? "unknown" : (end - start).ToString();
        }

        private static int GetGcDelta(int[] start, int[] end, int generation)
        {
            if (start == null || end == null || generation < 0 || generation >= start.Length ||
                generation >= end.Length)
            {
                return -1;
            }

            return end[generation] - start[generation];
        }

        private static void WriteRawLine(string line)
        {
            if (string.IsNullOrEmpty(currentLogPath))
            {
                return;
            }

            if (logSizeLimitReached)
            {
                return;
            }

            try
            {
                AppendLineWithinSizeLimit(line);
            }
            catch (Exception exception)
            {
                if (!writeFailureReported)
                {
                    writeFailureReported = true;
                    Debug.LogWarning("[PSDLayoutTool2] Failed to write diagnostic log: " + exception.Message);
                }
            }
        }

        private static void AppendLineWithinSizeLimit(string line)
        {
            string text = line + Environment.NewLine;
            byte[] bytes = LogEncoding.GetBytes(text);
            if (currentLogBytes + bytes.Length > MaxLogBytes)
            {
                TryWriteLogLimitMarker();
                return;
            }

            using (FileStream stream = new FileStream(currentLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            currentLogBytes += bytes.Length;
        }

        private static void TryWriteLogLimitMarker()
        {
            logSizeLimitReached = true;

            string marker =
                DateTime.Now.ToString("HH:mm:ss.fff") +
                " [WARN] Log file reached " +
                FormatBytes(MaxLogBytes) +
                "; further entries were omitted." +
                Environment.NewLine;
            byte[] markerBytes = LogEncoding.GetBytes(marker);
            if (currentLogBytes + markerBytes.Length <= MaxLogBytes)
            {
                using (FileStream stream = new FileStream(currentLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    stream.Write(markerBytes, 0, markerBytes.Length);
                }

                currentLogBytes += markerBytes.Length;
            }

            Debug.LogWarning("[PSDLayoutTool2] Diagnostic log reached " + FormatBytes(MaxLogBytes) + ": " + currentLogPath);
        }

        private static string[] SplitLines(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return new[] { string.Empty };
            }

            return message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static string GetProjectRootPath()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (dataPath.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase))
            {
                return dataPath.Substring(0, dataPath.Length - "/Assets".Length);
            }

            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : dataPath;
        }

        private static string MakeSafeFileName(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                bool invalid = InvalidFileNameChars.Contains(character) ||
                    character == '/' ||
                    character == '\\' ||
                    character == ':' ||
                    character == '*';
                builder.Append(invalid ? '_' : character);
            }

            string safe = builder.ToString().Trim('_', '.', ' ');
            if (string.IsNullOrEmpty(safe))
            {
                safe = "UnknownPSD";
            }

            return safe.Length > 80 ? safe.Substring(safe.Length - 80) : safe;
        }

        private static string GetLatestLogPath()
        {
            if (!Directory.Exists(LogDirectory))
            {
                return string.Empty;
            }

            return Directory.GetFiles(LogDirectory, "*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static void CleanupOldLogs()
        {
            FileInfo[] logs = Directory.GetFiles(LogDirectory, "*.log")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();

            for (int i = MaxLogFilesToKeep; i < logs.Length; i++)
            {
                DeleteLogFile(logs[i]);
            }

            logs = Directory.GetFiles(LogDirectory, "*.log")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();

            long totalBytes = logs.Sum(file => file.Exists ? file.Length : 0);
            for (int i = logs.Length - 1; i >= 0 && totalBytes > MaxLogBytes; i--)
            {
                FileInfo log = logs[i];
                long length = log.Exists ? log.Length : 0;
                if (DeleteLogFile(log))
                {
                    totalBytes -= length;
                }
            }
        }

        private static bool DeleteLogFile(FileInfo log)
        {
            try
            {
                log.Delete();
                return true;
            }
            catch
            {
                // Logging cleanup should never block an import.
                return false;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
            {
                return "unknown";
            }

            return (bytes / 1024f / 1024f).ToString("0.#") + " MB";
        }
    }
}
