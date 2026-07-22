namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Removes only Missing MonoBehaviour components from the public Common
    /// prefab folder, using Unity's prefab serialization API.
    /// </summary>
    internal static class RemoveMissingScriptsFromCommonPrefabs
    {
        private const string TargetFolder = "Assets/UI/Common/Prefabs/_Common";

        [MenuItem("Tools/PSD Layout/Remove Missing Scripts From Common Prefabs")]
        private static void RemoveMissingScripts()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetFolder });
            int removedCount = 0;
            int affectedPrefabCount = 0;
            var skippedPrefabPaths = new List<string>();

            try
            {
                for (int index = 0; index < prefabGuids.Length; index++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                    EditorUtility.DisplayProgressBar(
                        "Remove Missing Scripts",
                        path,
                        prefabGuids.Length == 0 ? 1f : (float)index / prefabGuids.Length);

                    GameObject prefabRoot = null;
                    try
                    {
                        prefabRoot = PrefabUtility.LoadPrefabContents(path);
                        if (prefabRoot == null)
                        {
                            throw new InvalidOperationException("PrefabUtility returned no root object.");
                        }

                        int removedFromPrefab = RemoveMissingScriptsRecursively(prefabRoot);
                        if (removedFromPrefab <= 0)
                        {
                            continue;
                        }

                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        removedCount += removedFromPrefab;
                        affectedPrefabCount++;
                    }
                    catch (Exception exception)
                    {
                        skippedPrefabPaths.Add(path);
                        Debug.LogWarning("Skipped missing-script cleanup because the prefab could not be loaded: " + path + "\n" + exception.Message);
                    }
                    finally
                    {
                        if (prefabRoot != null)
                        {
                            PrefabUtility.UnloadPrefabContents(prefabRoot);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Removed " + removedCount + " missing scripts from " + affectedPrefabCount + " prefabs under " + TargetFolder + ".");
            if (skippedPrefabPaths.Count > 0)
            {
                Debug.LogWarning("Skipped " + skippedPrefabPaths.Count + " prefabs that Unity could not load:\n" + string.Join("\n", skippedPrefabPaths.ToArray()));
            }
        }

        private static int RemoveMissingScriptsRecursively(GameObject gameObject)
        {
            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            Transform transform = gameObject.transform;
            for (int index = 0; index < transform.childCount; index++)
            {
                removedCount += RemoveMissingScriptsRecursively(transform.GetChild(index).gameObject);
            }

            return removedCount;
        }
    }
}
