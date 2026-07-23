namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
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
            CollectionAssert.Contains(adapter.Invocation.arguments, "--ignore-user-config",
                "The self-contained hierarchy planner must not start unrelated user MCP/plugin processes.");
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
                Assert.That(File.Exists(Path.Combine(invocation.workingDirectory, "focus.json")), Is.True);
                string prompt = File.ReadAllText(Path.Combine(invocation.workingDirectory, "prompt.txt"));
                Assert.That(prompt, Does.Contain("read-only"));
                Assert.That(prompt, Does.Contain("Every modifiable ID"),
                    "Focused replans must receive an explicit decision requirement instead of returning an empty plan.");
                Assert.That(prompt, Does.Contain("originalName"),
                    "A no-op focus decision is represented as an identity rename using the request node's originalName.");
                Assert.That(prompt, Does.Contain("You may create new group keys"),
                    "A first-time hierarchy has no existing group keys, so the planner must be allowed to create them.");
                Assert.That(File.ReadAllText(Path.Combine(invocation.workingDirectory, "plan.schema.json")),
                    Does.Contain("\"schemaVersion\":{\"type\":\"integer\",\"const\":1}"),
                    "Codex requires a type alongside const in object properties.");
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
        public async Task ExistingOperationDirectoryIsRejectedWithoutReadingStalePlan()
        {
            string stale = Path.Combine(tempRoot, "op-1");
            Directory.CreateDirectory(stale);
            File.WriteAllText(Path.Combine(stale, "plan.json"), PlanJson(Request("101")));
            var adapter = new RecordingProcessAdapter(invocation => Completed(0));

            PsdHierarchyAiRunResult result = await Runner(adapter).RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(result.succeeded, Is.False);
            Assert.That(result.error, Does.Contain("already exists").IgnoreCase);
            Assert.That(adapter.Invocation, Is.Null);
            Assert.That(File.Exists(Path.Combine(stale, "plan.json")), Is.True, "Rejected callers never delete stale/foreign data.");
        }

        [Test]
        public async Task ConcurrentSameOperationIdIsRejectedAndCannotDeleteOwnerPackage()
        {
            var entered = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<bool>();
            var adapter = new RecordingProcessAdapter(async (invocation, timeout, token) =>
            {
                entered.TrySetResult(true);
                await release.Task;
                File.WriteAllText(invocation.OutputPath, PlanJson(Request("101")));
                return Completed(0);
            });
            CodexCliHierarchyRunner runner = Runner(adapter);

            Task<PsdHierarchyAiRunResult> owner = runner.RunAsync(RunRequest(Request("101")), CancellationToken.None);
            await entered.Task;
            PsdHierarchyAiRunResult rejected = await runner.RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(rejected.succeeded, Is.False);
            Assert.That(rejected.error, Does.Contain("in progress").IgnoreCase);
            Assert.That(Directory.Exists(Path.Combine(tempRoot, "op-1")), Is.True);
            release.TrySetResult(true);
            Assert.That((await owner).succeeded, Is.True);
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
        public async Task FocusedReplanBatchesUngroupedInvalidatedNodesAndNeverSendsUnaffectedNode()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            var fake = new FakeRunner();
            fake.ResultFactory = run => Success(PlanFor(run.request, run.modifiableStableIds.ToArray()));
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.focusedInvalidatedScopeStableIds.Add("101");
            reconciliation.unsortedNewStableIds.Add("103");
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request), reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(fake.Requests.Count, Is.EqualTo(1),
                "Independent ungrouped nodes must share one Codex startup instead of waiting through one process per node.");
            CollectionAssert.AreEquivalent(new[] { "101", "103" }, fake.Requests.Single().modifiableStableIds);
            Assert.That(fake.Requests.Single().contextStableIds, Does.Contain("101"));
            Assert.That(fake.Requests.Single().contextStableIds, Does.Contain("103"));
            Assert.That(fake.Requests.Single().contextStableIds, Does.Contain("102"),
                "Read-only sibling context is expected, but it is never a modifiable ID.");
            Assert.That(fake.Requests.Single().modifiableStableIds, Does.Not.Contain("102"));
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
            Assert.That(model.proposedPlan.renames.Any(rename => rename.stableId == "102"), Is.True,
                "Unrelated baseline decisions survive byte-for-byte semantic merge.");
        }

        [Test]
        public async Task NewNodeScopeIncludesSiblingNeighborsRelevantPreviewAndModificationMarkers()
        {
            PsdHierarchyRequest request = Request("101", "102", "103", "104");
            request.nodes[0].rectangle = new PsdHierarchyRectangle { x = 0, width = 10, height = 10 };
            request.nodes[1].rectangle = new PsdHierarchyRectangle { x = 10, width = 10, height = 10 };
            request.nodes[2].rectangle = new PsdHierarchyRectangle { x = 20, width = 10, height = 10 };
            request.nodes[3].rectangle = new PsdHierarchyRectangle { x = 30, width = 10, height = 10 };
            request.previews.Add(new PsdHierarchyPreviewReference
            {
                key = "near", kind = "crop", crop = new PsdHierarchyRectangle { x = 15, width = 20, height = 10 }
            });
            request.previews.Add(new PsdHierarchyPreviewReference
            {
                key = "far", kind = "crop", crop = new PsdHierarchyRectangle { x = 1000, width = 20, height = 10 }
            });
            var fake = new FakeRunner { ResultFactory = run => Success(PlanFor(run.request, "103")) };
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.unsortedNewStableIds.Add("103");

            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request), reconciliation, fake);
            await model.RefreshAsync(false, CancellationToken.None);

            PsdHierarchyAiRunRequest focused = fake.Requests.Single();
            CollectionAssert.AreEquivalent(new[] { "102", "103", "104" }, focused.request.nodes.Select(node => node.stableId));
            CollectionAssert.AreEquivalent(new[] { "103" }, focused.modifiableStableIds);
            Assert.That(focused.request.previews.Select(preview => preview.key), Is.EqualTo(new[] { "near" }));
            Assert.That(focused.request.previews[0].crop.x, Is.GreaterThanOrEqualTo(20f), "Preview crop is clipped to focused bounds.");
        }

        [Test]
        public async Task NestedFocusedPlanReceivesAncestorGraphAndPreservesParentKey()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "parent", parentKey = "", memberStableIds = new List<string> { "101" },
                displayName = "Parent", evidence = "old", confidence = 1
            });
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "child", parentKey = "parent", memberStableIds = new List<string> { "102" },
                displayName = "Child", evidence = "old", confidence = 1
            });
            baseline.renames.RemoveAll(rename => rename.stableId == "102");
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(new PsdHierarchyPlan
                {
                    schemaVersion = 1,
                    sourcePsdGuid = run.request.sourcePsdGuid,
                    sourceFingerprint = run.request.sourceFingerprint,
                    contentFingerprint = run.request.contentFingerprint,
                    structureFingerprint = run.request.structureFingerprint,
                    geometryFingerprint = run.request.geometryFingerprint,
                    groups = new List<PsdHierarchyPlanGroup>
                    {
                        new PsdHierarchyPlanGroup { key = "child", parentKey = "parent", memberStableIds = new List<string> { "102" }, displayName = "Child2", evidence = "focused", confidence = .9 }
                    }
                })
            };
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.focusedInvalidatedScopeStableIds.Add("102");
            var model = new PsdHierarchyOrganizerPreviewModel("Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(fake.Requests.Single().baselineGroups.Select(group => group.key), Does.Contain("parent"));
            CollectionAssert.AreEquivalent(new[] { "child" }, fake.Requests.Single().scopeOwnedGroupKeys);
            CollectionAssert.AreEquivalent(new[] { "parent" }, fake.Requests.Single().readonlyNeighborGroupKeys);
            CollectionAssert.AreEquivalent(new[] { "child", "parent" }, fake.Requests.Single().modifiableGroupKeys);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "child").parentKey, Is.EqualTo("parent"));
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
        public async Task ReconciliationIsDeepSnapshottedAtConstruction()
        {
            PsdHierarchyRequest request = Request("101", "102");
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.focusedInvalidatedScopeStableIds.Add("101");
            reconciliation.pendingMissingStableIds.Add("999");
            var fake = new FakeRunner { ResultFactory = run => Success(PlanFor(run.request, "101")) };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request), reconciliation, fake);

            reconciliation.focusedInvalidatedScopeStableIds.Clear();
            reconciliation.focusedInvalidatedScopeStableIds.Add("102");
            reconciliation.pendingMissingStableIds.Clear();
            reconciliation.unsortedNewStableIds.Add("102");
            reconciliation.requiresReplan = false;
            await model.RefreshAsync(false, CancellationToken.None);

            CollectionAssert.AreEquivalent(new[] { "101" }, fake.Requests.Single().modifiableStableIds);
            Assert.That(model.pendingMissingStableIds, Does.Contain("999"));
            Assert.That(model.canApply, Is.False);
        }

        [Test]
        public async Task NewIdCanJoinNeighborGroupWithoutChangingReadonlyMemberOrder()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "existing", parentKey = "", memberStableIds = new List<string> { "101", "102" },
                displayName = "Existing", evidence = "old", confidence = 1
            });
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.unsortedNewStableIds.Add("103");
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(PlanWithBaselineGroup(
                    run.request, baseline.groups.Single(), "101", "102", "103"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            CollectionAssert.Contains(fake.Requests.Single().modifiableGroupKeys, "existing");
            CollectionAssert.AreEqual(new[] { "101", "102", "103" },
                model.proposedPlan.groups.Single(group => group.key == "existing").memberStableIds);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task NewIdCannotMoveOrReorderReadonlyNeighborMembers()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "existing", parentKey = "", memberStableIds = new List<string> { "101", "102" },
                displayName = "Existing", evidence = "old", confidence = 1
            });
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.unsortedNewStableIds.Add("103");
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(PlanWithBaselineGroup(
                    run.request, baseline.groups.Single(), "102", "101", "103"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(model.canApply, Is.False);
            Assert.That(model.validationErrors.Single(), Does.Contain("readonly").IgnoreCase);
        }

        [Test]
        public async Task PartialNewGroupCannotHijackExistingKeyOutsideFocusedScope()
        {
            PsdHierarchyRequest request = Request("101", "102", "103", "104");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "outside", parentKey = "", memberStableIds = new List<string> { "101" },
                displayName = "Outside", evidence = "old", confidence = 1
            });
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.unsortedNewStableIds.Add("104");
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(PlanWithGroup(run.request, "outside", "", "104"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(fake.Requests.Single().existingGroupKeys, Does.Contain("outside"));
            Assert.That(fake.Requests.Single().modifiableGroupKeys, Does.Not.Contain("outside"));
            Assert.That(model.canApply, Is.False);
            Assert.That(model.validationErrors.Single(), Does.Contain("existing group key").IgnoreCase);
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "outside").memberStableIds,
                Is.EqualTo(new[] { "101" }));
        }

        [Test]
        public async Task JoiningReadonlyNeighborGroupCannotAlterItsMetadata()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "existing", parentKey = "", memberStableIds = new List<string> { "101", "102" },
                displayName = "Existing", evidence = "original evidence", confidence = .75
            });
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.unsortedNewStableIds.Add("103");
            var fake = new FakeRunner
            {
                ResultFactory = run =>
                {
                    PsdHierarchyPlan plan = PlanWithBaselineGroup(
                        run.request, baseline.groups.Single(), "101", "102", "103");
                    plan.groups[0].displayName = "Hijacked metadata";
                    return Success(plan);
                }
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(model.canApply, Is.False);
            Assert.That(model.validationErrors.Single(), Does.Contain("metadata").IgnoreCase);
        }

        [Test]
        public async Task ScopeOwnedGroupCanDissolveWithEmptyPartialPlan()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("owned", "101", "102"));
            var reconciliation = Invalidated("101", "102");
            var fake = new FakeRunner { ResultFactory = run => Success(IdentityPlan(run.request)) };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            CollectionAssert.Contains(fake.Requests.Single().scopeOwnedGroupKeys, "owned");
            Assert.That(model.proposedPlan.groups.Any(group => group.key == "owned"), Is.False);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task ScopeOwnedGroupCanRekey()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("old-key", "101", "102"));
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(PlanWithGroup(run.request, "new-key", "", "101", "102"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, Invalidated("101", "102"), fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(model.proposedPlan.groups.Select(group => group.key), Is.EqualTo(new[] { "new-key" }));
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task ScopeOwnedGroupCanSplit()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("old", "101", "102"));
            var fake = new FakeRunner
            {
                ResultFactory = run =>
                {
                    PsdHierarchyPlan plan = IdentityPlan(run.request);
                    plan.groups.Add(Group("left", "101"));
                    plan.groups.Add(Group("right", "102"));
                    return Success(plan);
                }
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, Invalidated("101", "102"), fake);

            await model.RefreshAsync(false, CancellationToken.None);

            CollectionAssert.AreEquivalent(new[] { "left", "right" }, model.proposedPlan.groups.Select(group => group.key));
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task ScopeOwnedGroupCanMergeIntoReadonlyNeighborGroup()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("owned", "101"));
            PsdHierarchyPlanGroup neighbor = Group("neighbor", "102");
            baseline.groups.Add(neighbor);
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(PlanWithBaselineGroup(run.request, neighbor, "102", "101"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, Invalidated("101"), fake);

            await model.RefreshAsync(false, CancellationToken.None);

            CollectionAssert.Contains(fake.Requests.Single().readonlyNeighborGroupKeys, "neighbor");
            Assert.That(model.proposedPlan.groups.Single().key, Is.EqualTo("neighbor"));
            CollectionAssert.AreEqual(new[] { "102", "101" }, model.proposedPlan.groups.Single().memberStableIds);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task ReadonlyNeighborCannotBeRestatedWithoutAnyModifiableId()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            PsdHierarchyPlanGroup neighbor = Group("neighbor", "101", "102");
            baseline.groups.Add(neighbor);
            var reconciliation = new PsdHierarchyReconciliationResult { requiresReplan = true };
            reconciliation.unsortedNewStableIds.Add("103");
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(PlanWithBaselineGroup(run.request, neighbor, "101", "102"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(model.canApply, Is.False);
            Assert.That(model.validationErrors.Single(), Does.Contain("without adding a modifiable ID").IgnoreCase);
        }

        [Test]
        public async Task SecondScopeOmissionPreservesHybridCreatedByFirstScope()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("a", "101"));
            PsdHierarchyPlanGroup groupB = Group("b", "102");
            baseline.groups.Add(groupB);
            var reconciliation = Invalidated("101", "102");
            var fake = new FakeRunner
            {
                ResultFactory = run => run.modifiableStableIds.Contains("101")
                    ? Success(PlanWithBaselineGroup(run.request, groupB, "102", "101"))
                    : Success(IdentityPlan(run.request))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(fake.Requests.Count, Is.EqualTo(2));
            CollectionAssert.Contains(fake.Requests[1].hybridGroupKeys, "b");
            CollectionAssert.AreEqual(new[] { "102", "101" },
                model.proposedPlan.groups.Single(group => group.key == "b").memberStableIds);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task HybridMayExplicitlyRemoveAllCurrentScopeMembersOnly()
        {
            PsdHierarchyRequest request = Request("101", "102", "103");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(Group("a", "101"));
            PsdHierarchyPlanGroup groupB = Group("b", "102");
            baseline.groups.Add(groupB);
            var fake = new FakeRunner
            {
                ResultFactory = run => run.modifiableStableIds.Contains("101")
                    ? Success(PlanWithBaselineGroup(run.request, groupB, "102", "101"))
                    : Success(PlanWithBaselineGroup(run.request, groupB, "101"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, Invalidated("101", "102"), fake);

            await model.RefreshAsync(false, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "101" },
                model.proposedPlan.groups.Single(group => group.key == "b").memberStableIds);
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task DissolvingOwnedParentReparentsOmittedDependentDescendants()
        {
            PsdHierarchyRequest request = Request("101", "102", "103", "104");
            PsdHierarchyPlan baseline = Baseline(request);
            PsdHierarchyPlanGroup grandchild = Group("grandchild", "103");
            grandchild.parentKey = "child";
            PsdHierarchyPlanGroup child = Group("child", "102");
            child.parentKey = "parent";
            baseline.groups.Add(grandchild); // Deliberately reverse hierarchy order.
            baseline.groups.Add(child);
            baseline.groups.Add(Group("parent", "101"));
            var fake = new FakeRunner { ResultFactory = run => Success(IdentityPlan(run.request)) };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, Invalidated("101"), fake);

            await model.RefreshAsync(false, CancellationToken.None);

            CollectionAssert.AreEquivalent(new[] { "child", "grandchild" },
                fake.Requests.Single().structuralDependentGroupKeys);
            Assert.That(model.proposedPlan.groups.Any(group => group.key == "parent"), Is.False);
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "child").parentKey, Is.Empty);
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "grandchild").parentKey, Is.EqualTo("child"));
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task RekeyedOwnedParentMayMoveReadonlyDependentChild()
        {
            PsdHierarchyRequest request = Request("101", "102", "103", "104");
            PsdHierarchyPlan baseline = Baseline(request);
            PsdHierarchyPlanGroup grandchild = Group("grandchild", "103");
            grandchild.parentKey = "child";
            PsdHierarchyPlanGroup child = Group("child", "102");
            child.parentKey = "parent";
            baseline.groups.Add(grandchild);
            baseline.groups.Add(child);
            baseline.groups.Add(Group("parent", "101"));
            var fake = new FakeRunner
            {
                ResultFactory = run =>
                {
                    PsdHierarchyPlan plan = PlanWithGroup(run.request, "new-parent", "", "101");
                    PsdHierarchyPlanGroup movedChild = Group("child", "102");
                    movedChild.parentKey = "new-parent";
                    plan.groups.Add(movedChild);
                    return Success(plan);
                }
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, Invalidated("101"), fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(model.proposedPlan.groups.Any(group => group.key == "parent"), Is.False);
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "child").parentKey, Is.EqualTo("new-parent"));
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "grandchild").parentKey, Is.EqualTo("child"));
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
        }

        [Test]
        public async Task ReturnedOwnedParentWithSameKeyKeepsOmittedDependentChain()
        {
            PsdHierarchyRequest request = Request("101", "102", "103", "104");
            PsdHierarchyPlan baseline = Baseline(request);
            PsdHierarchyPlanGroup grandchild = Group("grandchild", "103");
            grandchild.parentKey = "child";
            PsdHierarchyPlanGroup child = Group("child", "102");
            child.parentKey = "parent";
            baseline.groups.Add(grandchild);
            baseline.groups.Add(child);
            baseline.groups.Add(Group("parent", "101"));
            var fake = new FakeRunner
            {
                ResultFactory = run => Success(PlanWithGroup(run.request, "parent", "", "101"))
            };
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, Invalidated("101"), fake);

            await model.RefreshAsync(false, CancellationToken.None);

            Assert.That(model.proposedPlan.groups.Single(group => group.key == "child").parentKey, Is.EqualTo("parent"));
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "grandchild").parentKey, Is.EqualTo("child"));
            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
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

        [Test]
        public async Task ProposedPlanSnapshotCannotMutateValidatedApplyClone()
        {
            PsdHierarchyRequest request = Request("101");
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, Baseline(request),
                new PsdHierarchyReconciliationResult(), new FakeRunner());
            await model.RefreshAsync(false, CancellationToken.None);

            PsdHierarchyPlan leaked = model.proposedPlan;
            leaked.renames[0].stableId = "999";

            PsdHierarchyPlan applyPlan;
            string error;
            Assert.That(model.TryCreateValidatedApplyPlan(out applyPlan, out error), Is.True, error);
            Assert.That(applyPlan.renames[0].stableId, Is.EqualTo("101"));
            applyPlan.renames[0].stableId = "888";
            Assert.That(model.proposedPlan.renames[0].stableId, Is.EqualTo("101"));
        }

        [Test]
        public void ManualPlanLoaderRejectsByteLimitBeforeDecode()
        {
            Directory.CreateDirectory(tempRoot);
            string path = Path.Combine(tempRoot, "oversized.json");
            File.WriteAllBytes(path, new byte[PsdHierarchyContractLimits.MaxJsonUtf8Bytes + 1]);

            Assert.Throws<PsdHierarchyPlanFormatException>(() => PsdHierarchyManualPlanLoader.Load(path));
        }

        [Test]
        public void ManualPlanLoaderCompletesSynchronouslyForValidJson()
        {
            Directory.CreateDirectory(tempRoot);
            PsdHierarchyRequest request = Request("101");
            string path = Path.Combine(tempRoot, "valid.json");
            File.WriteAllText(path, PlanJson(request));

            PsdHierarchyPlan plan = PsdHierarchyManualPlanLoader.Load(path);

            Assert.That(plan.sourcePsdGuid, Is.EqualTo(request.sourcePsdGuid));
        }

        [Test]
        public void BoundedTextReaderRejectsCharacterLimitPlusOne()
        {
            using (var reader = new StringReader(new string('x', 33)))
            {
                Assert.ThrowsAsync<PsdHierarchyOutputLimitException>(async () =>
                    await PsdHierarchyBoundedTextReader.ReadAsync(reader, 32, CancellationToken.None));
            }
        }

        [Test]
        public async Task ConfirmedMissingCleanupReparentsChildBeforeDeletingEmptyParent()
        {
            PsdHierarchyRequest request = Request("101");
            PsdHierarchyPlan baseline = Baseline(request);
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "empty-parent", parentKey = "", memberStableIds = new List<string> { "999" },
                displayName = "Parent", evidence = "old", confidence = 1
            });
            baseline.groups.Add(new PsdHierarchyPlanGroup
            {
                key = "child", parentKey = "empty-parent", memberStableIds = new List<string> { "101" },
                displayName = "Child", evidence = "old", confidence = 1
            });
            var reconciliation = new PsdHierarchyReconciliationResult();
            reconciliation.pendingMissingStableIds.Add("999");
            var model = new PsdHierarchyOrganizerPreviewModel(
                "Assets/UI/Target.prefab", request, baseline, reconciliation, new FakeRunner());

            await model.RefreshAsync(true, CancellationToken.None);

            Assert.That(model.canApply, Is.True, string.Join(";", model.validationErrors));
            Assert.That(model.proposedPlan.groups.Any(group => group.key == "empty-parent"), Is.False);
            Assert.That(model.proposedPlan.groups.Single(group => group.key == "child").parentKey, Is.Empty);
        }

        [Test]
        public async Task ConfirmedRuntimeCancellationCleansPackageOnlyAfterTreeKillAndExitWait()
        {
            var order = new List<string>();
            var entered = new TaskCompletionSource<bool>();
            var adapter = new RecordingProcessAdapter(async (invocation, timeout, token) =>
            {
                entered.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The fake models the real adapter after tree termination.
                }
                order.Add("kill-tree");
                order.Add("wait-exit");
                throw new PsdHierarchyProcessCancelledException("cancelled", true, true, token);
            });
            var source = new CancellationTokenSource();
            Task<PsdHierarchyAiRunResult> running = Runner(adapter).RunAsync(RunRequest(Request("101")), source.Token);
            await entered.Task;
            source.Cancel();

            Assert.That(async () => await running, Throws.InstanceOf<OperationCanceledException>());
            order.Add(Directory.Exists(Path.Combine(tempRoot, "op-1")) ? "package-retained" : "package-cleaned");
            CollectionAssert.AreEqual(new[] { "kill-tree", "wait-exit", "package-cleaned" }, order);
        }

        [Test]
        public async Task ParentExitWithoutConfirmedTreeKillRetainsPackage()
        {
            await AssertUnconfirmedTreeTerminationRetainsPackage(false, true);
        }

        [Test]
        public async Task ConfirmedTreeKillWithoutExitWaitRetainsPackage()
        {
            await AssertUnconfirmedTreeTerminationRetainsPackage(true, false);
        }

        private async Task AssertUnconfirmedTreeTerminationRetainsPackage(bool treeKilled, bool waited)
        {
            var entered = new TaskCompletionSource<bool>();
            var adapter = new RecordingProcessAdapter(async (invocation, timeout, token) =>
            {
                entered.TrySetResult(true);
                try { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                throw new PsdHierarchyProcessCancelledException("adapter cancel", treeKilled, waited, token);
            });
            var source = new CancellationTokenSource();
            Task<PsdHierarchyAiRunResult> running = Runner(adapter).RunAsync(RunRequest(Request("101")), source.Token);
            await entered.Task;
            source.Cancel();

            PsdHierarchyProcessTerminationException exception = Assert.ThrowsAsync<PsdHierarchyProcessTerminationException>(
                async () => await running);
            Assert.That(exception.Message, Does.Contain("retained").IgnoreCase);
            Assert.That(Directory.Exists(Path.Combine(tempRoot, "op-1")), Is.True);
        }

        [Test]
        public async Task PackageDirectoryCreationFailureAlwaysReleasesOperationLock()
        {
            var adapter = new RecordingProcessAdapter(invocation => Completed(0));
            var runner = new CodexCliHierarchyRunner(
                adapter, () => "codex-test", tempRoot,
                path => throw new IOException("injected create failure"));

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(Request("101")), CancellationToken.None);

            Assert.That(result.succeeded, Is.False);
            Assert.That(File.Exists(Path.Combine(tempRoot, ".op-1.lock")), Is.False);
            Assert.That(adapter.Invocation, Is.Null);
        }

        [Test]
        public void OutputMonitorReportsOneOverflowWithoutWaitingForOtherStream()
        {
            var never = new TaskCompletionSource<string>();
            Task<string> overflow = Task.FromException<string>(new PsdHierarchyOutputLimitException("cap+1"));
            var exit = new TaskCompletionSource<int>();

            Assert.ThrowsAsync<PsdHierarchyOutputLimitException>(async () =>
                await PsdHierarchyProcessOutputMonitor.WaitAsync(exit.Task, overflow, never.Task));
        }

        [TestCase(true, 0, true)]
        [TestCase(true, 1, false)]
        [TestCase(false, 0, false)]
        public void TaskkillConfirmationRequiresWaitAndZeroExit(bool waited, int exitCode, bool expected)
        {
            Assert.That(PsdHierarchyTaskkillConfirmation.IsConfirmed(waited, exitCode), Is.EqualTo(expected));
        }

        [Test]
        public void WindowsSelectsTaskkillAndNonWindowsIsNeverConfirmedByStrategyAlone()
        {
            Assert.That(PsdHierarchyProcessTreeStrategy.Select(PlatformID.Win32NT),
                Is.EqualTo(PsdHierarchyProcessTreeTerminationStrategy.WindowsTaskkill));
            Assert.That(PsdHierarchyProcessTreeStrategy.Select(PlatformID.Unix),
                Is.EqualTo(PsdHierarchyProcessTreeTerminationStrategy.NonWindowsUnconfirmedKill));
        }

        [Test]
        public void DefaultExecutableResolverUsesExistingWindowsNpmShimWhenUnityPathIsStale()
        {
            string roamingRoot = Path.Combine(tempRoot, "Roaming");
            string npmDirectory = Path.Combine(roamingRoot, "npm");
            Directory.CreateDirectory(npmDirectory);
            string shimPath = Path.Combine(npmDirectory, "codex.cmd");
            File.WriteAllText(shimPath, "@echo off");

            string executable = CodexCliHierarchyRunner.ResolveDefaultExecutable(roamingRoot);

            Assert.That(executable, Is.EqualTo(Path.GetFullPath(shimPath)));
        }

        [Test]
        public void ProcessAdapterWritesCodexPromptAsUtf8()
        {
            var invocation = new PsdHierarchyProcessInvocation
            {
                executable = "codex",
                workingDirectory = tempRoot,
                standardInput = "含有中文的提示",
                arguments = new List<string> { "exec" }
            };

            ProcessStartInfo startInfo = SystemHierarchyProcessAdapter.CreateStartInfo(invocation);

            Assert.That(startInfo.StandardInputEncoding.CodePage, Is.EqualTo(Encoding.UTF8.CodePage));
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

        private static PsdHierarchyPlan PlanFor(PsdHierarchyRequest request, params string[] stableIds)
        {
            PsdHierarchyPlan plan = IdentityPlan(request);
            foreach (string stableId in stableIds)
            {
                plan.renames.Add(new PsdHierarchyPlanRename
                {
                    stableId = stableId,
                    name = "Planned " + stableId,
                    evidence = "focused",
                    confidence = 0.9d
                });
            }
            return plan;
        }

        private static PsdHierarchyPlan PlanWithGroup(
            PsdHierarchyRequest request,
            string key,
            string parentKey,
            params string[] members)
        {
            PsdHierarchyPlan plan = IdentityPlan(request);
            plan.groups.Add(new PsdHierarchyPlanGroup
            {
                key = key,
                parentKey = parentKey,
                memberStableIds = new List<string>(members),
                displayName = key,
                evidence = "focused",
                confidence = .9
            });
            return plan;
        }

        private static PsdHierarchyPlan PlanWithBaselineGroup(
            PsdHierarchyRequest request,
            PsdHierarchyPlanGroup baseline,
            params string[] members)
        {
            PsdHierarchyPlan plan = IdentityPlan(request);
            plan.groups.Add(new PsdHierarchyPlanGroup
            {
                key = baseline.key,
                parentKey = baseline.parentKey,
                memberStableIds = new List<string>(members),
                displayName = baseline.displayName,
                evidence = baseline.evidence,
                confidence = baseline.confidence
            });
            return plan;
        }

        private static PsdHierarchyPlanGroup Group(string key, params string[] members)
        {
            return new PsdHierarchyPlanGroup
            {
                key = key,
                parentKey = string.Empty,
                memberStableIds = new List<string>(members),
                displayName = key,
                evidence = "baseline",
                confidence = 1
            };
        }

        private static PsdHierarchyReconciliationResult Invalidated(params string[] stableIds)
        {
            var result = new PsdHierarchyReconciliationResult { requiresReplan = true };
            result.focusedInvalidatedScopeStableIds.AddRange(stableIds);
            return result;
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
