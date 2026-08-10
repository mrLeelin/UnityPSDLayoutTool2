namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    public sealed class PsdHierarchyPlanWorkspaceStoreTests
    {
        private string temporaryProjectRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryProjectRoot = Path.Combine(
                Path.GetTempPath(),
                "PsdHierarchyPlanWorkspaceStoreTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryProjectRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryProjectRoot))
            {
                Directory.Delete(temporaryProjectRoot, true);
            }
        }

        [Test]
        public void TrySaveWritesUtf8JsonBelowLibraryOnly()
        {
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace("snapshot-a");

            PsdHierarchyPlanWorkspaceStoreResult result =
                PsdHierarchyPlanWorkspaceStore.TrySave(temporaryProjectRoot, workspace);

            string expectedRoot = Path.Combine(
                temporaryProjectRoot,
                "Library",
                "PSDLayoutTool2",
                "PlanWorkspaces");
            Assert.That(result.success, Is.True, result.error);
            Assert.That(Path.GetFullPath(result.path), Does.StartWith(Path.GetFullPath(expectedRoot)));
            Assert.That(File.Exists(result.path), Is.True);
            StringAssert.Contains(
                "variant sources must remain direct siblings",
                File.ReadAllText(result.path));
            byte[] bytes = File.ReadAllBytes(result.path);
            Assert.That(bytes.Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        }

        [Test]
        public void TrySaveNeverOverwritesAnEarlierDiagnostic()
        {
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace("snapshot-a");

            PsdHierarchyPlanWorkspaceStoreResult first =
                PsdHierarchyPlanWorkspaceStore.TrySave(temporaryProjectRoot, workspace);
            PsdHierarchyPlanWorkspaceStoreResult second =
                PsdHierarchyPlanWorkspaceStore.TrySave(temporaryProjectRoot, workspace);

            Assert.That(first.success, Is.True, first.error);
            Assert.That(second.success, Is.True, second.error);
            Assert.That(second.path, Is.Not.EqualTo(first.path));
            Assert.That(File.Exists(first.path), Is.True);
            Assert.That(File.Exists(second.path), Is.True);
        }

        [Test]
        public void TrySaveSanitizesTheSnapshotDirectory()
        {
            PsdHierarchyPlanWorkspace workspace = CreateBlockedWorkspace("snapshot:bad/../name");

            PsdHierarchyPlanWorkspaceStoreResult result =
                PsdHierarchyPlanWorkspaceStore.TrySave(temporaryProjectRoot, workspace);

            Assert.That(result.success, Is.True, result.error);
            Assert.That(Path.GetDirectoryName(result.path), Does.Not.Contain(".."));
            Assert.That(Path.GetFullPath(result.path), Does.StartWith(Path.GetFullPath(temporaryProjectRoot)));
        }

        [Test]
        public void TrySaveReturnsAnErrorInsteadOfThrowing()
        {
            PsdHierarchyPlanWorkspaceStoreResult result = default;

            Assert.DoesNotThrow(() => result = PsdHierarchyPlanWorkspaceStore.TrySave(
                "\0",
                CreateBlockedWorkspace("snapshot-a")));

            Assert.That(result.success, Is.False);
            Assert.That(result.path, Is.Empty);
            Assert.That(result.error, Is.Not.Empty);
        }

        private static PsdHierarchyPlanWorkspace CreateBlockedWorkspace(string fingerprint)
        {
            return PsdHierarchyPlanWorkspace.CreateBlockedPlan(
                fingerprint,
                "review",
                "raw reply",
                PsdHierarchyPlanIssueCategory.PlanPreparation,
                "计划包含相互冲突或无法确定的结构操作。",
                "variant sources must remain direct siblings",
                "保持原结构，或重新分析此项。",
                new[] { "family_002" },
                new[] { "n000059", "n000089" });
        }
    }
}
