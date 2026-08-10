namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdHierarchyPlanFailureRegressionTests
    {
        [Serializable]
        private sealed class FailureFixture
        {
            public string error = string.Empty;
            public string expectedCategory = string.Empty;
            public string expectedState = string.Empty;
            public string[] expectedCandidateIds = Array.Empty<string>();
            public string[] expectedNodeIds = Array.Empty<string>();
        }

        [TestCase("family-002-invalid-semantic-name")]
        [TestCase("variant-source-sibling-conflict")]
        public void RealFailureIsConvertedIntoAResolvableWorkspace(string fixtureName)
        {
            string assetPath =
                "Assets/PSDLayoutTool2/Editor/Tests/Fixtures/HierarchyPlanWorkspaces/" + fixtureName + ".json";
            TextAsset fixtureAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            Assert.That(fixtureAsset, Is.Not.Null, "Missing regression fixture: " + assetPath);

            FailureFixture fixture = JsonUtility.FromJson<FailureFixture>(fixtureAsset.text);
            Assert.That(Enum.TryParse(fixture.expectedCategory, out PsdHierarchyPlanIssueCategory category), Is.True);
            Assert.That(Enum.TryParse(fixture.expectedState, out PsdHierarchyPlanWorkspaceState expectedState), Is.True);

            PsdHierarchyPlanWorkspace workspace =
                PsdHierarchyChatCleanupExecution.CreateIssueWorkspace(
                    null,
                    "review remains available",
                    "raw reply remains available",
                    category,
                    fixture.error);

            Assert.That(workspace, Is.Not.Null);
            Assert.That(workspace.state, Is.EqualTo(expectedState));
            Assert.That(workspace.reviewText, Is.EqualTo("review remains available"));
            Assert.That(workspace.issues.Single().technicalDetails, Is.EqualTo(fixture.error));
            Assert.That(workspace.issues.Single().candidateIds, Is.EqualTo(fixture.expectedCandidateIds));
            Assert.That(workspace.issues.Single().affectedNodeIds, Is.EqualTo(fixture.expectedNodeIds));
            Assert.That(workspace.TryGetEnabledPlan(out _), Is.False);
            Assert.That(workspace.TryKeepOriginalStructure(workspace.issues.Single().id), Is.True);
            Assert.That(
                workspace.issues.Single().state,
                Is.EqualTo(PsdHierarchyPlanIssueState.KeptUnchanged));
        }
    }
}
