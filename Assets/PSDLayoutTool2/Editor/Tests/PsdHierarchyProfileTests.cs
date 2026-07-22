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
        public void NestedParentKeySurvivesCreateAndIncrementalReconcile()
        {
            PsdPrefabDocumentModel document = Document(
                Node("101", "A", 0, Rect.zero, "a"),
                Node("102", "B", 1, Rect.zero, "b"));
            PsdHierarchyProfile profile = PsdHierarchyProfile.Create(
                document,
                new[]
                {
                    new PsdHierarchyProfileGroup { key = "root-group", stableLayerIds = new List<string> { "101" } },
                    new PsdHierarchyProfileGroup { key = "child-group", parentKey = "root-group", stableLayerIds = new List<string> { "102" } }
                },
                null,
                "guid-123");
            byte[] childPlan = profile.groups[1].GetPlanBytes();

            profile.Reconcile(document);
            profile.Reconcile(document);

            Assert.That(profile.groups[1].parentKey, Is.EqualTo("root-group"));
            Assert.That(profile.groups[1].GetPlanBytes(), Is.EqualTo(childPlan));
            Assert.That(System.Text.Encoding.UTF8.GetString(childPlan), Does.Contain("root-group"));
        }

        [Test]
        public void NestedParentKeySurvivesAssetDatabaseRoundTrip()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/__PsdHierarchyNestedProfileTest_" + System.Guid.NewGuid().ToString("N") + ".asset");
            PsdHierarchyProfile profile = null;
            PsdHierarchyProfile loaded = null;
            try
            {
                PsdPrefabDocumentModel document = Document(
                    Node("101", "A", 0, Rect.zero, "a"),
                    Node("102", "B", 1, Rect.zero, "b"));
                profile = PsdHierarchyProfile.Create(
                    document,
                    new[]
                    {
                        new PsdHierarchyProfileGroup { key = "root-group", stableLayerIds = new List<string> { "101" } },
                        new PsdHierarchyProfileGroup { key = "child-group", parentKey = "root-group", stableLayerIds = new List<string> { "102" } }
                    },
                    null,
                    "guid-123");
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssetIfDirty(profile);
                Resources.UnloadAsset(profile);
                profile = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                loaded = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(path);

                Assert.That(loaded.groups[1].parentKey, Is.EqualTo("root-group"));
                Assert.That(System.Text.Encoding.UTF8.GetString(loaded.groups[1].GetPlanBytes()), Does.Contain("root-group"));
            }
            finally
            {
                if (loaded != null)
                {
                    Resources.UnloadAsset(loaded);
                }

                if (profile != null && AssetDatabase.Contains(profile))
                {
                    Resources.UnloadAsset(profile);
                }

                AssetDatabase.DeleteAsset(path);
            }
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

        [Test]
        public void TruthFingerprintsUseSha256AndChangeWithTheirRealInputs()
        {
            PsdPrefabNodeModel original = Node("101", "A", 0, new Rect(0, 0, 10, 10),
                PsdHierarchyFingerprints.Asset(new[] { new KeyValuePair<short, byte[]>(0, new byte[] { 1, 2, 3 }) }));
            PsdPrefabNodeModel changedContent = Node("101", "A", 0, new Rect(0, 0, 10, 10),
                PsdHierarchyFingerprints.Asset(new[] { new KeyValuePair<short, byte[]>(0, new byte[] { 1, 2, 4 }) }));
            PsdPrefabNodeModel changedStructure = Node("101", "A", 1, new Rect(0, 0, 10, 10), original.assetFingerprint);
            PsdPrefabNodeModel changedGeometry = Node("101", "A", 0, new Rect(0, 0, 11, 10), original.assetFingerprint);

            string asset = original.assetFingerprint;
            string content = PsdHierarchyFingerprints.Content(original);
            string structure = PsdHierarchyFingerprints.Structure(original);
            string geometry = PsdHierarchyFingerprints.Geometry(original);
            string document = PsdHierarchyFingerprints.Document(Document(original));

            Assert.That(new[] { asset, content, structure, geometry, document }.All(value => value.Length == 64), Is.True);
            Assert.That(PsdHierarchyFingerprints.Content(changedContent), Is.Not.EqualTo(content));
            Assert.That(PsdHierarchyFingerprints.Structure(changedStructure), Is.Not.EqualTo(structure));
            Assert.That(PsdHierarchyFingerprints.Geometry(changedGeometry), Is.Not.EqualTo(geometry));
            Assert.That(PsdHierarchyFingerprints.Document(Document(original)), Is.EqualTo(document));
            Assert.That(PsdHierarchyFingerprints.Document(Document(changedContent)), Is.Not.EqualTo(document));
        }

        [Test]
        public void ContentOnlyReconcileAdvancesAcceptedSourceFingerprint()
        {
            PsdPrefabDocumentModel original = Document(Node("101", "A", 0, Rect.zero, "pixels-a"));
            original.sourceFingerprint = PsdHierarchyFingerprints.Document(original);
            PsdHierarchyProfile profile = Profile(original, "101");
            PsdPrefabDocumentModel changed = Document(Node("101", "Renamed", 0, Rect.zero, "pixels-b"));
            changed.sourceFingerprint = PsdHierarchyFingerprints.Document(changed);

            PsdHierarchyReconciliationResult first = profile.Reconcile(changed);
            PsdHierarchyReconciliationResult repeated = profile.Reconcile(changed);

            Assert.That(first.contentOnlyStableIds, Is.EqualTo(new[] { "101" }));
            Assert.That(profile.sourceFingerprint, Is.EqualTo(changed.sourceFingerprint));
            Assert.That(profile.IsStale("guid-123", changed.sourceFingerprint), Is.False);
            Assert.That(repeated.contentOnlyStableIds, Is.Empty);
        }

        [Test]
        public void PendingGeometryDoesNotAdvanceAcceptedSourceFingerprint()
        {
            PsdPrefabDocumentModel original = Document(Node("101", "A", 0, new Rect(0, 0, 10, 10), "pixels"));
            original.sourceFingerprint = PsdHierarchyFingerprints.Document(original);
            PsdHierarchyProfile profile = Profile(original, "101");
            PsdPrefabDocumentModel changed = Document(Node("101", "A", 0, new Rect(0, 0, 20, 10), "pixels"));
            changed.sourceFingerprint = PsdHierarchyFingerprints.Document(changed);

            profile.Reconcile(changed);

            Assert.That(profile.sourceFingerprint, Is.EqualTo(original.sourceFingerprint));
            Assert.That(profile.IsStale("guid-123", changed.sourceFingerprint), Is.True);
        }

        [Test]
        public void MissingGeneratedRecordIdentityPlanMembershipAndRenameRemainPending()
        {
            PsdPrefabDocumentModel original = Document(
                Node("101", "A", 0, Rect.zero, "a"),
                Node("102", "B", 1, Rect.zero, "b"));
            original.sourceFingerprint = PsdHierarchyFingerprints.Document(original);
            PsdHierarchyProfile profile = Profile(original, "101", "102");
            profile.nodes[1].ownership = PsdHierarchyNodeOwnership.Generated;
            profile.nodes[1].localFileId = 7654L;
            profile.nodes[1].lastKnownPath = "Root/B";
            profile.renames.Add(new PsdHierarchyProfileRename { stableId = "102", name = "B Kept" });
            PsdPrefabDocumentModel current = Document(Node("101", "A", 0, Rect.zero, "a"));
            current.sourceFingerprint = PsdHierarchyFingerprints.Document(current);

            PsdHierarchyReconciliationResult result = profile.Reconcile(current);

            Assert.That(result.pendingMissingStableIds, Is.EqualTo(new[] { "102" }));
            Assert.That(profile.nodes.Single(node => node.stableId == "102").localFileId, Is.EqualTo(7654L));
            Assert.That(profile.groups.Single().stableLayerIds, Does.Contain("102"));
            Assert.That(profile.renames.Any(rename => rename.stableId == "102"), Is.True);
            Assert.That(profile.sourceFingerprint, Is.EqualTo(original.sourceFingerprint));
        }

        [Test]
        public void ImporterSessionClassifiesNativeNodesFromActualEmissionRegistry()
        {
            PsdPrefabDocumentModel document = Document(
                Node("101", "Visible", 0, Rect.zero, "a"),
                Node("102", "Hidden", 1, Rect.zero, "b"));
            PsdHierarchyProfile profile = Profile(document, "101");

            profile.UpdateImporterOwnership(document, new[] { "101" });

            Assert.That(profile.nodes.Single(node => node.stableId == "101").ownership,
                Is.EqualTo(PsdHierarchyNodeOwnership.Generated));
            Assert.That(profile.nodes.Single(node => node.stableId == "102").ownership,
                Is.EqualTo(PsdHierarchyNodeOwnership.NotEmitted));
        }

        [Test]
        public void NullSerializedCollectionsAreNormalizedWithoutNullReference()
        {
            PsdHierarchyProfile profile = Profile(Document(Node("101", "A", 0, Rect.zero, "a")), "101");
            profile.nodes = null;
            profile.groups = null;
            profile.renames = null;
            var corruptedDocument = new PsdPrefabDocumentModel { nodes = null };

            Assert.DoesNotThrow(() => profile.Reconcile(corruptedDocument));
            Assert.That(profile.nodes, Is.Not.Null);
            Assert.That(profile.groups, Is.Not.Null);
            Assert.That(profile.renames, Is.Not.Null);
        }

        [Test]
        public void ScriptableProfileSurvivesRealAssetDatabaseRoundTrip()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/__PsdHierarchyProfileTest_" + System.Guid.NewGuid().ToString("N") + ".asset");
            PsdHierarchyProfile profile = null;
            PsdHierarchyProfile loaded = null;
            bool assetCreated = false;
            bool cleanupSucceeded = true;
            try
            {
                PsdPrefabDocumentModel document = Document(Node("101", "A", 0, Rect.zero, "pixels"));
                document.sourceFingerprint = PsdHierarchyFingerprints.Document(document);
                profile = Profile(document, "101");
                AssetDatabase.CreateAsset(profile, path);
                assetCreated = true;
                AssetDatabase.SaveAssetIfDirty(profile);

                // This value exists only in memory and is deliberately not marked
                // dirty. A cached-object load would retain it; a disk import must
                // restore the value saved immediately above.
                profile.sourcePsdGuid = "memory-only-unsaved-sentinel";
                Assert.That(EditorUtility.IsDirty(profile), Is.False);

                // Unload the exact created object before importing again. Loading
                // without this step can return Unity's in-memory cache and would
                // not prove that ScriptableObject fields survived serialization.
                Resources.UnloadAsset(profile);
                profile = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                loaded = AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(path);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.sourcePsdGuid, Is.EqualTo("guid-123"));
                Assert.That(loaded.sourcePsdGuid, Is.Not.EqualTo("memory-only-unsaved-sentinel"));
                Assert.That(loaded.sourceFingerprint, Is.EqualTo(document.sourceFingerprint));
                Assert.That(loaded.groups[0].stableLayerIds, Is.EqualTo(new[] { "101" }));
                Assert.That(loaded.nodes[0].ownership, Is.EqualTo(PsdHierarchyNodeOwnership.Unknown));
                Assert.That(loaded.CheckSchema().status, Is.EqualTo(PsdHierarchyProfileSchemaStatus.Current));
            }
            finally
            {
                if (loaded != null)
                {
                    Resources.UnloadAsset(loaded);
                }

                if (profile != null)
                {
                    if (AssetDatabase.Contains(profile))
                    {
                        Resources.UnloadAsset(profile);
                    }
                    else
                    {
                        Object.DestroyImmediate(profile);
                    }
                }

                if (assetCreated)
                {
                    cleanupSucceeded = AssetDatabase.DeleteAsset(path);
                }
            }

            Assert.That(cleanupSucceeded, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<PsdHierarchyProfile>(path), Is.Null);
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
