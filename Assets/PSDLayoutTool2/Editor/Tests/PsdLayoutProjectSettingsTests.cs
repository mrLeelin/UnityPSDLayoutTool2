namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;
    using TMPro;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdLayoutProjectSettingsTests
    {
        private const string TempFolder = "Assets/__PsdLayoutTool2ProjectSettingsTests";
        private const string FontPath = TempFolder + "/ProjectFont.asset";
        private const string MaterialPath = TempFolder + "/ProjectFontMaterial.mat";
        private const string TemplatePath = TempFolder + "/Template.asset";
        private const string ProjectCopyPath = TempFolder + "/Project/PsdLayoutProjectSettings.asset";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.CreateFolder("Assets", "__PsdLayoutTool2ProjectSettingsTests");
        }

        [TearDown]
        public void TearDown()
        {
            PsdImporter.TextMeshProFont = null;
            PsdImporter.TextMeshProBaseMaterial = null;
            PsdImporter.AtlasVersion = PsdImporter.SpriteAtlasVersion.V1;
            AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void ResolveSnapshotUsesGuidReferencedFontAndMaterial()
        {
            TMP_FontAsset font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            AssetDatabase.CreateAsset(font, FontPath);

            Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);

            var data = new PsdLayoutProjectFontSettings();
            data.SetAssets(font, material);

            PsdLayoutProjectFontSnapshot snapshot = data.Resolve();

            Assert.That(snapshot.font, Is.SameAs(font));
            Assert.That(snapshot.baseMaterial, Is.SameAs(material));
            Assert.That(snapshot.fontStatus, Is.EqualTo(PsdProjectAssetStatus.Resolved));
            Assert.That(snapshot.materialStatus, Is.EqualTo(PsdProjectAssetStatus.Resolved));
        }

        [Test]
        public void EmptyAndMissingGuidsHaveDistinctStatuses()
        {
            var data = new PsdLayoutProjectFontSettings();

            PsdLayoutProjectFontSnapshot empty = data.Resolve();

            Assert.That(empty.font, Is.Null);
            Assert.That(empty.baseMaterial, Is.Null);
            Assert.That(empty.fontStatus, Is.EqualTo(PsdProjectAssetStatus.Empty));
            Assert.That(empty.materialStatus, Is.EqualTo(PsdProjectAssetStatus.Empty));

            JsonUtility.FromJsonOverwrite(
                "{\"textMeshProFontGuid\":\"missing-font\",\"textMeshProBaseMaterialGuid\":\"missing-material\"}",
                data);

            PsdLayoutProjectFontSnapshot missing = data.Resolve();

            Assert.That(missing.font, Is.Null);
            Assert.That(missing.baseMaterial, Is.Null);
            Assert.That(missing.fontStatus, Is.EqualTo(PsdProjectAssetStatus.Missing));
            Assert.That(missing.materialStatus, Is.EqualTo(PsdProjectAssetStatus.Missing));
        }

        [Test]
        public void CommonAssetNamingUsesBackwardCompatibleDefaults()
        {
            var data = new PsdLayoutProjectCommonAssetNamingSettings();

            PsdCommonAssetNamingSnapshot snapshot = data.Resolve();

            Assert.That(snapshot.prefabPrefix, Is.EqualTo("Common_Prefab_"));
            Assert.That(snapshot.texturePrefix, Is.EqualTo("Common_Texture_"));
        }

        [Test]
        public void CommonAssetNamingNormalizesSuffixAndBlankValues()
        {
            var data = new PsdLayoutProjectCommonAssetNamingSettings();

            Assert.That(data.TrySetPrefixes("UI_Prefab", "", out string error), Is.True, error);
            PsdCommonAssetNamingSnapshot snapshot = data.Resolve();

            Assert.That(snapshot.prefabPrefix, Is.EqualTo("UI_Prefab_"));
            Assert.That(snapshot.texturePrefix, Is.EqualTo("Common_Texture_"));
        }

        [Test]
        public void CommonAssetNamingRejectsAmbiguousDuplicatePrefixes()
        {
            var data = new PsdLayoutProjectCommonAssetNamingSettings();

            Assert.That(data.TrySetPrefixes("Shared", "Shared_", out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(data.Resolve().prefabPrefix, Is.EqualTo("Common_Prefab_"));
            Assert.That(data.Resolve().texturePrefix, Is.EqualTo("Common_Texture_"));
        }

        [Test]
        public void 输出配置使用兼容旧行为的默认值()
        {
            var data = new PsdLayoutProjectOutputSettings();

            PsdLayoutProjectOutputSnapshot snapshot = data.Resolve();

            Assert.That(snapshot.outputMode, Is.EqualTo(PsdImporter.OutputDirectoryMode.PsdDirectory));
            Assert.That(snapshot.outputFolderName, Is.Empty);
            Assert.That(snapshot.prefabMode, Is.EqualTo(PsdImporter.PrefabOutputMode.SiblingToOutputFolder));
            Assert.That(snapshot.spriteAtlasVersion, Is.EqualTo(PsdImporter.SpriteAtlasVersion.V1));
        }

        [Test]
        public void 输出配置保存项目级输出规则()
        {
            var data = new PsdLayoutProjectOutputSettings();

            data.Set(
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                "UI_Activity",
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                PsdImporter.SpriteAtlasVersion.V2);
            PsdLayoutProjectOutputSnapshot snapshot = data.Resolve();

            Assert.That(snapshot.outputMode, Is.EqualTo(PsdImporter.OutputDirectoryMode.PsdDirectory));
            Assert.That(snapshot.outputFolderName, Is.EqualTo("UI_Activity"));
            Assert.That(snapshot.prefabMode, Is.EqualTo(PsdImporter.PrefabOutputMode.InsideOutputFolder));
            Assert.That(snapshot.spriteAtlasVersion, Is.EqualTo(PsdImporter.SpriteAtlasVersion.V2));
        }

        [Test]
        public void 导入器应用项目级输出配置()
        {
            var snapshot = new PsdLayoutProjectOutputSnapshot(
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                "UI_Global",
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                PsdImporter.SpriteAtlasVersion.V2);

            PsdImporter.ApplyProjectOutputSettings(snapshot);

            Assert.That(PsdImporter.OutputMode, Is.EqualTo(PsdImporter.OutputDirectoryMode.PsdDirectory));
            Assert.That(PsdImporter.OutputFolderName, Is.EqualTo("UI_Global"));
            Assert.That(PsdImporter.PrefabMode, Is.EqualTo(PsdImporter.PrefabOutputMode.InsideOutputFolder));
            Assert.That(PsdImporter.AtlasVersion, Is.EqualTo(PsdImporter.SpriteAtlasVersion.V2));
        }

        [Test]
        public void FirstUseCopiesPackageTemplateIntoProjectAssets()
        {
            PsdLayoutProjectSettings template = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            Assert.That(template.TrySetCommonAssetPrefixes("Package_Prefab", "Package_Texture", out string error), Is.True, error);
            AssetDatabase.CreateAsset(template, TemplatePath);

            PsdLayoutProjectSettings result = PsdLayoutProjectSettingsAsset.EnsureAtPath(
                ProjectCopyPath,
                TemplatePath,
                default(PsdLayoutProjectSettingsMigrationSnapshot));

            Assert.That(result, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(result), Is.EqualTo(ProjectCopyPath));
            Assert.That(result.ResolveCommonAssetNaming().prefabPrefix, Is.EqualTo("Package_Prefab_"));
        }

        [Test]
        public void ExistingProjectSettingsAreNeverOverwrittenByPackageTemplate()
        {
            PsdLayoutProjectSettings template = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            Assert.That(template.TrySetCommonAssetPrefixes("Package_Prefab", "Package_Texture", out string error), Is.True, error);
            AssetDatabase.CreateAsset(template, TemplatePath);

            PsdLayoutProjectSettings existing = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            Assert.That(existing.TrySetCommonAssetPrefixes("Game_Prefab", "Game_Texture", out error), Is.True, error);
            EnsureAssetFolder(ProjectCopyPath);
            AssetDatabase.CreateAsset(existing, ProjectCopyPath);

            PsdLayoutProjectSettings result = PsdLayoutProjectSettingsAsset.EnsureAtPath(
                ProjectCopyPath,
                TemplatePath,
                default(PsdLayoutProjectSettingsMigrationSnapshot));

            Assert.That(result.ResolveCommonAssetNaming().prefabPrefix, Is.EqualTo("Game_Prefab_"));
            Assert.That(result.ResolveCommonAssetNaming().texturePrefix, Is.EqualTo("Game_Texture_"));
        }

        [Test]
        public void LegacyValuesOverrideTemplateOnlyDuringFirstProjectCopy()
        {
            PsdLayoutProjectSettings template = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            AssetDatabase.CreateAsset(template, TemplatePath);
            var migration = new PsdLayoutProjectSettingsMigrationSnapshot(
                string.Empty,
                string.Empty,
                "Legacy_Prefab_",
                "Legacy_Texture_");

            PsdLayoutProjectSettings result = PsdLayoutProjectSettingsAsset.EnsureAtPath(
                ProjectCopyPath,
                TemplatePath,
                migration);

            Assert.That(result.ResolveCommonAssetNaming().prefabPrefix, Is.EqualTo("Legacy_Prefab_"));
            Assert.That(result.ResolveCommonAssetNaming().texturePrefix, Is.EqualTo("Legacy_Texture_"));
        }

        [Test]
        public void MaterialWithoutSelectedFontAtlasIsIncompatible()
        {
            TMP_FontAsset font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            AssetDatabase.CreateAsset(font, FontPath);
            Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);

            Assert.That(PsdPrefabTextMaterialFactory.IsCompatibleWithFont(material, font), Is.False);
        }

        [Test]
        public void ImporterAppliesResolvedProjectFontSnapshot()
        {
            TMP_FontAsset font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            AssetDatabase.CreateAsset(font, FontPath);

            Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);

            var data = new PsdLayoutProjectFontSettings();
            data.SetAssets(font, material);

            PsdImporter.ApplyProjectFontSettings(data.Resolve());

            Assert.That(PsdImporter.TextMeshProFont, Is.SameAs(font));
            Assert.That(PsdImporter.TextMeshProBaseMaterial, Is.SameAs(material));
        }

        [Test]
        public void ImporterClearsMissingProjectFontReferences()
        {
            var data = new PsdLayoutProjectFontSettings();
            JsonUtility.FromJsonOverwrite(
                "{\"textMeshProFontGuid\":\"missing-font\",\"textMeshProBaseMaterialGuid\":\"missing-material\"}",
                data);

            TMP_FontAsset existingFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            AssetDatabase.CreateAsset(existingFont, FontPath);
            Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            Material existingMaterial = new Material(shader);
            AssetDatabase.CreateAsset(existingMaterial, MaterialPath);
            PsdImporter.TextMeshProFont = existingFont;
            PsdImporter.TextMeshProBaseMaterial = existingMaterial;

            PsdImporter.ApplyProjectFontSettings(data.Resolve());

            Assert.That(PsdImporter.TextMeshProFont, Is.Null);
            Assert.That(PsdImporter.TextMeshProBaseMaterial, Is.Null);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent + "/placeholder.asset");
            }

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
