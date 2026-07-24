namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebApplyTests
    {
        [Test]
        public async Task ApplyRejectsValidationErrorsBeforeCallingTheHandler()
        {
            PsdHierarchyOrganizerPreviewModel model = await ReadyModel();
            model.validationErrors.Add("validation failed");
            bool called = false;
            using (PsdHierarchyWebSession session = Session(model, plan => called = true))
            {
                var controller = new PsdHierarchyWebController(new RecordingMainThread());

                InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await controller.ApplyAsync(session, new PsdHierarchyWebApplyRequest { confirmed = true }));

                StringAssert.Contains("validation failed", exception.Message);
                Assert.That(called, Is.False);
                Assert.DoesNotThrow(() => session.Snapshot());
            }
        }

        [Test]
        public async Task ApplyRejectsUnacceptedGroups()
        {
            PsdHierarchyOrganizerPreviewModel model = await ReadyModel();
            using (PsdHierarchyWebSession session = Session(model, plan => { }))
            {
                var controller = new PsdHierarchyWebController(new RecordingMainThread());

                InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await controller.ApplyAsync(session, new PsdHierarchyWebApplyRequest { confirmed = true }));

                StringAssert.Contains("Accept every proposed group", exception.Message);
            }
        }

        [Test]
        public async Task ApplyUsesValidatedPlanOnMainThreadAndEntersPrefabReview()
        {
            PsdHierarchyOrganizerPreviewModel model = await ReadyModel();
            model.AcceptGroup("daily-list");
            PsdHierarchyPlan expected;
            string error;
            Assert.That(model.TryCreateValidatedApplyPlan(out expected, out error), Is.True, error);
            var dispatcher = new RecordingMainThread();
            PsdHierarchyPlan applied = null;
            using (PsdHierarchyWebSession session = Session(model, plan =>
            {
                Assert.That(dispatcher.isInside, Is.True);
                applied = plan;
            }))
            {
                var controller = new PsdHierarchyWebController(dispatcher);

                await controller.ApplyAsync(session, new PsdHierarchyWebApplyRequest { confirmed = true });

                Assert.That(JsonConvert.SerializeObject(applied), Is.EqualTo(JsonConvert.SerializeObject(expected)));
                PsdHierarchyWebSessionDto dto = await controller.GetSessionAsync(session);
                Assert.That(dto.phase, Is.EqualTo("prefabReview"));
                Assert.That(dto.resultingPrefabPath, Is.EqualTo("Assets/Generated/Test.prefab"));
                Assert.That(dto.canCreatePrefabs, Is.True);
                Assert.That(dto.canApply, Is.False);
                Assert.That(dto.operation.status, Is.EqualTo(PsdHierarchyWebOperationStatus.Succeeded));
            }
        }

        [Test]
        public async Task ApplyFailureLeavesSessionOpenWithReadableError()
        {
            PsdHierarchyOrganizerPreviewModel model = await ReadyModel();
            model.AcceptGroup("daily-list");
            using (PsdHierarchyWebSession session = Session(model, plan =>
                   throw new InvalidOperationException("simulated apply failure")))
            {
                var controller = new PsdHierarchyWebController(new RecordingMainThread());

                await controller.ApplyAsync(session, new PsdHierarchyWebApplyRequest { confirmed = true });

                PsdHierarchyWebSessionSnapshot snapshot = session.Snapshot();
                Assert.That(snapshot.operation.status, Is.EqualTo(PsdHierarchyWebOperationStatus.Failed));
                StringAssert.Contains("simulated apply failure", snapshot.operation.message);
                Assert.That(snapshot.resultingPrefabPath, Is.Empty);
            }
        }

        private static async Task<PsdHierarchyOrganizerPreviewModel> ReadyModel()
        {
            PsdHierarchyRequest request = Request();
            var plan = new PsdHierarchyPlan
            {
                schemaVersion = 1,
                sourcePsdGuid = request.sourcePsdGuid,
                sourceFingerprint = request.sourceFingerprint,
                contentFingerprint = request.contentFingerprint,
                structureFingerprint = request.structureFingerprint,
                geometryFingerprint = request.geometryFingerprint,
                groups = new List<PsdHierarchyPlanGroup>
                {
                    new PsdHierarchyPlanGroup
                    {
                        key = "daily-list",
                        parentKey = string.Empty,
                        memberStableIds = new List<string> { "101" },
                        displayName = "Daily List",
                        evidence = "fixture",
                        confidence = 1
                    }
                },
                renames = new List<PsdHierarchyPlanRename>
                {
                    new PsdHierarchyPlanRename
                    {
                        stableId = "101",
                        name = "DailyTaskList",
                        evidence = "fixture",
                        confidence = 1
                    }
                }
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/Generated/Test.prefab", request, plan,
                new PsdHierarchyReconciliationResult(), new NeverRunRunner());
            await model.RefreshAsync(false, CancellationToken.None);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
            return model;
        }

        private static PsdHierarchyRequest Request()
        {
            var request = new PsdHierarchyRequest
            {
                sourcePsdGuid = "guid",
                sourceFingerprint = "source",
                contentFingerprint = "content",
                structureFingerprint = "structure",
                geometryFingerprint = "geometry",
                documentWidth = 100,
                documentHeight = 100
            };
            request.nodes.Add(new PsdHierarchyRequestNode
            {
                stableId = "101",
                parentStableId = string.Empty,
                originalName = "Task List",
                kind = "Pixel",
                rectangle = new PsdHierarchyRectangle { width = 100, height = 100 },
                protectedBoundaryStableId = string.Empty
            });
            return request;
        }

        private static PsdHierarchyWebSession Session(
            PsdHierarchyOrganizerPreviewModel model,
            Action<PsdHierarchyPlan> apply)
        {
            return new PsdHierarchyWebSession(
                Guid.NewGuid().ToString("N"), "token", "guid", "Assets/Test.psd",
                Path.Combine(Path.GetTempPath(), "PsdHierarchyWebApplyTests", Guid.NewGuid().ToString("N")),
                model,
                apply);
        }

        private sealed class NeverRunRunner : IPsdHierarchyAiRunner
        {
            public Task<PsdHierarchyAiRunResult> RunAsync(
                PsdHierarchyAiRunRequest request,
                CancellationToken cancellationToken)
            {
                throw new AssertionException("Apply tests must not invoke AI.");
            }
        }

        private sealed class RecordingMainThread : IPsdHierarchyWebMainThread
        {
            public bool isInside { get; private set; }

            public async Task InvokeAsync(Func<Task> action)
            {
                isInside = true;
                try { await action(); }
                finally { isInside = false; }
            }

            public Task<TResult> InvokeAsync<TResult>(Func<TResult> action)
            {
                isInside = true;
                try { return Task.FromResult(action()); }
                finally { isInside = false; }
            }
        }
    }
}
