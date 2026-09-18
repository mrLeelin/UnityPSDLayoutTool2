namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// v2 重放绑定。
    /// <para>
    /// v2 快照里的节点 ID（n000001…）是位置编号，重新生成 PSD 后同一个编号会指向另一个对象，
    /// 所以“编号相同”或“覆盖指纹”都不能证明对应关系。保存阶段在这里记录被引用节点的可观察
    /// 身份证据；重放阶段在重新生成的临时 Prefab 快照上要求每个节点都有且只有一个匹配，
    /// 匹配缺失或不唯一时停止并要求重新分析。
    /// </para>
    /// </summary>
    internal static class PsdHierarchyReplayBinding
    {
        internal const int CurrentSchemaVersion = 1;
        internal const string NodeReferencePrefix = "node:";

        /// <summary>用于证明身份的可观察事实：路径、名称、同级顺序与组件类型清单。</summary>
        private static readonly string[] IdentityFields = { "path", "name", "siblingIndex", "components" };

        /// <summary>
        /// 为一份 v2 阶段计划生成绑定证据；计划引用的每个节点都必须出现在快照里。
        /// </summary>
        internal static bool TryBuildForPlan(
            JObject plan,
            string snapshotJson,
            out string bindingJson,
            out string error)
        {
            bindingJson = string.Empty;
            error = string.Empty;
            if (plan == null)
            {
                error = "缺少需要保存绑定的 v2 阶段计划。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                error = "缺少生成该计划时的权威快照，无法保存重放绑定。";
                return false;
            }

            try
            {
                var nodeById = ReadSnapshotNodes(snapshotJson)
                    .Where(node => !string.IsNullOrEmpty(node.Id))
                    .ToDictionary(node => node.Id, StringComparer.Ordinal);
                var referenced = CollectNodeReferences(plan);
                var nodes = new JObject();
                foreach (string id in referenced.OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (!nodeById.TryGetValue(id, out SnapshotNode node))
                    {
                        error = "计划引用了当前权威快照中不存在的节点：" + id + "。";
                        return false;
                    }

                    nodes[id] = node.Evidence;
                }

                bindingJson = new JObject
                {
                    ["schemaVersion"] = CurrentSchemaVersion,
                    ["schema"] = string.Join(",", IdentityFields),
                    ["nodes"] = nodes,
                }.ToString(Newtonsoft.Json.Formatting.None);
                return true;
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                error = "保存重放绑定时无法读取权威快照：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 在重新生成的快照上为每个已绑定节点求出唯一对应关系。
        /// </summary>
        internal static bool TryRebind(
            string bindingJson,
            string freshSnapshotJson,
            out IReadOnlyDictionary<string, string> nodeMap,
            out string error)
        {
            nodeMap = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(bindingJson))
            {
                error = "该 v2 阶段没有保存节点绑定证据。";
                return false;
            }

            try
            {
                var binding = JObject.Parse(bindingJson);
                if (binding.Value<int?>("schemaVersion") != CurrentSchemaVersion)
                {
                    error = "重放绑定证据的版本不受支持。";
                    return false;
                }

                if (!(binding["nodes"] is JObject boundNodes))
                {
                    error = "重放绑定证据缺少节点表。";
                    return false;
                }

                List<SnapshotNode> freshNodes = ReadSnapshotNodes(freshSnapshotJson).ToList();
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (JProperty property in boundNodes.Properties())
                {
                    if (!(property.Value is JObject evidence))
                    {
                        error = "重放绑定证据中的节点条目无效：" + property.Name + "。";
                        return false;
                    }

                    SnapshotNode[] matches = freshNodes
                        .Where(node => IsIdentityMatch(evidence, node.Evidence))
                        .ToArray();
                    if (matches.Length == 0)
                    {
                        error = "重新生成的 Prefab 中找不到与该节点对应的对象：" + Describe(evidence) +
                                "。请重新分析当前 Prefab 后确认新计划。";
                        return false;
                    }

                    if (matches.Length > 1)
                    {
                        error = "重新生成的 Prefab 中该节点存在多个候选，对应关系不唯一：" + Describe(evidence) +
                                "。请重新分析当前 Prefab 后确认新计划。";
                        return false;
                    }

                    map[property.Name] = matches[0].Id;
                }

                nodeMap = map;
                return true;
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                error = "重放绑定证据无法解析：" + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 用新对应关系改写阶段计划：节点 ID、指纹与目标路径；核验字段必须重建，不能沿用旧快照。
        /// </summary>
        internal static void RewritePlan(
            JObject plan,
            IReadOnlyDictionary<string, string> nodeMap,
            string freshFingerprint,
            string replayTargetPath)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (nodeMap == null) throw new ArgumentNullException(nameof(nodeMap));

            RewriteNodeReferences(plan, nodeMap);
            plan["snapshotFingerprint"] = freshFingerprint;
            plan["prefabAssetPath"] = replayTargetPath;
            if (!(plan["output"] is JObject output))
                throw new InvalidDataException("重放计划缺少 output 对象。");
            output["mode"] = "in_place";
            output["assetPath"] = replayTargetPath;
            plan["verify"] = new JObject();
        }

        private static void RewriteNodeReferences(JToken token, IReadOnlyDictionary<string, string> nodeMap)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties().ToList())
                {
                    if (property.Value.Type == JTokenType.String)
                        property.Value = RewriteReference(property.Value.Value<string>(), nodeMap);
                    else
                        RewriteNodeReferences(property.Value, nodeMap);
                }

                return;
            }

            if (token is JArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    if (array[index].Type == JTokenType.String)
                        array[index] = RewriteReference(array[index].Value<string>(), nodeMap);
                    else
                        RewriteNodeReferences(array[index], nodeMap);
                }
            }
        }

        private static JToken RewriteReference(string value, IReadOnlyDictionary<string, string> nodeMap)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(NodeReferencePrefix, StringComparison.Ordinal))
                return value;

            string id = value.Substring(NodeReferencePrefix.Length);
            if (!nodeMap.TryGetValue(id, out string rebound))
                throw new InvalidDataException("重放计划引用了没有绑定证据的节点：" + id + "。");
            return NodeReferencePrefix + rebound;
        }

        private static bool IsIdentityMatch(JObject evidence, JObject candidate)
        {
            foreach (string field in IdentityFields)
            {
                JToken expected = evidence[field];
                if (expected == null)
                {
                    // 缺少证据字段时不能证明对应关系。
                    return false;
                }

                if (!JToken.DeepEquals(expected, candidate[field]))
                    return false;
            }

            return true;
        }

        private static string Describe(JObject evidence)
        {
            string path = evidence.Value<string>("path") ?? "<unknown>";
            string name = evidence.Value<string>("name") ?? "<unnamed>";
            JToken sibling = evidence["siblingIndex"];
            return path + "（名称 " + name + "，同级顺序 " + (sibling?.ToString() ?? "?") + "）";
        }

        /// <summary>收集计划里出现的全部 node:&lt;id&gt; 引用。</summary>
        internal static ISet<string> CollectNodeReferences(JToken token)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Collect(token, ids);
            return ids;
        }

        private static void Collect(JToken token, ISet<string> ids)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    if (property.Value.Type == JTokenType.String)
                        Add(property.Value.Value<string>(), ids);
                    else
                        Collect(property.Value, ids);
                }

                return;
            }

            if (token is JArray array)
            {
                foreach (JToken item in array)
                {
                    if (item.Type == JTokenType.String) Add(item.Value<string>(), ids);
                    else Collect(item, ids);
                }
            }
        }

        private static void Add(string value, ISet<string> ids)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(NodeReferencePrefix, StringComparison.Ordinal))
                return;
            ids.Add(value.Substring(NodeReferencePrefix.Length));
        }

        private static IEnumerable<SnapshotNode> ReadSnapshotNodes(string snapshotJson)
        {
            var snapshot = JObject.Parse(snapshotJson ?? string.Empty);
            foreach (JToken token in snapshot["nodes"] as JArray ?? new JArray())
            {
                if (!(token is JObject node))
                {
                    continue;
                }

                string id = (node.Value<string>("id") ?? string.Empty).Trim();
                if (id.StartsWith(NodeReferencePrefix, StringComparison.Ordinal))
                    id = id.Substring(NodeReferencePrefix.Length);

                var evidence = new JObject();
                foreach (string field in IdentityFields)
                {
                    if (node[field] != null)
                        evidence[field] = node[field].DeepClone();
                }

                yield return new SnapshotNode { Id = id, Evidence = evidence };
            }
        }

        private sealed class SnapshotNode
        {
            internal string Id;
            internal JObject Evidence;
        }
    }
}
