namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;

    public sealed class PsdHierarchyPrefabCandidateAnalyzerTests
    {
        [Test]
        public void Analyze_ReportsReusableContainerWithEvidence()
        {
            var result = PsdHierarchyPrefabCandidateAnalyzer.Analyze(new[]
            {
                Node("card_a", "", "Image", true), Node("title_a", "card_a", "Text", false), Node("icon_a", "card_a", "Image", false),
                Node("card_b", "", "Image", true), Node("title_b", "card_b", "Text", false), Node("icon_b", "card_b", "Image", false)
            });

            PsdPrefabCandidate candidate = result.Find(value => value.rootStableId == "card_a");
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate.score, Is.GreaterThanOrEqualTo(0.70f));
            Assert.That(candidate.evidence, Does.Contain("repeated structure"));
        }

        private static PsdHierarchyRequestNode Node(string id, string parent, string kind, bool projectOwned)
        {
            return new PsdHierarchyRequestNode { stableId = id, parentStableId = parent, kind = kind, hasProjectComponents = projectOwned };
        }
    }
}
