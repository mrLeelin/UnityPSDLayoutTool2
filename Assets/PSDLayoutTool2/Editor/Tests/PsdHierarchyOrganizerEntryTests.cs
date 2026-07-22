namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class PsdHierarchyOrganizerEntryTests
    {
        private const string SourcePath = "Assets/UI/Daily.psd";
        private const string SourceGuid = "0123456789abcdef0123456789abcdef";

        [TearDown]
        public void TearDown()
        {
            PsdHierarchyPendingOperation.Clear();
        }

        [Test]
        public void AvailabilityUsesOnlyTheConfiguredTarget()
        {
            var probed = new List<string>();
            string target;
            string explanation;

            bool available = PsdHierarchyOrganizerEntry.TryResolveAvailability(
                SourcePath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                "Generated",
                PsdImporter.PrefabOutputMode.InsideOutputFolder,
                true,
                path =>
                {
                    probed.Add(path);
                    return path == "Assets/UI/Generated/Daily.prefab";
                },
                out target,
                out explanation);

            Assert.That(available, Is.True);
            Assert.That(target, Is.EqualTo("Assets/UI/Generated/Daily.prefab"));
            Assert.That(probed, Is.EqualTo(new[] { target }));
            Assert.That(explanation, Is.Empty);
        }

        [Test]
        public void MissingConfiguredTargetReturnsActionableMessageWithoutSearching()
        {
            var probed = new List<string>();
            string target;
            string explanation;

            bool available = PsdHierarchyOrganizerEntry.TryResolveAvailability(
                SourcePath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                true,
                path =>
                {
                    probed.Add(path);
                    return false;
                },
                out target,
                out explanation);

            Assert.That(available, Is.False);
            Assert.That(target, Is.EqualTo("Assets/UI/Daily.prefab"));
            Assert.That(probed, Is.EqualTo(new[] { target }));
            Assert.That(explanation, Does.Contain(target));
            Assert.That(explanation, Does.Contain("生成预制体"));
        }

        [Test]
        public void SceneObjectModeIsDisabledWithClearExplanation()
        {
            string target;
            string explanation;

            bool available = PsdHierarchyOrganizerEntry.TryResolveAvailability(
                SourcePath,
                PsdImporter.OutputDirectoryMode.PsdDirectory,
                string.Empty,
                PsdImporter.PrefabOutputMode.SiblingToOutputFolder,
                false,
                _ => true,
                out target,
                out explanation);

            Assert.That(available, Is.False);
            Assert.That(explanation, Does.Contain("Unity UI"));
        }

        [Test]
        public void InputCreationClonesProfileAndDoesNotMutatePersistedState()
        {
            PsdPrefabDocumentModel document = CreateDocument("native:11");
            PsdHierarchyProfile persisted = PsdHierarchyProfile.Create(
                document,
                Array.Empty<PsdHierarchyProfileGroup>(),
                Array.Empty<PsdHierarchyProfileRename>(),
                SourceGuid);
            persisted.sourceContentFingerprint = "persisted-content";

            try
            {
                PsdHierarchyOrganizerInput input = PsdHierarchyOrganizerEntry.BuildReadOnlyInput(
                    SourcePath,
                    SourceGuid,
                    "Assets/UI/Daily.prefab",
                    document,
                    Array.Empty<PsdHierarchyPrefabNodeMetadata>(),
                    persisted,
                    new NeverRunAiRunner());

                Assert.That(input, Is.Not.Null);
                Assert.That(input.previewModel, Is.Not.Null);
                Assert.That(input.targetPrefabPath, Is.EqualTo("Assets/UI/Daily.prefab"));
                Assert.That(persisted.sourceContentFingerprint, Is.EqualTo("persisted-content"));
                Assert.That(persisted.groups, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(persisted);
            }
        }

        [Test]
        public void PendingOperationIsExactKeyedOneShotAndStoresAClone()
        {
            PsdHierarchyPlan source = CreatePlan();

            PsdHierarchyPendingOperation.Enqueue(SourceGuid, SourcePath, "Assets/UI/Daily.prefab", source);
            source.groups[0].displayName = "changed-after-enqueue";

            PsdHierarchyPlan ignored;
            Assert.That(PsdHierarchyPendingOperation.TryTake(SourceGuid, "Assets/UI/Other.prefab", out ignored), Is.False);
            Assert.That(PsdHierarchyPendingOperation.TryTake(SourceGuid, "Assets/UI/Daily.prefab", out ignored), Is.True);
            Assert.That(ignored.groups[0].displayName, Is.EqualTo("Group"));
            Assert.That(PsdHierarchyPendingOperation.TryTake(SourceGuid, "Assets/UI/Daily.prefab", out ignored), Is.False);
        }

        [Test]
        public void ExplicitApplySelectionUpdatesExactPrefabWithoutSiblingOrDeletes()
        {
            const string exactTarget = "C:/Project/Assets/UI/Generated/Daily.prefab";
            const string siblingPrefab = "C:/Project/Assets/UI/Daily.prefab";
            const string generatedTexture = "C:/Project/Assets/UI/Generated/Leaf.png";
            const string staleTexture = "C:/Project/Assets/UI/Generated/Stale.png";
            PsdHierarchyPendingOperation.Enqueue(
                SourceGuid, SourcePath, "Assets/UI/Generated/Daily.prefab", CreatePlan());

            PsdHierarchyExplicitImportSelection selection =
                PsdImporter.CreateExplicitHierarchyApplySelection(
                    new[] { exactTarget, siblingPrefab, generatedTexture },
                    new[] { staleTexture },
                    exactTarget);

            Assert.That(PsdHierarchyPendingOperation.HasMatch(
                SourceGuid, "Assets/UI/Generated/Daily.prefab"), Is.True);
            Assert.That(selection.ShouldUpdate(exactTarget), Is.True,
                "The explicit target must reach the incremental Prefab save seam.");
            Assert.That(selection.ShouldUpdate(generatedTexture), Is.True);
            Assert.That(selection.ShouldUpdate(siblingPrefab), Is.False);
            Assert.That(selection.PathsToDelete, Is.Empty);
        }

        [Test]
        public void ReplacingWindowContextInvokesOnlyLatestHandlerOnce()
        {
            PsdHierarchyOrganizerInput first = CreateInput("Assets/UI/First.prefab");
            PsdHierarchyOrganizerInput second = CreateInput("Assets/UI/Second.prefab");
            int firstCalls = 0;
            int secondCalls = 0;
            PsdHierarchyOrganizerWindow window = ScriptableObject.CreateInstance<PsdHierarchyOrganizerWindow>();
            try
            {
                window.ReplaceContext(first.previewModel, _ => firstCalls++);
                window.ReplaceContext(second.previewModel, _ => secondCalls++);
                window.DispatchApply(CreatePlan());

                Assert.That(firstCalls, Is.Zero);
                Assert.That(secondCalls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReopeningSameWindowDoesNotDoubleInvokeAndClearRemovesHandler()
        {
            PsdHierarchyOrganizerInput input = CreateInput("Assets/UI/Daily.prefab");
            int calls = 0;
            Action<PsdHierarchyPlan> handler = _ => calls++;
            PsdHierarchyOrganizerWindow window = ScriptableObject.CreateInstance<PsdHierarchyOrganizerWindow>();
            try
            {
                window.ReplaceContext(input.previewModel, handler);
                window.ReplaceContext(input.previewModel, handler);
                window.DispatchApply(CreatePlan());
                Assert.That(calls, Is.EqualTo(1));

                window.ClearContext();
                window.DispatchApply(CreatePlan());
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static PsdHierarchyOrganizerInput CreateInput(string targetPath)
        {
            return PsdHierarchyOrganizerEntry.BuildReadOnlyInput(
                SourcePath,
                SourceGuid,
                targetPath,
                CreateDocument("native:11"),
                Array.Empty<PsdHierarchyPrefabNodeMetadata>(),
                null,
                new NeverRunAiRunner());
        }

        private static PsdPrefabDocumentModel CreateDocument(string stableId)
        {
            return new PsdPrefabDocumentModel
            {
                width = 100,
                height = 100,
                resolution = 72f,
                sourceFingerprint = "source",
                nodes = new List<PsdPrefabNodeModel>
                {
                    new PsdPrefabNodeModel
                    {
                        stableId = stableId,
                        name = "Leaf",
                        siblingIndex = 0,
                        bounds = new Rect(0f, 0f, 10f, 10f)
                    }
                }
            };
        }

        private static PsdHierarchyPlan CreatePlan()
        {
            return new PsdHierarchyPlan
            {
                schemaVersion = PsdHierarchyPlan.CurrentSchemaVersion,
                sourcePsdGuid = SourceGuid,
                sourceFingerprint = "source",
                contentFingerprint = "content",
                structureFingerprint = "structure",
                geometryFingerprint = "geometry",
                groups = new List<PsdHierarchyPlanGroup>
                {
                    new PsdHierarchyPlanGroup
                    {
                        key = "group",
                        displayName = "Group",
                        memberStableIds = new List<string> { "native:11" },
                        evidence = "test",
                        confidence = 1d
                    }
                }
            };
        }

        private sealed class NeverRunAiRunner : IPsdHierarchyAiRunner
        {
            public System.Threading.Tasks.Task<PsdHierarchyAiRunResult> RunAsync(
                PsdHierarchyAiRunRequest request,
                System.Threading.CancellationToken cancellationToken)
            {
                throw new AssertionException("Opening the preview must not run the planner.");
            }
        }
    }
}
