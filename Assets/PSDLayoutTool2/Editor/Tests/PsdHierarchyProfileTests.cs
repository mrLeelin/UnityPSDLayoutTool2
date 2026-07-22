namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdHierarchyProfileTests
    {
        [Test]
        public void NativeLayerRenameAndContentChangeReusePersistedPlan()
        {
            PsdPrefabDocumentModel original = Document(Node("101", "Old", 0, new Rect(0, 0, 10, 10), "pixels-a"));
            PsdHierarchyProfile profile = Profile(original, "101");
            byte[] originalPlan = profile.groups[0].GetPlanBytes();

            PsdHierarchyReconciliationResult result = profile.Reconcile(
                Document(Node("101", "Renamed", 0, new Rect(0, 0, 10, 10), "pixels-b")));

            Assert.That(result.requiresReplan, Is.False);
            Assert.That(result.contentOnlyStableIds, Is.EqualTo(new[] { "101" }));
            Assert.That(profile.groups[0].stableLayerIds, Is.EqualTo(new[] { "101" }));
            Assert.That(profile.groups[0].GetPlanBytes(), Is.EqualTo(originalPlan));
        }

        [Test]
        public void GeometryChangeRequestsValidationWithoutReplanningUnaffectedScopes()
        {
            PsdPrefabDocumentModel original = Document(
                Node("101", "A", 0, new Rect(0, 0, 10, 10), "a"),
                Node("102", "B", 1, new Rect(20, 0, 10, 10), "b"));
            PsdHierarchyProfile profile = Profile(original, "101", "102");
            byte[] originalPlan = profile.groups[0].GetPlanBytes();

            PsdHierarchyReconciliationResult result = profile.Reconcile(Document(
                Node("101", "A", 0, new Rect(0, 0, 30, 10), "a"),
                Node("102", "B", 1, new Rect(20, 0, 10, 10), "b")));

            Assert.That(result.requiresReplan, Is.False);
            Assert.That(result.geometryValidationStableIds, Is.EqualTo(new[] { "101" }));
            Assert.That(result.focusedInvalidatedScopeStableIds, Is.Empty);
            Assert.That(profile.groups[0].GetPlanBytes(), Is.EqualTo(originalPlan));
        }

        [Test]
        public void NewAndStructurallyChangedIdsInvalidateOnlyFocusedScopes()
        {
            PsdPrefabDocumentModel original = Document(
                Node("101", "A", 0, Rect.zero, "a"),
                Node("102", "B", 1, Rect.zero, "b"));
            PsdHierarchyProfile profile = Profile(original, "101", "102");

            PsdHierarchyReconciliationResult result = profile.Reconcile(Document(
                Node("101", "A", 1, Rect.zero, "a"),
                Node("102", "B", 0, Rect.zero, "b"),
                Node("103", "New", 2, Rect.zero, "c")));

            Assert.That(result.requiresReplan, Is.True);
            Assert.That(result.unsortedNewStableIds, Is.EqualTo(new[] { "103" }));
            Assert.That(result.focusedInvalidatedScopeStableIds, Is.EquivalentTo(new[] { "101", "102", "103" }));
            Assert.That(profile.groups[0].stableLayerIds, Is.EqualTo(new[] { "101", "102" }));

            PsdHierarchyReconciliationResult repeated = profile.Reconcile(Document(
                Node("101", "A", 1, Rect.zero, "a"),
                Node("102", "B", 0, Rect.zero, "b"),
                Node("103", "New", 2, Rect.zero, "c")));
            Assert.That(repeated.focusedInvalidatedScopeStableIds, Is.EquivalentTo(result.focusedInvalidatedScopeStableIds));
        }

        [Test]
        public void MissingIdsStayPendingUntilCleanupIsExplicitlyConfirmed()
        {
            PsdHierarchyProfile profile = Profile(
                Document(Node("101", "A", 0, Rect.zero, "a"), Node("102", "B", 1, Rect.zero, "b")),
                "101", "102");

            PsdHierarchyReconciliationResult pending = profile.Reconcile(
                Document(Node("101", "A", 0, Rect.zero, "a")));
            Assert.That(pending.pendingMissingStableIds, Is.EqualTo(new[] { "102" }));
            Assert.That(profile.groups[0].stableLayerIds, Does.Contain("102"));

            PsdHierarchyReconciliationResult cleaned = profile.Reconcile(
                Document(Node("101", "A", 0, Rect.zero, "a")), true);
            Assert.That(cleaned.pendingMissingStableIds, Is.Empty);
            Assert.That(profile.groups[0].stableLayerIds, Does.Not.Contain("102"));
        }

        [Test]
        public void ZeroLayerIdsAreUnstableAndCannotEnterPersistedPlan()
        {
            PsdStableLayerId first = PsdStableLayerIdUtility.Create(0U, "", 0, "Before");
            PsdStableLayerId renamed = PsdStableLayerIdUtility.Create(0U, "", 1, "After");
            PsdPrefabDocumentModel document = Document(
                Node(first.value, "Before", 0, Rect.zero, "a"),
                Node("101", "Stable", 1, Rect.zero, "b"));

            PsdHierarchyProfile profile = Profile(document, first.value, "101");
            PsdHierarchyReconciliationResult result = profile.Reconcile(Document(
                Node(renamed.value, "After", 0, Rect.zero, "a"),
                Node("101", "Stable", 1, Rect.zero, "b")));

            Assert.That(first.stability, Is.EqualTo(PsdStableLayerIdStability.FallbackUnstable));
            Assert.That(renamed.stability, Is.EqualTo(PsdStableLayerIdStability.FallbackUnstable));
            Assert.That(profile.groups[0].stableLayerIds, Is.EqualTo(new[] { "101" }));
            Assert.That(profile.renames.Any(item => item.stableId == first.value), Is.False);
            Assert.That(result.unsortedUnstableIds, Does.Contain(renamed.value));
        }

        [Test]
        public void GeneratedGroupKeysAreStableAndRepeatedReconciliationIsIdempotent()
        {
            PsdPrefabDocumentModel document = Document(Node("102", "B", 1, Rect.zero, "b"), Node("101", "A", 0, Rect.zero, "a"));
            string keyA = PsdHierarchyProfile.BuildGeneratedGroupKey(new[] { "102", "101" });
            string keyB = PsdHierarchyProfile.BuildGeneratedGroupKey(new[] { "101", "102" });
            PsdHierarchyProfile profile = Profile(document, "101", "102");

            profile.Reconcile(document);
            profile.Reconcile(document);

            Assert.That(keyA, Is.EqualTo(keyB));
            Assert.That(profile.groups.Select(group => group.key).Distinct().Count(), Is.EqualTo(profile.groups.Count));
            Assert.That(profile.nodes.Select(node => node.stableId).Distinct().Count(), Is.EqualTo(profile.nodes.Count));
        }

        [Test]
        public void PixelChannelBytesParticipateInAssetAndContentFingerprints()
        {
            string pixelsA = PsdHierarchyFingerprints.Asset(new[]
            {
                new KeyValuePair<short, byte[]>(0, new byte[] { 1, 2, 3 }),
                new KeyValuePair<short, byte[]>(1, new byte[] { 4, 5, 6 })
            });
            string pixelsB = PsdHierarchyFingerprints.Asset(new[]
            {
                new KeyValuePair<short, byte[]>(0, new byte[] { 1, 2, 9 }),
                new KeyValuePair<short, byte[]>(1, new byte[] { 4, 5, 6 })
            });
            PsdPrefabNodeModel node = Node("101", "Image", 0, Rect.zero, pixelsA);
            string contentA = PsdHierarchyFingerprints.Content(node);
            node.assetFingerprint = pixelsB;

            Assert.That(pixelsB, Is.Not.EqualTo(pixelsA));
            Assert.That(PsdHierarchyFingerprints.Content(node), Is.Not.EqualTo(contentA));
        }

        [Test]
        public void ProfileIsScriptableObjectWithSourceIdentityAndStaleDetection()
        {
            PsdPrefabDocumentModel document = Document(Node("101", "A", 0, Rect.zero, "a"));
            document.sourceFingerprint = "source-v1";
            PsdHierarchyProfile profile = Profile(document, "101");

            Assert.That(profile, Is.InstanceOf<ScriptableObject>());
            Assert.That(profile.sourcePsdGuid, Is.EqualTo("guid-123"));
            Assert.That(profile.sourceFingerprint, Is.EqualTo("source-v1"));
            Assert.That(profile.schemaVersion, Is.EqualTo(PsdHierarchyProfile.CurrentSchemaVersion));
            Assert.That(profile.IsStale("guid-123", "source-v1"), Is.False);
            Assert.That(profile.IsStale("guid-123", "source-v2"), Is.True);
        }

        [Test]
        public void SemanticDuplicateAndOverlappingGroupsAreNormalized()
        {
            PsdPrefabDocumentModel document = Document(
                Node("101", "A", 0, Rect.zero, "a"),
                Node("102", "B", 1, Rect.zero, "b"),
                Node("103", "C", 2, Rect.zero, "c"));
            PsdHierarchyProfile profile = PsdHierarchyProfile.Create(
                document,
                new[]
                {
                    new PsdHierarchyProfileGroup { key = "first", stableLayerIds = new List<string> { "101", "102" } },
                    new PsdHierarchyProfileGroup { key = "same-members", stableLayerIds = new List<string> { "102", "101" } },
                    new PsdHierarchyProfileGroup { key = "overlap", stableLayerIds = new List<string> { "102", "103" } }
                },
                null,
                "guid-123");

            profile.Reconcile(document);
            profile.Reconcile(document);

            Assert.That(profile.groups.Count, Is.EqualTo(2));
            Assert.That(profile.groups[0].stableLayerIds, Is.EqualTo(new[] { "101", "102" }));
            Assert.That(profile.groups[1].stableLayerIds, Is.EqualTo(new[] { "103" }));
            Assert.That(profile.groups.SelectMany(group => group.stableLayerIds).Distinct().Count(),
                Is.EqualTo(profile.groups.Sum(group => group.stableLayerIds.Count)));
        }

        [Test]
        public void ProfileRoundTripContainsOnlyValidatedStablePlanMembers()
        {
            PsdStableLayerId fallback = PsdStableLayerIdUtility.Create(0U, string.Empty, 0, "UnstableSecret");
            PsdHierarchyProfile profile = PsdHierarchyProfile.Create(
                Document(Node("101", "Stable", 0, Rect.zero, "a"), Node(fallback.value, "Unstable", 1, Rect.zero, "b")),
                new[] { new PsdHierarchyProfileGroup { stableLayerIds = new List<string> { "101", fallback.value } } },
                new[] { new PsdHierarchyProfileRename { stableId = fallback.value, name = "MustNotPersist" } },
                "guid-123");

            string json = EditorJsonUtility.ToJson(profile);
            PsdHierarchyProfile roundTrip = ScriptableObject.CreateInstance<PsdHierarchyProfile>();
            EditorJsonUtility.FromJsonOverwrite(json, roundTrip);

            Assert.That(json, Does.Not.Contain(fallback.value));
            Assert.That(json, Does.Not.Contain("MustNotPersist"));
            Assert.That(System.Text.Encoding.UTF8.GetString(roundTrip.groups[0].GetPlanBytes()), Does.Not.Contain(fallback.value));
            Assert.That(roundTrip.groups[0].stableLayerIds, Is.EqualTo(new[] { "101" }));
            Assert.That(roundTrip.sourcePsdGuid, Is.EqualTo("guid-123"));
        }

        [Test]
        public void ValidUniqueGroupKeySurvivesMembershipChanges()
        {
            PsdPrefabDocumentModel document = Document(
                Node("101", "A", 0, Rect.zero, "a"),
                Node("102", "B", 1, Rect.zero, "b"),
                Node("103", "C", 2, Rect.zero, "c"));
            PsdHierarchyProfile profile = PsdHierarchyProfile.Create(
                document,
                new[] { new PsdHierarchyProfileGroup { key = "main-root", stableLayerIds = new List<string> { "101", "102" } } },
                null,
                "guid-123");

            profile.groups[0].stableLayerIds.Add("103");
            profile.Reconcile(document);

            Assert.That(profile.groups[0].key, Is.EqualTo("main-root"));
        }

        [Test]
        public void ConflictingGroupKeysAreRepairedDeterministically()
        {
            PsdPrefabDocumentModel document = Document(
                Node("101", "A", 0, Rect.zero, "a"),
                Node("102", "B", 1, Rect.zero, "b"));
            PsdHierarchyProfile profile = PsdHierarchyProfile.Create(
                document,
                new[]
                {
                    new PsdHierarchyProfileGroup { key = "shared-key", stableLayerIds = new List<string> { "101" } },
                    new PsdHierarchyProfileGroup { key = "shared-key", stableLayerIds = new List<string> { "102" } }
                },
                null,
                "guid-123");
            string repairedKey = profile.groups[1].key;

            profile.Reconcile(document);
            profile.Reconcile(document);

            Assert.That(profile.groups[0].key, Is.EqualTo("shared-key"));
            Assert.That(repairedKey, Is.EqualTo(PsdHierarchyProfile.BuildGeneratedGroupKey(new[] { "102" })));
            Assert.That(profile.groups[1].key, Is.EqualTo(repairedKey));
            Assert.That(profile.groups.Select(group => group.key).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void NativeDocumentFingerprintIsOrderIndependentAndDetectsStaleContent()
        {
            PsdPrefabNodeModel nodeA = Node("101", "A", 0, new Rect(0, 0, 10, 10), "pixels-a");
            PsdPrefabNodeModel nodeB = Node("102", "B", 1, new Rect(10, 0, 10, 10), "pixels-b");
            PsdPrefabDocumentModel original = Document(nodeA, nodeB);
            PsdPrefabDocumentModel reorderedEnumeration = Document(nodeB, nodeA);
            original.sourceFingerprint = PsdHierarchyFingerprints.Document(original);
            reorderedEnumeration.sourceFingerprint = PsdHierarchyFingerprints.Document(reorderedEnumeration);
            PsdHierarchyProfile profile = Profile(original, "101", "102");

            PsdPrefabDocumentModel changed = Document(
                Node("101", "A", 0, new Rect(0, 0, 10, 10), "pixels-changed"),
                Node("102", "B", 1, new Rect(10, 0, 10, 10), "pixels-b"));
            changed.sourceFingerprint = PsdHierarchyFingerprints.Document(changed);

            Assert.That(original.sourceFingerprint, Is.Not.Empty);
            Assert.That(reorderedEnumeration.sourceFingerprint, Is.EqualTo(original.sourceFingerprint));
            Assert.That(profile.IsStale("guid-123", reorderedEnumeration.sourceFingerprint), Is.False);
            Assert.That(profile.IsStale("guid-123", changed.sourceFingerprint), Is.True);
        }

        [Test]
        public void SchemaCompatibilityDistinguishesCurrentOldAndFutureProfiles()
        {
            PsdHierarchyProfile profile = Profile(Document(Node("101", "A", 0, Rect.zero, "a")), "101");

            profile.schemaVersion = PsdHierarchyProfile.CurrentSchemaVersion;
            Assert.That(profile.CheckSchema().status, Is.EqualTo(PsdHierarchyProfileSchemaStatus.Current));
            Assert.That(profile.CheckSchema().canApply, Is.True);

            profile.schemaVersion = PsdHierarchyProfile.CurrentSchemaVersion - 1;
            Assert.That(profile.CheckSchema().status, Is.EqualTo(PsdHierarchyProfileSchemaStatus.RequiresRebuild));
            Assert.That(profile.CheckSchema().canApply, Is.False);

            profile.schemaVersion = PsdHierarchyProfile.CurrentSchemaVersion + 1;
            Assert.That(profile.CheckSchema().status, Is.EqualTo(PsdHierarchyProfileSchemaStatus.UnsupportedFuture));
            Assert.That(profile.CheckSchema().canApply, Is.False);
        }

        private static PsdHierarchyProfile Profile(PsdPrefabDocumentModel document, params string[] members)
        {
            return PsdHierarchyProfile.Create(
                document,
                new[]
                {
                    new PsdHierarchyProfileGroup
                    {
                        displayName = "Main",
                        stableLayerIds = members.ToList()
                    }
                },
                new[] { new PsdHierarchyProfileRename { stableId = members[0], name = "Readable" } },
                "guid-123");
        }

        private static PsdPrefabDocumentModel Document(params PsdPrefabNodeModel[] nodes)
        {
            var document = new PsdPrefabDocumentModel();
            document.nodes.AddRange(nodes);
            return document;
        }

        private static PsdPrefabNodeModel Node(string id, string name, int siblingIndex, Rect bounds, string assetFingerprint)
        {
            return new PsdPrefabNodeModel
            {
                stableId = id,
                parentStableId = string.Empty,
                siblingIndex = siblingIndex,
                name = name,
                kind = PsdPrefabNodeKind.Image,
                bounds = bounds,
                assetFingerprint = assetFingerprint,
                contentFingerprint = assetFingerprint
            };
        }
    }
}
