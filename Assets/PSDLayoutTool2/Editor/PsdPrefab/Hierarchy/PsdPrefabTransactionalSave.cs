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
            string directory = (Path.GetDirectoryName(prefabPath) ?? string.Empty).Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(prefabPath) + "." + sourcePsdGuid + ".HierarchyProfile.asset";
            return string.IsNullOrEmpty(directory) ? fileName : directory + "/" + fileName;
        }

        public static void Save(
            string prefabPath,
            GameObject loadedContents,
            string profilePath,
            PsdHierarchyProfile workingProfile,
            IReadOnlyDictionary<string, RectTransform> generatedByStableId,
            IReadOnlyDictionary<string, RectTransform> groupsByKey,
            IEnumerable<string> temporaryAssetPaths,
            Action<PsdPrefabTransactionStage> failureInjector)
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
            bool committed = false;
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(loadedContents, prefabPath);
                if (saved == null) throw new InvalidOperationException("Unity failed to save the incremental Prefab.");
                Invoke(failureInjector, PsdPrefabTransactionStage.AfterPrefabSave);

                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VerifyTargetIdentity(prefabPath, originalGuid);
                Invoke(failureInjector, PsdPrefabTransactionStage.AfterReimportVerification);

                // Identity is derived only after the Prefab save succeeded. It
                // is written to the detached working clone, never the current
                // Profile asset, until the second transaction phase begins.
                UpdateProfileIdentity(prefabPath, loadedContents.transform, workingProfile,
                    generatedByStableId, groupsByKey);
                SaveProfileClone(profilePath, workingProfile);
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

        private static void SaveProfileClone(string profilePath, PsdHierarchyProfile workingProfile)
        {
            PsdHierarchyProfile persistent = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath);
            if (persistent == null)
            {
                string directory = Path.GetDirectoryName(profilePath);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                    throw new InvalidOperationException("Profile directory does not exist: " + directory);
                PsdHierarchyProfile created = UnityEngine.Object.Instantiate(workingProfile);
                created.name = Path.GetFileNameWithoutExtension(profilePath);
                AssetDatabase.CreateAsset(created, profilePath);
            }
            else
            {
                EditorUtility.CopySerialized(workingProfile, persistent);
                EditorUtility.SetDirty(persistent);
            }
            if (persistent != null) AssetDatabase.SaveAssetIfDirty(persistent);
        }

        private static void VerifyPersistedProfile(string profilePath, PsdHierarchyProfile expected)
        {
            PsdHierarchyProfile actual = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(profilePath);
            if (actual == null) throw new InvalidOperationException("Hierarchy Profile was not saved.");
            Dictionary<string, PsdHierarchyProfileNode> actualNodes = (actual.nodes ?? new List<PsdHierarchyProfileNode>())
                .Where(node => node != null).ToDictionary(node => node.stableId, StringComparer.Ordinal);
            foreach (PsdHierarchyProfileNode expectedNode in expected.nodes ?? new List<PsdHierarchyProfileNode>())
            {
                PsdHierarchyProfileNode actualNode;
                if (expectedNode == null || !PsdStableLayerIdUtility.IsPersistable(expectedNode.stableId)) continue;
                if (!actualNodes.TryGetValue(expectedNode.stableId, out actualNode) ||
                    actualNode.localFileId != expectedNode.localFileId ||
                    !string.Equals(actualNode.lastKnownPath, expectedNode.lastKnownPath, StringComparison.Ordinal))
                    throw new InvalidOperationException("Hierarchy Profile identity verification failed.");
            }
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
