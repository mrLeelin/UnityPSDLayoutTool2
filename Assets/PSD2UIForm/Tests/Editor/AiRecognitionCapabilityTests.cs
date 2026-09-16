using System.IO;
using System.Linq;
using AiPatchPlannerNamespace;
using AiPatchValidatorNamespace;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

namespace Psd2UIForm.Tests
{
    public class AiRecognitionCapabilityTests
    {
        [TestCase("Layer", "Image", "Panel")]
        [TestCase("Layer", "Image", "Text")]
        [TestCase("TextLayer", "Text", "Image")]
        public void IncompatibleBaseLabelFallsBackToSourceCapability(string layer, string current, string label)
        {
            var package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "capability" };
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:68", parentId = "root", name = "EmptyLayer", layerType = layer, uiType = current, isTextLayer = layer == "TextLayer" });
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:1", parentId = "root", name = "Icon", layerType = "Layer", uiType = "Image", siblingIndex = 1 });
            var combined = new AiRecognitionCombinedResultDocument { version = "2.0", treeHash = package.treeHash };
            combined.owners.Add(new AiRecognitionOwnerEntry { ownerId = "icon", ownerType = "Image", carrierNodeId = "psd:1", memberNodeIds = new[] { "psd:1" }, confidence = 1, reason = "Icon" });
            combined.nodeLabels.Add(new AiRecognitionNodeLabelEntry { nodeId = "psd:68", currentUIType = current, labelType = label, confidence = 1, reason = "Model label" });

            Assert.That(new AiPatchPlanner().TryBuildPatchFromCombinedRecognition(package, combined, out var patch, out var error), Is.True, error);
            Assert.That(new AiPatchValidator().ValidatePatch(patch, package, out error), Is.True, error);
            Assert.That(patch.operations.Any(o => o.op == "set_ui_type" && o.targetId == "psd:68"), Is.False);
        }

        [Test]
        public void BareOwnerReferenceResolvesToItsCompiledCarrier()
        {
            var package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "owner-reference" };
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:1", parentId = "root", name = "Icon", layerType = "Layer", uiType = "Image" });
            var combined = new AiRecognitionCombinedResultDocument { version = "2.0", treeHash = package.treeHash, organizerVersion = "1.0" };
            combined.owners.Add(new AiRecognitionOwnerEntry { ownerId = "owner_icon", ownerType = "Image", carrierNodeId = "psd:1", memberNodeIds = new[] { "psd:1" }, confidence = 1, reason = "Icon" });
            combined.renames.Add(new AiOrganizerRename { nodeId = "owner_icon", name = "RewardIcon" });

