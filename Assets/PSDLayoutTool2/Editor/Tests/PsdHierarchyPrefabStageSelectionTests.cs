namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    public sealed class PsdHierarchyPrefabStageSelectionTests
    {
        [Test]
        public void ResolveStageTargets_MapsUniqueProfilePathsToStageObjects()
        {
            CollectionAssert.AreEqual(
                new[] { "Reward", "DayOne" },
                PsdHierarchyPrefabStageSelection.ResolveStageTargets(
                    new[] { "Root/Reward", "Root/DayOne" },
                    new[] { "Root", "Root/DayOne", "Root/Reward" },
                    new[] { "Root", "DayOne", "Reward" },
                    System.StringComparer.Ordinal));
        }

        [Test]
        public void ResolveStageTargets_RejectsAmbiguousPaths()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                PsdHierarchyPrefabStageSelection.ResolveStageTargets(
                    new[] { "Root/Day" },
                    new[] { "Root", "Root/Day", "Root/Day" },
                    new[] { "Root", "First", "Second" },
                    System.StringComparer.Ordinal));
        }
    }
}
