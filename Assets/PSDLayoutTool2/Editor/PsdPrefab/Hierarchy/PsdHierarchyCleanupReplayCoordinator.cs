namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    internal static class PsdHierarchyCleanupReplayCoordinator
    {
        private const string TemporaryPrefabFolder =
            "Assets/PSDLayoutTool2Settings/HierarchyCleanupReplayTemp";
        private const string PendingReplayDirectory =
            "Library/PSDLayoutTool2/HierarchyCleanupReplayPending";
        private const int MaxTransientStartupRetriesPerStage = 3;
        private static readonly HashSet<string> PendingTargets =
            new HashSet<string>(StringComparer.Ordinal);

        [Serializable]
        private sealed class PendingReplayRecord
        {
            public int schemaVersion;
            public string projectRoot = string.Empty;
            public string targetPath = string.Empty;
            public string expectedTargetGuid = string.Empty;
            public string sourcePsdGuid = string.Empty;
            public string temporaryPath = string.Empty;
            public List<string> replayPlanJsonStages = new List<string>();
            public List<string> replayBindingJsonStages = new List<string>();
            public int nextStageIndex;
            public int inFlightStageIndex = -1;
            public string checkpointPath = string.Empty;
            public bool retryAfterDomainReload;
            public int transientRetryStageIndex = -1;
            public int transientRetryAttempts;
            public long retryNotBeforeUtcTicks;
            public bool terminal;
            public int terminalStageIndex = -1;
            public string terminalState = string.Empty;
            public string terminalStage = string.Empty;
            public string terminalMessage = string.Empty;
            public bool rebindProfileAfterCommit;
            // Kept only so legacy records deserialize deterministically before
            // validation rejects them and requests a fresh v2 analysis.
            public string replayPlanJson = string.Empty;
        }

        [InitializeOnLoadMethod]
        private static void InstallReplayPumpAfterDomainReload()
        {
            FinalizeLegacyDeferredRetriesAfterDomainReload();
            EnsureReplayPump();
        }

        internal static bool TryStageAndSchedule(
            string sourcePsdAssetPath,
            string targetPrefabPath,
            GameObject generatedCandidate,
            out string error)
        {
            return TryStageAndSchedule(
                sourcePsdAssetPath,
                targetPrefabPath,
                generatedCandidate,
                rebindProfileAfterCommit: false,
                out error);
        }

        internal static bool TryStageAndScheduleFreshGeneration(
            string sourcePsdAssetPath,
            string targetPrefabPath,
            GameObject generatedCandidate,
            out string error)
        {
            return TryStageAndSchedule(
                sourcePsdAssetPath,
                targetPrefabPath,
                generatedCandidate,
                rebindProfileAfterCommit: true,
                out error);
        }

        private static bool TryStageAndSchedule(
            string sourcePsdAssetPath,
            string targetPrefabPath,
            GameObject generatedCandidate,
            bool rebindProfileAfterCommit,
            out string error)
        {
            error = string.Empty;
            string sourcePath = NormalizeAssetPath(sourcePsdAssetPath);
            string targetPath = NormalizeAssetPath(targetPrefabPath);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(sourceGuid))
            {
                error = "Source PSD GUID could not be resolved for cleanup replay.";
                return false;
            }
            PsdHierarchyCleanupReplayProfile profile =
                PsdHierarchyCleanupReplayProfile.Load(targetPath, sourceGuid);
            if (profile == null) return false;

            string expectedTargetGuid = AssetDatabase.AssetPathToGUID(targetPath);
            if (string.IsNullOrEmpty(expectedTargetGuid))
            {
                error = "Target Prefab GUID could not be resolved for cleanup replay.";
                return false;
            }
            if (generatedCandidate == null)
            {
                error = "Generated Prefab candidate is missing for cleanup replay.";
                return false;
            }
            if (HasPendingRecordForTarget(targetPath) || !PendingTargets.Add(targetPath))
            {
                error = "A cleanup replay is already pending for the target Prefab.";
                return false;
            }

            string temporaryPath = string.Empty;
            try
            {
                string temporaryDirectory =
                    TemporaryPrefabFolder + "/" + Guid.NewGuid().ToString("N");
                EnsureAssetFolder(temporaryDirectory);
                temporaryPath = temporaryDirectory + "/" + Path.GetFileName(targetPath);
                GameObject staged = PrefabUtility.SaveAsPrefabAsset(generatedCandidate, temporaryPath);
                if (staged == null)
                    throw new InvalidOperationException("Generated Prefab candidate could not be staged for cleanup replay.");
                if (!profile.TryGetReplayStageSources(
                        sourceGuid,
                        targetPath,
                        requireCurrentTargetGuid: !rebindProfileAfterCommit,
                        out IReadOnlyList<string> replayPlanJsonStages,
                        out IReadOnlyList<string> replayBindingJsonStages,
                        out string replayError))
                    throw new InvalidOperationException(replayError);

                // Fail before queueing when the first stage cannot bind. Later stages are intentionally
                // not built here because their evidence may refer to nodes created by an earlier stage.
                if (!PsdHierarchyCleanupReplayProfile.TryBuildReplayStage(
                        replayPlanJsonStages[0],
                        replayBindingJsonStages[0],
                        targetPath,
                        temporaryPath,
                        out _,
                        out replayError))
                    throw new InvalidOperationException(replayError);

                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    throw new InvalidOperationException("Unity project root could not be resolved.");

                WritePendingReplayRecord(
                    projectRoot,
                    targetPath,
                    expectedTargetGuid,
                    sourceGuid,
                    temporaryPath,
                    replayPlanJsonStages,
                    replayBindingJsonStages,
                    rebindProfileAfterCommit);
                PendingTargets.Remove(targetPath);
                EnsureReplayPump();
                return true;
            }
            catch (Exception exception)
            {
                PendingTargets.Remove(targetPath);
                DeleteTemporaryAsset(temporaryPath);
                error = exception.Message;
                if (IsPermanentReplayFailure(exception.Message))
                {
                    // 永久失败（v1 路径计划、绑定证据缺失或对应关系无法证明）时标记 Profile
                    // 需要重新分析，让下一次导入直接给出明确原因，而不是重复暂存后再失败。
                    PsdHierarchyCleanupReplayProfile.TryMarkRequiresRebindByGuid(
                        sourceGuid,
                        targetPath,
                        exception.Message);
                }

                return false;
            }
        }

        private static async void ReplayAndCommitAsync(
            string projectRoot,
            string targetPath,
            string temporaryPath,
            PendingReplayRecord record,
            string pendingRecordPath)
        {
            bool retainPendingRecord = false;
            try
            {
                AssertTargetGuid(record);
                RestoreInterruptedStage(record, pendingRecordPath);
                for (int stageIndex = record.nextStageIndex;
                     stageIndex < record.replayPlanJsonStages.Count;
                     stageIndex++)
                {
                    AssertTargetGuid(record);
                    CreateStageCheckpoint(record, stageIndex, pendingRecordPath);
                    if (!PsdHierarchyCleanupReplayProfile.TryBuildReplayStage(
                            record.replayPlanJsonStages[stageIndex],
                            record.replayBindingJsonStages[stageIndex],
                            targetPath,
                            temporaryPath,
                            out string preparedStageJson,
                            out string prepareError))
                    {
                        PsdHierarchyCleanupReplayProfile.TryMarkRequiresRebindByGuid(
                            record.sourcePsdGuid,
                            targetPath,
                            prepareError);
                        Debug.LogError(
                            "PSD Prefab cleanup replay stage " + (stageIndex + 1) + "/" +
                            record.replayPlanJsonStages.Count +
                            " could not bind to the Prefab saved by the previous stage. " + prepareError);
                        MarkTerminalFailure(
                            record,
                            stageIndex,
                            PsdHierarchyCleanupExecutionState.Rejected,
                            "rebind",
                            prepareError);
                        WritePendingReplayRecord(pendingRecordPath, record);
                        retainPendingRecord = true;
                        return;
                    }
                    PsdHierarchyChatCleanupExecutionResult replay =
                        await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                            projectRoot,
                            preparedStageJson);
                    if (!replay.success)
                    {
                        if (IsPermanentReplayFailure(replay.message))
                        {
                            PsdHierarchyCleanupReplayProfile.TryMarkRequiresRebindByGuid(
                                record.sourcePsdGuid,
                                targetPath,
                                replay.message);
                            Debug.LogError(
                                "PSD Prefab cleanup replay stage " + (stageIndex + 1) + "/" +
                                record.replayPlanJsonStages.Count +
                                " is incompatible with the current generated Prefab. " +
                                "The target replacement was not committed and the replay Profile now requires a fresh confirmed plan. " +
                                replay.message);
                        }

                        if (ShouldRetryAutomatically(replay.state, replay.message) &&
                            ScheduleTransientStartupRetry(record, stageIndex))
                        {
                            WritePendingReplayRecord(pendingRecordPath, record);
                            retainPendingRecord = true;
                            Debug.LogError(
                                "PSD Prefab cleanup replay stage " + (stageIndex + 1) + "/" +
                                record.replayPlanJsonStages.Count +
                                " was rejected because Unity is still starting. " +
                                "The staged candidate and checkpoint will retry automatically after startup completes. " +
                                replay.message);
                            return;
                        }

                        MarkTerminalFailure(
                            record,
                            stageIndex,
                            replay.state,
                            replay.stage,
                            replay.message);
                        WritePendingReplayRecord(pendingRecordPath, record);
                        retainPendingRecord = true;
                        Debug.LogError(
                            "PSD Prefab cleanup replay stage " + (stageIndex + 1) + "/" +
                            record.replayPlanJsonStages.Count +
                            " stopped with state " + replay.state + ". " +
                            "The target replacement was not committed. The staged candidate, checkpoint, and terminal evidence were retained and will not run automatically again. " +
                            (replay.state == PsdHierarchyCleanupExecutionState.Partial ||
                             replay.state == PsdHierarchyCleanupExecutionState.Uncertain
                                ? "Other assets may already have changed; inspect the retained evidence before any manual recovery. "
                                : string.Empty) +
                            replay.message);
                        return;
                    }

                    string completedCheckpoint = record.checkpointPath;
                    record.nextStageIndex = stageIndex + 1;
                    record.inFlightStageIndex = -1;
                    record.checkpointPath = string.Empty;
                    record.transientRetryStageIndex = -1;
                    record.transientRetryAttempts = 0;
                    record.retryNotBeforeUtcTicks = 0;
                    WritePendingReplayRecord(pendingRecordPath, record);
                    DeleteAssetIfOwned(completedCheckpoint);
                }

                AssertTargetGuid(record);
                GameObject replayedContents = PrefabUtility.LoadPrefabContents(temporaryPath);
                try
                {
                    if (replayedContents == null)
                        throw new InvalidOperationException("Replayed temporary Prefab could not be loaded.");
                    if (PrefabUtility.SaveAsPrefabAsset(replayedContents, targetPath) == null)
                        throw new InvalidOperationException("Replayed Prefab could not replace the generated target.");
                }
                finally
                {
                    if (replayedContents != null) PrefabUtility.UnloadPrefabContents(replayedContents);
                }

                AssetDatabase.ImportAsset(
                    targetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                string committedGuid = AssetDatabase.AssetPathToGUID(targetPath);
                if (!string.Equals(record.expectedTargetGuid, committedGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException("Cleanup replay changed the target Prefab GUID.");

                if (record.rebindProfileAfterCommit)
                {
                    PsdHierarchyCleanupReplayProfile profile =
                        PsdHierarchyCleanupReplayProfile.Load(targetPath, record.sourcePsdGuid);
                    if (profile == null)
                        throw new InvalidOperationException("Confirmed cleanup Profile disappeared before fresh replay could bind the target.");
                    profile.RebindToFreshTarget(record.sourcePsdGuid, targetPath);
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }

                Debug.Log("PSD Prefab cleanup replay completed: " + targetPath);
            }
            catch (Exception exception)
            {
                MarkTerminalFailure(
                    record,
                    record.inFlightStageIndex >= 0
                        ? record.inFlightStageIndex
                        : record.nextStageIndex,
                    PsdHierarchyCleanupExecutionState.Uncertain,
                    "exception",
                    exception.Message);
                try
                {
                    WritePendingReplayRecord(pendingRecordPath, record);
                    retainPendingRecord = true;
                }
                catch (Exception recordException)
                {
                    Debug.LogException(recordException);
                }
                Debug.LogException(exception);
            }
            finally
            {
                if (!retainPendingRecord)
                {
                    DeleteTemporaryAsset(temporaryPath);
                    DeletePendingReplayRecord(pendingRecordPath);
                }
                PendingTargets.Remove(targetPath);
                StopReplayPumpIfIdle();
            }
        }

        private static void EnsureReplayPump()
        {
            EditorApplication.update -= ReplayPump;
            EditorApplication.update += ReplayPump;
        }

        private static void ReplayPump()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            ResumePendingReplays();
            StopReplayPumpIfIdle();
        }

        private static void ResumePendingReplays()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            string pendingDirectory = Path.Combine(projectRoot, PendingReplayDirectory);
            if (!Directory.Exists(pendingDirectory)) return;

            foreach (string pendingRecordPath in Directory.GetFiles(pendingDirectory, "*.json"))
            {
                PendingReplayRecord record = null;
                try
                {
                    record = JsonUtility.FromJson<PendingReplayRecord>(
                        File.ReadAllText(pendingRecordPath));
                    if (record != null && record.terminal)
                        continue;
                    ValidatePendingReplayRecord(projectRoot, record);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "Discarded an invalid PSD Prefab cleanup replay record: " +
                        pendingRecordPath + ". " + exception.Message);
                    if (record != null) DeleteTemporaryAsset(record.temporaryPath);
                    DeletePendingReplayRecord(pendingRecordPath);
                    continue;
                }

                if (!IsPendingReplayRunnable(
                        record.terminal,
                        record.retryAfterDomainReload,
                        record.retryNotBeforeUtcTicks,
                        DateTime.UtcNow.Ticks))
                    continue;
                if (!PendingTargets.Add(record.targetPath)) continue;
                ReplayAndCommitAsync(
                    record.projectRoot,
                    record.targetPath,
                    record.temporaryPath,
                    record,
                    pendingRecordPath);
            }
        }

        private static void StopReplayPumpIfIdle()
        {
            if (PendingTargets.Count > 0) return;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string pendingDirectory = string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, PendingReplayDirectory);
            if (!string.IsNullOrEmpty(pendingDirectory) &&
                Directory.Exists(pendingDirectory) &&
                HasRunnablePendingRecordFiles(pendingDirectory))
                return;

            EditorApplication.update -= ReplayPump;
        }

        private static string WritePendingReplayRecord(
            string projectRoot,
            string targetPath,
            string expectedTargetGuid,
            string sourcePsdGuid,
            string temporaryPath,
            IReadOnlyList<string> replayPlanJsonStages,
            IReadOnlyList<string> replayBindingJsonStages,
            bool rebindProfileAfterCommit)
        {
            string pendingDirectory = Path.Combine(projectRoot, PendingReplayDirectory);
            Directory.CreateDirectory(pendingDirectory);
            string pendingRecordPath = Path.Combine(
                pendingDirectory,
                Guid.NewGuid().ToString("N") + ".json");
            var record = new PendingReplayRecord
            {
                schemaVersion = 3,
                projectRoot = projectRoot,
                targetPath = targetPath,
                expectedTargetGuid = expectedTargetGuid,
                sourcePsdGuid = sourcePsdGuid,
                temporaryPath = temporaryPath,
                replayPlanJsonStages = new List<string>(replayPlanJsonStages),
                replayBindingJsonStages = new List<string>(replayBindingJsonStages),
                nextStageIndex = 0,
                inFlightStageIndex = -1,
                rebindProfileAfterCommit = rebindProfileAfterCommit,
            };
            WritePendingReplayRecord(pendingRecordPath, record);
            return pendingRecordPath;
        }

        private static void WritePendingReplayRecord(
            string pendingRecordPath,
            PendingReplayRecord record)
        {
            string temporaryRecordPath = pendingRecordPath + ".tmp";
            File.WriteAllText(
                temporaryRecordPath,
                JsonUtility.ToJson(record),
                new System.Text.UTF8Encoding(false));
            if (File.Exists(pendingRecordPath))
                File.Replace(temporaryRecordPath, pendingRecordPath, null);
            else
                File.Move(temporaryRecordPath, pendingRecordPath);
        }

        private static void ValidatePendingReplayRecord(
            string currentProjectRoot,
            PendingReplayRecord record)
        {
            if (record == null)
                throw new InvalidDataException("Replay record is empty.");
            if (!string.Equals(
                    Path.GetFullPath(record.projectRoot ?? string.Empty),
                    Path.GetFullPath(currentProjectRoot),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Replay record belongs to a different Unity project.");

            record.targetPath = NormalizeAssetPath(record.targetPath);
            record.temporaryPath = NormalizeAssetPath(record.temporaryPath);
            if (!record.targetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !record.targetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Replay target Prefab path is invalid.");
            if (record.schemaVersion != 3)
            {
                throw new InvalidDataException(
                    PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                    " 旧重放记录缺少逐阶段绑定证据，不会迁移进执行队列。");
            }
            if (string.IsNullOrEmpty(record.expectedTargetGuid) ||
                !TargetGuidMatches(
                    record.expectedTargetGuid,
                    AssetDatabase.AssetPathToGUID(record.targetPath)))
                throw new InvalidDataException("Replay target Prefab GUID no longer matches the staged record.");
            if (!record.temporaryPath.StartsWith(TemporaryPrefabFolder + "/", StringComparison.Ordinal) ||
                !record.temporaryPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Replay temporary Prefab path is invalid.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(record.temporaryPath) == null)
                throw new InvalidDataException("Replay temporary Prefab no longer exists.");
            if (record.replayPlanJsonStages == null || record.replayPlanJsonStages.Count == 0)
                throw new InvalidDataException("Replay stage list is empty.");
            if (record.replayBindingJsonStages == null ||
                record.replayBindingJsonStages.Count != record.replayPlanJsonStages.Count)
                throw new InvalidDataException(
                    PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                    " 重放记录的阶段与绑定证据数量不一致。");
            ResolveRestartStage(
                record.nextStageIndex,
                record.inFlightStageIndex,
                record.replayPlanJsonStages.Count);
            if (record.transientRetryStageIndex < -1 ||
                record.transientRetryStageIndex >= record.replayPlanJsonStages.Count ||
                record.transientRetryAttempts < 0 ||
                record.retryNotBeforeUtcTicks < 0)
                throw new InvalidDataException("Replay transient retry state is invalid.");
            if (record.terminal &&
                (record.terminalStageIndex < 0 ||
                 record.terminalStageIndex >= record.replayPlanJsonStages.Count ||
                 string.IsNullOrWhiteSpace(record.terminalState) ||
                 string.IsNullOrWhiteSpace(record.terminalMessage)))
                throw new InvalidDataException("Replay terminal failure evidence is invalid.");
            record.checkpointPath = NormalizeAssetPath(record.checkpointPath);
            if (record.inFlightStageIndex >= 0)
            {
                if (!record.checkpointPath.StartsWith(
                        NormalizeAssetPath(Path.GetDirectoryName(record.temporaryPath)) + "/",
                        StringComparison.Ordinal) ||
                    !record.checkpointPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(record.checkpointPath) == null)
                    throw new InvalidDataException("Replay stage checkpoint is invalid or missing.");
            }
            for (int index = 0; index < record.replayPlanJsonStages.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(record.replayPlanJsonStages[index]))
                    throw new InvalidDataException("Replay stage is empty.");
                if (string.IsNullOrWhiteSpace(record.replayBindingJsonStages[index]))
                    throw new InvalidDataException(
                        PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                        " 重放阶段缺少节点绑定证据。");
            }
        }

        internal static bool TargetGuidMatches(string expectedGuid, string currentGuid)
        {
            return !string.IsNullOrEmpty(expectedGuid) &&
                   !string.IsNullOrEmpty(currentGuid) &&
                   string.Equals(expectedGuid, currentGuid, StringComparison.Ordinal);
        }

        internal static int ResolveRestartStage(
            int nextStageIndex,
            int inFlightStageIndex,
            int stageCount)
        {
            if (nextStageIndex < 0 || nextStageIndex > stageCount)
                throw new InvalidDataException("Replay stage index is invalid.");
            if (inFlightStageIndex < -1 || inFlightStageIndex >= stageCount ||
                (inFlightStageIndex >= 0 && inFlightStageIndex != nextStageIndex))
                throw new InvalidDataException("Replay in-flight stage index is invalid.");
            return inFlightStageIndex >= 0 ? inFlightStageIndex : nextStageIndex;
        }

        private static void AssertTargetGuid(PendingReplayRecord record)
        {
            string currentGuid = AssetDatabase.AssetPathToGUID(record.targetPath);
            if (!TargetGuidMatches(record.expectedTargetGuid, currentGuid))
                throw new InvalidOperationException(
                    "Cleanup replay target Prefab GUID changed while replay was pending.");
        }

        private static void CreateStageCheckpoint(
            PendingReplayRecord record,
            int stageIndex,
            string pendingRecordPath)
        {
            string directory = NormalizeAssetPath(Path.GetDirectoryName(record.temporaryPath));
            string checkpointPath = directory + "/__stage_checkpoint.prefab";
            DeleteAssetIfOwned(checkpointPath);
            CopyPrefabContents(record.temporaryPath, checkpointPath, "Replay stage checkpoint");
            record.inFlightStageIndex = stageIndex;
            record.checkpointPath = checkpointPath;
            record.retryAfterDomainReload = false;
            record.retryNotBeforeUtcTicks = 0;
            WritePendingReplayRecord(pendingRecordPath, record);
        }

        private static bool ScheduleTransientStartupRetry(
            PendingReplayRecord record,
            int stageIndex)
        {
            if (record.transientRetryStageIndex != stageIndex)
            {
                record.transientRetryStageIndex = stageIndex;
                record.transientRetryAttempts = 0;
            }

            record.transientRetryAttempts++;
            if (record.transientRetryAttempts > MaxTransientStartupRetriesPerStage)
            {
                record.retryNotBeforeUtcTicks = 0;
                return false;
            }

            record.retryAfterDomainReload = false;
            record.retryNotBeforeUtcTicks = DateTime.UtcNow.AddSeconds(
                GetTransientRetryDelaySeconds(record.transientRetryAttempts)).Ticks;
            return true;
        }

        internal static bool ShouldRetryAutomatically(
            PsdHierarchyCleanupExecutionState state,
            string message)
        {
            return state == PsdHierarchyCleanupExecutionState.Rejected &&
                   IsTransientEditorStartupFailure(message);
        }

        private static void MarkTerminalFailure(
            PendingReplayRecord record,
            int stageIndex,
            PsdHierarchyCleanupExecutionState state,
            string stage,
            string message)
        {
            record.terminal = true;
            record.terminalStageIndex = Math.Max(
                0,
                Math.Min(stageIndex, record.replayPlanJsonStages.Count - 1));
            record.terminalState = state.ToString();
            record.terminalStage = stage ?? string.Empty;
            record.terminalMessage = string.IsNullOrWhiteSpace(message)
                ? "Replay stopped without a diagnostic message."
                : message;
            record.retryAfterDomainReload = false;
            record.retryNotBeforeUtcTicks = 0;
        }

        internal static bool IsTransientEditorStartupFailure(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                   message.IndexOf("Unity server is starting", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsPermanentReplayFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            return message.IndexOf("Direct child was not found:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("asset did not load:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("has no Unity GUID:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("is not referenced by the current target Prefab:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   // 重放能力尚未迁移到 v2：这是永久失败，必须标记需要重新分析，
                   // 不能作为瞬时启动失败在每个域重载后反复重试。
                   message.IndexOf(
                       PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage,
                       StringComparison.Ordinal) >= 0;
        }

        internal static int GetTransientRetryDelaySeconds(int retryAttempt)
        {
            return retryAttempt <= 1 ? 2 : retryAttempt == 2 ? 4 : 8;
        }

        internal static bool IsPendingReplayRunnable(
            bool terminal,
            bool retryAfterDomainReload,
            long retryNotBeforeUtcTicks,
            long utcNowTicks)
        {
            return !terminal &&
                   !retryAfterDomainReload &&
                   retryNotBeforeUtcTicks <= utcNowTicks;
        }

        private static void RestoreInterruptedStage(
            PendingReplayRecord record,
            string pendingRecordPath)
        {
            int restartStage = ResolveRestartStage(
                record.nextStageIndex,
                record.inFlightStageIndex,
                record.replayPlanJsonStages.Count);
            if (record.inFlightStageIndex < 0) return;

            string completedCheckpoint = record.checkpointPath;
            CopyPrefabContents(completedCheckpoint, record.temporaryPath, "Replay stage restore");
            record.nextStageIndex = restartStage;
            record.inFlightStageIndex = -1;
            record.checkpointPath = string.Empty;
            WritePendingReplayRecord(pendingRecordPath, record);
            DeleteAssetIfOwned(completedCheckpoint);
        }

        private static void CopyPrefabContents(string sourcePath, string destinationPath, string operation)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                if (contents == null)
                    throw new InvalidOperationException(operation + " source could not be loaded.");
                if (PrefabUtility.SaveAsPrefabAsset(contents, destinationPath) == null)
                    throw new InvalidOperationException(operation + " could not save the Prefab.");
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.ImportAsset(
                destinationPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void DeleteAssetIfOwned(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (normalized.StartsWith(TemporaryPrefabFolder + "/", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(normalized);
        }

        private static void FinalizeLegacyDeferredRetriesAfterDomainReload()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;
            string pendingDirectory = Path.Combine(projectRoot, PendingReplayDirectory);
            if (!Directory.Exists(pendingDirectory)) return;

            foreach (string path in Directory.GetFiles(pendingDirectory, "*.json"))
            {
                try
                {
                    PendingReplayRecord record = JsonUtility.FromJson<PendingReplayRecord>(
                        File.ReadAllText(path));
                    if (record == null || record.schemaVersion != 3 || record.terminal || !record.retryAfterDomainReload)
                        continue;
                    MarkTerminalFailure(
                        record,
                        record.inFlightStageIndex >= 0
                            ? record.inFlightStageIndex
                            : record.nextStageIndex,
                        PsdHierarchyCleanupExecutionState.Rejected,
                        "legacy-deferred-retry",
                        "Replay was deferred by the previous retry policy. Automatic execution was stopped after reload; inspect the retained staged candidate and checkpoint before manual recovery.");
                    WritePendingReplayRecord(path, record);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "Could not finalize a deferred PSD Prefab cleanup replay: " + exception.Message);
                }
            }
        }

        private static bool HasRunnablePendingRecordFiles(string pendingDirectory)
        {
            foreach (string path in Directory.GetFiles(pendingDirectory, "*.json"))
            {
                try
                {
                    PendingReplayRecord record = JsonUtility.FromJson<PendingReplayRecord>(
                        File.ReadAllText(path));
                    // A scheduled retry keeps the pump alive until its deadline; terminal evidence never does.
                    if (record == null || (!record.terminal && !record.retryAfterDomainReload))
                        return true;
                }
                catch
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasPendingRecordForTarget(string targetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return false;
            string pendingDirectory = Path.Combine(projectRoot, PendingReplayDirectory);
            if (!Directory.Exists(pendingDirectory)) return false;

            foreach (string path in Directory.GetFiles(pendingDirectory, "*.json"))
            {
                try
                {
                    PendingReplayRecord record = JsonUtility.FromJson<PendingReplayRecord>(
                        File.ReadAllText(path));
                    if (record != null && string.Equals(
                            NormalizeAssetPath(record.targetPath),
                            targetPath,
                            StringComparison.Ordinal))
                        return true;
                }
                catch
                {
                    // ResumePendingReplays owns invalid record cleanup.
                }
            }
            return false;
        }

        private static void DeletePendingReplayRecord(string pendingRecordPath)
        {
            if (string.IsNullOrEmpty(pendingRecordPath)) return;
            try
            {
                if (File.Exists(pendingRecordPath)) File.Delete(pendingRecordPath);
            }
            catch (IOException exception)
            {
                Debug.LogWarning(
                    "Could not delete PSD Prefab cleanup replay record: " + exception.Message);
            }
        }

        private static void DeleteTemporaryAsset(string temporaryPath)
        {
            if (string.IsNullOrEmpty(temporaryPath)) return;
            string normalized = NormalizeAssetPath(temporaryPath);
            string directory = NormalizeAssetPath(Path.GetDirectoryName(normalized));
            if (directory.StartsWith(TemporaryPrefabFolder + "/", StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(directory);
                return;
            }
            AssetDatabase.DeleteAsset(normalized);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string[] parts = NormalizeAssetPath(assetFolder).Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[index]);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException("Could not create cleanup replay folder: " + next);
                }
                current = next;
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }
    }
}
