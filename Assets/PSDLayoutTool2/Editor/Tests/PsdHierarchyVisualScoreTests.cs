namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;
    using System.Linq;

    public sealed class PsdHierarchyVisualScoreTests
    {
        [Test]
        public void VisualScoreClampsBetween0And100()
        {
            var score1 = new PsdHierarchyVisualScore("n001", 150, "test");
            Assert.That(score1.similarityScore, Is.EqualTo(100));

            var score2 = new PsdHierarchyVisualScore("n002", -50, "test");
            Assert.That(score2.similarityScore, Is.EqualTo(0));

            var score3 = new PsdHierarchyVisualScore("n003", 85, "test");
            Assert.That(score3.similarityScore, Is.EqualTo(85));
        }

        [Test]
        public void VisualScoreRecommendedAndAcceptableThresholds()
        {
            var highScore = new PsdHierarchyVisualScore("n001", 90, "高度相似");
            Assert.That(highScore.IsRecommended, Is.True);
            Assert.That(highScore.IsAcceptable, Is.True);

            var mediumScore = new PsdHierarchyVisualScore("n002", 75, "中度相似");
            Assert.That(mediumScore.IsRecommended, Is.False);
            Assert.That(mediumScore.IsAcceptable, Is.True);

            var lowScore = new PsdHierarchyVisualScore("n003", 50, "差异较大");
            Assert.That(lowScore.IsRecommended, Is.False);
            Assert.That(lowScore.IsAcceptable, Is.False);
        }

        [Test]
        public void VisualAnalysisResultReturnsCorrectScores()
        {
            var scores = new[]
            {
                new PsdHierarchyVisualScore("n001", 95, "完全一致"),
                new PsdHierarchyVisualScore("n002", 85, "高度相似"),
                new PsdHierarchyVisualScore("n003", 70, "中度相似"),
                new PsdHierarchyVisualScore("n004", 50, "差异较大"),
            };

            var result = new PsdHierarchyVisualAnalysisResult(
                new[] { "n001", "n002" },
                scores,
                "test_v1");

            Assert.That(result.GetRecommendedNodeIds(), Is.EquivalentTo(new[] { "n001", "n002" }));
            Assert.That(result.GetAcceptableNodeIds(), Is.EquivalentTo(new[] { "n001", "n002", "n003" }));

            Assert.That(result.TryGetScore("n001", out var score1), Is.True);
            Assert.That(score1.similarityScore, Is.EqualTo(95));

            Assert.That(result.TryGetScore("n999", out var _), Is.False);
        }

        [Test]
        public void VisualAnalysisResultGetAllScoresReturnsAllScores()
        {
            var scores = new[]
            {
                new PsdHierarchyVisualScore("n001", 95, "test1"),
                new PsdHierarchyVisualScore("n002", 85, "test2"),
            };

            var result = new PsdHierarchyVisualAnalysisResult(
                new[] { "n001" },
                scores,
                "test_v1");

            var allScores = result.GetAllScores().ToArray();
            Assert.That(allScores.Length, Is.EqualTo(2));
        }
    }
}
