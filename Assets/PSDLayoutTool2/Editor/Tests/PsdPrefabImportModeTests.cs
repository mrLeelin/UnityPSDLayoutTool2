namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

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
    }
}
