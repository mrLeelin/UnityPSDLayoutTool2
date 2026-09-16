namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// The 9-slice border currently written into one exported PNG.
    /// Raw values stay in Unity's Sprite order (left, bottom, right, top) because
    /// that is what <see cref="TextureImporter.spriteBorder"/> stores.
    /// </summary>
    internal sealed class PsdNineSliceExportedBorder
    {
        internal PsdNineSliceExportedBorder(string assetPath, Vector4 unityBorder)
        {
            AssetPath = assetPath;
            UnityBorder = unityBorder;
        }

        internal string AssetPath { get; private set; }

        internal Vector4 UnityBorder { get; private set; }

        /// <summary>True when the exported sprite already carries a usable border.</summary>
        internal bool IsNineSlice
        {
            get
            {
                return PsdNineSliceImportPolicy.HasSpriteBorder(
                    UnityBorder.x, UnityBorder.y, UnityBorder.z, UnityBorder.w);
            }
        }

        internal string FileName
        {
            get
            {
                if (string.IsNullOrEmpty(AssetPath))
                {
                    return string.Empty;
                }

                int separator = AssetPath.LastIndexOf('/');
                return separator < 0 ? AssetPath : AssetPath.Substring(separator + 1);
            }
        }

        /// <summary>
        /// Converts to the author-facing left, top, right, bottom order the PSD
        /// editor and the override store use.
        /// </summary>
        internal PsdNineSliceBorder ToAuthorBorder()
        {
            return new PsdNineSliceBorder(
                Mathf.RoundToInt(UnityBorder.x),
                Mathf.RoundToInt(UnityBorder.w),
                Mathf.RoundToInt(UnityBorder.z),
                Mathf.RoundToInt(UnityBorder.y));
        }

        /// <summary>Compact author-order summary, for example "L104 T104 R104 B104".</summary>
        internal string Describe()
        {
            PsdNineSliceBorder border = ToAuthorBorder();
            return "L" + border.Left + " T" + border.Top + " R" + border.Right + " B" + border.Bottom;
        }
    }

    /// <summary>
    /// Maps PSD layer ids to the 9-slice border already written into their exported
    /// PNGs. The PSD editor uses this so a layer that is already nine-sliced no longer
    /// looks unconfigured just because it has no manual override.
    ///
    /// The PNG side is the source of truth: the export pipeline records the layer id
    /// in each generated PNG's <c>TextureImporter.userData</c>.
    /// </summary>
    internal static class PsdNineSliceExportedBorderLookup
    {
        internal static Dictionary<uint, PsdNineSliceExportedBorder> Build(string psdAssetPath)
        {
            var results = new Dictionary<uint, PsdNineSliceExportedBorder>();
            if (string.IsNullOrEmpty(psdAssetPath))
            {
                return results;
            }

            foreach (string folder in EnumerateTextureFolders(psdAssetPath))
            {
                CollectFromFolder(folder, results);
            }

            return results;
        }

        /// <summary>
        /// Texture folders that can hold this PSD's generated PNGs: the configured
        /// output path first, then the export-name convention.
        /// </summary>
        internal static IEnumerable<string> EnumerateTextureFolders(string psdAssetPath)
        {
            var folders = new List<string>(2);
            if (string.IsNullOrEmpty(psdAssetPath))
            {
                return folders;
            }

            PsdLayoutProjectOutputSnapshot snapshot = PsdLayoutProjectSettings.instance.ResolveOutputSettings();
            string configuredFolder;
            if (PsdGeneratedPrefabPathResolver.TryResolveContentFolders(
                    psdAssetPath,
                    snapshot.outputMode,
                    snapshot.outputFolderName,
                    snapshot.fixedOutputPath,
                    snapshot.atlasOutputPath,
                    snapshot.textureOutputPath,
                    snapshot.prefabOutputPath,
                    out string atlasFolder,
                    out configuredFolder,
                    out string prefabFolder) &&
                !string.IsNullOrEmpty(configuredFolder))
            {
                folders.Add(configuredFolder);
            }

            string conventionFolder = ConventionTextureFolder(psdAssetPath);
            if (!string.IsNullOrEmpty(conventionFolder) && !folders.Contains(conventionFolder))
            {
                folders.Add(conventionFolder);
            }

            return folders;
        }

        /// <summary>
        /// "&lt;psdDir&gt;/&lt;psdName&gt;.psd" -&gt; "&lt;psdDir&gt;/&lt;psdName&gt;/Texture".
        /// </summary>
        internal static string ConventionTextureFolder(string psdAssetPath)
        {
            string path = (psdAssetPath ?? string.Empty).Replace('\\', '/');
            if (!path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return path.Substring(0, path.Length - ".psd".Length) + "/Texture";
        }

        private static void CollectFromFolder(string folder, Dictionary<uint, PsdNineSliceExportedBorder> results)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                uint layerId;
                if (!PsdNineSliceAssetState.TryReadLayerIdentity(importer.userData, out layerId))
                {
                    continue;
                }

                // A later export wins: results are filled folder by folder.
                results[layerId] = new PsdNineSliceExportedBorder(path, importer.spriteBorder);
            }
        }
    }
}
