namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebControllerTests
    {
        [Test]
        public async Task AnalyzeReportsRunningAndRejectsASecondMutation()
        {
            var runner = new RecordingRunner { pending = new TaskCompletionSource<PsdHierarchyAiRunResult>() };
            using (PsdHierarchyWebSession session = Session(Model(Request("101"), runner)))
            {
                var controller = new PsdHierarchyWebController(new ImmediateMainThread());
                Task first = controller.AnalyzeAsync(session);
                Assert.That(runner.entered.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(controller.GetStatus(session).status, Is.EqualTo(PsdHierarchyWebOperationStatus.Running));

                Assert.ThrowsAsync<InvalidOperationException>(async () => await controller.AnalyzeAsync(session));
                runner.pending.SetResult(Success(PlanFor(runner.requests.Single())));
                await first;

                Assert.That(runner.requests.Count, Is.EqualTo(1));
                PsdHierarchyWebOperationState status = controller.GetStatus(session);
                Assert.That(status.status, Is.EqualTo(PsdHierarchyWebOperationStatus.Succeeded), status.message);
            }
        }

        [Test]
        public async Task RefineUsesSelectedStableIdsAndPreservesAcceptedUnselectedGroups()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("daily-list", "101"));
            var runner = new RecordingRunner();
            PsdHierarchyOrganizerPreviewModel model = Model(request, runner, baseline);
            model.AcceptGroup("daily-list");
            using (PsdHierarchyWebSession session = Session(model))
            {
                var controller = new PsdHierarchyWebController(new ImmediateMainThread());
                await controller.GetSnapshotAsync(session);

                await controller.RefineAsync(session, new PsdHierarchyWebRefineRequest
                {
                    stableIds = new List<string> { "102", "103" },
                    instruction = "这两个任务属于同一个列表项"
                });

                CollectionAssert.AreEquivalent(new[] { "102", "103" }, runner.requests.Single().modifiableStableIds);
                Assert.That(runner.requests.Single().instruction, Is.EqualTo("这两个任务属于同一个列表项"));
                CollectionAssert.Contains(model.acceptedGroupKeys, "daily-list");
                Assert.That((await controller.GetSnapshotAsync(session)).groups
                    .Single(group => group.key == "daily-list").isAccepted, Is.True);
            }
        }

        [Test]
        public async Task FailedRefineKeepsTheLastGoodSnapshot()
        {
            var runner = new RecordingRunner
            {
                resultFactory = request => new PsdHierarchyAiRunResult { succeeded = false, error = "simulated" }
            };
            using (PsdHierarchyWebSession session = Session(Model(Request("101"), runner)))
            {
                var controller = new PsdHierarchyWebController(new ImmediateMainThread());
                PsdHierarchyWebSnapshotDto before = await controller.GetSnapshotAsync(session);

                await controller.RefineAsync(session, new PsdHierarchyWebRefineRequest
                {
                    stableIds = new List<string> { "101" }
                });

                Assert.That(controller.GetStatus(session).status, Is.EqualTo(PsdHierarchyWebOperationStatus.Failed));
                Assert.That(await controller.GetSnapshotAsync(session), Is.SameAs(before));
            }
        }

        [Test]
        public async Task AcceptOnlyChangesTheRequestedGroups()
        {
            PsdHierarchyRequest request = Request("101", "102");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("already-accepted", "101"));
            baseline.groups.Add(Group("new-selection", "102"));
            PsdHierarchyOrganizerPreviewModel model = Model(request, new RecordingRunner(), baseline);
            model.AcceptGroup("already-accepted");
            using (PsdHierarchyWebSession session = Session(model))
            {
                var controller = new PsdHierarchyWebController(new ImmediateMainThread());
                await controller.AcceptAsync(session, new PsdHierarchyWebAcceptRequest
                {
                    groupKeys = new List<string> { "new-selection" },
                    isAccepted = true
                });

                CollectionAssert.AreEquivalent(
                    new[] { "already-accepted", "new-selection" },
                    model.acceptedGroupKeys);
            }
        }

        private static PsdHierarchyWebSession Session(PsdHierarchyOrganizerPreviewModel model)
        {
            return new PsdHierarchyWebSession(
                Guid.NewGuid().ToString("N"), "token", "guid", "Assets/Test.psd",
                Path.Combine(Path.GetTempPath(), "PsdHierarchyWebControllerTests", Guid.NewGuid().ToString("N")),
                model);
        }

        private static PsdHierarchyOrganizerPreviewModel Model(
            PsdHierarchyRequest request,
            IPsdHierarchyAiRunner runner,
            PsdHierarchyPlan baseline = null)
        {
            return new PsdHierarchyOrganizerPreviewModel(
                "Assets/Generated/Test.prefab", request, baseline ?? Baseline(request),
                new PsdHierarchyReconciliationResult(), runner);
        }

        private static PsdHierarchyRequest Request(params string[] stableIds)
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
            for (int index = 0; index < stableIds.Length; index++)
            {
                request.nodes.Add(new PsdHierarchyRequestNode
                {
                    stableId = stableIds[index],
                    originalName = "Node " + stableIds[index],
                    kind = "Pixel",
                    parentStableId = string.Empty,
                    siblingIndex = index,
                    rectangle = new PsdHierarchyRectangle { x = index * 10, width = 10, height = 10 },
                    protectedBoundaryStableId = string.Empty
                });
            }
            return request;
        }

        private static PsdHierarchyPlan Baseline(PsdHierarchyRequest request)
        {
            PsdHierarchyPlan plan = IdentityPlan(request);
            foreach (PsdHierarchyRequestNode node in request.nodes)
            {
                plan.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = node.stableId,
                    name = node.originalName,
                    evidence = "baseline",
                    confidence = 1
                });
            }
            return plan;
        }

        private static PsdHierarchyPlan PlanFor(PsdHierarchyAiRunRequest run)
        {
            PsdHierarchyPlan plan = IdentityPlan(run.request);
            foreach (string stableId in run.modifiableStableIds)
            {
                plan.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = stableId,
                    name = "Planned " + stableId,
                    evidence = "focused",
                    confidence = .9
                });
            }
            return plan;
        }

        private static PsdHierarchyPlan IdentityPlan(PsdHierarchyRequest request)
        {
            return new PsdHierarchyPlan
            {
                schemaVersion = 1,
                sourcePsdGuid = request.sourcePsdGuid,
                sourceFingerprint = request.sourceFingerprint,
                contentFingerprint = request.contentFingerprint,
                structureFingerprint = request.structureFingerprint,
                geometryFingerprint = request.geometryFingerprint
            };
        }

        private static PsdHierarchyPlanGroup Group(string key, params string[] members)
        {
            return new PsdHierarchyPlanGroup
            {
                key = key,
                memberStableIds = new List<string>(members),
                displayName = key,
                evidence = "baseline",
                confidence = 1
            };
        }

        private static PsdHierarchyAiRunResult Success(PsdHierarchyPlan plan)
        {
            return new PsdHierarchyAiRunResult { succeeded = true, plan = plan };
        }

        private sealed class ImmediateMainThread : IPsdHierarchyWebMainThread
        {
            public Task InvokeAsync(Func<Task> action) { return action(); }
            public Task<TResult> InvokeAsync<TResult>(Func<TResult> action) { return Task.FromResult(action()); }
        }

        private sealed class RecordingRunner : IPsdHierarchyAiRunner
        {
            public readonly List<PsdHierarchyAiRunRequest> requests = new List<PsdHierarchyAiRunRequest>();
            public readonly ManualResetEventSlim entered = new ManualResetEventSlim();
            public TaskCompletionSource<PsdHierarchyAiRunResult> pending;
            public Func<PsdHierarchyAiRunRequest, PsdHierarchyAiRunResult> resultFactory =
                request => Success(PlanFor(request));

            public Task<PsdHierarchyAiRunResult> RunAsync(
                PsdHierarchyAiRunRequest request,
                CancellationToken cancellationToken)
            {
                requests.Add(request);
                entered.Set();
                return pending != null ? pending.Task : Task.FromResult(resultFactory(request));
            }
        }
    }
}
