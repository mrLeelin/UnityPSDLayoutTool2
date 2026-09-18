namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// 分组后抽取（v2 计划中的 postGroupingExtractionIntents）。
    /// 已审阅的后续意图按「分组后的层级路径」记录；首阶段保存成功后重新生成权威快照，
    /// 把意图解析成带新指纹与新节点 ID 的第二阶段 v2 计划，再交给同一个共享核心执行。
    /// 解析失败、出现未审核的强制候选或模式不支持时停止，绝不静默跳过。
    /// </summary>
    internal static class PsdHierarchyPostGroupingExtraction
    {
        private static readonly string[] SupportedModes = { "component", "state", "variant", "stateful" };

        /// <summary>
        /// 校验意图形状（唯一的权威校验点：预检层与共享执行核心都调用它）。返回原始意图数组。
        /// 一份已审阅的意图必须精确描述后续抽取的模板、实例、模式与状态，任何含糊都在写入前失败。
        /// </summary>
        internal static JArray ValidateIntents(JObject plan)
        {
            if (plan["postGroupingExtractionIntents"] == null)
            {
                return new JArray();
            }

            if (!(plan["postGroupingExtractionIntents"] is JArray intents))
            {
                throw new InvalidDataException("postGroupingExtractionIntents 必须为数组。");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject intent in intents.OfType<JObject>())
            {
                string id = RequireString(intent, "id");
                if (!ids.Add(id))
                {
                    throw new InvalidDataException("postGroupingExtractionIntents 包含重复 id：" + id + "。");
                }

                string mode = RequireString(intent, "mode");
                if (!SupportedModes.Contains(mode, StringComparer.Ordinal))
                {
                    throw new InvalidDataException("postGroupingExtractionIntents[" + id + "].mode 无效：" + mode + "。");
                }

                RequireString(intent, "assetPath");
                RequireString(intent, "templatePath");
                if (!(intent["instances"] is JArray instances) ||
                    !(intent["states"] is JArray states) ||
                    !(intent["commonMembers"] is JArray commonMembers))
                {
                    throw new InvalidDataException(
                        "postGroupingExtractionIntents[" + id + "] 必须包含 instances、commonMembers 和 states 数组。");
                }

                if (instances.Count == 0)
                {
                    throw new InvalidDataException(
                        "postGroupingExtractionIntents[" + id + "].instances 不能为空。");
                }

                if (intent["defaultState"] == null || intent["defaultState"].Type != JTokenType.String)
                {
                    throw new InvalidDataException(
                        "postGroupingExtractionIntents[" + id + "].defaultState 必须为字符串。");
                }

                ValidateStringArray(id, "commonMembers", commonMembers);
                HashSet<string> stateIds = ValidateIntentStates(id, mode, states, intent["defaultState"].Value<string>());
                ValidateIntentInstances(id, mode, instances, stateIds);
            }

            if (intents.Count != intents.OfType<JObject>().Count())
            {
                throw new InvalidDataException("postGroupingExtractionIntents 的每一项都必须为对象。");
            }

            return intents;
        }

        private static void ValidateIntentInstances(
            string intentId,
            string mode,
            JArray instances,
            HashSet<string> stateIds)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < instances.Count; index++)
            {
                if (!(instances[index] is JObject instance))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].instances[{index}] 必须为对象。");
                }

                string path = RequireString(instance, "path");
                if (!paths.Add(path))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].instances 包含重复 path：{path}。");
                }

                JToken state = instance["state"];
                if (state == null || state.Type != JTokenType.String ||
                    (mode == "component" && !string.IsNullOrEmpty(state.Value<string>())) ||
                    (mode != "component" && !stateIds.Contains(state.Value<string>())))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].instances[{index}].state 必须为字符串" +
                        (mode == "component"
                            ? "，且 component 模式必须使用空 state。"
                            : "，且必须引用已声明的 state id：" + (state?.Value<string>() ?? "<empty>") + "。"));
                }

                if (!(instance["commonSourceNames"] is JArray commonSourceNames) ||
                    !(instance["stateSourceNames"] is JArray stateSourceNames))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].instances[{index}] 必须包含 commonSourceNames 和 stateSourceNames 数组。");
                }

                ValidateStringArray(intentId, $"instances[{index}].commonSourceNames", commonSourceNames);
                ValidateStringArray(intentId, $"instances[{index}].stateSourceNames", stateSourceNames);
            }
        }

        private static HashSet<string> ValidateIntentStates(
            string intentId,
            string mode,
            JArray states,
            string defaultState)
        {
            if (mode == "component")
            {
                if (states.Count != 0 || !string.IsNullOrEmpty(defaultState))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}] 的 component 模式必须使用空 states 和空 defaultState。");
                }

                return new HashSet<string>(StringComparer.Ordinal);
            }

            if (states.Count == 0 || string.IsNullOrWhiteSpace(defaultState))
            {
                throw new InvalidDataException(
                    $"postGroupingExtractionIntents[{intentId}] 必须声明 states 和 defaultState。");
            }

            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < states.Count; index++)
            {
                if (!(states[index] is JObject state))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].states[{index}] 必须为对象。");
                }

                string stateId = RequireString(state, "id");
                RequireString(state, "name");
                RequireString(state, "sourcePath");
                if (!stateIds.Add(stateId))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].states 包含重复 id：{stateId}。");
                }

                if (!(state["members"] is JArray members))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].states[{index}].members 必须为数组。");
                }

                ValidateStringArray(intentId, $"states[{index}].members", members);
            }

            if (!stateIds.Contains(defaultState))
            {
                throw new InvalidDataException(
                    $"postGroupingExtractionIntents[{intentId}].defaultState 必须引用已声明的 state id。");
            }

            return stateIds;
        }

        private static void ValidateStringArray(string intentId, string propertyPath, JArray values)
        {
            var uniqueValues = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index]?.Type == JTokenType.String
                    ? values[index].Value<string>()
                    : null;
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].{propertyPath}[{index}] 必须为非空字符串。");
                }

                if (!uniqueValues.Add(value))
                {
                    throw new InvalidDataException(
                        $"postGroupingExtractionIntents[{intentId}].{propertyPath} 包含重复值：{value}。");
                }
            }
        }

        /// <summary>
        /// 用刷新后的权威快照把意图解析成第二阶段 v2 计划；无法解析时返回 null 并给出原因。
        /// </summary>
        internal static JObject BuildStageTwoPlan(
            PsdHierarchyChatContext refreshed,
            JArray intents,
            string targetPrefabAssetPath,
            out string error)
        {
            error = string.Empty;
            if (refreshed == null)
            {
                error = "The authoritative snapshot context could not be created.";
                return null;
            }

            Dictionary<string, string> pathToNodeId;
            try
            {
                pathToNodeId = BuildPathIndex(refreshed.hierarchySnapshotJson);
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                error = "The refreshed snapshot could not be parsed: " + exception.Message;
                return null;
            }

            var plan = new JObject
            {
                ["version"] = 2,
                ["snapshotFingerprint"] = refreshed.hierarchySnapshotFingerprint,
                ["prefabName"] = Path.GetFileNameWithoutExtension(targetPrefabAssetPath),
                ["prefabAssetPath"] = targetPrefabAssetPath,
                ["output"] = new JObject { ["mode"] = "in_place", ["assetPath"] = targetPrefabAssetPath },
                ["verify"] = new JObject(),
            };
            foreach (string key in new[]
                     {
                         "wrappers", "moves", "renames", "tightBounds", "emptyContainerRemovals",
                         "textureRenames", "spriteAtlasRenames", "componentFamilyDecisions",
                         "containmentResolutions", "flatSiblingResolutions", "componentExtractions",
                         "stateComponentExtractions", "variantComponentExtractions",
                         "statefulComponentExtractions", "postGroupingExtractionIntents",
                     })
            {
                plan[key] = new JArray();
            }

            // 覆盖关系：强制组件候选必须由已审阅的抽取精确覆盖，第二阶段才能写出合法的
            // componentFamilyDecisions（candidateId + mode=component + extractionId）。
            var coveredBy = new Dictionary<string, string>(StringComparer.Ordinal);
            var coveredMode = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JToken token in intents)
            {
                var intent = (JObject)token;
                string intentId = intent.Value<string>("id");
                string label = "postGroupingExtractionIntents[" + intentId + "]";
                string mode = intent.Value<string>("mode");
                string assetPath = Normalize(intent.Value<string>("assetPath"));
                string templateId = Resolve(pathToNodeId, intent.Value<string>("templatePath"), label + ".templatePath", out error);
                if (templateId == null)
                {
                    return null;
                }

                var instances = (JArray)intent["instances"];
                var instanceIds = new List<string>();
                foreach (JToken instanceToken in instances)
                {
                    var instance = (JObject)instanceToken;
                    string id = Resolve(pathToNodeId, instance.Value<string>("path"), label + ".instances[].path", out error);
                    if (id == null)
                    {
                        return null;
                    }

                    instanceIds.Add(id);
                }

                RecordCoverage(templateId, intentId, mode, coveredBy, coveredMode);
                foreach (string instanceId in instanceIds)
                {
                    RecordCoverage(instanceId, intentId, mode, coveredBy, coveredMode);
                }

                switch (mode)
                {
                    case "component":
                        ((JArray)plan["componentExtractions"]).Add(new JObject
                        {
                            ["id"] = intentId,
                            ["assetPath"] = assetPath,
                            ["template"] = "node:" + templateId,
                            ["instances"] = new JArray(instanceIds.Select(id => (JToken)("node:" + id))),
                        });
                        break;
                    case "state":
                        if (!AddStateContract(
                                plan, intent, intentId, assetPath, templateId, pathToNodeId, label,
                                coveredBy, coveredMode, out error))
                        {
                            return null;
                        }

                        break;
                    case "variant":
                        if (!AddVariantContract(plan, intent, intentId, assetPath, templateId, pathToNodeId, label, out error))
                        {
                            return null;
                        }

                        break;
                    default:
                        if (!AddStatefulContract(plan, intent, intentId, assetPath, templateId, pathToNodeId, label, out error))
                        {
                            return null;
                        }

                        break;
                }
            }

            if (!AddRequiredCandidateDecisions(plan, refreshed, coveredBy, coveredMode, out error))
            {
                return null;
            }

            return plan;
        }

        private static void RecordCoverage(
            string nodeId,
            string intentId,
            string mode,
            IDictionary<string, string> coveredBy,
            IDictionary<string, string> coveredMode)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            if (!coveredBy.ContainsKey(nodeId))
            {
                coveredBy.Add(nodeId, intentId);
                coveredMode.Add(nodeId, mode);
            }
        }

        /// <summary>
        /// 刷新后的快照里 requiresExtraction=true 的候选必须在第二阶段被真正抽取，
        /// 否则写入前失败：这里既校验覆盖，也写出与抽取清单一致的 componentFamilyDecisions。
        /// </summary>
        private static bool AddRequiredCandidateDecisions(
            JObject plan,
            PsdHierarchyChatContext refreshed,
            IDictionary<string, string> coveredBy,
            IDictionary<string, string> coveredMode,
            out string error)
        {
            error = string.Empty;
            JArray candidates = ReadSnapshotCandidates(refreshed.hierarchySnapshotJson);
            var decisions = (JArray)plan["componentFamilyDecisions"];
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject candidate in candidates.OfType<JObject>())
            {
                if (!(candidate.Value<bool?>("requiresExtraction") ?? false))
                {
                    continue;
                }

                string candidateId = candidate.Value<string>("id") ?? "<unknown>";
                string parent = candidate.Value<string>("parent");
                if (string.IsNullOrWhiteSpace(parent) || !parent.StartsWith("node:", StringComparison.Ordinal))
                {
                    error = "The refreshed snapshot contains mandatory candidate " + candidateId +
                            " without a node:<id> parent. Re-analyze the current Prefab.";
                    return false;
                }

                string[] sources = (candidate["sources"] as JArray ?? new JArray())
                    .Select(value => NormalizeNodeId(value.Value<string>()))
                    .Where(value => value != null)
                    .ToArray();
                if (sources.Length == 0)
                {
                    error = "The refreshed snapshot contains mandatory candidate " + candidateId +
                            " without any source nodes. Re-analyze the current Prefab.";
                    return false;
                }

                string extractionId = null;
                string extractionMode = null;
                foreach (string source in sources)
                {
                    if (!coveredBy.TryGetValue(source, out string intentId))
                    {
                        error = "The refreshed snapshot requires an extraction that the reviewed " +
                                "postGroupingExtractionIntents do not cover: " + candidateId +
                                " (missing node " + source + "). Re-analyze the current Prefab instead of " +
                                "extending the stage automatically.";
                        return false;
                    }

                    string mode = coveredMode[source];
                    if (extractionId == null)
                    {
                        extractionId = intentId;
                        extractionMode = mode;
                    }
                    else if (!string.Equals(extractionId, intentId, StringComparison.Ordinal) ||
                             !string.Equals(extractionMode, mode, StringComparison.Ordinal))
                    {
                        error = "Mandatory candidate " + candidateId + " is covered by several reviewed " +
                                "extractions (" + extractionId + ", " + intentId +
                                "). Re-analyze the current Prefab with one extraction per mandatory family.";
                        return false;
                    }
                }

                if (emitted.Add(candidateId))
                {
                    decisions.Add(new JObject
                    {
                        ["candidateId"] = candidateId,
                        ["parent"] = parent,
                        ["sources"] = new JArray(sources.Select(source => (JToken)("node:" + source))),
                        ["mode"] = extractionMode,
                        ["extractionId"] = extractionId,
                    });
                }
            }

            return true;
        }

        private static bool AddStateContract(
            JObject plan,
            JObject intent,
            string extractionId,
            string assetPath,
            string templateId,
            IDictionary<string, string> pathToNodeId,
            string label,
            IDictionary<string, string> coveredBy,
            IDictionary<string, string> coveredMode,
            out string error)
        {
            error = string.Empty;
            var states = new JArray();
            bool templateCovered = false;
            foreach (JToken token in (JArray)intent["states"])
            {
                var state = (JObject)token;
                string sourceId = Resolve(pathToNodeId, state.Value<string>("sourcePath"), label + ".states[].sourcePath", out error);
                if (sourceId == null)
                {
                    return false;
                }

                RecordCoverage(sourceId, extractionId, "state", coveredBy, coveredMode);
                templateCovered |= string.Equals(sourceId, templateId, StringComparison.Ordinal);
                states.Add(new JObject
                {
                    ["id"] = state.Value<string>("id"),
                    ["source"] = "node:" + sourceId,
                    ["name"] = state.Value<string>("name"),
                });
            }

            if (!templateCovered)
            {
                error = label + ".templatePath must also appear in states[].sourcePath.";
                return false;
            }

            ((JArray)plan["stateComponentExtractions"]).Add(new JObject
            {
                ["id"] = extractionId,
                ["assetPath"] = assetPath,
                ["template"] = "node:" + templateId,
                ["defaultState"] = intent.Value<string>("defaultState"),
                ["states"] = states,
            });
            return true;
        }

        private static bool AddVariantContract(
            JObject plan,
            JObject intent,
            string extractionId,
            string assetPath,
            string templateId,
            IDictionary<string, string> pathToNodeId,
            string label,
            out string error)
        {
            error = string.Empty;
            var states = new JArray();
            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken token in (JArray)intent["states"])
            {
                var state = (JObject)token;
                string sourceId = Resolve(pathToNodeId, state.Value<string>("sourcePath"), label + ".states[].sourcePath", out error);
                if (sourceId == null)
                {
                    return false;
                }

                stateIds.Add(state.Value<string>("id"));
                states.Add(new JObject
                {
                    ["id"] = state.Value<string>("id"),
                    ["source"] = "node:" + sourceId,
                    ["name"] = state.Value<string>("name"),
                });
            }

            var instances = new JArray();
            foreach (JToken token in (JArray)intent["instances"])
            {
                var instance = (JObject)token;
                string path = Normalize(instance.Value<string>("path"));
                string sourceId = Resolve(pathToNodeId, path, label + ".instances[].path", out error);
                if (sourceId == null)
                {
                    return false;
                }

                string stateId = (instance.Value<string>("state") ?? string.Empty).Trim();
                if (!stateIds.Contains(stateId))
                {
                    error = label + " instance selects an unknown state: " + stateId;
                    return false;
                }

                instances.Add(new JObject
                {
                    ["source"] = "node:" + sourceId,
                    ["name"] = LeafName(path),
                    ["state"] = stateId,
                });
            }

            ((JArray)plan["variantComponentExtractions"]).Add(new JObject
            {
                ["id"] = extractionId,
                ["assetPath"] = assetPath,
                ["template"] = "node:" + templateId,
                ["commonName"] = "[Common]",
                ["statesName"] = "[States]",
                ["defaultState"] = intent.Value<string>("defaultState"),
                ["states"] = states,
                ["instances"] = instances,
            });
            return true;
        }

        private static bool AddStatefulContract(
            JObject plan,
            JObject intent,
            string extractionId,
            string assetPath,
            string templateId,
            IDictionary<string, string> pathToNodeId,
            string label,
            out string error)
        {
            error = string.Empty;
            var commonMembers = new JArray();
            foreach (JToken member in intent["commonMembers"] as JArray ?? new JArray())
            {
                string name = member.Value<string>();
                if (string.IsNullOrWhiteSpace(name))
                {
                    error = label + ".commonMembers must contain non-empty names.";
                    return false;
                }

                commonMembers.Add(new JObject { ["sourceName"] = name, ["name"] = name });
            }

            var states = new JArray();
            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken token in (JArray)intent["states"])
            {
                var state = (JObject)token;
                string sourceId = Resolve(pathToNodeId, state.Value<string>("sourcePath"), label + ".states[].sourcePath", out error);
                if (sourceId == null)
                {
                    return false;
                }

                var members = new JArray();
                foreach (JToken member in state["members"] as JArray ?? new JArray())
                {
                    string name = member.Value<string>();
                    members.Add(new JObject { ["sourceName"] = name, ["name"] = name });
                }

                stateIds.Add(state.Value<string>("id"));
                states.Add(new JObject
                {
                    ["id"] = state.Value<string>("id"),
                    ["source"] = "node:" + sourceId,
                    ["name"] = state.Value<string>("name"),
                    ["members"] = members,
                });
            }

            var instances = new JArray();
            foreach (JToken token in (JArray)intent["instances"])
            {
                var instance = (JObject)token;
                string path = Normalize(instance.Value<string>("path"));
                string sourceId = Resolve(pathToNodeId, path, label + ".instances[].path", out error);
                if (sourceId == null)
                {
                    return false;
                }

                string stateId = (instance.Value<string>("state") ?? string.Empty).Trim();
                if (!stateIds.Contains(stateId))
                {
                    error = label + " instance selects an unknown state: " + stateId;
                    return false;
                }

                instances.Add(new JObject
                {
                    ["source"] = "node:" + sourceId,
                    ["name"] = LeafName(path),
                    ["state"] = stateId,
                    ["commonSourceNames"] = instance["commonSourceNames"] as JArray ?? new JArray(),
                    ["stateSourceNames"] = instance["stateSourceNames"] as JArray ?? new JArray(),
                });
            }

            ((JArray)plan["statefulComponentExtractions"]).Add(new JObject
            {
                ["id"] = extractionId,
                ["assetPath"] = assetPath,
                ["template"] = "node:" + templateId,
                ["commonName"] = "[Common]",
                ["statesName"] = "[States]",
                ["defaultState"] = intent.Value<string>("defaultState"),
                ["common"] = new JObject
                {
                    ["source"] = "node:" + templateId,
                    ["members"] = commonMembers,
                },
                ["states"] = states,
                ["instances"] = instances,
            });
            return true;
        }

        private static Dictionary<string, string> BuildPathIndex(string snapshotJson)
        {
            var index = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return index;
            }

            var snapshot = JObject.Parse(snapshotJson);
            foreach (JToken token in snapshot["nodes"] as JArray ?? new JArray())
            {
                if (!(token is JObject node))
                {
                    continue;
                }

                string id = NormalizeNodeId(node.Value<string>("id"));
                string path = Normalize(node.Value<string>("path"));
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(path) && !index.ContainsKey(path))
                {
                    index.Add(path, id);
                }
            }

            return index;
        }

        private static JArray ReadSnapshotCandidates(string snapshotJson)
        {
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return new JArray();
            }

            try
            {
                return JObject.Parse(snapshotJson)["componentFamilyCandidates"] as JArray ?? new JArray();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return new JArray();
            }
        }

        private static string Resolve(
            IDictionary<string, string> pathToNodeId,
            string path,
            string label,
            out string error)
        {
            error = string.Empty;
            string normalized = Normalize(path);
            if (string.IsNullOrEmpty(normalized))
            {
                error = label + " must be a complete post-grouping path.";
                return null;
            }

            if (!pathToNodeId.TryGetValue(normalized, out string nodeId))
            {
                error = label + " does not exist in the refreshed snapshot: " + normalized +
                        ". Stop and re-analyze instead of reusing stale ids or guessing a path.";
                return null;
            }

            return nodeId;
        }

        private static string NormalizeNodeId(string value)
        {
            string text = (value ?? string.Empty).Trim();
            if (text.StartsWith("node:", StringComparison.Ordinal))
            {
                text = text.Substring("node:".Length);
            }

            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static string LeafName(string path)
        {
            int index = path.LastIndexOf('/');
            return index >= 0 ? path.Substring(index + 1) : path;
        }

        private static string RequireString(JObject owner, string field)
        {
            JToken value = owner[field];
            if (value == null || value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>()))
            {
                throw new InvalidDataException("计划缺少 " + field + "。");
            }

            return value.Value<string>();
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }
    }
}
