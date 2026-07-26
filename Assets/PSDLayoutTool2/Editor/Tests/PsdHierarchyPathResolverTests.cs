namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.U2D;
    using UnityEngine;
    using UnityEngine.U2D;

    public sealed class PsdHierarchyPathResolverTests
    {
        private const string PsdAssetPath = "Assets/PSDLayoutTool2/TestData/7日任务拆分.psd";

        [Test]
        public void LegacySiblingModeResolvesFixedPrefabFolder()
        {
            string resolvedPath;

            bool resolved = PsdGeneratedPrefabPathResolver.TryResolve(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                out resolvedPath);

            Assert.That(resolved, Is.True);
            Assert.That(
                resolvedPath,
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab/7日任务拆分.prefab"));

            bool exists = PsdImporter.TryResolveGeneratedPrefabPath(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                out resolvedPath);

            Assert.That(exists, Is.False);
            Assert.That(
                resolvedPath,
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab/7日任务拆分.prefab"));
        }

        [Test]
        public void InsideFolderModeResolvesOnlyConfiguredInsidePrefab()
        {
            string resolvedPath;

            bool resolved = PsdGeneratedPrefabPathResolver.TryResolve(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                out resolvedPath);

            Assert.That(resolved, Is.True);
            Assert.That(
                resolvedPath,
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab/7日任务拆分.prefab"));

            bool exists = PsdImporter.TryResolveGeneratedPrefabPath(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                out resolvedPath);

            Assert.That(exists, Is.False);
            Assert.That(
                resolvedPath,
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab/7日任务拆分.prefab"));
        }

        [Test]
        public void OutputRootResolvesAtlasTextureAndPrefabFolders()
        {
            string atlasPath;
            string texturePath;
            string prefabPath;

            bool resolved = PsdGeneratedPrefabPathResolver.TryResolveContentFolders(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                out atlasPath,
                out texturePath,
                out prefabPath);

            Assert.That(resolved, Is.True);
            Assert.That(atlasPath, Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/Atlas"));
            Assert.That(texturePath, Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/Texture"));
            Assert.That(prefabPath, Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/Prefab"));
        }

        [Test]
        public void MissingConfiguredTargetDoesNotFallBackToExistingSameNamePrefab()
        {
            string resolvedPath;

            bool exists = PsdImporter.TryResolveGeneratedPrefabPath(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                "不存在的输出目录",
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                out resolvedPath);

            Assert.That(exists, Is.False);
            Assert.That(
                resolvedPath,
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/不存在的输出目录/Prefab/7日任务拆分.prefab"));
        }

        [TestCase("Assets/../../Outside/Test.psd")]
        [TestCase("Assets/Folder/../Test.psd")]
        [TestCase("Assets/./Test.psd")]
        [TestCase("AssetsSibling/Test.psd")]
        public void TraversalAndNonAssetBoundaryPathsAreRejected(string psdAssetPath)
        {
            string resolvedPath;

            bool resolved = PsdGeneratedPrefabPathResolver.TryResolve(
                psdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                out resolvedPath);

            Assert.That(resolved, Is.False);
            Assert.That(resolvedPath, Is.Empty);
        }

        [Test]
        public void LegalChineseAndSpaceSegmentsRemainUnchanged()
        {
            string resolvedPath;

            bool resolved = PsdGeneratedPrefabPathResolver.TryResolve(
                "Assets/UI 空格/中文 文件.psd",
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                out resolvedPath);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedPath, Is.EqualTo("Assets/UI 空格/中文 文件/Prefab/中文 文件.prefab"));
        }
    }

    public sealed class PsdGeneratedSpriteAtlasTests
    {
        private const string TestsFolderPath = "Assets/PSDLayoutTool2/Editor/Tests";
        private const string RootPath = TestsFolderPath + "/GeneratedSpriteAtlasTemp";
        private const string AtlasFolderPath = RootPath + "/Atlas";
        private const string TextureFolderPath = RootPath + "/Texture";
        private const string AtlasV1AssetPath = AtlasFolderPath + "/GeneratedSpriteAtlas.spriteatlas";
        private const string AtlasV2AssetPath = AtlasFolderPath + "/GeneratedSpriteAtlas.spriteatlasv2";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(RootPath);
            AssetDatabase.CreateFolder(TestsFolderPath, "GeneratedSpriteAtlasTemp");
            AssetDatabase.CreateFolder(RootPath, "Atlas");
            AssetDatabase.CreateFolder(RootPath, "Texture");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(RootPath);
        }

        [Test]
        public void CreateOrUpdateCreatesV1AtlasWithTextureFolderPackable()
        {
            PsdGeneratedSpriteAtlas.CreateOrUpdate(
                AtlasV1AssetPath,
                TextureFolderPath,
                PsdImporter.SpriteAtlasVersion.V1);
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasV1AssetPath);

            Assert.That(atlas, Is.Not.Null);
            Object[] packables = atlas.GetPackables();
            Assert.That(packables, Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(packables[0]), Is.EqualTo(TextureFolderPath));
        }

        [Test]
        public void CreateOrUpdateCreatesV2AtlasWithTextureFolderPackable()
        {
            PsdGeneratedSpriteAtlas.CreateOrUpdate(
                AtlasV2AssetPath,
                TextureFolderPath,
                PsdImporter.SpriteAtlasVersion.V2);
            SpriteAtlasAsset atlasAsset = SpriteAtlasAsset.Load(AtlasV2AssetPath);
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasV2AssetPath);

            Assert.That(atlasAsset, Is.Not.Null);
            Assert.That(atlas, Is.Not.Null);
            Assert.That(atlas.GetPackables(), Has.Length.EqualTo(1));
        }

        [TestCase(PsdImporter.SpriteAtlasVersion.V1, AtlasV1AssetPath)]
        [TestCase(PsdImporter.SpriteAtlasVersion.V2, AtlasV2AssetPath)]
        public void CreateOrUpdateDoesNotDuplicateTextureFolderPackable(
            PsdImporter.SpriteAtlasVersion version,
            string atlasAssetPath)
        {
            PsdGeneratedSpriteAtlas.CreateOrUpdate(atlasAssetPath, TextureFolderPath, version);
            PsdGeneratedSpriteAtlas.CreateOrUpdate(atlasAssetPath, TextureFolderPath, version);
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasAssetPath);

            Assert.That(atlas, Is.Not.Null);
            Assert.That(atlas.GetPackables(), Has.Length.EqualTo(1));
        }
    }
}