            Assert.That(new AiPatchPlanner().TryBuildPatchFromCombinedRecognition(package, combined, out var patch, out var error), Is.True, error);
            Assert.That(new AiPatchValidator().ValidatePatch(patch, package, out error), Is.True, error);
            Assert.That(patch.operations.Single(o => o.op == "rename_node").targetId, Is.EqualTo("psd:1"));
        }

        [Test]
        public void ParserNormalizesCapabilitiesButRetainsValidPanelAndNullLabels()
        {
            var package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "parser-capability" };
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:1", parentId = "root", name = "Image", layerType = "Layer", uiType = "Image" });
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:2", parentId = "root", name = "Panel", layerType = "LayerGroup", uiType = "Null", isGroupLayer = true, siblingIndex = 1 });
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:3", parentId = "root", name = "Empty", layerType = "Layer", uiType = "Image", siblingIndex = 2 });
            var combined = new AiRecognitionCombinedResultDocument { version = "2.0", treeHash = package.treeHash };
            combined.owners.Add(new AiRecognitionOwnerEntry { ownerId = "image", ownerType = "Image", carrierNodeId = "psd:1", memberNodeIds = new[] { "psd:1" }, confidence = 1 });
            combined.nodeLabels.Add(new AiRecognitionNodeLabelEntry { nodeId = "psd:1", labelType = "Panel", confidence = 1 });
            combined.nodeLabels.Add(new AiRecognitionNodeLabelEntry { nodeId = "psd:2", labelType = "Panel", confidence = 1 });
            combined.nodeLabels.Add(new AiRecognitionNodeLabelEntry { nodeId = "psd:3", labelType = "Null", confidence = 1 });
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(combined));
                var parser = new AiRecognitionResultParserNamespace.AiRecognitionResultParser();
                Assert.That(parser.TryLoadCombinedRecognitionResult(new AiJobContext { RecognitionCombinedPath = path }, package, out var normalized, out var error), Is.True, error);
                Assert.That(normalized.nodeLabels.Select(n => n.labelType), Is.EqualTo(new[] { "Image", "Panel", "Null" }));
                Assert.That(new AiPatchPlanner().TryBuildPatchFromCombinedRecognition(package, normalized, out var patch, out error), Is.True, error);
                Assert.That(new AiPatchValidator().ValidatePatch(patch, package, out error), Is.True, error);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void PanelOwnerStillGeneratesGroupAndResolvesRenameAndComponentReferences()
        {
            var package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "generated-panel" };
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:1", parentId = "root", name = "Icon", layerType = "Layer", uiType = "Image" });
            var combined = new AiRecognitionCombinedResultDocument { version = "2.0", treeHash = package.treeHash, organizerVersion = "1.0" };
            combined.owners.Add(new AiRecognitionOwnerEntry { ownerId = "panel", ownerType = "Panel", carrierNodeId = "psd:1", memberNodeIds = new[] { "psd:1" }, confidence = 1, reason = "Panel owner" });
            combined.renames.Add(new AiOrganizerRename { nodeId = "panel", name = "RewardPanel" });
            combined.components.Add(new AiOrganizerComponent { name = "Reward", mode = "same", rootIds = new[] { "panel" } });
            Assert.That(new AiPatchPlanner().TryBuildPatchFromCombinedRecognition(package, combined, out var patch, out var error), Is.True, error);
            Assert.That(new AiPatchValidator().ValidatePatch(patch, package, out error), Is.True, error);
            string target = patch.operations.Single(o => o.op == "rename_node").targetId;
            Assert.That(target, Does.StartWith("gen:owner:"));
            Assert.That(patch.components.Single().rootIds, Is.EqualTo(new[] { target }));
            Assert.That(patch.operations.Any(o => o.op == "set_ui_type" && o.targetId == "psd:1" && o.uiType == "Panel"), Is.False);
        }

        [TestCase("psd:1", "psd:1", true)]
        [TestCase("owner:psd:1", "psd:2", true)]
        [TestCase("unknown_owner", "unknown_owner", false)]
        public void OwnerAliasesDoNotStealSourceIdsOrAcceptUnknownReferences(string reference, string expected, bool valid)
        {
            var package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "reference-boundary" };
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:1", parentId = "root", name = "First", layerType = "Layer", uiType = "Image" });
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:2", parentId = "root", name = "Second", layerType = "Layer", uiType = "Image", siblingIndex = 1 });
            var combined = new AiRecognitionCombinedResultDocument { version = "2.0", treeHash = package.treeHash, organizerVersion = "1.0" };
            combined.owners.Add(new AiRecognitionOwnerEntry { ownerId = "psd:1", ownerType = "Image", carrierNodeId = "psd:2", memberNodeIds = new[] { "psd:2" }, confidence = 1, reason = "Second image" });
            combined.renames.Add(new AiOrganizerRename { nodeId = reference, name = "Renamed" });
            Assert.That(new AiPatchPlanner().TryBuildPatchFromCombinedRecognition(package, combined, out var patch, out var error), Is.True, error);
            Assert.That(patch.operations.Single(o => o.op == "rename_node").targetId, Is.EqualTo(expected));
            Assert.That(new AiPatchValidator().ValidatePatch(patch, package, out error), Is.EqualTo(valid), error);
        }

        [Test]
        public void RawIncompatiblePatchIsStillRejected()
        {
            var package = new AiAnalysisPackageDocument { version = "4.0", treeHash = "strict-validator" };
            package.nodes.Add(new AiAnalysisNodeEntry { id = "psd:68", parentId = "root", name = "Layer", layerType = "Layer", uiType = "Image" });
            var patch = new AiPatchDocument { version = "2.0", treeHash = package.treeHash };
            patch.analysis.Add(new AiAuditEntry { targetId = "psd:68", currentUIType = "Image", predictedUIType = "Image", verdict = "correct", confidence = 1, reason = "Image" });
            patch.operations.Add(new AiPatchOperation { op = "set_ui_type", targetId = "psd:68", uiType = "Panel", confidence = 1, reason = "Invalid label" });
            Assert.That(new AiPatchValidator().ValidatePatch(patch, package, out var error), Is.False);
            Assert.That(error, Does.Contain("is incompatible with layerType 'Layer'"));
        }
    }
}
