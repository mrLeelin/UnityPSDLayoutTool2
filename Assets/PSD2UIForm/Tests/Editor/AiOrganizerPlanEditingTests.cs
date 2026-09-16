using System;
using System.Linq;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;

namespace Psd2UIForm.Tests
{
    public class AiOrganizerPlanEditingTests
    {
        static AiAnalysisPackageDocument Package()
        {
            var package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "editing" };
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:1", name = "First", parentId = "root", layerType = "LayerGroup", uiType = "Null" });
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:2", name = "Second", parentId = "root", layerType = "LayerGroup", uiType = "Null", siblingIndex = 1 });
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:3", name = "Icon", parentId = "psd:1", layerType = "Layer", uiType = "Image" });
            return package;
        }

        static AiPatchDocument Patch(AiAnalysisPackageDocument package)
        {
            var patch = new AiPatchDocument { version = "2.0", treeHash = package.treeHash };
            foreach (var node in package.nodes)
                patch.analysis.Add(new AiAuditEntry { targetId = node.id, currentUIType = node.uiType, predictedUIType = node.uiType, verdict = "correct", confidence = 1, reason = "Current type" });
            return patch;
        }

        [Test] public void EditMovesAndRenamesWithoutMutatingPreviousVersion()
        {
            var package = Package(); var original = Patch(package);
            var edited = AiOrganizerPlanEditing.Edit(package, original, "psd:3", "RewardIcon", "psd:2", 0);
            var node = AiOrganizerPlanEditing.Tree(package, edited).Single(n => n.Id == "psd:3");
            Assert.That(node.Parent, Is.EqualTo("psd:2"));
            Assert.That(node.Name, Is.EqualTo("RewardIcon"));
            Assert.That(node.Depth, Is.EqualTo(1));
            Assert.That(original.operations, Is.Empty);
            var revised = AiOrganizerPlanEditing.Edit(package, edited, "psd:3", "RewardIcon", "psd:1", 0);
            Assert.That(revised.operations.Count(o => o.op == "move_node"), Is.EqualTo(1));
        }

        [Test] public void InvalidParentCycleDoesNotDestroyPreviousVersion()
        {
            var package = Package(); var patch = Patch(package);
            Assert.Throws<InvalidOperationException>(() => AiOrganizerPlanEditing.Edit(package, patch, "psd:1", "First", "psd:3", 0));
            Assert.That(patch.operations, Is.Empty);
        }

        [Test] public void ReorderShowsTheNewSiblingOrder()
        {
            var package = Package();
            var patch = AiOrganizerPlanEditing.Edit(package, Patch(package), "psd:2", "Second", "root", 0);
            Assert.That(AiOrganizerPlanEditing.Tree(package, patch).Where(n => n.Parent == "root").Select(n => n.Id), Is.EqualTo(new[] { "psd:2", "psd:1" }));
        }

        [Test] public void FailedOrCancelledRevisionKeepsCurrentPlan()
        {
            var window = UnityEngine.ScriptableObject.CreateInstance<AiOrganizerWindow>();
            try
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var package = Package(); var patch = Patch(package);
                typeof(AiOrganizerWindow).GetField("_patch", flags).SetValue(window, patch);
                var job = new AiJobContext { JobId = "revision-test" };
                typeof(AiOrganizerWindow).GetField("_job", flags).SetValue(window, job);
                typeof(AiOrganizerWindow).GetField("_running", flags).SetValue(window, true);
                window.OnJobFailed(job, "Controlled failure");
                Assert.That(typeof(AiOrganizerWindow).GetField("_patch", flags).GetValue(window), Is.SameAs(patch));
                // A completion arriving after cancellation must not replace the accepted plan.
                typeof(AiOrganizerWindow).GetField("_job", flags).SetValue(window, null);
                window.OnJobCompleted(job);
                Assert.That(typeof(AiOrganizerWindow).GetField("_patch", flags).GetValue(window), Is.SameAs(patch));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test] public void FeedbackCarriesCurrentEditsAndRequestsCompleteReplacement()
        {
            var package = Package();
            var patch = AiOrganizerPlanEditing.Edit(package, Patch(package), "psd:3", "RewardIcon", "psd:2", 0);
            string prompt = AiOrganizerPlanEditing.RevisionInstructions(patch, "Keep the icon in the second group");
            Assert.That(prompt, Does.Contain("RewardIcon").And.Contain("Keep the icon in the second group").And.Contain("COMPLETE replacement"));
            Assert.Throws<InvalidOperationException>(() => AiOrganizerPlanEditing.RevisionInstructions(patch, " "));
        }
    }
}
