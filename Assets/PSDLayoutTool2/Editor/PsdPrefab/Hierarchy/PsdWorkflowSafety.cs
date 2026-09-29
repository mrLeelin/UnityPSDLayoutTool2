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
        // Keep approval records on the same protocol version as session and result records.
        public int version = PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion;
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
                if (approval.version != PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion)
                {
                    error = "审批凭证协议版本不匹配；当前要求 version=" +
                            PsdHierarchyTerminalApplyWatcher.CurrentProtocolVersion + "。";
                    approval = null;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(approval.planPath) ||
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
            if (HasTerminalAttempted(applyPath, out string terminalStateError))
            {
                error = terminalStateError;
                return false;
            }
            try
            {
                JObject plan = JObject.Parse(planJson);
                string planReviewVersion = ReadStringField(plan, "reviewVersion");
                if (string.IsNullOrWhiteSpace(planReviewVersion))
                {
                    error = "当前计划缺少 reviewVersion，不能创建审批凭证。";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(reviewVersion) &&
                    !string.Equals(reviewVersion, planReviewVersion, StringComparison.Ordinal))
                {
                    error = "传入的 reviewVersion 与当前计划不匹配。";
                    return false;
                }
                var approval = new PsdWorkflowApproval
                {
                    approvalText = approvalText,
                    planPath = planPath,
                    planSha256 = Sha256(planJson),
                    snapshotFingerprint = snapshotFingerprint,
                    reviewVersion = planReviewVersion,
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
            var bindingMismatches = new List<string>();
            if (!string.Equals(approval.planPath, planPath, StringComparison.OrdinalIgnoreCase))
            {
                bindingMismatches.Add("planPath");
            }
            if (!string.Equals(approval.planSha256, Sha256(planJson), StringComparison.OrdinalIgnoreCase))
            {
                bindingMismatches.Add("planSha256");
            }
            if (!string.Equals(approval.snapshotFingerprint, snapshotFingerprint, StringComparison.Ordinal))
            {
                bindingMismatches.Add("snapshotFingerprint");
            }
            if (!string.Equals(approval.targetPrefabPath, targetPrefabPath, StringComparison.Ordinal))
            {
                bindingMismatches.Add("targetPrefabPath");
            }
            if (bindingMismatches.Count > 0)
            {
                error = bindingMismatches.Contains("planSha256")
                    ? "审批后的 JSON 计划内容已改变（planSha256 不匹配）；当前请求必须重新生成 review/plan 和新的审批会话。"
                    : "审批凭证绑定不匹配：" + string.Join("、", bindingMismatches) + "。";
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
                string planFingerprint = ReadStringField(plan, "snapshotFingerprint");
                string planTarget = ReadStringField(plan, "targetPrefabAssetPath") ?? ReadStringField(plan, "prefabAssetPath");
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
                string reviewVersion = ReadStringField(plan, "reviewVersion");
                if (string.IsNullOrWhiteSpace(reviewVersion))
                {
                    error = "计划缺少 reviewVersion，必须重新生成并审核计划。";
                    return false;
                }
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
                JToken operationScope = plan["operationScope"];
                if (plan["expectedNodeCount"] == null || plan["expectedNodeCount"].Type != JTokenType.Integer ||
                    plan["expectedHierarchy"] == null || plan["directChildren"] == null ||
                    plan["absentPaths"] == null || plan["preserveRequirements"] == null ||
                    !HasStructuredValue(operationScope))
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
                JObject node = token as JObject;
                string id = node?["id"]?.Type == JTokenType.String ? node["id"].Value<string>() :
                    (node?["stableId"]?.Type == JTokenType.String ? node["stableId"].Value<string>() : null);
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
            return ids;
        }

        private static bool HasStructuredValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return false;
            if (token.Type == JTokenType.String)
                return !string.IsNullOrWhiteSpace(token.Value<string>());
            if (token.Type == JTokenType.Object)
                return token.HasValues;
            return token.Type == JTokenType.Array && token.HasValues;
        }

        private static string ReadStringField(JObject owner, string field)
        {
            JToken token = owner[field];
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return null;
            if (token.Type != JTokenType.String)
                throw new InvalidDataException("计划字段 " + field + " 必须是字符串，实际类型为 " + token.Type + "。");
            return token.Value<string>();
        }

        private static bool HasTerminalAttempted(string applyPath, out string error)
        {
            error = string.Empty;
            string resultPath = PsdHierarchyTerminalApplyWatcher.BuildResultPath(applyPath);
            if (!File.Exists(resultPath)) return false;

            try
            {
                JObject result = JObject.Parse(File.ReadAllText(resultPath, Encoding.UTF8));
                string status = result.Value<string>("status") ?? string.Empty;
                string stage = result.Value<string>("stage") ?? string.Empty;
                bool approvalOnly = string.Equals(status, PsdHierarchyTerminalApplyWatcher.StatusRejected, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(stage, "approval", StringComparison.OrdinalIgnoreCase);
                if (approvalOnly) return false;

                error = "当前 Apply 会话已经结束（" + status + "/" + stage + "）；计划或 JSON 发生变化后必须重新生成 review/plan，并创建新的审批会话。";
                return true;
            }
            catch (Exception exception)
            {
                error = "当前 Apply 会话已有不可读的终态回执，必须重新生成 review/plan 和新的审批会话：" + exception.Message;
                return true;
            }
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
