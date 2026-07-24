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
        public async Task GetOrCreate_SamePsdGuid_ReusesSessionAndReplacesPreview()
        {
            string root = CreateRoot();
            try
            {
                using (var registry = new PsdHierarchyWebSessionRegistry(root, () => UtcNow))
                {
                    PsdHierarchyOrganizerPreviewModel firstPreview = CreatePreviewModel("guid-a");
                    PsdHierarchyOrganizerPreviewModel updatedPreview = CreatePreviewModel("guid-a");
                    PsdHierarchyWebSession first = await registry.GetOrCreateAsync(
                        "guid-a", "Assets/A.psd", firstPreview);
                    PsdHierarchyWebSession second = await registry.GetOrCreateAsync(
                        "guid-a", "Assets/A.psd", updatedPreview);

                    Assert.That(second, Is.SameAs(first));
                    Assert.That(second.sessionId, Is.EqualTo(first.sessionId));
                    Assert.That(second.token, Is.EqualTo(first.token));
                    Assert.That(second.directory, Is.EqualTo(first.directory));
                    await second.UsePreviewAsync(model =>
                    {
                        Assert.That(model, Is.SameAs(updatedPreview));
                        return Task.CompletedTask;
                    });
                }
            }
            finally { Delete(root); }
        }

        [Test]
        public async Task GetOrCreate_DifferentPsdGuids_UsesDifferentSecretsAndDirectories()
        {
            string root = CreateRoot();
            try
            {
                using (var registry = new PsdHierarchyWebSessionRegistry(root, () => UtcNow))
                {
                    PsdHierarchyWebSession first = await registry.GetOrCreateAsync("guid-a", "Assets/A.psd", null);
                    PsdHierarchyWebSession second = await registry.GetOrCreateAsync("guid-b", "Assets/B.psd", null);

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
            PsdHierarchyWebOperationLease first = null;
            try
            {
                first = session.Start(
                    PsdHierarchyWebOperationKind.Analyze, "working");
                Assert.Throws<InvalidOperationException>(() =>
                    session.Start(PsdHierarchyWebOperationKind.Refine, "second"));
                session.Complete(first, "done");
                PsdHierarchyWebSessionSnapshot snapshot = session.Snapshot();
                snapshot.operation.message = "tampered";

                Assert.That(first.token.IsCancellationRequested, Is.False);
                Assert.That(session.Snapshot().operation.message, Is.EqualTo("done"));
                Assert.That(session.Snapshot().operation.status,
                    Is.EqualTo(PsdHierarchyWebOperationStatus.Succeeded));
            }
            finally { first?.Dispose(); session.Dispose(); Delete(Path.GetDirectoryName(session.directory)); }
        }

        [Test]
        public void Dispose_CancelsActiveOperation()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"), null);
            PsdHierarchyWebOperationLease lease = session.Start(
                PsdHierarchyWebOperationKind.Analyze, "working");

            session.Dispose();

            Assert.That(lease.token.IsCancellationRequested, Is.True);
            lease.Dispose();
            Delete(Path.GetDirectoryName(session.directory));
        }

        [Test]
        public void CancelledLease_RemainsUsableUntilReleased_WithoutAffectingNextOperation()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"), null);
            PsdHierarchyWebOperationLease first = null;
            PsdHierarchyWebOperationLease second = null;
            try
            {
                first = session.Start(PsdHierarchyWebOperationKind.Analyze, "A");
                session.Cancel(first);
                second = session.Start(PsdHierarchyWebOperationKind.Refine, "B");

                Assert.DoesNotThrow(() =>
                {
                    using (first.token.Register(() => { })) { }
                    WaitHandle waitHandle = first.token.WaitHandle;
                    Assert.That(waitHandle.WaitOne(0), Is.True);
                    using (CancellationTokenSource.CreateLinkedTokenSource(first.token)) { }
                });

                first.Dispose();

                Assert.That(second.token.IsCancellationRequested, Is.False);
                Assert.That(session.Snapshot().operation.operationId, Is.EqualTo(second.operationId));
                Assert.That(session.Snapshot().operation.status, Is.EqualTo(PsdHierarchyWebOperationStatus.Running));
            }
            finally
            {
                first?.Dispose();
                second?.Dispose();
                session.Dispose();
                Delete(Path.GetDirectoryName(session.directory));
            }
        }

        [Test]
        public void Dispose_SignalsLeaseWithoutInvalidatingItsTokenUntilRelease()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"), null);
            PsdHierarchyWebOperationLease lease = session.Start(
                PsdHierarchyWebOperationKind.Analyze, "working");

            session.Dispose();

            Assert.DoesNotThrow(() =>
            {
                using (lease.token.Register(() => { })) { }
                WaitHandle waitHandle = lease.token.WaitHandle;
                Assert.That(waitHandle.WaitOne(0), Is.True);
                using (CancellationTokenSource.CreateLinkedTokenSource(lease.token)) { }
            });
            lease.Dispose();
            Delete(Path.GetDirectoryName(session.directory));
        }

        [Test]
        public void OperationLifecycle_LateTerminalCallbacksAfterCancel_DoNotAffectNextOperation()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"), null);
            PsdHierarchyWebOperationLease first = null;
            PsdHierarchyWebOperationLease second = null;
            try
            {
                first = session.Start(
                    PsdHierarchyWebOperationKind.Analyze, "A");
                session.Cancel(first);
                second = session.Start(
                    PsdHierarchyWebOperationKind.Refine, "B");

                session.Complete(first, "late complete");
                session.Fail(first, "late failure");
                session.Cancel(first, "late cancellation");

                PsdHierarchyWebOperationState operation = session.Snapshot().operation;
                Assert.That(operation.operationId, Is.EqualTo(second.operationId));
                Assert.That(operation.status, Is.EqualTo(PsdHierarchyWebOperationStatus.Running));
                Assert.That(operation.message, Is.EqualTo("B"));
            }
            finally
            {
                first?.Dispose();
                second?.Dispose();
                session.Dispose();
                Delete(Path.GetDirectoryName(session.directory));
            }
        }

        [Test]
        public void OperationLifecycle_LateTerminalCallbacksAfterDispose_DoNotThrow()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"), null);
            PsdHierarchyWebOperationLease lease = session.Start(
                PsdHierarchyWebOperationKind.Analyze, "working");

            session.Dispose();

            Assert.DoesNotThrow(() => session.Complete(lease, "late complete"));
            Assert.DoesNotThrow(() => session.Fail(lease, "late failure"));
            Assert.DoesNotThrow(() => session.Cancel(lease, "late cancellation"));
            lease.Dispose();
            Delete(Path.GetDirectoryName(session.directory));
        }

        [Test]
        public async Task UsePreviewAsync_SerializesAccessAndReplacement()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"),
                CreatePreviewModel("first"));
            try
            {
                PsdHierarchyOrganizerPreviewModel replacement = CreatePreviewModel("replacement");
                using (var entered = new ManualResetEventSlim())
                {
                    var release = new TaskCompletionSource<bool>();
                    Task access = session.UsePreviewAsync(async model =>
                    {
                        Assert.That(model.requestSnapshot.sourcePsdGuid, Is.EqualTo("first"));
                        entered.Set();
                        await release.Task;
                    });
                    Task replace = null;
                    try
                    {
                        Assert.That(entered.Wait(TimeSpan.FromSeconds(2)), Is.True);

                        replace = session.ReplacePreviewAsync(replacement);
                        Assert.That(replace.IsCompleted, Is.False);
                    }
                    finally
                    {
                        release.TrySetResult(true);
                        if (replace == null) await access;
                        else await Task.WhenAll(access, replace);
                    }
                }

                await session.UsePreviewAsync(model =>
                {
                    Assert.That(model, Is.SameAs(replacement));
                    return Task.CompletedTask;
                });
            }
            finally { session.Dispose(); Delete(Path.GetDirectoryName(session.directory)); }
        }

        [Test]
        public void UsePreviewAsync_DiscardsResultWhenSessionDisposes()
        {
            var session = new PsdHierarchyWebSession(
                "session", "token", "guid", "Assets/A.psd", Path.Combine(CreateRoot(), "session"),
                CreatePreviewModel("first"));
            try
            {
                using (var entered = new ManualResetEventSlim())
                {
                    var release = new TaskCompletionSource<bool>();
                    Task<int> access = session.UsePreviewAsync(async model =>
                    {
                        entered.Set();
                        await release.Task;
                        return 42;
                    });
                    Assert.That(entered.Wait(TimeSpan.FromSeconds(2)), Is.True);

                    session.Dispose();
                    release.SetResult(true);

                    Assert.ThrowsAsync<InvalidOperationException>(async () => await access);
                }
            }
            finally { session.Dispose(); Delete(Path.GetDirectoryName(session.directory)); }
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
            Directory.SetLastWriteTimeUtc(old, UtcNow.AddDays(-8));
            Directory.SetLastWriteTimeUtc(current, UtcNow.AddDays(-6));
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
            Directory.SetLastWriteTimeUtc(failed, UtcNow.AddDays(-8));
            Directory.SetLastWriteTimeUtc(successful, UtcNow.AddDays(-8));
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
        public async Task CleanupStaleDirectories_DoesNotDeleteAnActiveOldSessionDirectory()
        {
            string root = CreateRoot();
            try
            {
                using (var registry = new PsdHierarchyWebSessionRegistry(root, () => UtcNow))
                {
                    PsdHierarchyWebSession session = await registry.GetOrCreateAsync("guid-a", "Assets/A.psd", null);
                    Directory.SetLastWriteTimeUtc(session.directory, UtcNow.AddDays(-8));

                    registry.CleanupStaleDirectories();

                    Assert.That(Directory.Exists(session.directory), Is.True);
                }
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
