namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using UnityEditor;

    /// <summary>
    /// 私有资源改名（v2 计划中的 textureRenames / spriteAtlasRenames）。
    /// 预检只校验（身份、所有权、目标冲突），Apply 才真正改名；改名保持 GUID，因此 Prefab 引用自动保持有效。
    /// 中途失败不回滚也不重做，而是把「已完成的改名」写进错误信息，交由调用方报告实际写入状态。
    /// </summary>
    internal sealed class NativeAssetRename
    {
        internal NativeAssetRename(string fromPath, string toPath, string toName, string expectedGuid, bool isTexture)
        {
            this.fromPath = fromPath;
            this.toPath = toPath;
            this.toName = toName;
            this.expectedGuid = expectedGuid;
            this.isTexture = isTexture;
        }

        internal readonly string fromPath;
        internal readonly string toPath;
        internal readonly string toName;
        internal readonly string expectedGuid;
        internal readonly bool isTexture;
    }

    internal static class PsdHierarchyNativeAssetRenamer
    {
        private static readonly string[] TextureExtensions =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".bmp", ".gif", ".exr", ".hdr",
        };

        internal static IReadOnlyList<NativeAssetRename> Bind(
            JObject plan,
            string targetPrefabAssetPath)
        {
            var renames = new List<NativeAssetRename>();
            JArray textures = plan["textureRenames"] as JArray ?? new JArray();
            JArray atlases = plan["spriteAtlasRenames"] as JArray ?? new JArray();
            if (textures.Count == 0 && atlases.Count == 0)
            {
                return renames;
            }

            string prefabName = (plan.Value<string>("prefabName") ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(prefabName))
                throw new InvalidDataException("prefabName is required when the plan renames private assets.");

            var dependencies = new HashSet<string>(
                AssetDatabase.GetDependencies(Normalize(targetPrefabAssetPath), true).Select(Normalize),
                StringComparer.Ordinal);
            string prefabDirectory = Path.GetDirectoryName(Normalize(targetPrefabAssetPath))?.Replace('\\', '/') ?? string.Empty;

            BindRenames(renames, textures, prefabName, true, dependencies, prefabDirectory);
            BindRenames(renames, atlases, prefabName, false, dependencies, prefabDirectory);

            var targetPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (NativeAssetRename rename in renames)
            {
                if (!targetPaths.Add(rename.toPath))
                    throw new InvalidDataException("Two renames would produce the same asset path: " + rename.toPath);
            }

            return renames;
        }

        private static void BindRenames(
            ICollection<NativeAssetRename> output,
            JArray entries,
            string prefabName,
            bool isTexture,
            ISet<string> dependencies,
            string prefabDirectory)
        {
            string label = isTexture ? "textureRenames" : "spriteAtlasRenames";
            var sources = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                if (!(entries[index] is JObject entry))
                    throw new InvalidDataException(label + " entries must be objects.");

                string entryLabel = label + "[" + index + "]";
                string fromPath = Normalize(ReadRequired(entry, "from", entryLabel));
                string toName = ReadRequired(entry, "toName", entryLabel);
                if (toName.IndexOf('/') >= 0 || toName.IndexOf('\\') >= 0 || toName.IndexOf('.') >= 0)
                    throw new InvalidDataException(entryLabel + ".toName must be a file name without extension: " + toName);

                if (isTexture)
                {
                    if (!TextureExtensions.Contains(Path.GetExtension(fromPath), StringComparer.OrdinalIgnoreCase))
                        throw new InvalidDataException(entryLabel + ".from is not a private Texture asset: " + fromPath);
                    if (!toName.StartsWith(prefabName + "_", StringComparison.Ordinal))
                        throw new InvalidDataException(
                            entryLabel + ".toName must start with \"" + prefabName + "_\": " + toName);
                    if (!dependencies.Contains(fromPath))
                        throw new InvalidDataException(
                            entryLabel + ".from is not referenced by the current target Prefab: " + fromPath);
                }
                else
                {
                    string extension = Path.GetExtension(fromPath);
                    if (!string.Equals(extension, ".spriteatlas", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".spriteatlasv2", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(entryLabel + ".from is not a SpriteAtlas asset: " + fromPath);
                    }

                    if (!string.Equals(toName, prefabName, StringComparison.Ordinal))
                        throw new InvalidDataException(
                            entryLabel + ".toName must equal prefabName \"" + prefabName + "\": " + toName);
                    string directory = Path.GetDirectoryName(fromPath)?.Replace('\\', '/') ?? string.Empty;
                    if (!string.IsNullOrEmpty(prefabDirectory) &&
                        !directory.StartsWith(prefabDirectory, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            entryLabel + ".from is not a private SpriteAtlas of the current target Prefab: " + fromPath);
                    }
                }

                if (!sources.Add(fromPath))
                    throw new InvalidDataException(label + " contains a duplicate source: " + fromPath);
                if (AssetDatabase.LoadMainAssetAtPath(fromPath) == null)
                    throw new InvalidDataException(entryLabel + ".from asset did not load: " + fromPath);

                string currentGuid = AssetDatabase.AssetPathToGUID(fromPath);
                if (string.IsNullOrEmpty(currentGuid))
                    throw new InvalidDataException(entryLabel + ".from has no Unity GUID: " + fromPath);

                string expectedGuid = (entry.Value<string>("expectedGuid") ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(expectedGuid) &&
                    !string.Equals(expectedGuid, currentGuid, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        entryLabel + " asset identity changed: expectedGuid=" + expectedGuid +
                        ";actualGuid=" + currentGuid + ";path=" + fromPath);
                }

                string toPath = (Path.GetDirectoryName(fromPath)?.Replace('\\', '/') ?? string.Empty) + "/" +
                                toName + Path.GetExtension(fromPath);
                if (string.Equals(toPath, fromPath, StringComparison.Ordinal))
                    throw new InvalidDataException(entryLabel + ".from already uses the reviewed name: " + fromPath);
                if (AssetDatabase.LoadMainAssetAtPath(toPath) != null)
                    throw new InvalidDataException(entryLabel + " target already exists: " + toPath);

                output.Add(new NativeAssetRename(fromPath, toPath, toName, currentGuid, isTexture));
            }
        }

        /// <summary>预检不写入；Apply 才改名。改名保持 GUID，所以 Prefab 引用不需要重映射。</summary>
        internal static void Apply(IReadOnlyList<NativeAssetRename> renames, bool save)
        {
            if (!save || renames == null || renames.Count == 0)
            {
                return;
            }

            var completed = new List<string>();
            foreach (NativeAssetRename rename in renames)
            {
                string currentGuid = AssetDatabase.AssetPathToGUID(rename.fromPath);
                if (!string.Equals(currentGuid, rename.expectedGuid, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Asset identity changed before renaming: path=" + rename.fromPath +
                        ";expectedGuid=" + rename.expectedGuid + ";actualGuid=" + currentGuid +
                        (completed.Count == 0 ? string.Empty : ";alreadyRenamed=" + string.Join(", ", completed.ToArray())));
                }

                if (AssetDatabase.LoadMainAssetAtPath(rename.toPath) != null)
                {
                    throw new InvalidDataException(
                        "Asset rename target already exists: " + rename.toPath +
                        (completed.Count == 0 ? string.Empty : ";alreadyRenamed=" + string.Join(", ", completed.ToArray())));
                }

                string error = AssetDatabase.RenameAsset(rename.fromPath, rename.toName);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException(
                        "Asset rename failed (" + rename.fromPath + " -> " + rename.toName + "): " + error +
                        (completed.Count == 0 ? string.Empty : ";alreadyRenamed=" + string.Join(", ", completed.ToArray())));
                }

                // 改名必须保持身份：GUID 变化说明改到了别的资源，立即停止并报告实际状态。
                string renamedGuid = AssetDatabase.AssetPathToGUID(rename.toPath);
                if (!string.Equals(renamedGuid, rename.expectedGuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Asset rename lost the asset identity: path=" + rename.toPath +
                        ";expectedGuid=" + rename.expectedGuid + ";actualGuid=" + renamedGuid);
                }

                AssetDatabase.ImportAsset(rename.toPath, ImportAssetOptions.ForceUpdate);
                completed.Add(rename.fromPath + " -> " + rename.toPath);
            }
        }

        internal static void Verify(
            IReadOnlyList<NativeAssetRename> renames,
            string targetPrefabAssetPath,
            bool persisted)
        {
            if (!persisted || renames == null || renames.Count == 0)
            {
                return;
            }

            var dependencies = new HashSet<string>(
                AssetDatabase.GetDependencies(Normalize(targetPrefabAssetPath), true).Select(Normalize),
                StringComparer.Ordinal);

            foreach (NativeAssetRename rename in renames)
            {
                if (AssetDatabase.LoadMainAssetAtPath(rename.toPath) == null)
                    throw new InvalidOperationException("Renamed asset is missing: " + rename.toPath);
                string guid = AssetDatabase.AssetPathToGUID(rename.toPath);
                if (!string.Equals(guid, rename.expectedGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Renamed asset lost its identity: path=" + rename.toPath +
                        ";expectedGuid=" + rename.expectedGuid + ";actualGuid=" + guid);
                if (AssetDatabase.LoadMainAssetAtPath(rename.fromPath) != null)
                    throw new InvalidOperationException("Renamed asset still exists at its old path: " + rename.fromPath);
                if (rename.isTexture && !dependencies.Contains(rename.toPath))
                    throw new InvalidOperationException(
                        "Target Prefab no longer references the renamed Texture: " + rename.toPath);
            }
        }

        private static string ReadRequired(JObject owner, string field, string label)
        {
            string value = owner.Value<string>(field);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException(label + "." + field + " must be a non-empty string.");
            return value;
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }
    }
}
