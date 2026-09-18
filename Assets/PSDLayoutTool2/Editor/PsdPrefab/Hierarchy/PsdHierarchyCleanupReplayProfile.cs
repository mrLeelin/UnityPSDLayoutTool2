namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdHierarchyCleanupReplayProfile : ScriptableObject
    {
        public const int CurrentSchemaVersion = 2;
        private const string ProfileFolder =
            "Assets/PSDLayoutTool2Settings/HierarchyCleanupReplayProfiles";

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string sourcePsdGuid = string.Empty;
        [SerializeField] private string targetPrefabGuid = string.Empty;
        [SerializeField] private string targetPrefabPath = string.Empty;
        [SerializeField] private bool requiresRebind;
        [SerializeField, TextArea(2, 4)] private string rebindReason = string.Empty;
        // Kept for schema-1 assets. It is migrated into runnerPlanStages on the
        // next successful append/save.
        [SerializeField, TextArea(4, 20)] private string runnerPlanJson = string.Empty;
        [SerializeField] private List<string> runnerPlanStages = new List<string>();
        // 与 runnerPlanStages 一一对应的节点绑定证据（v2 阶段必需；v1 阶段为空字符串）。
        [SerializeField] private List<string> runnerPlanBindings = new List<string>();

        public void Initialize(string sourceGuid, string prefabPath, string validatedRunnerPlanJson)
        {
            Initialize(sourceGuid, prefabPath, validatedRunnerPlanJson, string.Empty);
        }

        public void Initialize(
            string sourceGuid,
            string prefabPath,
            string validatedRunnerPlanJson,
            string validatedBindingJson)
        {
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            if (string.IsNullOrWhiteSpace(sourceGuid))
                throw new ArgumentException("Source PSD GUID is required.", nameof(sourceGuid));
            if (!IsAssetPath(normalizedTarget) || !normalizedTarget.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Target Prefab must be an Assets path.", nameof(prefabPath));

            JObject plan = ParseAndValidatePlan(validatedRunnerPlanJson, normalizedTarget);

            schemaVersion = CurrentSchemaVersion;
            sourcePsdGuid = sourceGuid.Trim();
            targetPrefabPath = normalizedTarget;
            targetPrefabGuid = AssetDatabase.AssetPathToGUID(normalizedTarget);
            if (string.IsNullOrEmpty(targetPrefabGuid))
                throw new InvalidOperationException("Target Prefab GUID could not be resolved.");
            runnerPlanJson = string.Empty;
            requiresRebind = false;
            rebindReason = string.Empty;
            runnerPlanStages = new List<string>
            {
                plan.ToString(Newtonsoft.Json.Formatting.None),
            };
            runnerPlanBindings = new List<string> { NormalizeBinding(validatedBindingJson) };
        }

        public void AppendStage(string sourceGuid, string prefabPath, string validatedRunnerPlanJson)
        {
            AppendStage(sourceGuid, prefabPath, validatedRunnerPlanJson, string.Empty);
        }

        public void AppendStage(
            string sourceGuid,
            string prefabPath,
            string validatedRunnerPlanJson,
            string validatedBindingJson)
        {
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            bool migratesSchemaOne = schemaVersion == 1;
            ValidateBinding(sourceGuid, normalizedTarget);
            if (requiresRebind)
                throw new InvalidDataException(BuildRebindRequiredMessage());
            JObject plan = ParseAndValidatePlan(validatedRunnerPlanJson, normalizedTarget);

            List<string> stages = ReadStoredStages(normalizedTarget);
            stages.Add(plan.ToString(Newtonsoft.Json.Formatting.None));
            List<string> bindings = ReadStoredBindings(normalizedTarget);
            bindings.Add(NormalizeBinding(validatedBindingJson));
            if (migratesSchemaOne && string.IsNullOrEmpty(targetPrefabGuid))
            {
                targetPrefabGuid = AssetDatabase.AssetPathToGUID(normalizedTarget);
                if (string.IsNullOrEmpty(targetPrefabGuid))
                    throw new InvalidDataException(
                        "Schema-1 cleanup replay Profile cannot migrate because the target Prefab GUID is missing.");
            }
            schemaVersion = CurrentSchemaVersion;
            runnerPlanJson = string.Empty;
            runnerPlanStages = stages;
            runnerPlanBindings = bindings;
        }

        public bool TryBuildReplayPlans(
            string sourceGuid,
            string prefabPath,
            string replayTargetPath,
            out IReadOnlyList<string> replayPlanJsonStages,
            out string error)
        {
            return TryBuildReplayPlans(
                sourceGuid,
                prefabPath,
                replayTargetPath,
                requireCurrentTargetGuid: true,
                out replayPlanJsonStages,
                out error);
        }

        internal bool TryBuildFreshGenerationReplayPlans(
            string sourceGuid,
            string prefabPath,
            string replayTargetPath,
            out IReadOnlyList<string> replayPlanJsonStages,
            out string error)
        {
            return TryBuildReplayPlans(
                sourceGuid,
                prefabPath,
                replayTargetPath,
                requireCurrentTargetGuid: false,
                out replayPlanJsonStages,
                out error);
        }

        private bool TryBuildReplayPlans(
            string sourceGuid,
            string prefabPath,
            string replayTargetPath,
            bool requireCurrentTargetGuid,
            out IReadOnlyList<string> replayPlanJsonStages,
            out string error)
        {
            replayPlanJsonStages = Array.Empty<string>();
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            string normalizedReplayTarget = NormalizeAssetPath(replayTargetPath);
            if (schemaVersion != 1 && schemaVersion != CurrentSchemaVersion)
            {
                error = "Cleanup replay Profile schema is unsupported.";
                return false;
            }
            if (schemaVersion == CurrentSchemaVersion && string.IsNullOrEmpty(targetPrefabGuid))
            {
                error = "Cleanup replay Profile target Prefab GUID is missing.";
                return false;
            }
            if (requiresRebind)
            {
                error = BuildRebindRequiredMessage();
                return false;
            }
            if (!string.Equals(sourcePsdGuid, (sourceGuid ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                error = "Cleanup replay Profile belongs to a different source PSD.";
                return false;
            }
            if (!string.Equals(targetPrefabPath, normalizedTarget, StringComparison.Ordinal))
            {
                error = "Cleanup replay Profile belongs to a different target Prefab.";
                return false;
            }
            if (!IsAssetPath(normalizedReplayTarget) ||
                !normalizedReplayTarget.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                error = "Cleanup replay target must be a Prefab path under Assets.";
                return false;
            }

            string currentTargetGuid = AssetDatabase.AssetPathToGUID(normalizedTarget);
            if (requireCurrentTargetGuid &&
                !string.IsNullOrEmpty(targetPrefabGuid) &&
                !string.IsNullOrEmpty(currentTargetGuid) &&
                !string.Equals(targetPrefabGuid, currentTargetGuid, StringComparison.Ordinal))
            {
                error = "Cleanup replay Profile target Prefab GUID no longer matches.";
                return false;
            }

            try
            {
                var replayStages = new List<string>();
                List<string> storedStages = ReadStoredStages(normalizedTarget);
                List<string> storedBindings = ReadStoredBindings(normalizedTarget);
                if (storedBindings.Count != storedStages.Count)
                    throw new InvalidDataException(
                        "Cleanup replay Profile stage and binding counts do not match. Re-analyze the current Prefab.");

                // v2 阶段引用旧快照的位置型节点 ID，必须先在重新生成的结果上证明唯一对应关系。
                // 缺少绑定证据是永久失败：绝不猜对应关系，也不覆盖指纹后强行执行。
                for (int index = 0; index < storedStages.Count; index++)
                {
                    if (ParseAndValidatePlan(storedStages[index], normalizedTarget).Value<int?>("version") == 2 &&
                        string.IsNullOrWhiteSpace(storedBindings[index]))
                    {
                        throw new InvalidDataException(
                            PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                            " 该 v2 阶段没有保存节点绑定证据。");
                    }
                }

                bool hasVersionTwoStage = storedStages.Any(stage =>
                    ParseAndValidatePlan(stage, normalizedTarget).Value<int?>("version") == 2);
                if (hasVersionTwoStage && storedStages.Count > 1)
                {
                    throw new InvalidDataException(
                        "Multi-stage v2 cleanup replay must build and execute one stage at a time so each stage uses the Prefab saved by the previous stage.");
                }
                string freshSnapshotJson = string.Empty;
                string freshFingerprint = string.Empty;
                if (hasVersionTwoStage &&
                    !PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                        normalizedReplayTarget,
                        out freshSnapshotJson,
                        out freshFingerprint,
                        out string snapshotError))
                {
                    throw new InvalidDataException(
                        "Cleanup replay could not snapshot the regenerated Prefab: " + snapshotError);
                }

                for (int index = 0; index < storedStages.Count; index++)
                {
                    JObject plan = ParseAndValidatePlan(storedStages[index], normalizedTarget);
                    if (plan.Value<int?>("version") != 2)
                    {
                        plan["replaySourcePrefabAssetPath"] = normalizedTarget;
                        plan["prefabAssetPath"] = normalizedReplayTarget;
                        ((JObject)plan["output"])["assetPath"] = normalizedReplayTarget;
                        plan["verify"] = new JObject();
                        replayStages.Add(plan.ToString(Newtonsoft.Json.Formatting.None));
                        continue;
                    }

                    if (!PsdHierarchyReplayBinding.TryRebind(
                            storedBindings[index],
                            freshSnapshotJson,
                            out IReadOnlyDictionary<string, string> nodeMap,
                            out string rebindError))
                    {
                        throw new InvalidDataException(
                            PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                            " " + rebindError);
                    }

                    PsdHierarchyReplayBinding.RewritePlan(
                        plan,
                        nodeMap,
                        freshFingerprint,
                        normalizedReplayTarget);
                    replayStages.Add(plan.ToString(Newtonsoft.Json.Formatting.None));
                }

                if (replayStages.Count == 0)
                    throw new InvalidDataException("Cleanup replay Profile contains no stages.");

                replayPlanJsonStages = replayStages;
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException ||
                exception is InvalidDataException)
            {
                error = exception.Message;
                return false;
            }
        }

        internal bool TryGetReplayStageSources(
            string sourceGuid,
            string prefabPath,
            bool requireCurrentTargetGuid,
            out IReadOnlyList<string> storedStageJson,
            out IReadOnlyList<string> storedBindingJson,
            out string error)
        {
            storedStageJson = Array.Empty<string>();
            storedBindingJson = Array.Empty<string>();
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            if (schemaVersion != CurrentSchemaVersion)
            {
                error = PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                        " 旧 v1 Replay Profile 不会自动迁移或进入执行队列。";
                return false;
            }
            if (string.IsNullOrEmpty(targetPrefabGuid))
            {
                error = "Cleanup replay Profile target Prefab GUID is missing.";
                return false;
            }
            if (requiresRebind)
            {
                error = BuildRebindRequiredMessage();
                return false;
            }
            if (!string.Equals(sourcePsdGuid, (sourceGuid ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                error = "Cleanup replay Profile belongs to a different source PSD.";
                return false;
            }
            if (!string.Equals(targetPrefabPath, normalizedTarget, StringComparison.Ordinal))
            {
                error = "Cleanup replay Profile belongs to a different target Prefab.";
                return false;
            }

            string currentTargetGuid = AssetDatabase.AssetPathToGUID(normalizedTarget);
            if (requireCurrentTargetGuid &&
                !string.IsNullOrEmpty(currentTargetGuid) &&
                !string.Equals(targetPrefabGuid, currentTargetGuid, StringComparison.Ordinal))
            {
                error = "Cleanup replay Profile target Prefab GUID no longer matches.";
                return false;
            }

            try
            {
                List<string> stages = ReadStoredStages(normalizedTarget);
                List<string> bindings = ReadStoredBindings(normalizedTarget);
                if (stages.Count == 0)
                    throw new InvalidDataException("Cleanup replay Profile contains no stages.");
                if (bindings.Count != stages.Count)
                    throw new InvalidDataException(
                        "Cleanup replay Profile stage and binding counts do not match. Re-analyze the current Prefab.");

                for (int index = 0; index < stages.Count; index++)
                {
                    JObject plan = ParseAndValidatePlan(stages[index], normalizedTarget);
                    if (plan.Value<int?>("version") != 2)
                    {
                        throw new InvalidDataException(
                            PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                            " 旧 v1 清理阶段不会自动迁移或进入执行队列。请重新分析并审核 v2 计划。");
                    }
                    if (string.IsNullOrWhiteSpace(bindings[index]))
                    {
                        throw new InvalidDataException(
                            PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                            " 该 v2 阶段没有保存节点绑定证据。");
                    }
                }

                storedStageJson = stages;
                storedBindingJson = bindings;
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException ||
                exception is InvalidDataException)
            {
                error = exception.Message;
                return false;
            }
        }

        internal bool TryBuildReplayStage(
            string sourceGuid,
            string prefabPath,
            string replayTargetPath,
            bool requireCurrentTargetGuid,
            int stageIndex,
            out string replayPlanJson,
            out string error)
        {
            replayPlanJson = string.Empty;
            if (!TryGetReplayStageSources(
                    sourceGuid,
                    prefabPath,
                    requireCurrentTargetGuid,
                    out IReadOnlyList<string> stages,
                    out IReadOnlyList<string> bindings,
                    out error))
                return false;
            if (stageIndex < 0 || stageIndex >= stages.Count)
            {
                error = "Cleanup replay stage index is invalid.";
                return false;
            }

            return TryBuildReplayStage(
                stages[stageIndex],
                bindings[stageIndex],
                prefabPath,
                replayTargetPath,
                out replayPlanJson,
                out error);
        }

        internal static bool TryBuildReplayStage(
            string storedStageJson,
            string storedBindingJson,
            string sourcePrefabPath,
            string replayTargetPath,
            out string replayPlanJson,
            out string error)
        {
            replayPlanJson = string.Empty;
            string normalizedSource = NormalizeAssetPath(sourcePrefabPath);
            string normalizedReplayTarget = NormalizeAssetPath(replayTargetPath);
            try
            {
                JObject plan = ParseAndValidatePlan(storedStageJson, normalizedSource);
                if (plan.Value<int?>("version") != 2)
                {
                    throw new InvalidDataException(
                        PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage +
                        " 旧 v1 清理阶段不会自动迁移或执行。请重新分析并审核 v2 计划。");
                }
                if (!PsdHierarchyChatContextBuilder.TryBuildSnapshotForPrefab(
                        normalizedReplayTarget,
                        out string freshSnapshotJson,
                        out string freshFingerprint,
                        out string snapshotError))
                {
                    throw new InvalidDataException(
                        "Cleanup replay could not snapshot the regenerated Prefab: " + snapshotError);
                }
                if (!PsdHierarchyReplayBinding.TryRebind(
                        storedBindingJson,
                        freshSnapshotJson,
                        out IReadOnlyDictionary<string, string> nodeMap,
                        out string rebindError))
                {
                    throw new InvalidDataException(
                        PsdHierarchyChatCleanupExecution.ReplayRequiresFreshAnalysisMessage + " " + rebindError);
                }

                PsdHierarchyReplayBinding.RewritePlan(
                    plan,
                    nodeMap,
                    freshFingerprint,
                    normalizedReplayTarget);
                replayPlanJson = plan.ToString(Newtonsoft.Json.Formatting.None);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException ||
                exception is InvalidDataException)
            {
                error = exception.Message;
                return false;
            }
        }

        private void ValidateBinding(string sourceGuid, string normalizedTarget)
        {
            if (schemaVersion != 1 && schemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException("Cleanup replay Profile schema is unsupported.");
            if (!string.Equals(sourcePsdGuid, (sourceGuid ?? string.Empty).Trim(), StringComparison.Ordinal))
                throw new InvalidDataException("Cleanup replay Profile belongs to a different source PSD.");
            if (!string.Equals(targetPrefabPath, normalizedTarget, StringComparison.Ordinal))
                throw new InvalidDataException("Cleanup replay Profile belongs to a different target Prefab.");
            if (schemaVersion == CurrentSchemaVersion && string.IsNullOrEmpty(targetPrefabGuid))
                throw new InvalidDataException("Cleanup replay Profile target Prefab GUID is missing.");

            string currentTargetGuid = AssetDatabase.AssetPathToGUID(normalizedTarget);
            if (!string.IsNullOrEmpty(targetPrefabGuid) &&
                !string.IsNullOrEmpty(currentTargetGuid) &&
                !string.Equals(targetPrefabGuid, currentTargetGuid, StringComparison.Ordinal))
                throw new InvalidDataException("Cleanup replay Profile target Prefab GUID no longer matches.");
        }

        internal bool TryGetProtectedRenameTargets(
            string sourceGuid,
            string prefabPath,
            out IReadOnlyList<string> assetPaths,
            out string error)
        {
            assetPaths = Array.Empty<string>();
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            try
            {
                ValidateBinding(sourceGuid, normalizedTarget);
                var protectedPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (string storedStage in ReadStoredStages(normalizedTarget))
                {
                    JObject plan = ParseAndValidatePlan(storedStage, normalizedTarget);
                    foreach (string propertyName in new[] { "textureRenames", "spriteAtlasRenames" })
                    {
                        if (!(plan[propertyName] is JArray renames)) continue;
                        foreach (JToken token in renames)
                        {
                            if (!(token is JObject rename))
                                throw new InvalidDataException(propertyName + " contains a non-object entry.");
                            string sourcePath = NormalizeAssetPath(rename.Value<string>("from"));
                            string targetName = (rename.Value<string>("toName") ?? string.Empty).Trim();
                            string expectedGuid = (rename.Value<string>("expectedGuid") ?? string.Empty).Trim();
                            string targetPath = GetRenamedAssetTarget(sourcePath, targetName);
                            string currentGuid = AssetDatabase.AssetPathToGUID(targetPath);
                            if (!string.IsNullOrEmpty(expectedGuid) &&
                                string.Equals(expectedGuid, currentGuid, StringComparison.Ordinal))
                            {
                                protectedPaths.Add(targetPath);
                                continue;
                            }
                            string sourceAssetGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                            if (string.IsNullOrEmpty(expectedGuid) ||
                                !string.Equals(expectedGuid, sourceAssetGuid, StringComparison.Ordinal))
                                throw new InvalidDataException(
                                    "Cleanup replay renamed asset GUID no longer matches: " + targetPath);
                        }
                    }
                }

                assetPaths = new List<string>(protectedPaths);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException ||
                exception is InvalidDataException)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool TryGetVerifiedTargetGuid(string prefabPath, out string targetGuid)
        {
            targetGuid = string.Empty;
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            string currentGuid = AssetDatabase.AssetPathToGUID(normalizedTarget);
            if (string.IsNullOrEmpty(currentGuid) || !AssetDatabase.IsValidFolder(ProfileFolder))
                return false;

            foreach (string profileGuid in AssetDatabase.FindAssets(
                         "t:PsdHierarchyCleanupReplayProfile",
                         new[] { ProfileFolder }))
            {
                PsdHierarchyCleanupReplayProfile profile =
                    AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(
                        AssetDatabase.GUIDToAssetPath(profileGuid));
                if (profile == null ||
                    !string.Equals(profile.targetPrefabPath, normalizedTarget, StringComparison.Ordinal) ||
                    !string.Equals(profile.targetPrefabGuid, currentGuid, StringComparison.Ordinal))
                    continue;
                targetGuid = currentGuid;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 与已存阶段一一对应的节点绑定证据；旧 schema-1 记录没有绑定，返回空字符串占位。
        /// </summary>
        private List<string> ReadStoredBindings(string normalizedTarget)
        {
            if (schemaVersion == 1 && !string.IsNullOrWhiteSpace(runnerPlanJson))
                return new List<string> { string.Empty };

            var bindings = new List<string>();
            foreach (string stage in runnerPlanStages ?? new List<string>())
                bindings.Add(string.Empty);
            List<string> stored = runnerPlanBindings ?? new List<string>();
            for (int index = 0; index < bindings.Count && index < stored.Count; index++)
                bindings[index] = NormalizeBinding(stored[index]);
            return bindings;
        }

        private static string NormalizeBinding(string bindingJson)
        {
            return string.IsNullOrWhiteSpace(bindingJson)
                ? string.Empty
                : JObject.Parse(bindingJson).ToString(Newtonsoft.Json.Formatting.None);
        }

        private List<string> ReadStoredStages(string normalizedTarget)
        {
            var stages = new List<string>();
            if (schemaVersion == 1 && !string.IsNullOrWhiteSpace(runnerPlanJson))
            {
                stages.Add(ParseAndValidatePlan(runnerPlanJson, normalizedTarget)
                    .ToString(Newtonsoft.Json.Formatting.None));
                return stages;
            }

            foreach (string stage in runnerPlanStages ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(stage))
                    throw new InvalidDataException("Cleanup replay Profile contains an empty stage.");
                stages.Add(ParseAndValidatePlan(stage, normalizedTarget)
                    .ToString(Newtonsoft.Json.Formatting.None));
            }
            return stages;
        }

        public static string GetProfilePath(string prefabPath, string sourceGuid)
        {
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            string key = (sourceGuid ?? string.Empty).Trim() + "\n" + normalizedTarget;
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(key));
                var builder = new StringBuilder(24);
                for (int index = 0; index < 12; index++) builder.Append(bytes[index].ToString("x2"));
                return ProfileFolder + "/" + builder + ".asset";
            }
        }

        public static PsdHierarchyCleanupReplayProfile Load(string prefabPath, string sourceGuid)
        {
            return AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(
                GetProfilePath(prefabPath, sourceGuid));
        }

        internal static bool HasConfirmedStages(string sourcePsdAssetPath, string prefabPath)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(NormalizeAssetPath(sourcePsdAssetPath));
            if (string.IsNullOrEmpty(sourceGuid)) return false;

            PsdHierarchyCleanupReplayProfile profile = Load(prefabPath, sourceGuid);
            if (profile == null) return false;

            try
            {
                return profile.ReadStoredStages(NormalizeAssetPath(prefabPath)).Count > 0;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        internal static bool RequiresRebind(
            string sourcePsdAssetPath,
            string prefabPath,
            out string reason)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(NormalizeAssetPath(sourcePsdAssetPath));
            return RequiresRebindByGuid(sourceGuid, prefabPath, out reason);
        }

        internal static bool RequiresRebindByGuid(
            string sourceGuid,
            string prefabPath,
            out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceGuid)) return false;

            PsdHierarchyCleanupReplayProfile profile = Load(prefabPath, sourceGuid.Trim());
            if (profile == null || !profile.requiresRebind) return false;

            reason = profile.rebindReason;
            return true;
        }

        internal static bool TryMarkRequiresRebindByGuid(
            string sourceGuid,
            string prefabPath,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(sourceGuid)) return false;

            PsdHierarchyCleanupReplayProfile profile = Load(prefabPath, sourceGuid.Trim());
            if (profile == null) return false;

            profile.requiresRebind = true;
            profile.rebindReason = (reason ?? string.Empty).Trim();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return true;
        }



        /// <summary>
        /// Finds the active replay Profile for a moved Prefab by its persistent
        /// GUID, then migrates both the Profile asset location and stored plan
        /// target paths to the Prefab's current AssetDatabase path.
        /// </summary>
        internal static bool TryResolveMovedTargetPrefabPath(
            string sourceGuid,
            string configuredPrefabPath,
            out string prefabPath)
        {
            prefabPath = string.Empty;
            string normalizedSourceGuid = (sourceGuid ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalizedSourceGuid) ||
                !AssetDatabase.IsValidFolder(ProfileFolder))
                return false;

            var matches = new List<KeyValuePair<string, PsdHierarchyCleanupReplayProfile>>();
            foreach (string profileGuid in AssetDatabase.FindAssets(
                         "t:PsdHierarchyCleanupReplayProfile", new[] { ProfileFolder }))
            {
                string profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                PsdHierarchyCleanupReplayProfile profile =
                    AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(profilePath);
                if (profile == null ||
                    !string.Equals(profile.sourcePsdGuid, normalizedSourceGuid, StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(profile.targetPrefabGuid))
                    continue;

                string resolvedPath = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(profile.targetPrefabGuid));
                if (string.IsNullOrEmpty(resolvedPath) ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(resolvedPath) == null ||
                    string.Equals(resolvedPath, NormalizeAssetPath(profile.targetPrefabPath), StringComparison.Ordinal))
                    continue;

                matches.Add(new KeyValuePair<string, PsdHierarchyCleanupReplayProfile>(profilePath, profile));
            }

            if (matches.Count != 1) return false;

            string oldProfilePath = matches[0].Key;
            PsdHierarchyCleanupReplayProfile movedProfile = matches[0].Value;
            string resolvedPrefabPath = NormalizeAssetPath(
                AssetDatabase.GUIDToAssetPath(movedProfile.targetPrefabGuid));
            string newProfilePath = GetProfilePath(resolvedPrefabPath, normalizedSourceGuid);
            if (!string.Equals(oldProfilePath, newProfilePath, StringComparison.Ordinal))
            {
                if (AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(newProfilePath) != null)
                    return false;
                string moveError = AssetDatabase.MoveAsset(oldProfilePath, newProfilePath);
                if (!string.IsNullOrEmpty(moveError)) return false;
                movedProfile = AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(newProfilePath);
                if (movedProfile == null) return false;
            }

            movedProfile.MigrateTargetPath(resolvedPrefabPath);
            EditorUtility.SetDirty(movedProfile);
            AssetDatabase.SaveAssetIfDirty(movedProfile);
            prefabPath = resolvedPrefabPath;
            return true;
        }

        /// <summary>
        /// Reports whether this exact PSD and Prefab pair has a replay Profile
        /// that can preserve the organized Prefab hierarchy during an update.
        /// </summary>
        internal static bool CanReplayIncrementalUpdate(
            string sourceGuid,
            string prefabPath,
            out string reason)
        {
            reason = string.Empty;
            string normalizedSourceGuid = (sourceGuid ?? string.Empty).Trim();
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            if (string.IsNullOrEmpty(normalizedSourceGuid))
            {
                reason = "Source PSD GUID is required.";
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(normalizedTarget) == null)
            {
                reason = "The cleanup replay Profile target Prefab is missing or cannot be loaded.";
                return false;
            }

            PsdHierarchyCleanupReplayProfile profile = Load(normalizedTarget, normalizedSourceGuid);
            if (profile == null)
            {
                reason = "No cleanup replay Profile exists for this PSD and Prefab.";
                return false;
            }

            return profile.TryGetReplayStageSources(
                normalizedSourceGuid,
                normalizedTarget,
                requireCurrentTargetGuid: true,
                out _,
                out _,
                out reason);
        }

        internal static bool CanReplayFreshGeneration(
            string sourceGuid,
            string prefabPath,
            out string reason)
        {
            reason = string.Empty;
            string normalizedSourceGuid = (sourceGuid ?? string.Empty).Trim();
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            if (string.IsNullOrEmpty(normalizedSourceGuid))
            {
                reason = "Source PSD GUID is required.";
                return false;
            }

            PsdHierarchyCleanupReplayProfile profile = Load(normalizedTarget, normalizedSourceGuid);
            if (profile == null)
            {
                reason = "No cleanup replay Profile exists for this PSD and Prefab.";
                return false;
            }

            return profile.TryGetReplayStageSources(
                normalizedSourceGuid,
                normalizedTarget,
                requireCurrentTargetGuid: false,
                out _,
                out _,
                out reason);
        }

        internal void RebindToFreshTarget(string sourceGuid, string prefabPath)
        {
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            if (schemaVersion != 1 && schemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException("Cleanup replay Profile schema is unsupported.");
            if (!string.Equals(sourcePsdGuid, (sourceGuid ?? string.Empty).Trim(), StringComparison.Ordinal))
                throw new InvalidDataException("Cleanup replay Profile belongs to a different source PSD.");
            if (!string.Equals(targetPrefabPath, normalizedTarget, StringComparison.Ordinal))
                throw new InvalidDataException("Cleanup replay Profile belongs to a different target Prefab.");

            string currentTargetGuid = AssetDatabase.AssetPathToGUID(normalizedTarget);
            if (string.IsNullOrEmpty(currentTargetGuid))
                throw new InvalidDataException("Freshly replayed target Prefab GUID could not be resolved.");

            schemaVersion = CurrentSchemaVersion;
            targetPrefabGuid = currentTargetGuid;
            targetPrefabPath = normalizedTarget;
        }

        private string BuildRebindRequiredMessage()
        {
            return string.IsNullOrWhiteSpace(rebindReason)
                ? "Cleanup replay Profile requires a fresh confirmed plan before it can replay again."
                : "Cleanup replay Profile requires a fresh confirmed plan before it can replay again: " +
                  rebindReason;
        }

        private void MigrateTargetPath(string newTargetPath)
        {
            string normalizedTarget = NormalizeAssetPath(newTargetPath);
            targetPrefabPath = normalizedTarget;
            runnerPlanJson = RewritePlanTarget(runnerPlanJson, normalizedTarget);
            var migratedStages = new List<string>();
            foreach (string stage in runnerPlanStages ?? new List<string>())
                migratedStages.Add(RewritePlanTarget(stage, normalizedTarget));
            runnerPlanStages = migratedStages;
        }

        private static string RewritePlanTarget(string planJson, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(planJson)) return string.Empty;
            JObject plan = JObject.Parse(planJson);
            plan["prefabAssetPath"] = targetPath;
            if (!(plan["output"] is JObject output))
                throw new InvalidDataException("Cleanup replay plan output is missing.");
            output["assetPath"] = targetPath;
            return ParseAndValidatePlan(plan.ToString(Newtonsoft.Json.Formatting.None), targetPath)
                .ToString(Newtonsoft.Json.Formatting.None);
        }

        internal static bool IsMissingTargetRecoveryEligible(string prefabPath, string sourceGuid)
        {
            PsdHierarchyCleanupReplayProfile profile = Load(prefabPath, sourceGuid);
            if (profile == null || string.IsNullOrEmpty(profile.targetPrefabGuid)) return false;

            try
            {
                string normalizedTarget = NormalizeAssetPath(prefabPath);
                profile.ValidateBinding(sourceGuid, normalizedTarget);
                return AssetDatabase.LoadAssetAtPath<GameObject>(normalizedTarget) == null;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        internal static bool TryArchiveForMissingTargetRecovery(
            string prefabPath,
            string sourceGuid,
            out string archivedProfilePath,
            out string failureReason)
        {
            archivedProfilePath = string.Empty;
            failureReason = string.Empty;
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            string profilePath = GetProfilePath(normalizedTarget, sourceGuid);
            PsdHierarchyCleanupReplayProfile profile =
                AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(profilePath);
            if (profile == null)
            {
                failureReason = "No cleanup replay Profile exists for this PSD.";
                return false;
            }
            if (!IsMissingTargetRecoveryEligible(normalizedTarget, sourceGuid))
            {
                failureReason =
                    "The cleanup replay Profile still has a loadable Prefab target or an invalid binding and cannot be reset.";
                return false;
            }

            const string archiveFolder =
                "Assets/PSDLayoutTool2Settings/OrphanedHierarchyCleanupReplayProfiles";
            EnsureAssetFolder(archiveFolder);
            string archiveFileName = Path.GetFileNameWithoutExtension(profilePath) +
                ".orphaned-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".asset";
            archivedProfilePath = AssetDatabase.GenerateUniqueAssetPath(
                archiveFolder + "/" + archiveFileName);
            string moveError = AssetDatabase.MoveAsset(profilePath, archivedProfilePath);
            if (!string.IsNullOrEmpty(moveError))
            {
                failureReason = "Could not archive the orphaned cleanup replay Profile: " + moveError;
                archivedProfilePath = string.Empty;
                return false;
            }

            AssetDatabase.ImportAsset(archivedProfilePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return true;
        }

        public static PsdHierarchyCleanupReplayProfile Persist(
            string sourcePsdAssetPath,
            string prefabPath,
            string validatedRunnerPlanJson)
        {
            return Persist(sourcePsdAssetPath, prefabPath, validatedRunnerPlanJson, string.Empty);
        }

        public static PsdHierarchyCleanupReplayProfile Persist(
            string sourcePsdAssetPath,
            string prefabPath,
            string validatedRunnerPlanJson,
            string validatedBindingJson)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(NormalizeAssetPath(sourcePsdAssetPath));
            if (string.IsNullOrEmpty(sourceGuid))
                throw new InvalidOperationException("Source PSD asset GUID could not be resolved.");

            string profilePath = GetProfilePath(prefabPath, sourceGuid);
            EnsureAssetFolder(ProfileFolder);
            PsdHierarchyCleanupReplayProfile profile =
                AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(profilePath);
            if (profile == null)
            {
                profile = CreateInstance<PsdHierarchyCleanupReplayProfile>();
                profile.Initialize(sourceGuid, prefabPath, validatedRunnerPlanJson, validatedBindingJson);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                profile.AppendStage(sourceGuid, prefabPath, validatedRunnerPlanJson, validatedBindingJson);
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        public static PsdHierarchyCleanupReplayProfile ReplaceWithFirstStage(
            string sourcePsdAssetPath,
            string prefabPath,
            string validatedRunnerPlanJson)
        {
            return ReplaceWithFirstStage(sourcePsdAssetPath, prefabPath, validatedRunnerPlanJson, string.Empty);
        }

        public static PsdHierarchyCleanupReplayProfile ReplaceWithFirstStage(
            string sourcePsdAssetPath,
            string prefabPath,
            string validatedRunnerPlanJson,
            string validatedBindingJson)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(NormalizeAssetPath(sourcePsdAssetPath));
            if (string.IsNullOrEmpty(sourceGuid))
                throw new InvalidOperationException("Source PSD asset GUID could not be resolved.");

            string profilePath = GetProfilePath(prefabPath, sourceGuid);
            EnsureAssetFolder(ProfileFolder);
            PsdHierarchyCleanupReplayProfile profile =
                AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(profilePath);
            if (profile == null)
            {
                profile = CreateInstance<PsdHierarchyCleanupReplayProfile>();
                profile.Initialize(sourceGuid, prefabPath, validatedRunnerPlanJson, validatedBindingJson);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                profile.Initialize(sourceGuid, prefabPath, validatedRunnerPlanJson, validatedBindingJson);
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        internal static bool TryDiscardMatchingLastStage(
            string sourcePsdAssetPath,
            string prefabPath,
            string validatedRunnerPlanJson,
            out string error)
        {
            error = string.Empty;
            string sourceGuid = AssetDatabase.AssetPathToGUID(NormalizeAssetPath(sourcePsdAssetPath));
            if (string.IsNullOrEmpty(sourceGuid))
            {
                error = "Source PSD GUID could not be resolved.";
                return false;
            }

            string normalizedTarget = NormalizeAssetPath(prefabPath);
            string profilePath = GetProfilePath(normalizedTarget, sourceGuid);
            PsdHierarchyCleanupReplayProfile profile =
                AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(profilePath);
            if (profile == null) return false;

            try
            {
                string expectedStage = ParseAndValidatePlan(validatedRunnerPlanJson, normalizedTarget)
                    .ToString(Newtonsoft.Json.Formatting.None);
                List<string> stages = profile.ReadStoredStages(normalizedTarget);
                if (stages.Count == 0 || !string.Equals(
                        stages[stages.Count - 1], expectedStage, StringComparison.Ordinal))
                    return false;

                List<string> bindings = profile.ReadStoredBindings(normalizedTarget);
                stages.RemoveAt(stages.Count - 1);
                if (bindings.Count == stages.Count + 1)
                    bindings.RemoveAt(bindings.Count - 1);
                if (stages.Count == 0)
                {
                    AssetDatabase.DeleteAsset(profilePath);
                    return true;
                }

                profile.schemaVersion = CurrentSchemaVersion;
                profile.runnerPlanJson = string.Empty;
                profile.runnerPlanStages = stages;
                profile.runnerPlanBindings = bindings;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                return true;
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException ||
                exception is InvalidDataException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static void Remove(string sourcePsdAssetPath, string prefabPath)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(NormalizeAssetPath(sourcePsdAssetPath));
            if (string.IsNullOrEmpty(sourceGuid)) return;
            string profilePath = GetProfilePath(prefabPath, sourceGuid);
            if (AssetDatabase.LoadAssetAtPath<PsdHierarchyCleanupReplayProfile>(profilePath) != null)
                AssetDatabase.DeleteAsset(profilePath);
        }



        private static JObject ParseAndValidatePlan(string json, string expectedTarget)
        {
            JObject plan = JObject.Parse(json ?? string.Empty);
            int? version = plan.Value<int?>("version");
            // 容器版本与内嵌清理计划版本分别检查：v2 阶段计划连同节点绑定证据一起保存，
            // 重放读取端（TryBuildReplayPlans）用绑定证据证明对应关系；v1 路径计划没有证据。
            if (version != 1 && version != 2)
                throw new InvalidDataException("Cleanup replay plan version must be 1 or 2.");
            if (!string.Equals(
                    NormalizeAssetPath(plan.Value<string>("prefabAssetPath")),
                    expectedTarget,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Cleanup replay plan target Prefab does not match.");
            if (!(plan["output"] is JObject output) ||
                !string.Equals(output.Value<string>("mode"), "in_place", StringComparison.Ordinal) ||
                !string.Equals(
                    NormalizeAssetPath(output.Value<string>("assetPath")),
                    expectedTarget,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Cleanup replay plan output must update the target Prefab in place.");
            return plan;
        }

        private static string GetRenamedAssetTarget(string sourcePath, string targetName)
        {
            if (!IsAssetPath(sourcePath) || string.IsNullOrWhiteSpace(targetName))
                throw new InvalidDataException("Cleanup replay asset rename is incomplete.");
            string directory = NormalizeAssetPath(Path.GetDirectoryName(sourcePath));
            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(extension))
                throw new InvalidDataException("Cleanup replay asset rename source path is invalid.");
            return directory + "/" + targetName + extension;
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
                        throw new InvalidOperationException("Could not create cleanup replay Profile folder: " + next);
                }
                current = next;
            }
        }

        private static bool IsAssetPath(string path)
        {
            return string.Equals(path, "Assets", StringComparison.Ordinal) ||
                   path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }
    }
}
