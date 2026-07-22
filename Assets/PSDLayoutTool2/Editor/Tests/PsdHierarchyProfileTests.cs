namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using System.Text;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class PsdHierarchyProfileTests
    {
        [Test]
        public void NativeLayerRenameAndContentChangeReusePersistedPlan()
        {
            PsdPrefabDocumentModel original = Document(Node("101", "Old", 0, new Rect(0, 0, 10, 10), "pixels-a"));
            PsdHierarchyProfile profile = Profile(original, "101");
            byte[] originalPlan = profile.groups[0].planBytes.ToArray();

            PsdHierarchyReconciliationResult result = profile.Reconcile(
                Document(Node("101", "Renamed", 0, new Rect(0, 0, 10, 10), "pixels-b")));

            Assert.That(result.requiresReplan, Is.False);
            Assert.That(result.contentOnlyStableIds, Is.EqualTo(new[] { "101" }));
            Assert.That(profile.groups[0].stableLayerIds, Is.EqualTo(new[] { "101" }));
            Assert.That(profile.groups[0].planBytes, Is.EqualTo(originalPlan));
        }

        [Test]
        public void GeometryChangeRequestsValidationWithoutReplanningUnaffectedScopes()
        {
            PsdPrefabDocumentModel original = Document(
                Node("101", "A", 0, new Rect(0, 0, 10, 10), "a"),
                Node("102", "B", 1, new Rect(20, 0, 10, 10), "b"));
            PsdHierarchyProfile profile = Profile(original, "101", "102");
            byte[] originalPlan = profile.groups[0].planBytes.ToArray();

            PsdHierarchyReconciliationResult result = profile.Reconcile(Document(
                Node("101", "A", 0, new Rect(0, 0, 30, 10), "a"),
                Node("102", "B", 1, new Rect(20, 0, 10, 10), "b")));

            Assert.That(result.requiresReplan, Is.False);
            Assert.That(result.geometryValidationStableIds, Is.EqualTo(new[] { "101" }));
            Assert.That(result.focusedInvalidatedScopeStableIds, Is.Empty);
            Assert.That(profile.groups[0].planBytes, Is.EqualTo(originalPlan));
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

        private static PsdHierarchyProfile Profile(PsdPrefabDocumentModel document, params string[] members)
        {
            return PsdHierarchyProfile.Create(
                document,
                new[]
                {
                    new PsdHierarchyProfileGroup
                    {
                        displayName = "Main",
                        stableLayerIds = members.ToList(),
                        planBytes = Encoding.UTF8.GetBytes("unchanged-plan")
                    }
                },
                new[] { new PsdHierarchyProfileRename { stableId = members[0], name = "Readable" } });
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
