namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public enum PsdPrefabTransactionStage
    {
        AfterPrefabSave,
        AfterReimportVerification,
        BeforeProfileCopy,
        DuringProfileCopy,
        AfterProfileCopy,
        AfterProfileSave,
        AfterFinalVerification
    }

    /// <summary>
    /// Commits the exact configured Prefab and its hierarchy Profile as one
    /// recoverable operation. Backups include both asset and meta bytes, so a
    /// failure cannot change the Prefab GUID or leave half-written identity.
    /// </summary>
    public static class PsdPrefabTransactionalSave
    {
        /// <summary>
        /// Produces a collision-resistant sidecar path from the exact target
        /// and source PSD GUID. No directory scan or same-name heuristic is
        /// involved, so a sibling Prefab can never be selected accidentally.
        /// </summary>
        public static string GetProfilePath(string prefabPath, string sourcePsdGuid)
        {
            if (string.IsNullOrEmpty(prefabPath)) throw new ArgumentException("Prefab path is required.", "prefabPath");
            if (string.IsNullOrEmpty(sourcePsdGuid)) throw new ArgumentException("Source PSD GUID is required.", "sourcePsdGuid");
            if (sourcePsdGuid.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
                throw new ArgumentException("Source PSD GUID contains unsafe path characters.", "sourcePsdGuid");
            return "Assets/PSDLayoutTool2Settings/HierarchyProfiles/" + sourcePsdGuid + ".asset";
        }

        public static void ValidateProfileTargetBinding(PsdHierarchyProfile profile, string prefabPath)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            string normalizedPath = NormalizeAssetPath(prefabPath);
            string guid = AssetDatabase.AssetPathToGUID(normalizedPath);
            if (string.IsNullOrEmpty(profile.targetPrefabPath) || string.IsNullOrEmpty(profile.targetPrefabGuid))
                throw new InvalidOperationException("Hierarchy Profile is not explicitly bound to a target Prefab.");
            if (!string.Equals(NormalizeAssetPath(profile.targetPrefabPath), normalizedPath, StringComparison.Ordinal) ||
                !string.Equals(profile.targetPrefabGuid, guid, StringComparison.Ordinal))
                throw new InvalidOperationException("Hierarchy Profile target path or GUID does not match the configured Prefab.");
        }

        public static PsdHierarchyProfile ResolveBoundProfileForImport(string profilePath, string prefabPath)
        {
            PsdHierarchyProfile profile = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath);
            if (profile == null) return null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new InvalidOperationException(
                    "Hierarchy Profile exists but its exact target Prefab is missing or cannot be loaded: " + prefabPath);
            ValidateProfileTargetBinding(profile, prefabPath);
            return profile;
        }

        public static void Save(
            string prefabPath,
            GameObject loadedContents,
            string profilePath,
            PsdHierarchyProfile workingProfile,
            IReadOnlyDictionary<string, RectTransform> generatedByStableId,
            IReadOnlyDictionary<string, RectTransform> groupsByKey,
            IEnumerable<string> temporaryAssetPaths,
            Action<PsdPrefabTransactionStage> failureInjector,
            bool allowInitialTargetBinding = false)
        {
            if (string.IsNullOrEmpty(prefabPath)) throw new ArgumentException("Prefab path is required.", "prefabPath");
            if (loadedContents == null) throw new ArgumentNullException("loadedContents");
            if (string.IsNullOrEmpty(profilePath)) throw new ArgumentException("Profile path is required.", "profilePath");
            if (workingProfile == null) throw new ArgumentNullException("workingProfile");
            if (generatedByStableId == null) throw new ArgumentNullException("generatedByStableId");
            if (groupsByKey == null) throw new ArgumentNullException("groupsByKey");

            string[] temporaryPaths = (temporaryAssetPaths ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray();
            if (temporaryPaths.Any(path => string.Equals(path, prefabPath, StringComparison.Ordinal) ||
                                           string.Equals(path, profilePath, StringComparison.Ordinal)))
                throw new ArgumentException("A transaction target cannot also be registered as a temporary asset.",
                    "temporaryAssetPaths");

            AssetBackup prefabBackup = AssetBackup.Capture(prefabPath);
            AssetBackup profileBackup = AssetBackup.Capture(profilePath);
            string originalGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            bool wasBound = !string.IsNullOrEmpty(workingProfile.targetPrefabPath) ||
                            !string.IsNullOrEmpty(workingProfile.targetPrefabGuid);
            if (wasBound)
            {
                ValidateProfileTargetBinding(workingProfile, prefabPath);
            }
            else if (!allowInitialTargetBinding)
            {
                throw new InvalidOperationException("Only an explicit adoption/creation flow may bind a hierarchy Profile.");
            }
            bool committed = false;
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(loadedContents, prefabPath);
                if (saved == null) throw new InvalidOperationException("Unity failed to save the incremental Prefab.");
                Invoke(failureInjector, PsdPrefabTransactionStage.AfterPrefabSave);

                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VerifyTargetIdentity(prefabPath, originalGuid);
                Invoke(failureInjector, PsdPrefabTransactionStage.AfterReimportVerification);

                workingProfile.targetPrefabPath = NormalizeAssetPath(prefabPath);
                workingProfile.targetPrefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);

                // Identity is derived only after the Prefab save succeeded. It
                // is written to the detached working clone, never the current
                // Profile asset, until the second transaction phase begins.
                UpdateProfileIdentity(prefabPath, loadedContents.transform, workingProfile,
                    generatedByStableId, groupsByKey);
                SaveProfileClone(profilePath, workingProfile, failureInjector);
                Invoke(failureInjector, PsdPrefabTransactionStage.AfterProfileSave);

                AssetDatabase.ImportAsset(profilePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VerifyTargetIdentity(prefabPath, originalGuid);
                VerifyPersistedProfile(profilePath, workingProfile);
                Invoke(failureInjector, PsdPrefabTransactionStage.AfterFinalVerification);
                committed = true;
            }
            catch
            {
                prefabBackup.Restore();
                profileBackup.Restore();
                ImportIfPresent(prefabPath);
                ImportIfPresent(profilePath);
                throw;
            }
            finally
            {
                CleanupTemporaryAssets(temporaryPaths);
                if (!committed)
                {
                    // Refresh above restores Unity's object cache; this final
                    // import is intentionally exact-path only and cannot touch
                    // another same-name Prefab elsewhere in the project.
                    ImportIfPresent(prefabPath);
                    ImportIfPresent(profilePath);
                }
            }
        }

        private static void UpdateProfileIdentity(
            string prefabPath,
            Transform loadedRoot,
            PsdHierarchyProfile profile,
            IReadOnlyDictionary<string, RectTransform> generated,
            IReadOnlyDictionary<string, RectTransform> groups)
        {
            GameObject persistentRootObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (persistentRootObject == null) throw new InvalidOperationException("Saved Prefab cannot be reloaded.");
            Transform persistentRoot = persistentRootObject.transform;
            Dictionary<Transform, long> localIds = ResolveLocalIds(loadedRoot, persistentRoot);

            foreach (PsdHierarchyProfileNode record in profile.nodes ?? new List<PsdHierarchyProfileNode>())
            {
                RectTransform target;
                long localId;
                if (record == null || !PsdStableLayerIdUtility.IsPersistable(record.stableId) ||
                    !generated.TryGetValue(record.stableId, out target)) continue;
                if (!localIds.TryGetValue(target, out localId) || localId <= 0L)
                    throw new InvalidOperationException("Saved Prefab local ID is missing for PSD layer '" + record.stableId + "'.");
                record.localFileId = localId;
                record.lastKnownPath = HierarchyPath(target, loadedRoot);
                record.pendingCreation = false;
            }

            foreach (PsdHierarchyProfileGroup record in profile.groups ?? new List<PsdHierarchyProfileGroup>())
            {
                RectTransform target;
                long localId;
                if (record == null || !groups.TryGetValue(record.key, out target)) continue;
                if (!localIds.TryGetValue(target, out localId) || localId <= 0L)
                    throw new InvalidOperationException("Saved Prefab local ID is missing for organizer group '" + record.key + "'.");
                record.localFileId = localId;
                record.lastKnownPath = HierarchyPath(target, loadedRoot);
            }
        }

        private static Dictionary<Transform, long> ResolveLocalIds(Transform loadedRoot, Transform persistentRoot)
        {
            Transform[] loaded = loadedRoot.GetComponentsInChildren<Transform>(true);
            Transform[] persistent = persistentRoot.GetComponentsInChildren<Transform>(true);
            if (loaded.Length != persistent.Length)
                throw new InvalidOperationException("Saved Prefab hierarchy differs from the validated loaded contents.");
            var result = new Dictionary<Transform, long>();
            for (int index = 0; index < loaded.Length; index++)
            {
                string guid;
                long localId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(persistent[index].gameObject, out guid, out localId) || localId <= 0L)
                    throw new InvalidOperationException("Unity did not expose a local file ID for the saved Prefab object.");
                result.Add(loaded[index], localId);
            }
            return result;
        }

        private static void SaveProfileClone(
            string profilePath,
            PsdHierarchyProfile workingProfile,
            Action<PsdPrefabTransactionStage> failureInjector)
        {
            Invoke(failureInjector, PsdPrefabTransactionStage.BeforeProfileCopy);
            PsdHierarchyProfile persistent = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath);
            if (persistent == null)
            {
                string directory = Path.GetDirectoryName(profilePath);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                    throw new InvalidOperationException("Profile directory does not exist: " + directory);
                PsdHierarchyProfile created = UnityEngine.Object.Instantiate(workingProfile);
                created.name = Path.GetFileNameWithoutExtension(profilePath);
                AssetDatabase.CreateAsset(created, profilePath);
                persistent = created;
            }
            else
            {
                EditorUtility.CopySerialized(workingProfile, persistent);
                EditorUtility.SetDirty(persistent);
            }
            Invoke(failureInjector, PsdPrefabTransactionStage.DuringProfileCopy);
            Invoke(failureInjector, PsdPrefabTransactionStage.AfterProfileCopy);
            AssetDatabase.SaveAssetIfDirty(persistent);
        }

        private static void VerifyPersistedProfile(string profilePath, PsdHierarchyProfile expected)
        {
            PsdHierarchyProfile actual = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath);
            if (actual == null) throw new InvalidOperationException("Hierarchy Profile was not saved.");
            if (!string.Equals(CanonicalProfile(actual), CanonicalProfile(expected), StringComparison.Ordinal))
                throw new InvalidOperationException("Hierarchy Profile canonical verification failed.");
        }

        private static string CanonicalProfile(PsdHierarchyProfile profile)
        {
            var value = new System.Text.StringBuilder();
            Append(value, profile.schemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(value, profile.sourcePsdGuid);
            Append(value, profile.sourceFingerprint);
            Append(value, profile.sourceContentFingerprint);
            Append(value, profile.sourceStructureFingerprint);
            Append(value, profile.sourceGeometryFingerprint);
            Append(value, profile.targetPrefabGuid);
            Append(value, NormalizeAssetPath(profile.targetPrefabPath));
            foreach (PsdHierarchyProfileNode node in (profile.nodes ?? new List<PsdHierarchyProfileNode>())
                         .Where(node => node != null).OrderBy(node => node.stableId, StringComparer.Ordinal))
            {
                Append(value, node.stableId);
                Append(value, ((int)node.ownership).ToString(System.Globalization.CultureInfo.InvariantCulture));
                Append(value, node.contentFingerprint);
                Append(value, node.structureFingerprint);
                Append(value, node.geometryFingerprint);
                Append(value, node.pendingCreation ? "1" : "0");
                Append(value, node.localFileId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Append(value, node.lastKnownPath);
                foreach (string componentType in (node.importerOwnedComponentTypes ?? new List<string>())
                             .OrderBy(type => type, StringComparer.Ordinal))
                    Append(value, componentType);
            }
            foreach (PsdHierarchyProfileGroup group in (profile.groups ?? new List<PsdHierarchyProfileGroup>())
                         .Where(group => group != null).OrderBy(group => group.key, StringComparer.Ordinal))
            {
                Append(value, group.key);
                Append(value, group.parentKey);
                Append(value, group.displayName);
                foreach (string member in group.stableLayerIds ?? new List<string>()) Append(value, member);
                Append(value, group.localFileId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Append(value, group.lastKnownPath);
            }
            foreach (PsdHierarchyProfileRename rename in (profile.renames ?? new List<PsdHierarchyProfileRename>())
                         .Where(rename => rename != null).OrderBy(rename => rename.stableId, StringComparer.Ordinal))
            {
                Append(value, rename.stableId);
                Append(value, rename.name);
            }
            return value.ToString();
        }

        private static void Append(System.Text.StringBuilder target, string field)
        {
            field = field ?? string.Empty;
            target.Append(field.Length).Append(':').Append(field).Append('|');
        }

        private static void VerifyTargetIdentity(string prefabPath, string originalGuid)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new InvalidOperationException("Saved Prefab cannot be loaded.");
            if (!string.IsNullOrEmpty(originalGuid) &&
                !string.Equals(AssetDatabase.AssetPathToGUID(prefabPath), originalGuid, StringComparison.Ordinal))
                throw new InvalidOperationException("Incremental save changed the target Prefab GUID.");
        }

        private static string HierarchyPath(Transform target, Transform root)
        {
            var names = new Stack<string>();
            for (Transform cursor = target; cursor != null; cursor = cursor.parent)
            {
                names.Push(cursor.name);
                if (cursor == root) break;
            }
            return string.Join("/", names.ToArray());
        }

        private static void CleanupTemporaryAssets(IEnumerable<string> paths)
        {
            foreach (string path in (paths ?? Enumerable.Empty<string>())
                         .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal))
            {
                AssetDatabase.DeleteAsset(path);
                string fullPath = ToFullPath(path);
                DeleteFile(fullPath);
                DeleteFile(fullPath + ".meta");
            }
        }

        private static void ImportIfPresent(string assetPath)
        {
            if (File.Exists(ToFullPath(assetPath)))
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void Invoke(Action<PsdPrefabTransactionStage> injector, PsdPrefabTransactionStage stage)
        {
            if (injector != null) injector(stage);
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private sealed class AssetBackup
        {
            private readonly string assetPath;
            private readonly bool assetExisted;
            private readonly bool metaExisted;
            private readonly byte[] assetBytes;
            private readonly byte[] metaBytes;

            private AssetBackup(string assetPath)
            {
                this.assetPath = assetPath;
                string fullPath = ToFullPath(assetPath);
                assetExisted = File.Exists(fullPath);
                metaExisted = File.Exists(fullPath + ".meta");
                assetBytes = assetExisted ? File.ReadAllBytes(fullPath) : null;
                metaBytes = metaExisted ? File.ReadAllBytes(fullPath + ".meta") : null;
            }

            public static AssetBackup Capture(string assetPath)
            {
                return new AssetBackup(assetPath);
            }

            public void Restore()
            {
                string fullPath = ToFullPath(assetPath);
                if (!assetExisted)
                {
                    // Delete through Unity first so its imported-object cache
                    // cannot retain a ghost ScriptableObject after rollback.
                    AssetDatabase.DeleteAsset(assetPath);
                    DeleteFile(fullPath);
                    DeleteFile(fullPath + ".meta");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    return;
                }
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                RestoreFile(fullPath, assetExisted, assetBytes);
                RestoreFile(fullPath + ".meta", metaExisted, metaBytes);
            }

            private static void RestoreFile(string path, bool existed, byte[] bytes)
            {
                if (existed) File.WriteAllBytes(path, bytes);
                else DeleteFile(path);
            }
        }
    }
}
