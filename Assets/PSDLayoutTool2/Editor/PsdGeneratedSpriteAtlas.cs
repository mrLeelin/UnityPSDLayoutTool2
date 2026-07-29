namespace PsdLayoutTool2
{
    using System;
    using UnityEditor;
    using UnityEditor.U2D;
    using UnityEngine;
    using UnityEngine.U2D;

    /// <summary>
    /// Creates the generated SpriteAtlas while preserving settings on an existing asset.
    /// </summary>
    internal static class PsdGeneratedSpriteAtlas
    {
        internal static SpriteAtlas CreateOrUpdate(
            string atlasAssetPath,
            string textureFolderAssetPath,
            PsdImporter.SpriteAtlasVersion version)
        {
            string normalizedAtlasPath = NormalizeAssetPath(atlasAssetPath);
            string normalizedTextureFolderPath = NormalizeAssetPath(textureFolderAssetPath);
            string expectedExtension;
            switch (version)
            {
                case PsdImporter.SpriteAtlasVersion.V1:
                    expectedExtension = ".spriteatlas";
                    break;
                case PsdImporter.SpriteAtlasVersion.V2:
                    expectedExtension = ".spriteatlasv2";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("version", version, "Unsupported SpriteAtlas version.");
            }

            if (!IsAssetPath(normalizedAtlasPath) ||
                !normalizedAtlasPath.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "SpriteAtlas path must be an Assets-relative " + expectedExtension + " path.",
                    "atlasAssetPath");
            }

            if (!IsAssetPath(normalizedTextureFolderPath) ||
                !AssetDatabase.IsValidFolder(normalizedTextureFolderPath))
            {
                throw new ArgumentException("Texture folder must be an existing Assets-relative folder.", "textureFolderAssetPath");
            }

            UnityEngine.Object textureFolder =
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(normalizedTextureFolderPath);
            return version == PsdImporter.SpriteAtlasVersion.V2
                ? CreateOrUpdateV2(normalizedAtlasPath, normalizedTextureFolderPath, textureFolder)
                : CreateOrUpdateV1(normalizedAtlasPath, normalizedTextureFolderPath, textureFolder);
        }

        private static SpriteAtlas CreateOrUpdateV1(
            string atlasAssetPath,
            string textureFolderAssetPath,
            UnityEngine.Object textureFolder)
        {
            UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(atlasAssetPath);
            SpriteAtlas atlas = existingAsset as SpriteAtlas;
            if (existingAsset != null && atlas == null)
            {
                throw new InvalidOperationException("A non-SpriteAtlas V1 asset already exists at: " + atlasAssetPath);
            }

            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasAssetPath);
                ApplyCanvasPackingDefaults(atlas);
            }

            if (!ContainsPackable(atlas, textureFolderAssetPath))
            {
                SpriteAtlasExtensions.Add(atlas, new[] { textureFolder });
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssets();
            }

            return atlas;
        }

        private static SpriteAtlas CreateOrUpdateV2(
            string atlasAssetPath,
            string textureFolderAssetPath,
            UnityEngine.Object textureFolder)
        {
            SpriteAtlasAsset atlasAsset = SpriteAtlasAsset.Load(atlasAssetPath);
            UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(atlasAssetPath);
            if (existingAsset != null && atlasAsset == null)
            {
                throw new InvalidOperationException("A non-SpriteAtlas V2 asset already exists at: " + atlasAssetPath);
            }

            bool shouldSave = false;
            bool created = false;
            if (atlasAsset == null)
            {
                atlasAsset = new SpriteAtlasAsset();
                shouldSave = true;
                created = true;
            }

            SpriteAtlas atlas = existingAsset as SpriteAtlas;
            if (!ContainsPackable(atlas, textureFolderAssetPath))
            {
                atlasAsset.Add(new[] { textureFolder });
                shouldSave = true;
            }

            if (shouldSave)
            {
                SpriteAtlasAsset.Save(atlasAsset, atlasAssetPath);
                AssetDatabase.ImportAsset(
                    atlasAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }

            SpriteAtlas resultAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasAssetPath);
            if (created && resultAtlas != null)
            {
                ApplyCanvasPackingDefaults(resultAtlas);
                EditorUtility.SetDirty(resultAtlas);
                AssetDatabase.SaveAssets();
            }

            return resultAtlas;
        }

        private static void ApplyCanvasPackingDefaults(SpriteAtlas atlas)
        {
            // This atlas is generated for the imported Canvas. Rotation and tight packing
            // can invalidate the expected UI sprite geometry.
            SpriteAtlasPackingSettings packingSettings = atlas.GetPackingSettings();
            packingSettings.enableRotation = false;
            packingSettings.enableTightPacking = false;
            atlas.SetPackingSettings(packingSettings);
        }

        private static bool ContainsPackable(SpriteAtlas atlas, string assetPath)
        {
            if (atlas == null)
            {
                return false;
            }

            UnityEngine.Object[] packables = atlas.GetPackables();
            for (int i = 0; i < packables.Length; i++)
            {
                if (string.Equals(
                        AssetDatabase.GetAssetPath(packables[i]),
                        assetPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAssetPath(string path)
        {
            return path.Equals("Assets", StringComparison.Ordinal) ||
                   path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
        }
    }
}
