namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
        // Kept for schema-1 assets. It is migrated into runnerPlanStages on the
        // next successful append/save.
        [SerializeField, TextArea(4, 20)] private string runnerPlanJson = string.Empty;
        [SerializeField] private List<string> runnerPlanStages = new List<string>();

        public void Initialize(string sourceGuid, string prefabPath, string validatedRunnerPlanJson)
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
            runnerPlanStages = new List<string>
            {
                plan.ToString(Newtonsoft.Json.Formatting.None),
            };
        }

        public void AppendStage(string sourceGuid, string prefabPath, string validatedRunnerPlanJson)
        {
            string normalizedTarget = NormalizeAssetPath(prefabPath);
            bool migratesSchemaOne = schemaVersion == 1;
            ValidateBinding(sourceGuid, normalizedTarget);
            JObject plan = ParseAndValidatePlan(validatedRunnerPlanJson, normalizedTarget);

            List<string> stages = ReadStoredStages(normalizedTarget);
            stages.Add(plan.ToString(Newtonsoft.Json.Formatting.None));
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
        }

        public bool TryBuildReplayPlans(
            string sourceGuid,
            string prefabPath,
            string replayTargetPath,
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
            if (!string.IsNullOrEmpty(targetPrefabGuid) &&
                !string.IsNullOrEmpty(currentTargetGuid) &&
                !string.Equals(targetPrefabGuid, currentTargetGuid, StringComparison.Ordinal))
            {
                error = "Cleanup replay Profile target Prefab GUID no longer matches.";
                return false;
            }

            try
            {
                var replayStages = new List<string>();
                foreach (string storedStage in ReadStoredStages(normalizedTarget))
                {
                    JObject plan = ParseAndValidatePlan(storedStage, normalizedTarget);
                    plan["replaySourcePrefabAssetPath"] = normalizedTarget;
                    plan["prefabAssetPath"] = normalizedReplayTarget;
                    ((JObject)plan["output"])["assetPath"] = normalizedReplayTarget;
                    plan["verify"] = new JObject();
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

            return profile.TryBuildReplayPlans(
                normalizedSourceGuid,
                normalizedTarget,
                normalizedTarget,
                out _,
                out reason);
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
                profile.Initialize(sourceGuid, prefabPath, validatedRunnerPlanJson);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                profile.AppendStage(sourceGuid, prefabPath, validatedRunnerPlanJson);
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

                stages.RemoveAt(stages.Count - 1);
                if (stages.Count == 0)
                {
                    AssetDatabase.DeleteAsset(profilePath);
                    return true;
                }

                profile.schemaVersion = CurrentSchemaVersion;
                profile.runnerPlanJson = string.Empty;
                profile.runnerPlanStages = stages;
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

        internal static bool HasReusableExtractions(string planJson)
        {
            try
            {
                return HasReusableExtractions(JObject.Parse(planJson ?? string.Empty));
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        private static bool HasReusableExtractions(JObject plan)
        {
            foreach (string property in new[]
                     {
                         "componentExtractions",
                         "stateComponentExtractions",
                         "variantComponentExtractions",
                         "statefulComponentExtractions",
                     })
            {
                if (plan[property] is JArray values && values.Count > 0) return true;
            }
            return false;
        }

        private static JObject ParseAndValidatePlan(string json, string expectedTarget)
        {
            JObject plan = JObject.Parse(json ?? string.Empty);
            if (plan.Value<int?>("version") != 1)
                throw new InvalidDataException("Cleanup replay plan version must be 1.");
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
