namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdPrefabImportModeTests
    {
        [Test]
        public void FullGenerationAlwaysSavesTheGeneratedCandidate()
        {
            Assert.That(
                PsdImporter.ResolvePrefabSaveRoute(
                    PsdImporter.PrefabImportMode.FullGenerate,
                    hasCleanupReplayProfile: true,
                    hasHierarchyProfile: true),
                Is.EqualTo(PsdImporter.PrefabSaveRoute.FullCandidateSave));
        }

        [Test]
        public void IncrementalUpdateSelectsTheAvailablePreservationRoute()
        {
            Assert.That(
                PsdImporter.ResolvePrefabSaveRoute(
                    PsdImporter.PrefabImportMode.IncrementalUpdate,
                    hasCleanupReplayProfile: true,
                    hasHierarchyProfile: true),
                Is.EqualTo(PsdImporter.PrefabSaveRoute.CleanupReplay));
            Assert.That(
                PsdImporter.ResolvePrefabSaveRoute(
                    PsdImporter.PrefabImportMode.IncrementalUpdate,
                    hasCleanupReplayProfile: false,
                    hasHierarchyProfile: true),
                Is.EqualTo(PsdImporter.PrefabSaveRoute.HierarchyMerge));
        }

        [Test]
        public void IncrementalUpdateWithoutAProfileIsRejected()
        {
            Assert.That(
                PsdImporter.ResolvePrefabSaveRoute(
                    PsdImporter.PrefabImportMode.IncrementalUpdate,
                    hasCleanupReplayProfile: false,
                    hasHierarchyProfile: false),
                Is.EqualTo(PsdImporter.PrefabSaveRoute.Rejected));
        }

        [Test]
        public void IncrementalButtonOnlyAppearsWhenAProfileIsAvailable()
        {
            Assert.That(PsdInspector.ShouldShowIncrementalUpdateButton(true), Is.True);
            Assert.That(PsdInspector.ShouldShowIncrementalUpdateButton(false), Is.False);
        }

        [Test]
        public void PingPrefabDoesNotChangeTheActiveInspectorSelection()
        {
            const string folder = "Assets/__PsdInspectorPingTests";
            const string prefabPath = folder + "/Example.prefab";
            Object previouslySelected = Selection.activeObject;
            var inspectorAnchor = new GameObject("InspectorAnchor");
            GameObject prefabRoot = new GameObject("Example");
            try
            {
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.CreateFolder("Assets", "__PsdInspectorPingTests");
                Assert.That(PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath), Is.Not.Null);
                Selection.activeObject = inspectorAnchor;

                Assert.That(PsdInspector.TryPingPrefab(prefabPath), Is.True);
                Assert.That(Selection.activeObject, Is.SameAs(inspectorAnchor));
            }
            finally
            {
                Selection.activeObject = previouslySelected;
                Object.DestroyImmediate(prefabRoot);
                Object.DestroyImmediate(inspectorAnchor);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
