namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    public sealed class PsdHierarchyPathResolverTests
    {
        private const string PsdAssetPath = "Assets/PSDLayoutTool2/TestData/7日任务拆分.psd";

        [Test]
        public void SiblingModeResolvesOnlyConfiguredSiblingPrefab()
        {
            string resolvedPath;

            bool resolved = PsdGeneratedPrefabPathResolver.TryResolve(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                out resolvedPath);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedPath, Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分.prefab"));

            bool exists = PsdImporter.TryResolveGeneratedPrefabPath(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                out resolvedPath);

            Assert.That(exists, Is.True);
            Assert.That(resolvedPath, Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分.prefab"));
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
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/7日任务拆分.prefab"));

            bool exists = PsdImporter.TryResolveGeneratedPrefabPath(
                PsdAssetPath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                out resolvedPath);

            Assert.That(exists, Is.True);
            Assert.That(
                resolvedPath,
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/7日任务拆分/7日任务拆分.prefab"));
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
                Is.EqualTo("Assets/PSDLayoutTool2/TestData/不存在的输出目录/7日任务拆分.prefab"));
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
            Assert.That(resolvedPath, Is.EqualTo("Assets/UI 空格/中文 文件/中文 文件.prefab"));
        }
    }
}
