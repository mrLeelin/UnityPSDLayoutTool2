namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebCreatePrefabsTests
    {
        [Test]
        public async Task ConfirmedCandidatesAreCreatedOnMainThreadThenSessionCompletes()
        {
            var dispatcher = new RecordingMainThread();
            string receivedPath = null;
            IReadOnlyList<PsdHierarchyWebPrefabCandidateDto> receivedCandidates = null;
            Action<string, IReadOnlyList<PsdHierarchyWebPrefabCandidateDto>> create = (path, candidates) =>
            {
                Assert.That(dispatcher.isInside, Is.True);
                receivedPath = path;
                receivedCandidates = candidates;
            };

            ConstructorInfo constructor = typeof(PsdHierarchyWebSession).GetConstructors()
                .SingleOrDefault(value => value.GetParameters().Length == 8);
            Assert.That(constructor, Is.Not.Null, "The session must own a Prefab creation handler.");
            using (var session = (PsdHierarchyWebSession)constructor.Invoke(new object[]
                   {
                       Guid.NewGuid().ToString("N"), "token", "guid", "Assets/Test.psd",
                       Path.Combine(Path.GetTempPath(), "PsdHierarchyWebCreatePrefabsTests", Guid.NewGuid().ToString("N")),
                       Model(), null, create
                   }))
            {
                session.RecordAppliedPrefab("Assets/Generated/Test.prefab");
                var controller = new PsdHierarchyWebController(dispatcher);
                MethodInfo method = typeof(PsdHierarchyWebController).GetMethod("CreatePrefabsAsync");
                Assert.That(method, Is.Not.Null, "The controller must expose the explicit Prefab confirmation phase.");

                await (Task)method.Invoke(controller, new object[]
                {
                    session,
                    new PsdHierarchyWebCreatePrefabsRequest
                    {
                        candidateIds = new List<string> { "candidate:card_a" }
                    }
                });

                Assert.That(receivedPath, Is.EqualTo("Assets/Generated/Test.prefab"));
                Assert.That(receivedCandidates, Has.Count.EqualTo(1));
                CollectionAssert.AreEquivalent(
                    new[] { "card_a", "card_b" },
                    receivedCandidates[0].instanceStableIds);
                PsdHierarchyWebSessionDto dto = await controller.GetSessionAsync(session);
                Assert.That(dto.phase, Is.EqualTo("complete"));
                Assert.That(dto.canCreatePrefabs, Is.False);
                Assert.That(dto.operation.status, Is.EqualTo(PsdHierarchyWebOperationStatus.Succeeded));
            }
        }

        private static PsdHierarchyOrganizerPreviewModel Model()
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
            request.nodes.Add(Node("card_a", string.Empty, "Image", true));
            request.nodes.Add(Node("title_a", "card_a", "Text", false));
            request.nodes.Add(Node("icon_a", "card_a", "Image", false));
            request.nodes.Add(Node("card_b", string.Empty, "Image", true));
            request.nodes.Add(Node("title_b", "card_b", "Text", false));
            request.nodes.Add(Node("icon_b", "card_b", "Image", false));
            var plan = new PsdHierarchyPlan
            {
                schemaVersion = 1,
                sourcePsdGuid = request.sourcePsdGuid,
                sourceFingerprint = request.sourceFingerprint,
                contentFingerprint = request.contentFingerprint,
                structureFingerprint = request.structureFingerprint,
                geometryFingerprint = request.geometryFingerprint
            };
            return new PsdHierarchyOrganizerPreviewModel(
                "Assets/Generated/Test.prefab", request, plan,
                new PsdHierarchyReconciliationResult(), new NeverRunRunner());
        }

        private static PsdHierarchyRequestNode Node(string id, string parentId, string kind, bool projectOwned)
        {
            return new PsdHierarchyRequestNode
            {
                stableId = id,
                parentStableId = parentId,
                originalName = id,
                kind = kind,
                hasProjectComponents = projectOwned,
                rectangle = new PsdHierarchyRectangle { width = 10, height = 10 }
            };
        }

        private sealed class NeverRunRunner : IPsdHierarchyAiRunner
        {
            public Task<PsdHierarchyAiRunResult> RunAsync(
                PsdHierarchyAiRunRequest request,
                CancellationToken cancellationToken)
            {
                throw new AssertionException("Prefab creation must not invoke AI.");
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
