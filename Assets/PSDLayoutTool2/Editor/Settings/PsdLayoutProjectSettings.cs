namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using TMPro;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

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

    [Serializable]
    internal sealed class PsdCommonAssetPreviewSettings
    {
        internal const int DefaultPort = 52342;
        [SerializeField] private int port = DefaultPort;
        internal int ResolvePort() => port >= 1 && port <= 65535 ? port : DefaultPort;
        internal bool TrySetPort(int value, out string error)
        {
            if (value < 1 || value > 65535) { error = "端口必须在 1 到 65535 之间。"; return false; }
            error = string.Empty;
            if (port == value) return false;
            port = value;
            return true;
        }
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
            : this(
                outputMode,
                outputFolderName,
                string.Empty,
                prefabMode,
                string.Empty,
                string.Empty,
                string.Empty,
                spriteAtlasVersion)
        {
        }

        internal PsdLayoutProjectOutputSnapshot(
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            string fixedOutputPath,
            PsdImporter.PrefabOutputMode prefabMode,
            string atlasOutputPath,
            string textureOutputPath,
            string prefabOutputPath,
            PsdImporter.SpriteAtlasVersion spriteAtlasVersion)
        {
            this.outputMode = outputMode;
            this.outputFolderName = outputFolderName ?? string.Empty;
            this.fixedOutputPath = fixedOutputPath ?? string.Empty;
            this.prefabMode = prefabMode;
            this.atlasOutputPath = atlasOutputPath ?? string.Empty;
            this.textureOutputPath = textureOutputPath ?? string.Empty;
            this.prefabOutputPath = prefabOutputPath ?? string.Empty;
            this.spriteAtlasVersion = spriteAtlasVersion;
        }

        internal readonly PsdImporter.OutputDirectoryMode outputMode;
        internal readonly string outputFolderName;
        internal readonly string fixedOutputPath;
        internal readonly PsdImporter.PrefabOutputMode prefabMode;
        internal readonly string atlasOutputPath;
        internal readonly string textureOutputPath;
        internal readonly string prefabOutputPath;
        internal readonly PsdImporter.SpriteAtlasVersion spriteAtlasVersion;
    }

    /// <summary>
    /// Resolved UI component types used while generating PSD UI nodes.
    /// </summary>
    internal readonly struct PsdLayoutProjectUiComponentSnapshot
    {
        internal PsdLayoutProjectUiComponentSnapshot(Type imageComponentType, Type buttonComponentType)
        {
            this.imageComponentType = imageComponentType ?? typeof(Image);
            this.buttonComponentType = buttonComponentType ?? typeof(Button);
        }

        internal readonly Type imageComponentType;
        internal readonly Type buttonComponentType;
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
    internal sealed class PsdLayoutProjectUiComponentSettings
    {
        internal const string DefaultImageComponentTypeName = "UnityEngine.UI.Image";
        internal const string DefaultButtonComponentTypeName = "UnityEngine.UI.Button";

        private static readonly Dictionary<Type, List<Type>> ComponentTypesByBaseType =
            new Dictionary<Type, List<Type>>();

        [SerializeField]
        private string imageComponentTypeName = DefaultImageComponentTypeName;

        [SerializeField]
        private string buttonComponentTypeName = DefaultButtonComponentTypeName;

        internal PsdLayoutProjectUiComponentSnapshot Resolve()
        {
            if (!ResolveComponentType(imageComponentTypeName, typeof(Image), out Type imageComponentType, out _))
            {
                imageComponentType = typeof(Image);
            }

            if (!ResolveComponentType(buttonComponentTypeName, typeof(Button), out Type buttonComponentType, out _))
            {
                buttonComponentType = typeof(Button);
            }

            return new PsdLayoutProjectUiComponentSnapshot(imageComponentType, buttonComponentType);
        }

        internal bool TrySetComponentTypes(string imageTypeName, string buttonTypeName, out string error)
        {
            if (!ResolveComponentType(imageTypeName, typeof(Image), out Type imageType, out error))
            {
                return false;
            }

            if (!ResolveComponentType(buttonTypeName, typeof(Button), out Type buttonType, out error))
            {
                return false;
            }

            string normalizedImageTypeName = imageType.FullName;
            string normalizedButtonTypeName = buttonType.FullName;
            error = string.Empty;
            if (string.Equals(imageComponentTypeName, normalizedImageTypeName, StringComparison.Ordinal) &&
                string.Equals(buttonComponentTypeName, normalizedButtonTypeName, StringComparison.Ordinal))
            {
                return false;
            }

            imageComponentTypeName = normalizedImageTypeName;
            buttonComponentTypeName = normalizedButtonTypeName;
            return true;
        }

        internal static List<string> FindComponentTypeNames(Type expectedBaseType, string query, int maxResults)
        {
            var matches = new List<Type>();
            foreach (Type candidate in GetComponentTypes(expectedBaseType))
            {
                if (GetFuzzyMatchScore(candidate, query) >= 0)
                {
                    matches.Add(candidate);
                }
            }

            matches.Sort((left, right) =>
            {
                int leftScore = GetFuzzyMatchScore(left, query);
                int rightScore = GetFuzzyMatchScore(right, query);
                int scoreComparison = leftScore.CompareTo(rightScore);
                return scoreComparison != 0
                    ? scoreComparison
                    : string.Compare(left.FullName, right.FullName, StringComparison.Ordinal);
            });

            int count = Math.Min(Math.Max(maxResults, 0), matches.Count);
            var results = new List<string>(count);
            for (int index = 0; index < count; index++)
            {
                results.Add(matches[index].FullName);
            }

            return results;
        }

        private static List<Type> GetComponentTypes(Type expectedBaseType)
        {
            if (ComponentTypesByBaseType.TryGetValue(expectedBaseType, out List<Type> cached))
            {
                return cached;
            }

            var types = new HashSet<Type> { expectedBaseType };
            foreach (Type candidate in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (!candidate.IsAbstract && expectedBaseType.IsAssignableFrom(candidate))
                {
                    types.Add(candidate);
                }
            }

            var results = new List<Type>(types);
            ComponentTypesByBaseType[expectedBaseType] = results;
            return results;
        }

        private static int GetFuzzyMatchScore(Type candidate, string query)
        {
            string normalizedQuery = query == null ? string.Empty : query.Trim();
            int simpleNameScore = GetFuzzyMatchScore(candidate.Name, normalizedQuery);
            int fullNameScore = GetFuzzyMatchScore(candidate.FullName, normalizedQuery);
            if (simpleNameScore < 0)
            {
                return fullNameScore;
            }

            return fullNameScore < 0 ? simpleNameScore : Math.Min(simpleNameScore, fullNameScore);
        }

        private static int GetFuzzyMatchScore(string candidate, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return 0;
            }

            int substringIndex = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (substringIndex >= 0)
            {
                return substringIndex * 2;
            }

            int candidateIndex = 0;
            int score = 0;
            for (int queryIndex = 0; queryIndex < query.Length; queryIndex++)
            {
                char queryCharacter = char.ToUpperInvariant(query[queryIndex]);
                int matchIndex = -1;
                for (; candidateIndex < candidate.Length; candidateIndex++)
                {
                    if (char.ToUpperInvariant(candidate[candidateIndex]) == queryCharacter)
                    {
                        matchIndex = candidateIndex++;
                        break;
                    }
                }

                if (matchIndex < 0)
                {
                    return -1;
                }

                score += matchIndex;
            }

            return 100 + score;
        }

        private static bool ResolveComponentType(string typeName, Type expectedBaseType, out Type resolvedType, out string error)
        {
            string requestedName = string.IsNullOrWhiteSpace(typeName)
                ? expectedBaseType.FullName
                : typeName.Trim();
            resolvedType = string.Equals(requestedName, expectedBaseType.FullName, StringComparison.Ordinal) ||
                           string.Equals(requestedName, expectedBaseType.AssemblyQualifiedName, StringComparison.Ordinal)
                ? expectedBaseType
                : Type.GetType(requestedName, false);

            if (resolvedType == null)
            {
                foreach (Type candidate in TypeCache.GetTypesDerivedFrom<Component>())
                {
                    if (string.Equals(candidate.FullName, requestedName, StringComparison.Ordinal) ||
                        string.Equals(candidate.Name, requestedName, StringComparison.Ordinal))
                    {
                        if (resolvedType != null)
                        {
                            resolvedType = null;
                            error = "组件类名不唯一，请填写完整命名空间。";
                            return false;
                        }

                        resolvedType = candidate;
                    }
                }
            }

            if (resolvedType == null)
            {
                error = "找不到组件类型：" + requestedName;
                return false;
            }

            if (resolvedType.IsAbstract || !typeof(Component).IsAssignableFrom(resolvedType))
            {
                error = "组件类型必须是可挂载的 Unity Component：" + requestedName;
                return false;
            }

            if (!expectedBaseType.IsAssignableFrom(resolvedType))
            {
                error = requestedName + " 必须继承 " + expectedBaseType.FullName + "。";
                return false;
            }

            error = string.Empty;
            return true;
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
        private string fixedOutputPath = string.Empty;

        [SerializeField]
        private PsdImporter.PrefabOutputMode prefabMode = PsdImporter.PrefabOutputMode.SiblingToOutputFolder;

        [SerializeField]
        private string atlasOutputPath = string.Empty;

        [SerializeField]
        private string textureOutputPath = string.Empty;

        [SerializeField]
        private string prefabOutputPath = string.Empty;

        [SerializeField]
        private PsdImporter.SpriteAtlasVersion spriteAtlasVersion = PsdImporter.SpriteAtlasVersion.V1;

        internal bool Set(
            PsdImporter.OutputDirectoryMode newOutputMode,
            string newOutputFolderName,
            PsdImporter.PrefabOutputMode newPrefabMode,
            PsdImporter.SpriteAtlasVersion newSpriteAtlasVersion)
        {
            return Set(
                newOutputMode,
                newOutputFolderName,
                string.Empty,
                newPrefabMode,
                string.Empty,
                string.Empty,
                string.Empty,
                newSpriteAtlasVersion);
        }

        internal bool Set(
            PsdImporter.OutputDirectoryMode newOutputMode,
            string newOutputFolderName,
            string newFixedOutputPath,
            PsdImporter.PrefabOutputMode newPrefabMode,
            string newAtlasOutputPath,
            string newTextureOutputPath,
            string newPrefabOutputPath,
            PsdImporter.SpriteAtlasVersion newSpriteAtlasVersion)
        {
            string normalizedFolderName = (newOutputFolderName ?? string.Empty).Trim();
            string normalizedAtlasPath = (newAtlasOutputPath ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            string normalizedTexturePath = (newTextureOutputPath ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            string normalizedPrefabPath = (newPrefabOutputPath ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            if (outputMode == newOutputMode &&
                outputFolderName == normalizedFolderName &&
                prefabMode == newPrefabMode &&
                atlasOutputPath == normalizedAtlasPath &&
                textureOutputPath == normalizedTexturePath &&
                prefabOutputPath == normalizedPrefabPath &&
                spriteAtlasVersion == newSpriteAtlasVersion)
            {
                return false;
            }

            outputMode = newOutputMode;
            outputFolderName = normalizedFolderName;
            fixedOutputPath = string.Empty;
            prefabMode = newPrefabMode;
            atlasOutputPath = normalizedAtlasPath;
            textureOutputPath = normalizedTexturePath;
            prefabOutputPath = normalizedPrefabPath;
            spriteAtlasVersion = newSpriteAtlasVersion;
            return true;
        }

        internal PsdLayoutProjectOutputSnapshot Resolve()
        {
            return new PsdLayoutProjectOutputSnapshot(
                outputMode,
                outputFolderName,
                string.Empty,
                prefabMode,
                atlasOutputPath,
                textureOutputPath,
                prefabOutputPath,
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
        private const int CurrentSettingsVersion = 7;

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
        private PsdLayoutProjectUiComponentSettings uiComponentSettings = new PsdLayoutProjectUiComponentSettings();

        [SerializeField]
        private PsdHierarchyAiSettings hierarchyAiSettings = new PsdHierarchyAiSettings();

        [SerializeField]
        private PsdHierarchyCleanupExecutionSettings hierarchyCleanupExecutionSettings =
            new PsdHierarchyCleanupExecutionSettings();

        [SerializeField]
        private PsdCommonAssetPreviewSettings previewServerSettings = new PsdCommonAssetPreviewSettings();

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

        internal PsdLayoutProjectUiComponentSnapshot ResolveUiComponentSettings()
        {
            EnsureData();
            return uiComponentSettings.Resolve();
        }

        internal bool TrySetUiComponentTypes(string imageTypeName, string buttonTypeName, out string error)
        {
            EnsureData();
            if (!uiComponentSettings.TrySetComponentTypes(imageTypeName, buttonTypeName, out error))
            {
                return false;
            }

            SaveAsset();
            return true;
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

        internal PsdHierarchyCleanupExecutionSettingsSnapshot ResolveHierarchyCleanupExecutionSettings()
        {
            EnsureData();
            return hierarchyCleanupExecutionSettings.Resolve();
        }

        internal void SetHierarchyCleanupExecutionBackend(PsdHierarchyCleanupExecutionBackend backend)
        {
            EnsureData();
            if (hierarchyCleanupExecutionSettings.Set(backend))
            {
                SaveAsset();
            }
        }

        internal int ResolvePreviewServerPort() { EnsureData(); return previewServerSettings.ResolvePort(); }

        internal bool TrySetPreviewServerPort(int port, out string error)
        {
            EnsureData();
            bool changed = previewServerSettings.TrySetPort(port, out error);
            if (changed) SaveAsset();
            return changed;
        }

        internal void SetOutputSettings(
            PsdImporter.OutputDirectoryMode outputMode,
            string outputFolderName,
            string fixedOutputPath,
            PsdImporter.PrefabOutputMode prefabMode,
            string atlasOutputPath,
            string textureOutputPath,
            string prefabOutputPath,
            PsdImporter.SpriteAtlasVersion spriteAtlasVersion)
        {
            EnsureData();
            if (outputSettings.Set(
                    outputMode,
                    outputFolderName,
                    fixedOutputPath,
                    prefabMode,
                    atlasOutputPath,
                    textureOutputPath,
                    prefabOutputPath,
                    spriteAtlasVersion))
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

            if (uiComponentSettings == null)
            {
                uiComponentSettings = new PsdLayoutProjectUiComponentSettings();
                changed = true;
            }

            if (hierarchyAiSettings == null)
            {
                hierarchyAiSettings = new PsdHierarchyAiSettings();
                changed = true;
            }

            if (hierarchyCleanupExecutionSettings == null)
            {
                hierarchyCleanupExecutionSettings = new PsdHierarchyCleanupExecutionSettings();
                changed = true;
            }

            if (previewServerSettings == null)
            {
                previewServerSettings = new PsdCommonAssetPreviewSettings();
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
