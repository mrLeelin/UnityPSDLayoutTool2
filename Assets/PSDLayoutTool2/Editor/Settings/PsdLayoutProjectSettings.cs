namespace PsdLayoutTool2
{
    using System;
    using TMPro;
    using UnityEditor;
    using UnityEngine;

    internal enum PsdProjectAssetStatus
    {
        Empty,
        Resolved,
        Missing,
    }

    internal readonly struct PsdLayoutProjectFontSnapshot
    {
        internal PsdLayoutProjectFontSnapshot(
            TMP_FontAsset font,
            Material baseMaterial,
            PsdProjectAssetStatus fontStatus,
            PsdProjectAssetStatus materialStatus,
            string fontGuid,
            string materialGuid)
        {
            this.font = font;
            this.baseMaterial = baseMaterial;
            this.fontStatus = fontStatus;
            this.materialStatus = materialStatus;
            this.fontGuid = fontGuid;
            this.materialGuid = materialGuid;
        }

        internal readonly TMP_FontAsset font;
        internal readonly Material baseMaterial;
        internal readonly PsdProjectAssetStatus fontStatus;
        internal readonly PsdProjectAssetStatus materialStatus;
        internal readonly string fontGuid;
        internal readonly string materialGuid;
    }

    /// <summary>
    /// 项目级通用资源命名前缀快照，同时用于 PSD 图层解析和公共资源映射表扫描。
    /// 前缀之后的文本会作为资源键，例如 UI_Prefab_Button_Green 的资源键为 Button_Green。
    /// </summary>
    internal readonly struct PsdCommonAssetNamingSnapshot
    {
        internal PsdCommonAssetNamingSnapshot(string prefabPrefix, string texturePrefix)
        {
            this.prefabPrefix = prefabPrefix;
            this.texturePrefix = texturePrefix;
        }

        internal readonly string prefabPrefix;
        internal readonly string texturePrefix;
    }

    /// <summary>
    /// 项目级资源与 Prefab 输出规则快照。
    /// 输出文件夹名为空时，导入器继续使用当前 PSD 文件名。
    /// </summary>
    internal readonly struct PsdLayoutProjectOutputSnapshot
    {
        internal PsdLayoutProjectOutputSnapshot(
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            PsdImporter.PrefabOutputMode prefabMode,
            PsdImporter.SpriteAtlasVersion spriteAtlasVersion)
        {
            this.outputMode = outputMode;
            this.outputFolderName = outputFolderName ?? string.Empty;
            this.prefabMode = prefabMode;
            this.spriteAtlasVersion = spriteAtlasVersion;
        }

        internal readonly PsdImporter.OutputDirectoryMode outputMode;
        internal readonly string outputFolderName;
        internal readonly PsdImporter.PrefabOutputMode prefabMode;
        internal readonly PsdImporter.SpriteAtlasVersion spriteAtlasVersion;
    }

    internal readonly struct PsdLayoutProjectSettingsMigrationSnapshot
    {
        internal PsdLayoutProjectSettingsMigrationSnapshot(
            string fontGuid,
            string materialGuid,
            string prefabPrefix,
            string texturePrefix)
        {
            this.fontGuid = fontGuid ?? string.Empty;
            this.materialGuid = materialGuid ?? string.Empty;
            this.prefabPrefix = prefabPrefix ?? string.Empty;
            this.texturePrefix = texturePrefix ?? string.Empty;
        }

        internal readonly string fontGuid;
        internal readonly string materialGuid;
        internal readonly string prefabPrefix;
        internal readonly string texturePrefix;

        internal bool HasValues =>
            !string.IsNullOrEmpty(fontGuid) ||
            !string.IsNullOrEmpty(materialGuid) ||
            !string.IsNullOrEmpty(prefabPrefix) ||
            !string.IsNullOrEmpty(texturePrefix);
    }

    [Serializable]
    internal sealed class PsdLayoutProjectCommonAssetNamingSettings
    {
        internal const string DefaultPrefabPrefix = "Common_Prefab_";
        internal const string DefaultTexturePrefix = "Common_Texture_";

        [SerializeField]
        private string prefabPrefix = DefaultPrefabPrefix;

        [SerializeField]
        private string texturePrefix = DefaultTexturePrefix;

        /// <summary>
        /// 保存规范化后的前缀。缺少末尾下划线时自动补充，空值恢复为兼容旧项目的默认值。
        /// </summary>
        internal bool TrySetPrefixes(string newPrefabPrefix, string newTexturePrefix, out string error)
        {
            string normalizedPrefab = NormalizePrefix(newPrefabPrefix, DefaultPrefabPrefix);
            string normalizedTexture = NormalizePrefix(newTexturePrefix, DefaultTexturePrefix);
            if (string.Equals(normalizedPrefab, normalizedTexture, StringComparison.OrdinalIgnoreCase))
            {
                error = "Prefab 前缀和 Texture 前缀不能相同，否则无法确定公共资源类型。";
                return false;
            }

            prefabPrefix = normalizedPrefab;
            texturePrefix = normalizedTexture;
            error = string.Empty;
            return true;
        }

        internal PsdCommonAssetNamingSnapshot Resolve()
        {
            return new PsdCommonAssetNamingSnapshot(
                NormalizePrefix(prefabPrefix, DefaultPrefabPrefix),
                NormalizePrefix(texturePrefix, DefaultTexturePrefix));
        }

        internal void ApplyMigration(string migratedPrefabPrefix, string migratedTexturePrefix)
        {
            string ignored;
            TrySetPrefixes(migratedPrefabPrefix, migratedTexturePrefix, out ignored);
        }

        private static string NormalizePrefix(string value, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return normalized.EndsWith("_", StringComparison.Ordinal) ? normalized : normalized + "_";
        }
    }

    [Serializable]
    internal sealed class PsdLayoutProjectOutputSettings
    {
        [SerializeField]
        private PsdImporter.OutputDirectoryMode outputMode = PsdImporter.OutputDirectoryMode.PsdDirectory;

        [SerializeField]
        private string outputFolderName = string.Empty;

        [SerializeField]
        private PsdImporter.PrefabOutputMode prefabMode = PsdImporter.PrefabOutputMode.SiblingToOutputFolder;

        [SerializeField]
        private PsdImporter.SpriteAtlasVersion spriteAtlasVersion = PsdImporter.SpriteAtlasVersion.V1;

        internal bool Set(
            PsdImporter.OutputDirectoryMode newOutputMode,
            string newOutputFolderName,
            PsdImporter.PrefabOutputMode newPrefabMode,
            PsdImporter.SpriteAtlasVersion newSpriteAtlasVersion)
        {
            string normalizedFolderName = (newOutputFolderName ?? string.Empty).Trim();
            if (outputMode == newOutputMode &&
                outputFolderName == normalizedFolderName &&
                prefabMode == newPrefabMode &&
                spriteAtlasVersion == newSpriteAtlasVersion)
            {
                return false;
            }

            outputMode = newOutputMode;
            outputFolderName = normalizedFolderName;
            prefabMode = newPrefabMode;
            spriteAtlasVersion = newSpriteAtlasVersion;
            return true;
        }

        internal PsdLayoutProjectOutputSnapshot Resolve()
        {
            return new PsdLayoutProjectOutputSnapshot(
                outputMode,
                outputFolderName,
                prefabMode,
                spriteAtlasVersion);
        }
    }

    [Serializable]
    internal sealed class PsdLayoutProjectFontSettings
    {
        [SerializeField]
        private string textMeshProFontGuid = string.Empty;

        [SerializeField]
        private string textMeshProBaseMaterialGuid = string.Empty;

        internal bool SetAssets(TMP_FontAsset font, Material baseMaterial)
        {
            string newFontGuid = GetAssetGuid(font);
            string newMaterialGuid = GetAssetGuid(baseMaterial);
            if (textMeshProFontGuid == newFontGuid &&
                textMeshProBaseMaterialGuid == newMaterialGuid)
            {
                return false;
            }

            textMeshProFontGuid = newFontGuid;
            textMeshProBaseMaterialGuid = newMaterialGuid;
            return true;
        }

        internal PsdLayoutProjectFontSnapshot Resolve()
        {
            TMP_FontAsset font = ResolveAsset<TMP_FontAsset>(
                textMeshProFontGuid,
                out PsdProjectAssetStatus fontStatus);
            Material material = ResolveAsset<Material>(
                textMeshProBaseMaterialGuid,
                out PsdProjectAssetStatus materialStatus);
            return new PsdLayoutProjectFontSnapshot(
                font,
                material,
                fontStatus,
                materialStatus,
                textMeshProFontGuid,
                textMeshProBaseMaterialGuid);
        }

        internal void ApplyMigration(string fontGuid, string materialGuid)
        {
            textMeshProFontGuid = fontGuid ?? string.Empty;
            textMeshProBaseMaterialGuid = materialGuid ?? string.Empty;
        }

        internal void GetGuids(out string fontGuid, out string materialGuid)
        {
            fontGuid = textMeshProFontGuid;
            materialGuid = textMeshProBaseMaterialGuid;
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static T ResolveAsset<T>(string guid, out PsdProjectAssetStatus status)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid))
            {
                status = PsdProjectAssetStatus.Empty;
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            status = asset == null ? PsdProjectAssetStatus.Missing : PsdProjectAssetStatus.Resolved;
            return asset;
        }
    }

    /// <summary>
    /// 由使用方项目持有的 PSD Layout Tool 配置。
    /// UPM 包内只提供只读模板，读取或编辑前由 PsdLayoutProjectSettingsAsset 复制到 Assets 目录。
    /// </summary>
    internal sealed class PsdLayoutProjectSettings : ScriptableObject
    {
        private const int CurrentSettingsVersion = 4;

        [SerializeField]
        private int settingsVersion;

        [SerializeField]
        private PsdLayoutProjectFontSettings fontSettings = new PsdLayoutProjectFontSettings();

        [SerializeField]
        private PsdLayoutProjectCommonAssetNamingSettings commonAssetNamingSettings =
            new PsdLayoutProjectCommonAssetNamingSettings();

        [SerializeField]
        private PsdLayoutProjectOutputSettings outputSettings = new PsdLayoutProjectOutputSettings();

        [SerializeField]
        private PsdHierarchyAiSettings hierarchyAiSettings = new PsdHierarchyAiSettings();

        internal static PsdLayoutProjectSettings instance => PsdLayoutProjectSettingsAsset.GetOrCreate();

        internal PsdLayoutProjectFontSnapshot ResolveFontSettings()
        {
            EnsureData();
            return fontSettings.Resolve();
        }

        internal void SetFontSettings(TMP_FontAsset font, Material baseMaterial)
        {
            EnsureData();
            if (fontSettings.SetAssets(font, baseMaterial))
            {
                SaveAsset();
            }
        }

        internal PsdCommonAssetNamingSnapshot ResolveCommonAssetNaming()
        {
            EnsureData();
            return commonAssetNamingSettings.Resolve();
        }

        internal PsdLayoutProjectOutputSnapshot ResolveOutputSettings()
        {
            EnsureData();
            return outputSettings.Resolve();
        }

        internal PsdHierarchyAiSettingsSnapshot ResolveHierarchyAiSettings()
        {
            EnsureData();
            return hierarchyAiSettings.Resolve();
        }

        internal void SetHierarchyAiSettings(
            PsdHierarchyAiProvider provider,
            PsdHierarchyAiConnectionMode connectionMode,
            string customEndpoint,
            string customModel)
        {
            EnsureData();
            if (hierarchyAiSettings.Set(provider, connectionMode, customEndpoint, customModel))
            {
                SaveAsset();
            }
        }

        internal void SetOutputSettings(
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            PsdImporter.PrefabOutputMode prefabMode,
            PsdImporter.SpriteAtlasVersion spriteAtlasVersion)
        {
            EnsureData();
            if (outputSettings.Set(outputMode, outputFolderName, prefabMode, spriteAtlasVersion))
            {
                SaveAsset();
            }
        }

        internal bool TrySetCommonAssetPrefixes(string prefabPrefix, string texturePrefix, out string error)
        {
            EnsureData();
            PsdCommonAssetNamingSnapshot before = commonAssetNamingSettings.Resolve();
            if (!commonAssetNamingSettings.TrySetPrefixes(prefabPrefix, texturePrefix, out error))
            {
                return false;
            }

            PsdCommonAssetNamingSnapshot after = commonAssetNamingSettings.Resolve();
            if (!string.Equals(before.prefabPrefix, after.prefabPrefix, StringComparison.Ordinal) ||
                !string.Equals(before.texturePrefix, after.texturePrefix, StringComparison.Ordinal))
            {
                SaveAsset();
                PsdCommonAssetCatalog.MarkNeedsRefresh();
            }

            return true;
        }

        internal PsdLayoutProjectSettingsMigrationSnapshot CreateMigrationSnapshot()
        {
            EnsureData();
            fontSettings.GetGuids(out string fontGuid, out string materialGuid);
            PsdCommonAssetNamingSnapshot naming = commonAssetNamingSettings.Resolve();
            return new PsdLayoutProjectSettingsMigrationSnapshot(
                fontGuid,
                materialGuid,
                naming.prefabPrefix,
                naming.texturePrefix);
        }

        internal void ApplyMigration(PsdLayoutProjectSettingsMigrationSnapshot migration)
        {
            if (!migration.HasValues)
            {
                return;
            }

            EnsureData();
            fontSettings.ApplyMigration(migration.fontGuid, migration.materialGuid);
            commonAssetNamingSettings.ApplyMigration(migration.prefabPrefix, migration.texturePrefix);
            SaveAsset();
        }

        private void SaveAsset()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void EnsureData()
        {
            bool changed = false;
            if (settingsVersion < CurrentSettingsVersion)
            {
                settingsVersion = CurrentSettingsVersion;
                changed = true;
            }

            if (fontSettings == null)
            {
                fontSettings = new PsdLayoutProjectFontSettings();
                changed = true;
            }

            if (commonAssetNamingSettings == null)
            {
                commonAssetNamingSettings = new PsdLayoutProjectCommonAssetNamingSettings();
                changed = true;
            }

            if (outputSettings == null)
            {
                outputSettings = new PsdLayoutProjectOutputSettings();
                changed = true;
            }

            if (hierarchyAiSettings == null)
            {
                hierarchyAiSettings = new PsdHierarchyAiSettings();
                changed = true;
            }

            // 旧版项目配置缺少新增数据块时，只补默认数据并保存一次，不覆盖已有配置。
            if (changed && AssetDatabase.Contains(this))
            {
                SaveAsset();
            }
        }
    }
}
