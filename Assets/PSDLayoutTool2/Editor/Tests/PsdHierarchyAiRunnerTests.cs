namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    public sealed class PsdHierarchyAiRunnerTests
    {
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "PsdHierarchyAiRunnerTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public async Task InvocationUsesReadOnlySandboxEphemeralSchemaAndSeparateArguments()
        {
            var adapter = new RecordingProcessAdapter(invocation =>
            {
                File.WriteAllText(invocation.OutputPath, PlanJson(Request("101")));
                return Completed(0);
            });
            var runner = Runner(adapter);

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(result.succeeded, Is.True, result.error);
            Assert.That(adapter.Invocation.executable, Is.EqualTo("codex-test"));
            CollectionAssert.Contains(adapter.Invocation.arguments, "exec");
            CollectionAssert.Contains(adapter.Invocation.arguments, "--sandbox");
            CollectionAssert.Contains(adapter.Invocation.arguments, "read-only");
            CollectionAssert.Contains(adapter.Invocation.arguments, "--ephemeral");
            CollectionAssert.Contains(adapter.Invocation.arguments, "--output-schema");
            CollectionAssert.Contains(adapter.Invocation.arguments, "-o");
            Assert.That(adapter.Invocation.arguments, Has.None.Contains("Assets"));
            Assert.That(adapter.Invocation.useShellExecute, Is.False);
        }

        [Test]
        public async Task RequestPackageIsWrittenOnlyBelowConfiguredTempRoot()
        {
            var adapter = new RecordingProcessAdapter(invocation =>
            {
                Assert.That(Path.GetFullPath(invocation.workingDirectory), Does.StartWith(Path.GetFullPath(tempRoot)));
                Assert.That(File.Exists(Path.Combine(invocation.workingDirectory, "request.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(invocation.workingDirectory, "plan.schema.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(invocation.workingDirectory, "prompt.txt")), Is.True);
                Assert.That(File.ReadAllText(Path.Combine(invocation.workingDirectory, "prompt.txt")), Does.Contain("read-only"));
                File.WriteAllText(invocation.OutputPath, PlanJson(Request("101")));
                return Completed(0);
            });

            PsdHierarchyAiRunResult result = await Runner(adapter).RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(result.succeeded, Is.True);
            Assert.That(Directory.Exists(result.requestPackagePath), Is.False, "Successful operations clean their package.");
        }

        [Test]
        public async Task TimeoutKillsProcessAndReturnsOfflinePackage()
        {
            var adapter = new RecordingProcessAdapter((invocation, timeout, cancellationToken) =>
                Task.FromResult(new PsdHierarchyProcessResult { timedOut = true, wasKilled = true, error = "timeout" }));

            PsdHierarchyAiRunResult result = await Runner(adapter).RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(result.succeeded, Is.False);
            Assert.That(result.error, Does.Contain("timeout").IgnoreCase);
            Assert.That(result.offlinePackageAvailable, Is.True);
            Assert.That(Directory.Exists(result.requestPackagePath), Is.True);
        }

        [Test]
        public void CancellationIsPropagatedAndPackageIsCleaned()
        {
            var adapter = new RecordingProcessAdapter(async (invocation, timeout, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return Completed(0);
            });
            var source = new CancellationTokenSource();
            source.Cancel();

            Assert.That(async () =>
                    await Runner(adapter).RunAsync(RunRequest(Request("101")), source.Token),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(Directory.Exists(Path.Combine(tempRoot, "op-1")), Is.False);
        }

        [Test]
        public async Task NonZeroExitCapturesErrorAndPreservesOfflinePackage()
        {
            var adapter = new RecordingProcessAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 9,
                standardError = "authentication failed"
            });

            PsdHierarchyAiRunResult result = await Runner(adapter).RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(result.succeeded, Is.False);
            Assert.That(result.error, Does.Contain("authentication failed"));
            Assert.That(result.offlinePackageAvailable, Is.True);
        }

        [Test]
        public async Task MalformedOutputIsRejectedByStrictParser()
        {
            var adapter = new RecordingProcessAdapter(invocation =>
            {
                File.WriteAllText(invocation.OutputPath, "{\"schemaVersion\":1,\"command\":\"write Assets\"}");
                return Completed(0);
            });

            PsdHierarchyAiRunResult result = await Runner(adapter).RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(result.succeeded, Is.False);
            Assert.That(result.error, Does.Contain("plan").IgnoreCase);
            Assert.That(result.offlinePackageAvailable, Is.True);
        }

        [Test]
        public async Task ContentOnlyUpdateMakesZeroPlannerCallsAndValidatesBaseline()
        {
            PsdHierarchyRequest request = Request("101", "102");
            var fake = new FakeRunner();
            var reconciliation = new PsdHierarchyReconciliationResult();
            reconciliation.contentOnlyStableIds.Add("101");
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request), reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(fake.Requests, Is.Empty);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task FocusedReplanCallsOncePerInvalidatedScopeAndNeverSendsUnaffectedNode()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            var fake = new FakeRunner();
            fake.ResultFactory = run => Success(PlanFor(run.request, run.request.nodes[0].stableId));
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.focusedInvalidatedScopeStableIds.Add("101");
            reconciliation.unsortedNewStableIds.Add("103");
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request), reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(fake.Requests.Count, Is.EqualTo(2));
            Assert.That(fake.Requests.All(run => run.request.nodes.Count == 1), Is.True);
            CollectionAssert.AreEquivalent(new[] { "101", "103" },
                fake.Requests.Select(run => run.request.nodes.Single().stableId));
            Assert.That(fake.Requests.SelectMany(run => run.request.nodes).Any(node => node.stableId == "102"), Is.False);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
            Assert.That(model.proposedPlan.renames.Any(rename => rename.stableId == "102"), Is.True,
                "Unrelated baseline decisions survive byte-for-byte semantic merge.");
        }

        [Test]
        public async Task PartialPlanCannotTouchOutsideItsFocusedScope()
        {
            PsdHierarchyRequest request = Request("101", "102");
            var fake = new FakeRunner();
            fake.ResultFactory = run => Success(PlanFor(run.request, "102"));
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.focusedInvalidatedScopeStableIds.Add("101");
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request), reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(model.canApply, Is.False);
            Assert.That(model.validationErrors.Single(), Does.Contain("scope").IgnoreCase);
        }

        [Test]
        public async Task MissingIdsRemainPendingUntilExplicitPreviewConfirmation()
        {
            PsdHierarchyRequest request = Request("101");
            var reconciliation = new PsdHierarchyReconciliationResult();
            reconciliation.pendingMissingStableIds.Add("999");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.renames.Add(new PsdHierarchyPlanRename
            {
                stableId = "999", name = "Missing", evidence = "old profile", confidence = 1d
            });
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, new FakeRunner());

            await model.RefreshAsync(false, CancellationToken.None);
            Assert.That(model.pendingMissingStableIds, Does.Contain("999"));
            Assert.That(model.canApply, Is.False);

            await model.RefreshAsync(true, CancellationToken.None);
            Assert.That(model.pendingMissingStableIds, Is.Empty);
            Assert.That(model.canApply, Is.True);
            Assert.That(model.proposedPlan.renames.Any(rename => rename.stableId == "999"), Is.False);
            Assert.That(reconciliation.pendingMissingStableIds, Does.Contain("999"), "Preview must not mutate Profile reconciliation state.");
        }

        [Test]
        public void CurrentTreeIsAnImmutablePreviewSnapshot()
        {
            PsdHierarchyRequest request = Request("101", "102");
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request),
                new PsdHierarchyReconciliationResult(), new FakeRunner());

            request.nodes[0].originalName = "Changed after opening";

            Assert.That(model.currentTreeNodes.Select(node => node.originalName),
                Is.EqualTo(new[] { "Node 101", "Node 102" }));
        }

        private CodexCliHierarchyRunner Runner(IHierarchyProcessAdapter adapter)
        {
            return new CodexCliHierarchyRunner(adapter, () => "codex-test", tempRoot);
        }

        private PsdHierarchyAiRunRequest RunRequest(PsdHierarchyRequest request)
        {
            return new PsdHierarchyAiRunRequest
            {
                operationId = "op-1",
                request = request,
                targetPrefabPath = "Assets/UI/Target.prefab",
                timeout = TimeSpan.FromMilliseconds(25)
            };
        }

        private static PsdHierarchyRequest Request(params string[] stableIds)
        {
            var request = new PsdHierarchyRequest
            {
                sourcePsdGuid = "guid",
                sourceFingerprint = "source",
                contentFingerprint = "content",
                structureFingerprint = "structure",
                geometryFingerprint = "geometry"
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
                    rectangle = new PsdHierarchyRectangle { width = 10f, height = 10f },
                    protectedBoundaryStableId = string.Empty
                });
            }
            return request;
        }

        private static PsdHierarchyPlan Baseline(PsdHierarchyRequest request)
        {
            var plan = IdentityPlan(request);
            foreach (PsdHierarchyRequestNode node in request.nodes)
            {
                plan.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = node.stableId,
                    name = "Baseline " + node.stableId,
                    evidence = "existing",
                    confidence = 1d
                });
            }
            return plan;
        }

        private static PsdHierarchyPlan PlanFor(PsdHierarchyRequest request, string stableId)
        {
            PsdHierarchyPlan plan = IdentityPlan(request);
            plan.renames.Add(new PsdHierarchyPlanRename
            {
                stableId = stableId,
                name = "Planned " + stableId,
                evidence = "focused",
                confidence = 0.9d
            });
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

        private static string PlanJson(PsdHierarchyRequest request)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(IdentityPlan(request));
        }

        private static PsdHierarchyAiRunResult Success(PsdHierarchyPlan plan)
        {
            return new PsdHierarchyAiRunResult { succeeded = true, plan = plan };
        }

        private static PsdHierarchyProcessResult Completed(int exitCode)
        {
            return new PsdHierarchyProcessResult { exitCode = exitCode };
        }

        private sealed class FakeRunner : IPsdHierarchyAiRunner
        {
            public readonly List<PsdHierarchyAiRunRequest> Requests = new List<PsdHierarchyAiRunRequest>();
            public Func<PsdHierarchyAiRunRequest, PsdHierarchyAiRunResult> ResultFactory =
                request => Success(IdentityPlan(request.request));

            public Task<PsdHierarchyAiRunResult> RunAsync(PsdHierarchyAiRunRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(ResultFactory(request));
            }
        }

        private sealed class RecordingProcessAdapter : IHierarchyProcessAdapter
        {
            private readonly Func<PsdHierarchyProcessInvocation, TimeSpan, CancellationToken, Task<PsdHierarchyProcessResult>> run;

            public RecordingProcessAdapter(Func<PsdHierarchyProcessInvocation, PsdHierarchyProcessResult> run)
                : this((invocation, timeout, cancellationToken) => Task.FromResult(run(invocation)))
            {
            }

            public RecordingProcessAdapter(
                Func<PsdHierarchyProcessInvocation, TimeSpan, CancellationToken, Task<PsdHierarchyProcessResult>> run)
            {
                this.run = run;
            }

            public PsdHierarchyProcessInvocation Invocation { get; private set; }

            public Task<PsdHierarchyProcessResult> RunAsync(
                PsdHierarchyProcessInvocation invocation,
                TimeSpan timeout,
                CancellationToken cancellationToken)
            {
                Invocation = invocation;
                return run(invocation, timeout, cancellationToken);
            }
        }
    }
}
