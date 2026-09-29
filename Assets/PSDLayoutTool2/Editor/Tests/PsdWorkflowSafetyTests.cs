namespace PsdLayoutTool2
{
    using System;
    using System.IO;
    using System.Text;
    using NUnit.Framework;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using TMPro;
    using UnityEngine;

    public sealed class PsdWorkflowSafetyTests
    {
        [Test]
        public void SelectionReadCannotWrite()
        {
            Assert.That(PsdWorkflowStateMachine.CanWrite(PsdWorkflowState.SelectionRead), Is.False);
            Assert.That(PsdWorkflowStateMachine.CanWrite(PsdWorkflowState.AwaitingApproval), Is.False);
        }

        [Test]
        public void AmbiguousConfirmationDoesNotApprove()
        {
            Assert.That(PsdWorkflowPlanBinding.IsExplicitApproval("确认"), Is.False);
            Assert.That(PsdWorkflowPlanBinding.IsExplicitApproval("可以，继续"), Is.False);
            Assert.That(PsdWorkflowPlanBinding.IsExplicitApproval(PsdWorkflowPlanBinding.ExplicitApprovalPhrase), Is.True);
        }

        [Test]
        public void TmpSubMeshNormalizesToTextRoot()
        {
            GameObject root = new GameObject("Diary", typeof(RectTransform), typeof(TextMeshProUGUI));
            GameObject child = new GameObject("TMP SubMeshUI", typeof(RectTransform), typeof(TMP_SubMeshUI));
            child.transform.SetParent(root.transform, false);
            try
            {
                PsdHierarchyNormalizedSelection result = PsdHierarchySelectionNormalizer.Normalize(child);
                Assert.That(result.changed, Is.True);
                Assert.That(result.normalized, Is.SameAs(root));
                Assert.That(result.original, Is.SameAs(child));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlanBindingRejectsMissingMetadataAndUnknownNode()
        {
            string snapshot = "{\"fingerprint\":\"snap-a\",\"nodes\":[{\"id\":\"root\"}]}";
            string plan = BuildPlan("snap-a", "node:missing", includeMetadata: true);
            PsdWorkflowApproval approval = BuildApproval(plan, "snap-a", "Assets/Diary.prefab");
            string review = Path.GetTempFileName();
            try
            {
                File.WriteAllText(review, "review v1");
                Assert.That(PsdWorkflowPlanBinding.TryValidate(plan, snapshot, "snap-a", "Assets/Diary.prefab",
                    "Assets/Diary.plan.json", review, approval, out string error), Is.False);
                Assert.That(error, Does.Contain("node:<id>"));
            }
            finally { File.Delete(review); }
        }

        [Test]
        public void PlanFingerprintChangeRejectsApplyV2()
        {
            string snapshot = "{\"fingerprint\":\"snap-b\",\"nodes\":[{\"id\":\"root\"}]}";
            string plan = BuildPlan("snap-a", "node:root", includeMetadata: true);
            PsdWorkflowApproval approval = BuildApproval(plan, "snap-a", "Assets/Diary.prefab");
            string review = Path.GetTempFileName();
            try
            {
                File.WriteAllText(review, "review v1");
                Assert.That(PsdWorkflowPlanBinding.TryValidate(plan, snapshot, "snap-b", "Assets/Diary.prefab",
                    "Assets/Diary.plan.json", review, approval, out string error), Is.False);
                Assert.That(error, Does.Contain("审批"));
            }
            finally { File.Delete(review); }
        }

        [Test]
        public void OnlyExplicitApprovalCreatesValidApprovalRecord()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "{}");
                Assert.That(PsdWorkflowPlanBinding.TryReadApproval(path, out _, out _), Is.False);
                File.WriteAllText(path, JsonConvert.SerializeObject(new PsdWorkflowApproval
                {
                    approvalText = PsdWorkflowPlanBinding.ExplicitApprovalPhrase,
                    planPath = "p",
                    planSha256 = "h",
                    snapshotFingerprint = "s",
                    targetPrefabPath = "t",
                }));
                Assert.That(PsdWorkflowPlanBinding.TryReadApproval(path, out _, out _), Is.True);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ProtocolTwoApprovalRecordIsAcceptedAndLegacyVersionIsRejected()
        {
            string path = Path.GetTempFileName();
            try
            {
                var approval = new PsdWorkflowApproval
                {
                    approvalText = PsdWorkflowPlanBinding.ExplicitApprovalPhrase,
                    planPath = "p",
                    planSha256 = "h",
                    snapshotFingerprint = "s",
                    targetPrefabPath = "t",
                };
                File.WriteAllText(path, JsonConvert.SerializeObject(approval));
                Assert.That(approval.version, Is.EqualTo(PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion));
                Assert.That(PsdWorkflowPlanBinding.TryReadApproval(path, out _, out string error), Is.True, error);

                approval.version = 1;
                File.WriteAllText(path, JsonConvert.SerializeObject(approval));
                Assert.That(PsdWorkflowPlanBinding.TryReadApproval(path, out _, out error), Is.False);
                Assert.That(error, Does.Contain("协议版本"));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ApplySentinelCreationRequiresExplicitApprovalAndIsOneShot()
        {
            string applyPath = Path.Combine(Path.GetTempPath(), "psd-safety-" + Guid.NewGuid().ToString("N") + ".apply");
            try
            {
                const string plan = "{\"reviewVersion\":\"review-a\"}";
                Assert.That(PsdWorkflowPlanBinding.TryCreateApplySentinel(
                    applyPath, "Assets/Diary.plan.json", plan, "snap-a", "review-a", "Assets/Diary.prefab", "确认", out _), Is.False);
                Assert.That(PsdWorkflowPlanBinding.TryCreateApplySentinel(
                    applyPath, "Assets/Diary.plan.json", plan, "snap-a", "review-a", "Assets/Diary.prefab",
                    PsdWorkflowPlanBinding.ExplicitApprovalPhrase, out _), Is.True);
                Assert.That(PsdWorkflowPlanBinding.TryCreateApplySentinel(
                    applyPath, "Assets/Diary.plan.json", plan, "snap-a", "review-a", "Assets/Diary.prefab",
                    PsdWorkflowPlanBinding.ExplicitApprovalPhrase, out _), Is.False);
            }
            finally { if (File.Exists(applyPath)) File.Delete(applyPath); }
        }

        [Test]
        public void EditedPlanCannotReuseApprovalHash()
        {
            string plan = BuildPlan("snap-a", "node:root", includeMetadata: true);
            JObject editedObject = JObject.Parse(plan);
            editedObject["expectedNodeCount"] = 2;
            string editedPlan = editedObject.ToString(Formatting.None);
            PsdWorkflowApproval approval = BuildApproval(plan, "snap-a", "Assets/Diary.prefab");
            string review = Path.GetTempFileName();
            try
            {
                File.WriteAllText(review, "review v1");
                Assert.That(PsdWorkflowPlanBinding.TryValidate(
                    editedPlan, "{\"fingerprint\":\"snap-a\",\"nodes\":[{\"id\":\"root\"}]}",
                    "snap-a", "Assets/Diary.prefab", "Assets/Diary.plan.json", review, approval,
                    out string error), Is.False);
                Assert.That(error, Does.Contain("planSha256"));
            }
            finally { File.Delete(review); }
        }

        [Test]
        public void CompletedOrPreflightRejectedSessionRequiresNewApprovalSession()
        {
            string applyPath = Path.Combine(Path.GetTempPath(), "psd-terminal-state-" + Guid.NewGuid().ToString("N") + ".apply");
            string resultPath = PsdHierarchyTerminalApplyWatcher.BuildResultPath(applyPath);
            try
            {
                File.WriteAllText(resultPath, new JObject { ["status"] = "rejected", ["stage"] = "preflight" }.ToString(), new UTF8Encoding(false));
                string plan = BuildPlan("snap-a", "node:root", includeMetadata: true);
                Assert.That(PsdWorkflowPlanBinding.TryCreateApplySentinel(
                    applyPath, "Assets/Diary.plan.json", plan, "snap-a", "review-a", "Assets/Diary.prefab",
                    PsdWorkflowPlanBinding.ExplicitApprovalPhrase, out string error), Is.False);
                Assert.That(error, Does.Contain("必须重新生成 review/plan"));
            }
            finally
            {
                if (File.Exists(applyPath)) File.Delete(applyPath);
                if (File.Exists(resultPath)) File.Delete(resultPath);
            }
        }

        [Test]
        public void TerminalStatesCannotCreateAnotherApply()
        {
            Assert.That(PsdWorkflowStateMachine.CanTransition(PsdWorkflowState.Partial, PsdWorkflowState.Approved), Is.False);
            Assert.That(PsdWorkflowStateMachine.CanTransition(PsdWorkflowState.Uncertain, PsdWorkflowState.Approved), Is.False);
            Assert.That(PsdWorkflowStateMachine.CanWrite(PsdWorkflowState.Partial), Is.False);
        }

        [Test]
        public void CrossParentSelectionCannotUseDirectPrefabSave()
        {
            Assert.That(PsdWorkflowPlanBinding.CanDirectSaveAsPrefabAssetAndConnect(
                new[] { "node:parent-a", "node:parent-b" }, false, false), Is.False);
            Assert.That(PsdWorkflowPlanBinding.CanDirectSaveAsPrefabAssetAndConnect(
                new[] { "node:parent-a" }, true, false), Is.False);
            Assert.That(PsdWorkflowPlanBinding.CanDirectSaveAsPrefabAssetAndConnect(
                new[] { "node:parent-a" }, false, true), Is.False);
            Assert.That(PsdWorkflowPlanBinding.CanDirectSaveAsPrefabAssetAndConnect(
                new[] { "node:parent-a" }, false, false), Is.True);
        }

        [Test]
        public void ValidPlanBindingAcceptsCurrentSnapshotAndNode()
        {
            string snapshot = "{\"fingerprint\":\"snap-a\",\"nodes\":[{\"id\":\"root\"}]}";
            string plan = BuildPlan("snap-a", "node:root", includeMetadata: true);
            PsdWorkflowApproval approval = BuildApproval(plan, "snap-a", "Assets/Diary.prefab");
            string review = Path.GetTempFileName();
            try
            {
                File.WriteAllText(review, "review v1");
                Assert.That(PsdWorkflowPlanBinding.TryValidate(plan, snapshot, "snap-a", "Assets/Diary.prefab",
                    "Assets/Diary.plan.json", review, approval, out string error), Is.True, error);
            }
            finally { File.Delete(review); }
        }

        [Test]
        public void ValidPlanBindingAcceptsObjectOperationScope()
        {
            string snapshot = "{\"fingerprint\":\"snap-a\",\"nodes\":[{\"id\":\"root\"}]}";
            JObject planObject = JObject.Parse(BuildPlan("snap-a", "node:root", includeMetadata: true));
            planObject["operationScope"] = new JObject
            {
                ["kind"] = "selected_navigation_item_review",
                ["root"] = "node:root",
                ["includeAllDescendants"] = true,
            };
            string plan = planObject.ToString(Formatting.None);
            PsdWorkflowApproval approval = BuildApproval(plan, "snap-a", "Assets/Diary.prefab");
            string review = Path.GetTempFileName();
            try
            {
                File.WriteAllText(review, "review v1");
                Assert.That(PsdWorkflowPlanBinding.TryValidate(plan, snapshot, "snap-a", "Assets/Diary.prefab",
                    "Assets/Diary.plan.json", review, approval, out string error), Is.True, error);
            }
            finally { File.Delete(review); }
        }

        [Test]
        public void PlanBindingRejectsMissingReviewVersion()
        {
            string snapshot = "{\"fingerprint\":\"snap-a\",\"nodes\":[{\"id\":\"root\"}]}";
            JObject planObject = JObject.Parse(BuildPlan("snap-a", "node:root", includeMetadata: true));
            planObject.Remove("reviewVersion");
            string plan = planObject.ToString(Formatting.None);
            PsdWorkflowApproval approval = BuildApproval(plan, "snap-a", "Assets/Diary.prefab");
            string review = Path.GetTempFileName();
            try
            {
                File.WriteAllText(review, "review v1");
                Assert.That(PsdWorkflowPlanBinding.TryValidate(plan, snapshot, "snap-a", "Assets/Diary.prefab",
                    "Assets/Diary.plan.json", review, approval, out string error), Is.False);
                Assert.That(error, Does.Contain("缺少 reviewVersion"));
            }
            finally { File.Delete(review); }
        }

        private static PsdWorkflowApproval BuildApproval(string plan, string fingerprint, string target)
        {
            return new PsdWorkflowApproval
            {
                approvalText = PsdWorkflowPlanBinding.ExplicitApprovalPhrase,
                planPath = "Assets/Diary.plan.json",
                planSha256 = PsdWorkflowPlanBinding.Sha256(plan),
                snapshotFingerprint = fingerprint,
                targetPrefabPath = target,
                reviewVersion = "review-a",
            };
        }

        private static string BuildPlan(string fingerprint, string node, bool includeMetadata)
        {
            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = fingerprint,
                ["prefabAssetPath"] = "Assets/Diary.prefab",
                ["reviewVersion"] = "review-a",
                ["selectionNodeIds"] = new JArray(node),
                ["operationScope"] = "selected hierarchy",
                ["expectedNodeCount"] = 1,
                ["expectedHierarchy"] = new JObject(),
                ["directChildren"] = new JArray(),
                ["absentPaths"] = new JArray(),
                ["preserveRequirements"] = new JObject(),
            };
            if (includeMetadata) return plan.ToString(Formatting.None);
            return "{}";
        }
    }
}
