namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebSessionTests
    {
        [Test]
        public void GetOrCreate_SamePsdGuid_ReusesSessionAndReplacesPreview()
        {
            string root = CreateRoot();
            try
            {
                using (var registry = new PsdHierarchyWebSessionRegistry(root, () => UtcNow))
                {
                    PsdHierarchyOrganizerPreviewModel firstPreview = CreatePreviewModel("guid-a");
                    PsdHierarchyOrganizerPreviewModel updatedPreview = CreatePreviewModel("guid-a");
                    PsdHierarchyWebSession first = registry.GetOrCreate(
                        "guid-a", "Assets/A.psd", firstPreview);
                    PsdHierarchyWebSession second = registry.GetOrCreate(
                        "guid-a", "Assets/A.psd", updatedPreview);

                    Assert.That(second, Is.SameAs(first));
                    Assert.That(second.sessionId, Is.EqualTo(first.sessionId));
                    Assert.That(second.token, Is.EqualTo(first.token));
                    Assert.That(second.directory, Is.EqualTo(first.directory));
                    Assert.That(second.previewModel, Is.SameAs(updatedPreview));
                }
            }
            finally { Delete(root); }
        }

        [Test]
        public void GetOrCreate_DifferentPsdGuids_UsesDifferentSecretsAndDirectories()
        {
            string root = CreateRoot();
            try
            {
                using (var registry = new PsdHierarchyWebSessionRegistry(root, () => UtcNow))
                {
                    PsdHierarchyWebSession first = registry.GetOrCreate("guid-a", "Assets/A.psd", null);
                    PsdHierarchyWebSession second = registry.GetOrCreate("guid-b", "Assets/B.psd", null);

                    Assert.That(first.sessionId, Is.Not.EqualTo(second.sessionId));
                    Assert.That(first.token, Is.Not.EqualTo(second.token));
                    Assert.That(first.directory, Is.Not.EqualTo(second.directory));
                    StringAssert.IsMatch("^[0-9a-f]+$", first.sessionId);
                    StringAssert.IsMatch("^[0-9a-f]+$", first.token);
                }
            }
            finally { Delete(root); }
        }

        [Test]
        public void OperationLifecycle_AllowsOnlyCurrentOperationAndReturnsCopies()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"), null);
            try
            {
                CancellationToken first = session.Start(PsdHierarchyWebOperationKind.Analyze, "working");
                Assert.Throws<InvalidOperationException>(() =>
                    session.Start(PsdHierarchyWebOperationKind.Refine, "second"));
                session.Complete("done");
                PsdHierarchyWebSessionSnapshot snapshot = session.Snapshot();
                snapshot.operation.message = "tampered";

                Assert.That(first.IsCancellationRequested, Is.False);
                Assert.That(session.Snapshot().operation.message, Is.EqualTo("done"));
                Assert.That(session.Snapshot().operation.status,
                    Is.EqualTo(PsdHierarchyWebOperationStatus.Succeeded));
            }
            finally { session.Dispose(); Delete(Path.GetDirectoryName(session.directory)); }
        }

        [Test]
        public void Dispose_CancelsActiveOperation()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"), null);
            CancellationToken token = session.Start(PsdHierarchyWebOperationKind.Analyze, "working");

            session.Dispose();

            Assert.That(token.IsCancellationRequested, Is.True);
            Delete(Path.GetDirectoryName(session.directory));
        }

        [Test]
        public void CleanupStaleDirectories_DeletesOnlyRecognizedOldChildrenInsideRoot()
        {
            string root = CreateRoot();
            string outside = CreateRoot();
            string old = Path.Combine(root, "0123456789abcdef");
            string current = Path.Combine(root, "fedcba9876543210");
            string unknown = Path.Combine(root, "not-a-session");
            Directory.CreateDirectory(old);
            Directory.CreateDirectory(current);
            Directory.CreateDirectory(unknown);
            File.SetLastWriteTimeUtc(old, UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(current, UtcNow.AddDays(-6));
            try
            {
                using (var registry = new PsdHierarchyWebSessionRegistry(root, () => UtcNow))
                {
                    registry.CleanupStaleDirectories();
                }

                Assert.That(Directory.Exists(old), Is.False);
                Assert.That(Directory.Exists(current), Is.True);
                Assert.That(Directory.Exists(unknown), Is.True);
                Assert.That(Directory.Exists(outside), Is.True);
            }
            finally { Delete(root); Delete(outside); }
        }

        [Test]
        public void CleanupStaleDirectories_RecoversAfterOneChildDeletionFails()
        {
            string root = CreateRoot();
            string failed = Path.Combine(root, "0123456789abcdef");
            string successful = Path.Combine(root, "abcdef0123456789");
            Directory.CreateDirectory(failed);
            Directory.CreateDirectory(successful);
            File.SetLastWriteTimeUtc(failed, UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(successful, UtcNow.AddDays(-8));
            try
            {
                using (var registry = new PsdHierarchyWebSessionRegistry(
                    root, () => UtcNow, path =>
                    {
                        if (string.Equals(path, failed, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("simulated");
                        Directory.Delete(path, true);
                    }))
                {
                    registry.CleanupStaleDirectories();
                }

                Assert.That(Directory.Exists(failed), Is.True);
                Assert.That(Directory.Exists(successful), Is.False);
            }
            finally { Delete(root); }
        }

        [Test]
        public void Constructor_RejectsFilesystemRootAsSessionRoot()
        {
            string filesystemRoot = Path.GetPathRoot(Path.GetTempPath());

            Assert.Throws<IOException>(() =>
                new PsdHierarchyWebSessionRegistry(filesystemRoot, () => UtcNow));
        }

        private static readonly DateTime UtcNow = new DateTime(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc);

        private static string CreateRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "PsdHierarchyWebSessionTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void Delete(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static PsdHierarchyOrganizerPreviewModel CreatePreviewModel(string guid)
        {
            return new PsdHierarchyOrganizerPreviewModel(
                "Assets/Generated/Test.prefab",
                new PsdHierarchyRequest { sourcePsdGuid = guid },
                new PsdHierarchyPlan { sourcePsdGuid = guid },
                new PsdHierarchyReconciliationResult(),
                new NeverRunAiRunner());
        }

        private sealed class NeverRunAiRunner : IPsdHierarchyAiRunner
        {
            public Task<PsdHierarchyAiRunResult> RunAsync(
                PsdHierarchyAiRunRequest request,
                CancellationToken cancellationToken)
            {
                throw new AssertionException("Session tests must not invoke the AI runner.");
            }
        }
    }
}
