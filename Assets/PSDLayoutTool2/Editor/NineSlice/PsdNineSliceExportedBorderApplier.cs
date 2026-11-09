namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>从原始 PSD 应用九宫边距的结果；引用组件始终保持不变。</summary>
    internal struct PsdNineSliceApplyReport
    {
        internal string PngAssetPath;
        internal PsdNineSliceBorder Border;
        internal uint LayerId;
        internal int SourceWidth;
        internal int SourceHeight;
        internal int Width;
        internal int Height;
        // 保留旧报告字段的内部兼容性，应用流程不再改写组件或 Prefab。
        internal int ImageCount;
        internal int PrefabCount;
        internal int RawImageCount;
        internal string Error;
        internal bool Succeeded { get { return string.IsNullOrEmpty(Error); } }
        internal bool WasCropped { get { return SourceWidth > 0 && (SourceWidth != Width || SourceHeight != Height); } }
        internal string FileName { get { return Path.GetFileName(PngAssetPath ?? string.Empty); } }
    }

    /// <summary>只重建目标 PNG、导入信息和源 PSD 的九宫参数，不修改引用组件。</summary>
    internal static class PsdNineSliceExportedBorderApplier
    {
        internal const string PrefabFolderName = "Prefab";

        internal static PsdNineSliceApplyReport Apply(string pngAssetPath, uint layerId, PsdNineSliceBorder border)
        {
            var importer = AssetImporter.GetAtPath(pngAssetPath) as TextureImporter;
            string sourcePath;
            PsdNineSliceAssetState.TryReadSourcePsdIdentity(importer == null ? null : importer.userData, out sourcePath);
            return Apply(pngAssetPath, sourcePath, layerId, border);
        }

        internal static PsdNineSliceApplyReport Apply(string pngAssetPath, string psdAssetPath,
            uint layerId, PsdNineSliceBorder border)
        {
            var report = new PsdNineSliceApplyReport
            {
                PngAssetPath = pngAssetPath, LayerId = layerId, Border = border,
                ImageCount = 0, PrefabCount = 0, RawImageCount = 0
            };
            try
            {
                TextureImporter importer;
                AssetImporter psdImporter;
                ValidateTarget(pngAssetPath, psdAssetPath, layerId, out importer, out psdImporter);
                PsdNineSliceRaster source;
                string error;
                if (!PsdNineSlicePsdLayerSession.TryReadOriginalRaster(psdAssetPath, layerId, out source, out error))
                    throw new InvalidOperationException(error);
                if (border == null || !border.IsValidFor(source.Width, source.Height))
                    throw new InvalidOperationException("四边必须在原始图层 " + source.Width + "×" + source.Height + " 内保留至少 2 像素中心。");

                report.SourceWidth = source.Width;
                report.SourceHeight = source.Height;
                Persist(ref report, importer, psdImporter, source);
            }
            catch (Exception exception)
            {
                report.Error = exception.Message + " 本次应用未写入 PNG 或 PSD 参数。";
            }

            return report;
        }

        private static void ValidateTarget(string pngPath, string psdPath, uint layerId,
            out TextureImporter importer, out AssetImporter psdImporter)
        {
            if (string.IsNullOrEmpty(pngPath) || !pngPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !pngPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("未解析到工程内的目标 PNG。");
            if (string.IsNullOrEmpty(psdPath) || !psdPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !psdPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("无法确定源 PSD；不能从旧 PNG 恢复原始像素。");

            importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            psdImporter = AssetImporter.GetAtPath(psdPath);
            if (importer == null || psdImporter == null)
                throw new InvalidOperationException("无法读取目标 PNG 或源 PSD 的导入信息。");
            uint recordedId;
            if (layerId == 0U || !PsdNineSliceAssetState.TryReadLayerIdentity(importer.userData, out recordedId) || recordedId != layerId)
                throw new InvalidOperationException("目标 PNG 的 Photoshop 图层 ID 与所选图层不一致或缺失。");
            string recordedSource;
            if (PsdNineSliceAssetState.TryReadSourcePsdIdentity(importer.userData, out recordedSource) &&
                !string.Equals(recordedSource.Replace('\\', '/'), psdPath, StringComparison.Ordinal))
                throw new InvalidOperationException("目标 PNG 记录的源 PSD 与当前 PSD 不一致。");
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                throw new InvalidOperationException("仅支持已有的单 Sprite PNG，以免改变现有子资源引用。");
        }

        private static void Persist(ref PsdNineSliceApplyReport report, TextureImporter importer,
            AssetImporter psdImporter, PsdNineSliceRaster source)
        {
            string pngPath = FullPath(report.PngAssetPath);
            string psdMetaPath = FullPath(psdImporter.assetPath) + ".meta";
            // 常见的只读/占用错误在任何写入前发现，避免先覆盖 PNG 再发现 meta 不可写。
            foreach (string path in new[] { pngPath, pngPath + ".meta", psdMetaPath })
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) { }
            byte[] oldPng = File.ReadAllBytes(pngPath);
            byte[] oldPngMeta = File.ReadAllBytes(pngPath + ".meta");
            byte[] oldPsdMeta = File.ReadAllBytes(psdMetaPath);
            string oldPsdUserData = psdImporter.userData;
            string guid = AssetDatabase.AssetPathToGUID(report.PngAssetPath);
            try
            {
                string error;
                if (!PsdNineSliceTextureProcessor.TryCropAndPersist(report.PngAssetPath, importer,
                        report.LayerId, report.Border, source, out error))
                    throw new IOException(error);
                importer.userData = PsdNineSliceAssetState.WriteSourcePsdIdentity(importer.userData, psdImporter.assetPath);
                importer.spriteBorder = PsdNineSliceTextureProcessor.ToUnityBorder(report.Border);
                importer.SaveAndReimport();
                importer = AssetImporter.GetAtPath(report.PngAssetPath) as TextureImporter;
                int width, height;
                if (importer == null) throw new IOException("PNG 重新导入后无法读取导入器。");
                importer.GetSourceTextureWidthAndHeight(out width, out height);
                Vector4 expected = PsdNineSliceTextureProcessor.ToUnityBorder(report.Border);
                if (AssetDatabase.AssetPathToGUID(report.PngAssetPath) != guid || importer.spriteBorder != expected ||
                    width != (report.Border.Left + report.Border.Right > 0 ? report.Border.Left + report.Border.Right + 2 : source.Width) ||
                    height != (report.Border.Top + report.Border.Bottom > 0 ? report.Border.Top + report.Border.Bottom + 2 : source.Height))
                    throw new IOException("PNG 导入后的 GUID、尺寸或九宫边距与预期不一致。");

                // PNG 成功后才保存 PSD 参数，只写 meta，不重新导入整个 PSD。
                psdImporter.userData = PsdNineSliceOverrideStore.Write(oldPsdUserData, report.LayerId, true, report.Border);
                bool saved = AssetDatabase.WriteImportSettingsIfDirty(psdImporter.assetPath);
                if (!saved && !string.Equals(oldPsdUserData, psdImporter.userData, StringComparison.Ordinal))
                    throw new IOException("PSD 九宫参数未能落盘。");
                report.Width = width;
                report.Height = height;
            }
            catch (Exception exception)
            {
                report.Error = "应用失败：" + exception.Message + Restore(pngPath, psdMetaPath,
                    report.PngAssetPath, psdImporter, oldPng, oldPngMeta, oldPsdMeta, oldPsdUserData);
            }
        }

        private static string Restore(string pngPath, string psdMetaPath, string pngAssetPath,
            AssetImporter psdImporter, byte[] png, byte[] pngMeta, byte[] psdMeta, string psdUserData)
        {
            try
            {
                File.WriteAllBytes(pngPath, png);
                File.WriteAllBytes(pngPath + ".meta", pngMeta);
                psdImporter.userData = psdUserData;
                AssetDatabase.WriteImportSettingsIfDirty(psdImporter.assetPath);
                File.WriteAllBytes(psdMetaPath, psdMeta);
                AssetDatabase.ImportAsset(pngAssetPath, ImportAssetOptions.ForceUpdate);
                return " 已恢复应用前的 PNG、PNG 导入信息和 PSD 参数。";
            }
            catch (Exception rollbackException)
            {
                return " 回滚未完成，可能存在部分写入，请检查 " + pngAssetPath + " 和 " + psdMetaPath + "：" + rollbackException.Message;
            }
        }

        private static string FullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath));
        }

        internal static string GeneratedPrefabFolder(string pngAssetPath)
        {
            string path = (pngAssetPath ?? string.Empty).Replace('\\', '/');
            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder) || !Path.GetFileName(folder).Equals("Texture", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return (Path.GetDirectoryName(folder) + "/" + PrefabFolderName).Replace('\\', '/');
        }
    }
}
