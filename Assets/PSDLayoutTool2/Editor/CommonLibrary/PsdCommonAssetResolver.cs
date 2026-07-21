namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Cached, exact-key resolver for assets below configured common-library roots.
    /// </summary>
    public static class PsdCommonAssetResolver
    {
        private static readonly Dictionary<PsdCommonAssetKind, Dictionary<string, List<UnityEngine.Object>>> Index =
            new Dictionary<PsdCommonAssetKind, Dictionary<string, List<UnityEngine.Object>>>();
        private static bool isDirty = true;

        public static void Invalidate()
        {
            isDirty = true;
        }

        public static bool TryResolve(
            PsdCommonAssetReference reference,
            out UnityEngine.Object asset,
            out string error)
        {
            asset = null;
            error = string.Empty;
            if (reference == null)
            {
                error = "The Common_* layer rule is missing.";
                return false;
            }

            PsdCommonAssetLibrarySettings settings = PsdCommonAssetLibrarySettings.Load();
            if (settings == null)
            {
                error = "Common Asset Library is not configured. Open Project Settings > PSD Layout Tool > Common Asset Library.";
                return false;
            }

            EnsureIndex(settings);
            Dictionary<string, List<UnityEngine.Object>> byKey;
            if (!Index.TryGetValue(reference.Kind, out byKey) || !byKey.TryGetValue(reference.Key, out List<UnityEngine.Object> matches))
            {
                error = "Common " + reference.Kind + " was not found for key '" + reference.Key + "'.";
                return false;
            }

            if (matches.Count != 1)
            {
                error = "Common " + reference.Kind + " key '" + reference.Key + "' is ambiguous: " + matches.Count + " assets share that exact name.";
                return false;
            }

            asset = matches[0];
            return true;
        }

        private static void EnsureIndex(PsdCommonAssetLibrarySettings settings)
        {
            if (!isDirty)
            {
                return;
            }

            Index.Clear();
            Index[PsdCommonAssetKind.Prefab] = BuildPrefabIndex(settings);
            Index[PsdCommonAssetKind.Texture] = BuildSpriteIndex(settings);
            isDirty = false;
        }

        private static Dictionary<string, List<UnityEngine.Object>> BuildPrefabIndex(PsdCommonAssetLibrarySettings settings)
        {
            var result = CreateKeyMap();
            foreach (string root in settings.GetRootPaths(PsdCommonAssetKind.Prefab))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        Add(result, Path.GetFileNameWithoutExtension(path), prefab);
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, List<UnityEngine.Object>> BuildSpriteIndex(PsdCommonAssetLibrarySettings settings)
        {
            var result = CreateKeyMap();
            foreach (string root in settings.GetRootPaths(PsdCommonAssetKind.Texture))
            {
                foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        Sprite sprite = asset as Sprite;
                        if (sprite != null)
                        {
                            Add(result, sprite.name, sprite);
                        }
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, List<UnityEngine.Object>> CreateKeyMap()
        {
            return new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);
        }

        private static void Add(
            Dictionary<string, List<UnityEngine.Object>> target,
            string key,
            UnityEngine.Object asset)
        {
            if (string.IsNullOrEmpty(key) || asset == null)
            {
                return;
            }

            if (!target.TryGetValue(key, out List<UnityEngine.Object> items))
            {
                items = new List<UnityEngine.Object>();
                target.Add(key, items);
            }

            items.Add(asset);
        }
    }
}
