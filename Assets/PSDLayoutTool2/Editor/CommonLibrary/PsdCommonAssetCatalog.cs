namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 为通用资源 PSD 命名规则生成并进行版本管理的“资源键到资源”映射表。
    /// 导入器只读取直接的 Unity 资源引用；仅在明确刷新映射表时扫描整个项目。
    /// </summary>
    public sealed class PsdCommonAssetCatalog : ScriptableObject
    {
        public const string AssetPath = "Assets/PSDLayoutTool2Settings/PsdCommonAssetCatalog.asset";
        private static bool catalogSaveQueued;
        public bool needsRefresh;
        public List<PsdCommonPrefabCatalogEntry> prefabs = new List<PsdCommonPrefabCatalogEntry>();
        public List<PsdCommonTextureCatalogEntry> textures = new List<PsdCommonTextureCatalogEntry>();

        public static PsdCommonAssetCatalog Load()
        {
            return AssetDatabase.LoadAssetAtPath<PsdCommonAssetCatalog>(AssetPath);
        }

        public static PsdCommonAssetCatalog CreateOrRefresh()
        {
            PsdCommonAssetCatalog catalog = Load();
            if (catalog == null)
            {
                EnsureFolder("Assets/PSDLayoutTool2Settings");
                catalog = CreateInstance<PsdCommonAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            catalog.prefabs.Clear();
            catalog.textures.Clear();
            AddPrefabs(catalog);
            AddTextures(catalog);
            catalog.needsRefresh = false;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>
        /// 修改前缀后，所有已生成资源键都会失效。
        /// 在用户完整刷新映射表之前，暂停增量更新，避免混用新旧命名规则。
        /// </summary>
        public static void MarkNeedsRefresh()
        {
            PsdCommonAssetCatalog catalog = Load();
            if (catalog == null || catalog.needsRefresh)
            {
                return;
            }

            catalog.needsRefresh = true;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 只处理 Unity 资源导入回调提供的资源路径。
        /// 全项目扫描仅由 CreateOrRefresh 主动执行。
        /// </summary>
        public static void ApplyAssetChanges(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            PsdCommonAssetCatalog catalog = Load();
            if (catalog == null || catalog.needsRefresh ||
                !CouldAffectCatalog(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths))
            {
                return;
            }

            var replacedOrRemovedPaths = new List<string>();
            AddPaths(replacedOrRemovedPaths, importedAssets);
            AddPaths(replacedOrRemovedPaths, deletedAssets);
            AddPaths(replacedOrRemovedPaths, movedAssets);
            AddPaths(replacedOrRemovedPaths, movedFromAssetPaths);

            List<PsdCommonCatalogEntryState> delta = PsdCommonCatalogDelta.Apply(
                GetExistingEntries(catalog),
                replacedOrRemovedPaths,
                GetCurrentEntries(importedAssets, movedAssets));

            WriteEntries(catalog, delta);
            catalog.needsRefresh = false;
            EditorUtility.SetDirty(catalog);
            QueueCatalogSave();
        }

        private static void AddPrefabs(PsdCommonAssetCatalog catalog)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!PsdCommonCatalogPathPolicy.IsPublicAssetPath(path))
                {
                    continue;
                }

                string key;
                if (!PsdCommonAssetNameParser.TryParsePrefabAssetKey(Path.GetFileNameWithoutExtension(path), out key))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    catalog.prefabs.Add(new PsdCommonPrefabCatalogEntry
                    {
                        key = key,
                        prefab = prefab,
                        guid = guid,
                        assetPath = path
                    });
                }
            }
        }

        private static void AddTextures(PsdCommonAssetCatalog catalog)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!PsdCommonCatalogPathPolicy.IsPublicAssetPath(path))
                {
                    continue;
                }

                string key;
                if (!PsdCommonAssetNameParser.TryParseTextureAssetKey(Path.GetFileNameWithoutExtension(path), out key))
                {
                    continue;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    catalog.textures.Add(new PsdCommonTextureCatalogEntry
                    {
                        key = key,
                        sprite = sprite,
                        guid = guid,
                        assetPath = path
                    });
                }
            }
        }

        private static bool CouldAffectCatalog(params string[][] pathGroups)
        {
            foreach (string[] paths in pathGroups)
            {
                if (paths == null)
                {
                    continue;
                }

                foreach (string path in paths)
                {
                    if (!PsdCommonCatalogPathPolicy.IsPublicAssetPath(path))
                    {
                        continue;
                    }

                    string key;
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    if (PsdCommonAssetNameParser.TryParsePrefabAssetKey(fileName, out key) ||
                        PsdCommonAssetNameParser.TryParseTextureAssetKey(fileName, out key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<PsdCommonCatalogEntryState> GetExistingEntries(PsdCommonAssetCatalog catalog)
        {
            foreach (PsdCommonPrefabCatalogEntry entry in catalog.prefabs)
            {
                if (entry == null || entry.prefab == null)
                {
                    continue;
                }

                PsdCommonCatalogEntryState state = CreateState(PsdCommonAssetKind.Prefab, entry.key, entry.guid, entry.assetPath, entry.prefab);
                if (PsdCommonCatalogPathPolicy.IsPublicAssetPath(state.AssetPath))
                {
                    yield return state;
                }
            }

            foreach (PsdCommonTextureCatalogEntry entry in catalog.textures)
            {
                if (entry == null || entry.sprite == null)
                {
                    continue;
                }

                PsdCommonCatalogEntryState state = CreateState(PsdCommonAssetKind.Texture, entry.key, entry.guid, entry.assetPath, entry.sprite);
                if (PsdCommonCatalogPathPolicy.IsPublicAssetPath(state.AssetPath))
                {
                    yield return state;
                }
            }
        }

        private static IEnumerable<PsdCommonCatalogEntryState> GetCurrentEntries(params string[][] pathGroups)
        {
            foreach (string[] paths in pathGroups)
            {
                if (paths == null)
                {
                    continue;
                }

                foreach (string path in paths)
                {
                    if (!PsdCommonCatalogPathPolicy.IsPublicAssetPath(path))
                    {
                        continue;
                    }

                    string key;
                    if (PsdCommonAssetNameParser.TryParsePrefabAssetKey(Path.GetFileNameWithoutExtension(path), out key))
                    {
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null)
                        {
                            yield return CreateState(PsdCommonAssetKind.Prefab, key, AssetDatabase.AssetPathToGUID(path), path, prefab);
                        }

                        continue;
                    }

                    if (PsdCommonAssetNameParser.TryParseTextureAssetKey(Path.GetFileNameWithoutExtension(path), out key))
                    {
                        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (sprite != null)
                        {
                            yield return CreateState(PsdCommonAssetKind.Texture, key, AssetDatabase.AssetPathToGUID(path), path, sprite);
                        }
                    }
                }
            }
        }

        private static PsdCommonCatalogEntryState CreateState(
            PsdCommonAssetKind kind,
            string key,
            string guid,
            string assetPath,
            UnityEngine.Object asset)
        {
            string resolvedPath = string.IsNullOrEmpty(assetPath) ? AssetDatabase.GetAssetPath(asset) : assetPath;
            string resolvedGuid = string.IsNullOrEmpty(guid) ? AssetDatabase.AssetPathToGUID(resolvedPath) : guid;
            return new PsdCommonCatalogEntryState(kind, key, resolvedGuid, resolvedPath);
        }

        private static void WriteEntries(PsdCommonAssetCatalog catalog, IEnumerable<PsdCommonCatalogEntryState> states)
        {
            catalog.prefabs.Clear();
            catalog.textures.Clear();

            foreach (PsdCommonCatalogEntryState state in states)
            {
                if (state.Kind == PsdCommonAssetKind.Prefab)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(state.AssetPath);
                    if (prefab != null)
                    {
                        catalog.prefabs.Add(new PsdCommonPrefabCatalogEntry
                        {
                            key = state.Key,
                            prefab = prefab,
                            guid = state.Guid,
                            assetPath = state.AssetPath
                        });
                    }

                    continue;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(state.AssetPath);
                if (sprite != null)
                {
                    catalog.textures.Add(new PsdCommonTextureCatalogEntry
                    {
                        key = state.Key,
                        sprite = sprite,
                        guid = state.Guid,
                        assetPath = state.AssetPath
                    });
                }
            }
        }

        private static void AddPaths(ICollection<string> destination, IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    destination.Add(path);
                }
            }
        }

        private static void QueueCatalogSave()
        {
            if (catalogSaveQueued)
            {
                return;
            }

            catalogSaveQueued = true;
            EditorApplication.delayCall += SaveQueuedCatalogChanges;
        }

        private static void SaveQueuedCatalogChanges()
        {
            catalogSaveQueued = false;
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    [Serializable]
    public sealed class PsdCommonPrefabCatalogEntry
    {
        public string key;
        public GameObject prefab;
        public string guid;
        public string assetPath;
    }

    [Serializable]
    public sealed class PsdCommonTextureCatalogEntry
    {
        public string key;
        public Sprite sprite;
        public string guid;
        public string assetPath;
    }
}
