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
        private static readonly HashSet<string> PendingTargets =
            new HashSet<string>(StringComparer.Ordinal);

        [Serializable]
        private sealed class PendingReplayRecord
        {
            public int schemaVersion;
            public string projectRoot = string.Empty;
            public string targetPath = string.Empty;
            public string expectedTargetGuid = string.Empty;
            public string temporaryPath = string.Empty;
            public List<string> replayPlanJsonStages = new List<string>();
            public int nextStageIndex;
            public int inFlightStageIndex = -1;
            public string checkpointPath = string.Empty;
            public bool retryAfterDomainReload;
            // Schema-1 pending records used one plan. Retain the field so a
            // domain reload during an upgrade can still resume safely.
            public string replayPlanJson = string.Empty;
        }

        [InitializeOnLoadMethod]
        private static void InstallReplayPumpAfterDomainReload()
        {
            ReleaseDeferredRetriesAfterDomainReload();
            EnsureReplayPump();
        }

        internal static bool TryStageAndSchedule(
            string sourcePsdAssetPath,
            string targetPrefabPath,
            GameObject generatedCandidate,
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
            string expectedTargetGuid = AssetDatabase.AssetPathToGUID(targetPath);
            if (string.IsNullOrEmpty(expectedTargetGuid))
            {
                error = "Target Prefab GUID could not be resolved for cleanup replay.";
                return false;
            }

            PsdHierarchyCleanupReplayProfile profile =
                PsdHierarchyCleanupReplayProfile.Load(targetPath, sourceGuid);
            if (profile == null) return false;
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
                if (!profile.TryBuildReplayPlans(
                        sourceGuid,
                        targetPath,
                        temporaryPath,
                        out IReadOnlyList<string> replayPlanJsonStages,
                        out string replayError))
                    throw new InvalidOperationException(replayError);

                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    throw new InvalidOperationException("Unity project root could not be resolved.");

                WritePendingReplayRecord(
                    projectRoot,
                    targetPath,
                    expectedTargetGuid,
                    temporaryPath,
                    replayPlanJsonStages);
                PendingTargets.Remove(targetPath);
                EnsureReplayPump();
                return true;
            }
            catch (Exception exception)
            {
                PendingTargets.Remove(targetPath);
                DeleteTemporaryAsset(temporaryPath);
                error = exception.Message;
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
            bool retainForRetry = false;
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
                    PsdHierarchyChatCleanupExecutionResult replay =
                        await PsdHierarchyChatCleanupExecution.ReapplyPersistedPlanAsync(
                            projectRoot,
                            record.replayPlanJsonStages[stageIndex]);
                    if (!replay.success)
                    {
                        record.retryAfterDomainReload = true;
                        WritePendingReplayRecord(pendingRecordPath, record);
                        retainForRetry = true;
                        Debug.LogError(
                            "PSD Prefab cleanup replay stage " + (stageIndex + 1) + "/" +
                            record.replayPlanJsonStages.Count +
                            " failed; the existing organized Prefab was kept unchanged. " +
                            "The staged candidate and checkpoint were retained for one retry after the next domain reload. " +
                            replay.message);
                        return;
                    }

                    string completedCheckpoint = record.checkpointPath;
                    record.nextStageIndex = stageIndex + 1;
                    record.inFlightStageIndex = -1;
                    record.checkpointPath = string.Empty;
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

                Debug.Log("PSD Prefab cleanup replay completed: " + targetPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (!retainForRetry)
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
                    if (ValidatePendingReplayRecord(projectRoot, record))
                        WritePendingReplayRecord(pendingRecordPath, record);
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

                if (record.retryAfterDomainReload) continue;
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
            string temporaryPath,
            IReadOnlyList<string> replayPlanJsonStages)
        {
            string pendingDirectory = Path.Combine(projectRoot, PendingReplayDirectory);
            Directory.CreateDirectory(pendingDirectory);
            string pendingRecordPath = Path.Combine(
                pendingDirectory,
                Guid.NewGuid().ToString("N") + ".json");
            var record = new PendingReplayRecord
            {
                schemaVersion = 2,
                projectRoot = projectRoot,
                targetPath = targetPath,
                expectedTargetGuid = expectedTargetGuid,
                temporaryPath = temporaryPath,
                replayPlanJsonStages = new List<string>(replayPlanJsonStages),
                nextStageIndex = 0,
                inFlightStageIndex = -1,
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

        private static bool ValidatePendingReplayRecord(
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
            bool migratedLegacyRecord = false;
            if (record.schemaVersion == 0)
            {
                if (string.IsNullOrWhiteSpace(record.replayPlanJson) ||
                    !PsdHierarchyCleanupReplayProfile.TryGetVerifiedTargetGuid(
                        record.targetPath,
                        out string verifiedTargetGuid))
                    throw new InvalidDataException(
                        "Legacy replay record cannot migrate without a GUID-bound cleanup replay Profile.");
                record.schemaVersion = 2;
                record.expectedTargetGuid = verifiedTargetGuid;
                record.replayPlanJsonStages = new List<string> { record.replayPlanJson };
                record.nextStageIndex = 0;
                record.inFlightStageIndex = -1;
                record.checkpointPath = string.Empty;
                record.retryAfterDomainReload = false;
                migratedLegacyRecord = true;
            }
            else if (record.schemaVersion != 2)
            {
                throw new InvalidDataException("Replay record schema is unsupported.");
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
            if ((record.replayPlanJsonStages == null || record.replayPlanJsonStages.Count == 0) &&
                !string.IsNullOrWhiteSpace(record.replayPlanJson))
                record.replayPlanJsonStages = new List<string> { record.replayPlanJson };
            if (record.replayPlanJsonStages == null || record.replayPlanJsonStages.Count == 0)
                throw new InvalidDataException("Replay stage list is empty.");
            ResolveRestartStage(
                record.nextStageIndex,
                record.inFlightStageIndex,
                record.replayPlanJsonStages.Count);
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
            foreach (string stage in record.replayPlanJsonStages)
                if (string.IsNullOrWhiteSpace(stage))
                    throw new InvalidDataException("Replay stage is empty.");
            return migratedLegacyRecord;
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
            WritePendingReplayRecord(pendingRecordPath, record);
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

        private static void ReleaseDeferredRetriesAfterDomainReload()
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
                    if (record == null || record.schemaVersion != 2 || !record.retryAfterDomainReload)
                        continue;
                    record.retryAfterDomainReload = false;
                    WritePendingReplayRecord(path, record);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "Could not release a deferred PSD Prefab cleanup replay: " + exception.Message);
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
                    if (record == null || !record.retryAfterDomainReload) return true;
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
