namespace PsdLayoutTool2
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Result of pushing one edited border onto an exported PNG.
    /// </summary>
    internal struct PsdNineSliceApplyReport
    {
        internal string PngAssetPath;
        internal PsdNineSliceBorder Border;
        internal uint LayerId;

        /// <summary>Texture size before the crop.</summary>
        internal int SourceWidth;
        internal int SourceHeight;

        /// <summary>Texture size after the crop.</summary>
        internal int Width;
        internal int Height;

        /// <summary>Images switched from Simple to Sliced.</summary>
        internal int ImageCount;

        /// <summary>Prefab assets saved because one of their Images changed.</summary>
        internal int PrefabCount;

        /// <summary>RawImage components that also use this texture and cannot be sliced.</summary>
        internal int RawImageCount;

        internal string Error;

        internal bool Succeeded
        {
            get { return string.IsNullOrEmpty(Error); }
        }

        internal bool WasCropped
        {
            get { return SourceWidth > 0 && (SourceWidth != Width || SourceHeight != Height); }
        }

        internal string FileName
        {
            get
            {
                int separator = PngAssetPath == null ? -1 : PngAssetPath.LastIndexOf('/');
                return separator < 0 ? (PngAssetPath ?? string.Empty) : PngAssetPath.Substring(separator + 1);
            }
        }
    }

    /// <summary>
    /// Cuts an edited 9-slice border into the exported PNG straight away, so the
    /// generated UI reflects the manual edit without re-importing the whole PSD.
    ///
    /// The PNG is cropped down to the minimum source Unity's Sliced renderer needs
    /// (protected edges plus a two-pixel stretch sample), the same way the importer's
    /// own auto-crop does, and every Image already using this texture is switched to
    /// Sliced - in the loaded scenes and in the Prefab folder of the PSD export folder.
    /// </summary>
    internal static class PsdNineSliceExportedBorderApplier
    {
        internal const string PrefabFolderName = "Prefab";
        private const string UndoLabel = "Apply 9-slice border to exported PNG";

        internal static PsdNineSliceApplyReport Apply(string pngAssetPath, uint layerId, PsdNineSliceBorder border)
        {
            var report = new PsdNineSliceApplyReport();
            report.PngAssetPath = pngAssetPath ?? string.Empty;
            report.LayerId = layerId;

            if (string.IsNullOrEmpty(pngAssetPath) || border == null)
            {
                report.Error = "No exported PNG is resolved for this layer, so there is nothing to update.";
                return report;
            }

            if (layerId == 0U)
            {
                report.Error = "This PSD layer has no stable Photoshop id, so the crop recipe cannot be stored on the PNG.";
                return report;
            }

            var importer = AssetImporter.GetAtPath(pngAssetPath) as TextureImporter;
            if (importer == null)
            {
                report.Error = "The generated asset is not a texture: " + pngAssetPath;
                return report;
            }

            int width;
            int height;
            importer.GetSourceTextureWidthAndHeight(out width, out height);
            if (!border.IsValidFor(width, height))
            {
                report.Error = "The four borders must leave at least a two-pixel center in " + width + "x" + height +
                    ". Nothing was written.";
                return report;
            }

            report.SourceWidth = width;
            report.SourceHeight = height;
            report.Border = border;

            // 1) Cut the texture down to the minimal stretchable 9-slice source. The
            // recipe is stored on the PNG so the same crop can be recreated later.
            string cropError;
            if (!PsdNineSliceTextureProcessor.TryCropAndPersist(pngAssetPath, importer, layerId, border, out cropError))
            {
                report.Error = cropError;
                return report;
            }

            // 2) Publish the border for the cropped pixels. Unity stores it as
            // left, bottom, right, top.
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
            }

            importer.spriteBorder = PsdNineSliceTextureProcessor.ToUnityBorder(border);
            importer.SaveAndReimport();

            int croppedWidth;
            int croppedHeight;
            importer.GetSourceTextureWidthAndHeight(out croppedWidth, out croppedHeight);
            report.Width = croppedWidth;
            report.Height = croppedHeight;

            // 3) A border alone is invisible while the Image is Simple.
            int savedPrefabs;
            int rawImages;
            report.ImageCount = ApplyToReferencingComponents(pngAssetPath, out savedPrefabs, out rawImages);
            report.PrefabCount = savedPrefabs;
            report.RawImageCount = rawImages;
            return report;
        }

        /// <summary>
        /// The Prefab folder that belongs to an exported PNG:
        /// "&lt;psdDir&gt;/&lt;psdName&gt;/Texture/x.png" -&gt; "&lt;psdDir&gt;/&lt;psdName&gt;/Prefab".
        /// </summary>
        internal static string GeneratedPrefabFolder(string pngAssetPath)
        {
            string path = (pngAssetPath ?? string.Empty).Replace('\\', '/');
            string textureFolder = ParentFolder(path);
            if (!LeafName(textureFolder).Equals("Texture", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string root = ParentFolder(textureFolder);
            return string.IsNullOrEmpty(root) ? string.Empty : root + "/" + PrefabFolderName;
        }

        /// <summary>
        /// A border on its own changes nothing visible while the Image is Simple, so the
        /// nodes that already use this texture are switched to Sliced in the same action.
        /// RawImage nodes cannot be sliced and are only counted, so the artist is warned
        /// that the crop resizes their texture too.
        /// </summary>
        private static int ApplyToReferencingComponents(string pngAssetPath, out int savedPrefabs, out int rawImageCount)
        {
            savedPrefabs = 0;
            rawImageCount = 0;
            string targetGuid = AssetDatabase.AssetPathToGUID(pngAssetPath);
            if (string.IsNullOrEmpty(targetGuid))
            {
                return 0;
            }

            int changed = 0;

            // Loaded scenes, including prefab instances.
            Image[] loadedImages = UnityEngine.Object.FindObjectsByType<Image>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < loadedImages.Length; index++)
            {
                Image image = loadedImages[index];
                if (image == null || image.type == Image.Type.Sliced || !ReferencesAsset(image.sprite, targetGuid))
                {
                    continue;
                }

                Undo.RecordObject(image, UndoLabel);
                image.type = Image.Type.Sliced;
                EditorUtility.SetDirty(image);
                changed++;
            }

            RawImage[] loadedRawImages = UnityEngine.Object.FindObjectsByType<RawImage>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < loadedRawImages.Length; index++)
            {
                if (loadedRawImages[index] != null && ReferencesAsset(loadedRawImages[index].texture, targetGuid))
                {
                    rawImageCount++;
                }
            }

            // The generated Prefab folder sitting next to the exported textures.
            string prefabFolder = GeneratedPrefabFolder(pngAssetPath);
            if (!string.IsNullOrEmpty(prefabFolder) && AssetDatabase.IsValidFolder(prefabFolder))
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
                for (int index = 0; index < guids.Length; index++)
                {
                    int rawInPrefab;
                    int inPrefab = ApplyToPrefab(AssetDatabase.GUIDToAssetPath(guids[index]), targetGuid, out rawInPrefab);
                    rawImageCount += rawInPrefab;
                    if (inPrefab > 0)
                    {
                        changed += inPrefab;
                        savedPrefabs++;
                    }
                }
            }

            return changed;
        }

        private static int ApplyToPrefab(string prefabPath, string targetGuid, out int rawImageCount)
        {
            rawImageCount = 0;
            if (string.IsNullOrEmpty(prefabPath))
            {
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                return 0;
            }

            int changed = 0;
            try
            {
                Image[] images = root.GetComponentsInChildren<Image>(true);
                for (int index = 0; index < images.Length; index++)
                {
                    Image image = images[index];
                    if (image == null || image.type == Image.Type.Sliced || !ReferencesAsset(image.sprite, targetGuid))
                    {
                        continue;
                    }

                    image.type = Image.Type.Sliced;
                    changed++;
                }

                RawImage[] rawImages = root.GetComponentsInChildren<RawImage>(true);
                for (int index = 0; index < rawImages.Length; index++)
                {
                    if (rawImages[index] != null && ReferencesAsset(rawImages[index].texture, targetGuid))
                    {
                        rawImageCount++;
                    }
                }

                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }

        private static bool ReferencesAsset(UnityEngine.Object asset, string targetGuid)
        {
            if (asset == null)
            {
                return false;
            }

            string guid;
            long localId;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId) &&
                string.Equals(guid, targetGuid, StringComparison.Ordinal);
        }

        private static string ParentFolder(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            return separator <= 0 ? string.Empty : assetPath.Substring(0, separator);
        }

        private static string LeafName(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            return separator < 0 ? assetPath : assetPath.Substring(separator + 1);
        }
    }
}
