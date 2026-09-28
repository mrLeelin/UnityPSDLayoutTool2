namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using TMPro;
    using UnityEngine;

    internal enum PsdWorkflowState
    {
        SelectionRead,
        SelectionNormalized,
        SnapshotCaptured,
        ReviewDrafted,
        PlanValidated,
        AwaitingApproval,
        Approved,
        Applying,
        Applied,
        Rejected,
        Partial,
        Uncertain,
    }

    internal static class PsdWorkflowStateMachine
    {
        internal static bool CanTransition(PsdWorkflowState from, PsdWorkflowState to)
        {
            if (from == PsdWorkflowState.Partial || from == PsdWorkflowState.Uncertain ||
                from == PsdWorkflowState.Applied || from == PsdWorkflowState.Rejected)
                return false;
            switch (from)
            {
                case PsdWorkflowState.SelectionRead: return to == PsdWorkflowState.SelectionNormalized;
                case PsdWorkflowState.SelectionNormalized: return to == PsdWorkflowState.SnapshotCaptured;
                case PsdWorkflowState.SnapshotCaptured: return to == PsdWorkflowState.ReviewDrafted;
                case PsdWorkflowState.ReviewDrafted: return to == PsdWorkflowState.PlanValidated;
                case PsdWorkflowState.PlanValidated: return to == PsdWorkflowState.AwaitingApproval;
                case PsdWorkflowState.AwaitingApproval: return to == PsdWorkflowState.Approved;
                case PsdWorkflowState.Approved: return to == PsdWorkflowState.Applying;
                case PsdWorkflowState.Applying:
                    return to == PsdWorkflowState.Applied || to == PsdWorkflowState.Rejected ||
                           to == PsdWorkflowState.Partial || to == PsdWorkflowState.Uncertain;
                default: return false;
            }
        }

        internal static bool CanWrite(PsdWorkflowState state)
        {
            return state == PsdWorkflowState.Approved;
        }

        internal static bool IsTerminal(PsdWorkflowState state)
        {
            return state == PsdWorkflowState.Applied || state == PsdWorkflowState.Rejected ||
                   state == PsdWorkflowState.Partial || state == PsdWorkflowState.Uncertain;
        }
    }

    [Serializable]
    internal sealed class PsdWorkflowApproval
    {
        public int version = 1;
        public string approvalText = string.Empty;
        public string planPath = string.Empty;
        public string planSha256 = string.Empty;
        public string snapshotFingerprint = string.Empty;
        public string reviewVersion = string.Empty;
        public string targetPrefabPath = string.Empty;
        public string approvedAtUtc = string.Empty;
    }

    internal static class PsdWorkflowPlanBinding
    {
        internal const string ExplicitApprovalPhrase = "批准当前 JSON 计划并执行 apply";

        internal static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return string.Concat(digest.Select(valueByte => valueByte.ToString("x2")));
            }
        }

        internal static bool IsExplicitApproval(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                   message.IndexOf(ExplicitApprovalPhrase, StringComparison.Ordinal) >= 0;
        }

        internal static bool CanDirectSaveAsPrefabAssetAndConnect(
            IEnumerable<string> parentIds,
            bool crossesNestedPrefabBoundary,
            bool changesCanvasDrawOrder)
        {
            string[] parents = (parentIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return parents.Length <= 1 && !crossesNestedPrefabBoundary && !changesCanvasDrawOrder;
        }

        internal static bool TryReadApproval(string applyPath, out PsdWorkflowApproval approval, out string error)
        {
            approval = null;
            error = string.Empty;
            try
            {
                string json = File.ReadAllText(applyPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "Apply 哨兵缺少审批凭证；空 .apply 永远不能触发写入。";
                    return false;
                }

                approval = JsonConvert.DeserializeObject<PsdWorkflowApproval>(json);
                if (approval == null || !IsExplicitApproval(approval.approvalText))
                {
                    error = "审批语句必须明确包含：" + ExplicitApprovalPhrase;
                    approval = null;
                    return false;
                }
                if (approval.version != 1 || string.IsNullOrWhiteSpace(approval.planPath) ||
                    string.IsNullOrWhiteSpace(approval.planSha256) ||
                    string.IsNullOrWhiteSpace(approval.snapshotFingerprint) ||
                    string.IsNullOrWhiteSpace(approval.targetPrefabPath))
                {
                    error = "审批凭证缺少 planPath、planSha256、snapshotFingerprint 或 targetPrefabPath。";
                    approval = null;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "解析审批凭证失败：" + exception.Message;
                return false;
            }
        }

        internal static bool TryCreateApplySentinel(
            string applyPath,
            string planPath,
            string planJson,
            string snapshotFingerprint,
            string reviewVersion,
            string targetPrefabPath,
            string approvalText,
            out string error)
        {
            error = string.Empty;
            if (!IsExplicitApproval(approvalText))
            {
                error = "只有明确批准当前 JSON 计划的语句才能创建 .apply。";
                return false;
            }
            if (File.Exists(applyPath))
            {
                error = "当前请求已经创建过 .apply，禁止重复创建。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(planPath) || string.IsNullOrWhiteSpace(planJson) ||
                string.IsNullOrWhiteSpace(snapshotFingerprint) || string.IsNullOrWhiteSpace(targetPrefabPath))
            {
                error = "创建 .apply 需要完整的当前计划、快照和目标 Prefab 绑定。";
                return false;
            }
            try
            {
                var approval = new PsdWorkflowApproval
                {
                    approvalText = approvalText,
                    planPath = planPath,
                    planSha256 = Sha256(planJson),
                    snapshotFingerprint = snapshotFingerprint,
                    reviewVersion = reviewVersion ?? string.Empty,
                    targetPrefabPath = targetPrefabPath,
                    approvedAtUtc = DateTime.UtcNow.ToString("o"),
                };
                string temporaryPath = applyPath + ".tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(approval, Formatting.Indented), new UTF8Encoding(false));
                File.Move(temporaryPath, applyPath);
                return true;
            }
            catch (Exception exception)
            {
                error = "创建 .apply 失败：" + exception.Message;
                return false;
            }
        }

        internal static bool TryValidate(
            string planJson,
            string snapshotJson,
            string snapshotFingerprint,
            string targetPrefabPath,
            string planPath,
            string reviewPath,
            PsdWorkflowApproval approval,
            out string error)
        {
            error = string.Empty;
            if (approval == null)
            {
                error = "缺少审批凭证。";
                return false;
            }
            if (!string.Equals(approval.planPath, planPath, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(approval.planSha256, Sha256(planJson), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(approval.snapshotFingerprint, snapshotFingerprint, StringComparison.Ordinal) ||
                !string.Equals(approval.targetPrefabPath, targetPrefabPath, StringComparison.Ordinal))
            {
                error = "审批凭证与当前计划、快照或目标 Prefab 不匹配。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(reviewPath) || !File.Exists(reviewPath))
            {
                error = "当前计划没有可读取的 review 文件。";
                return false;
            }

            try
            {
                JObject plan = JObject.Parse(planJson ?? string.Empty);
                string planFingerprint = plan.Value<string>("snapshotFingerprint");
                string planTarget = plan.Value<string>("targetPrefabAssetPath") ?? plan.Value<string>("prefabAssetPath");
                if (!string.Equals(planFingerprint, snapshotFingerprint, StringComparison.Ordinal))
                {
                    error = "计划 snapshot fingerprint 已变化，必须重新生成 review/plan。";
                    return false;
                }
                if (!string.Equals(planTarget, targetPrefabPath, StringComparison.Ordinal))
                {
                    error = "计划目标 Prefab 路径与当前会话不匹配。";
                    return false;
                }
                string reviewVersion = plan.Value<string>("reviewVersion");
                if (!string.IsNullOrWhiteSpace(approval.reviewVersion) &&
                    !string.Equals(approval.reviewVersion, reviewVersion, StringComparison.Ordinal))
                {
                    error = "审批绑定的 review 版本与计划不匹配。";
                    return false;
                }
                JArray selected = plan["selectionNodeIds"] as JArray;
                if (selected == null || selected.Count == 0)
                {
                    error = "计划必须包含 selectionNodeIds。";
                    return false;
                }
                HashSet<string> snapshotIds = ReadSnapshotIds(snapshotJson);
                foreach (JToken token in selected)
                {
                    string id = token.Type == JTokenType.String ? token.Value<string>() : string.Empty;
                    if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("node:", StringComparison.Ordinal) ||
                        !snapshotIds.Contains(id.Substring("node:".Length)))
                    {
                        error = "计划引用的选择 node:<id> 不存在于当前快照。";
                        return false;
                    }
                }
                if (plan.Value<int?>("expectedNodeCount") == null ||
                    plan["expectedHierarchy"] == null || plan["directChildren"] == null ||
                    plan["absentPaths"] == null || plan["preserveRequirements"] == null ||
                    string.IsNullOrWhiteSpace(plan.Value<string>("operationScope")))
                {
                    error = "计划缺少 operationScope、expectedNodeCount、expectedHierarchy、directChildren、absentPaths 或 preserveRequirements。";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "计划绑定校验失败：" + exception.Message;
                return false;
            }
        }

        private static HashSet<string> ReadSnapshotIds(string snapshotJson)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            JObject snapshot = JObject.Parse(snapshotJson ?? string.Empty);
            foreach (JToken token in snapshot["nodes"] as JArray ?? new JArray())
            {
                string id = token.Value<string>("id") ?? token.Value<string>("stableId");
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
            return ids;
        }
    }

    internal sealed class PsdHierarchyNormalizedSelection
    {
        internal GameObject original;
        internal GameObject normalized;
        internal string originalPath = string.Empty;
        internal string normalizedPath = string.Empty;
        internal int originalInstanceId;
        internal int normalizedInstanceId;
        internal string[] componentTypes = Array.Empty<string>();
        internal bool changed;
    }

    internal static class PsdHierarchySelectionNormalizer
    {
        internal static PsdHierarchyNormalizedSelection Normalize(GameObject selected)
        {
            if (selected == null) throw new ArgumentNullException("selected");
            GameObject normalized = selected;
            TMP_SubMeshUI subMesh = selected.GetComponent<TMP_SubMeshUI>();
            if (subMesh != null)
            {
                TextMeshProUGUI root = selected.GetComponentInParent<TextMeshProUGUI>();
                if (root != null) normalized = root.gameObject;
            }
            return new PsdHierarchyNormalizedSelection
            {
                original = selected,
                normalized = normalized,
                originalPath = GetPath(selected.transform),
                normalizedPath = GetPath(normalized.transform),
                originalInstanceId = selected.GetInstanceID(),
                normalizedInstanceId = normalized.GetInstanceID(),
                componentTypes = selected.GetComponents<Component>().Where(component => component != null)
                    .Select(component => component.GetType().FullName).ToArray(),
                changed = selected != normalized,
            };
        }

        private static string GetPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent) parts.Push(current.name);
            return string.Join("/", parts.ToArray());
        }
    }
}
