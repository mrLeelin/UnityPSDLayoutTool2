namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    internal enum PsdHierarchyLocalRepairScopeMode
    {
        SelectedNodes,
        SelectedParentSubtree,
        SelectedNodesWithSiblings,
    }

    internal readonly struct PsdHierarchySelectedPrefabExtraction
    {
        internal PsdHierarchySelectedPrefabExtraction(
            string componentName,
            string assetPath,
            string parentNodeId,
            IEnumerable<string> sourceNodeIds)
        {
            this.componentName = componentName ?? string.Empty;
            this.assetPath = assetPath ?? string.Empty;
            this.parentNodeId = parentNodeId ?? string.Empty;
            this.sourceNodeIds = (sourceNodeIds ?? Array.Empty<string>()).ToArray();
        }

        internal readonly string componentName;
        internal readonly string assetPath;
        internal readonly string parentNodeId;
        internal readonly string[] sourceNodeIds;
    }

    internal readonly struct PsdHierarchyCrossParentPrefabInstance
    {
        internal PsdHierarchyCrossParentPrefabInstance(int sequence, IEnumerable<string> sourceNodeIds)
        {
            this.sequence = sequence;
            this.sourceNodeIds = (sourceNodeIds ?? Array.Empty<string>()).ToArray();
        }

        internal readonly int sequence;
        internal readonly string[] sourceNodeIds;
    }

    internal readonly struct PsdHierarchyCrossParentPrefabExtraction
    {
        internal PsdHierarchyCrossParentPrefabExtraction(
            string componentName,
            string assetPath,
            string rootNodeId,
            IEnumerable<string> templateSourceNodeIds,
            IEnumerable<PsdHierarchyCrossParentPrefabInstance> instances,
            IEnumerable<string> unmatchedNodeIds,
            PsdHierarchyVisualAnalysisResult visualAnalysis = null)
        {
            this.componentName = componentName ?? string.Empty;
            this.assetPath = assetPath ?? string.Empty;
            this.rootNodeId = rootNodeId ?? string.Empty;
            this.templateSourceNodeIds = (templateSourceNodeIds ?? Array.Empty<string>()).ToArray();
            this.instances = (instances ?? Array.Empty<PsdHierarchyCrossParentPrefabInstance>()).ToArray();
            this.unmatchedNodeIds = (unmatchedNodeIds ?? Array.Empty<string>()).ToArray();
            this.visualAnalysis = visualAnalysis;
        }

        internal readonly string componentName;
        internal readonly string assetPath;
        internal readonly string rootNodeId;
        internal readonly string[] templateSourceNodeIds;
        internal readonly PsdHierarchyCrossParentPrefabInstance[] instances;
        internal readonly string[] unmatchedNodeIds;
        internal readonly PsdHierarchyVisualAnalysisResult visualAnalysis;

        /// <summary>获取指定实例的视觉评分</summary>
        internal int GetVisualScore(PsdHierarchyCrossParentPrefabInstance instance)
        {
            if (visualAnalysis == null || instance.sourceNodeIds == null || instance.sourceNodeIds.Length == 0)
            {
                return -1; // 未分析
            }

            // 取该实例中所有节点的平均评分
            int totalScore = 0;
            int count = 0;
            foreach (string nodeId in instance.sourceNodeIds)
            {
                if (visualAnalysis.TryGetScore(nodeId, out PsdHierarchyVisualScore score))
                {
                    totalScore += score.similarityScore;
                    count++;
                }
            }

            return count > 0 ? totalScore / count : -1;
        }

        /// <summary>获取推荐的实例（视觉评分 >= 80% 或未分析）</summary>
        internal PsdHierarchyCrossParentPrefabInstance[] GetRecommendedInstances()
        {
            if (visualAnalysis == null)
            {
                return instances; // 未启用视觉分析，返回所有实例
            }

            // 复制 this 到局部变量以避免在 lambda 中访问结构体实例成员
            var self = this;
            return instances.Where(inst =>
            {
                int score = self.GetVisualScore(inst);
                return score >= 80 || score < 0;
            }).ToArray();
        }
    }

    /// <summary>
    /// An explicit editable boundary for a corrective AI hierarchy stage.
    /// </summary>
    internal sealed class PsdHierarchyLocalRepairScope
    {
        private readonly HashSet<string> selectedNodeIds;
        private readonly HashSet<string> editableNodeIds;
        private readonly HashSet<string> allowedContainerNodeIds;
        private readonly HashSet<string> crossParentCandidateNodeIds;
        private readonly string crossParentRootNodeId;

        private PsdHierarchyLocalRepairScope(
            PsdHierarchyLocalRepairScopeMode mode,
            IEnumerable<string> selectedPaths,
            IEnumerable<string> selectedIds,
            IEnumerable<string> editableIds,
            IEnumerable<string> allowedContainerIds,
            string crossParentRootNodeId,
            IEnumerable<string> crossParentCandidateIds)
        {
            this.mode = mode;
            this.selectedPaths = (selectedPaths ?? Array.Empty<string>()).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            selectedNodeIds = new HashSet<string>(selectedIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            editableNodeIds = new HashSet<string>(editableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            allowedContainerNodeIds = new HashSet<string>(allowedContainerIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            this.crossParentRootNodeId = crossParentRootNodeId ?? string.Empty;
            crossParentCandidateNodeIds = new HashSet<string>(
                crossParentCandidateIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
        }

        internal readonly PsdHierarchyLocalRepairScopeMode mode;
        internal readonly string[] selectedPaths;

        internal string Describe()
        {
            return "scope=" + mode + ", selected=" + selectedPaths.Length + ", editable=" + editableNodeIds.Count;
        }

        internal static bool TryCaptureCurrentSelection(
            PsdHierarchyChatContext context,
            PsdHierarchyLocalRepairScopeMode mode,
            out PsdHierarchyLocalRepairScope scope,
            out string error)
        {
            scope = null;
            error = string.Empty;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null ||
                !string.Equals(stage.assetPath, context?.targetPrefabAssetPath, StringComparison.Ordinal))
            {
                error = "请先在当前目标 Prefab 的 Prefab Stage 中选择要修复的节点。";
                return false;
            }

            var paths = new List<string>();
            foreach (UnityEngine.Object selected in Selection.objects)
            {
                Transform transform = GetTransform(selected);
                if (transform == null)
                {
                    continue;
                }

                if (!transform.IsChildOf(stage.prefabContentsRoot.transform))
                {
                    error = "当前选择包含不属于目标 Prefab Stage 的对象：" + transform.name;
                    return false;
                }

                paths.Add(BuildPlanPath(transform));
            }

            return TryCreateFromSelectedPaths(context, paths, mode, out scope, out error);
        }

        internal static bool TryCreateFromSelectedPaths(
            PsdHierarchyChatContext context,
            IEnumerable<string> selectedPaths,
            PsdHierarchyLocalRepairScopeMode mode,
            out PsdHierarchyLocalRepairScope scope,
            out string error)
        {
            scope = null;
            error = string.Empty;
            if (context == null)
            {
                error = "缺少当前 Prefab 快照。";
                return false;
            }

            string[] normalizedPaths = (selectedPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalizedPaths.Length == 0)
            {
                error = "请先在当前目标 Prefab 的 Hierarchy 中选择至少一个节点。";
                return false;
            }

            if (!TryReadNodes(context.hierarchySnapshotJson, out Dictionary<string, SnapshotNode> nodesById, out error))
            {
                return false;
            }

            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in normalizedPaths)
            {
                if (!context.TryGetNodeId(path, out string nodeId))
                {
                    error = "当前选择不在已分析的目标 Prefab 快照中：" + path;
                    return false;
                }

                selectedIds.Add(nodeId);
            }

            var editableIds = new HashSet<string>(selectedIds, StringComparer.Ordinal);
            var allowedContainerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string selectedId in selectedIds)
            {
                if (!nodesById.TryGetValue(selectedId, out SnapshotNode selectedNode))
                {
                    error = "当前选择的节点在快照中缺少详细信息：" + selectedId;
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(selectedNode.parentId))
                {
                    allowedContainerIds.Add(selectedNode.parentId);
                }

                if (mode == PsdHierarchyLocalRepairScopeMode.SelectedParentSubtree)
                {
                    AddDescendants(selectedId, nodesById, editableIds);
                }
                else if (mode == PsdHierarchyLocalRepairScopeMode.SelectedNodesWithSiblings &&
                         !string.IsNullOrWhiteSpace(selectedNode.parentId))
                {
                    foreach (SnapshotNode sibling in nodesById.Values.Where(node =>
                                 string.Equals(node.parentId, selectedNode.parentId, StringComparison.Ordinal)))
                    {
                        editableIds.Add(sibling.id);
                    }
                }
            }

            string crossParentRootId = FindLowestCommonAncestor(selectedIds, nodesById);
            var crossParentCandidateIds = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(crossParentRootId))
            {
                crossParentCandidateIds.Add(crossParentRootId);
                AddDescendants(crossParentRootId, nodesById, crossParentCandidateIds);
            }

            scope = new PsdHierarchyLocalRepairScope(
                mode,
                normalizedPaths,
                selectedIds,
                editableIds,
                allowedContainerIds,
                crossParentRootId,
                crossParentCandidateIds);
            return true;
        }

        internal bool TryCreateSelectedPrefabExtraction(
            PsdHierarchyChatContext context,
            string componentName,
            out PsdHierarchySelectedPrefabExtraction extraction,
            out string error)
        {
            extraction = default(PsdHierarchySelectedPrefabExtraction);
            error = string.Empty;
            string normalizedName = (componentName ?? string.Empty).Trim();
            if (!IsPascalCaseIdentifier(normalizedName))
            {
                error = "组件名称必须是 PascalCase，例如 DaySignCard。";
                return false;
            }

            if (selectedNodeIds.Count < 2)
            {
                error = "抽取单实例 Nested Prefab 至少需要选择两个同级节点。";
                return false;
            }

            if (!TryReadNodes(context?.hierarchySnapshotJson, out Dictionary<string, SnapshotNode> nodesById, out error))
            {
                return false;
            }

            string parentId = string.Empty;
            foreach (string selectedId in selectedNodeIds)
            {
                if (!nodesById.TryGetValue(selectedId, out SnapshotNode node) || string.IsNullOrWhiteSpace(node.parentId))
                {
                    error = "选区包含 Prefab 根节点或当前快照中不存在的节点，无法抽取。";
                    return false;
                }

                if (string.IsNullOrEmpty(parentId))
                {
                    parentId = node.parentId;
                }
                else if (!string.Equals(parentId, node.parentId, StringComparison.Ordinal))
                {
                    error = "抽取单实例 Nested Prefab 时，所有选中节点必须是同一父节点的直接子节点。";
                    return false;
                }
            }

            string targetPrefabPath = context?.targetPrefabAssetPath ?? string.Empty;
            int separator = targetPrefabPath.LastIndexOf('/');
            if (separator <= 0)
            {
                error = "当前目标 Prefab 没有可用的资源目录。";
                return false;
            }

            string assetPath = targetPrefabPath.Substring(0, separator) + "/Common/" + normalizedName + ".prefab";
            string fullPath = Path.Combine(
                context.projectRoot ?? string.Empty,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null ||
                File.Exists(fullPath) ||
                File.Exists(fullPath + ".meta"))
            {
                error = "组件 Prefab 已存在，请换一个名称：" + assetPath;
                return false;
            }

            extraction = new PsdHierarchySelectedPrefabExtraction(
                normalizedName,
                assetPath,
                parentId,
                selectedNodeIds.OrderBy(id => id, StringComparer.Ordinal));
            return true;
        }

        internal bool TryCreateCrossParentPrefabExtraction(
            PsdHierarchyChatContext context,
            string componentName,
            out PsdHierarchyCrossParentPrefabExtraction extraction,
            out string error)
        {
            extraction = default(PsdHierarchyCrossParentPrefabExtraction);
            error = string.Empty;
            string normalizedName = (componentName ?? string.Empty).Trim();
            if (!IsPascalCaseIdentifier(normalizedName))
            {
                error = "Component name must be a PascalCase identifier, for example DaySignRewardItem.";
                return false;
            }

            if (selectedNodeIds.Count < 2 || string.IsNullOrWhiteSpace(crossParentRootNodeId))
            {
                error = "Cross-parent organization requires at least two selected nodes under a shared UI root.";
                return false;
            }

            if (!TryReadNodes(context?.hierarchySnapshotJson, out Dictionary<string, SnapshotNode> nodesById, out error))
            {
                return false;
            }

            var selectedSlots = new List<SequenceSlot>();
            foreach (string selectedId in selectedNodeIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!nodesById.TryGetValue(selectedId, out SnapshotNode selectedNode) ||
                    !TryParseSequenceSlot(selectedNode.name, out string prefix, out int sequence))
                {
                    error = "Cross-parent organization requires selected nodes with a shared numeric naming pattern, for example GiftBox4.";
                    return false;
                }

                if (selectedSlots.Any(slot => string.Equals(slot.prefix, prefix, StringComparison.Ordinal)))
                {
                    error = "Cross-parent organization requires one selected node for each visual role.";
                    return false;
                }

                selectedSlots.Add(new SequenceSlot(prefix, sequence, selectedId));
            }

            int groupSequence = selectedSlots
                .GroupBy(slot => slot.sequence)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First()
                .Key;
            var offsetsByPrefix = selectedSlots.ToDictionary(
                slot => slot.prefix,
                slot => slot.sequence - groupSequence,
                StringComparer.Ordinal);
            string[] roleOrder = offsetsByPrefix.Keys.OrderBy(prefix => prefix, StringComparer.Ordinal).ToArray();

            var nodesByRoleAndSequence = new Dictionary<string, Dictionary<int, List<SnapshotNode>>>(StringComparer.Ordinal);
            foreach (SnapshotNode node in nodesById.Values)
            {
                if (!crossParentCandidateNodeIds.Contains(node.id) ||
                    !TryParseSequenceSlot(node.name, out string prefix, out int sequence) ||
                    !offsetsByPrefix.ContainsKey(prefix))
                {
                    continue;
                }

                if (!nodesByRoleAndSequence.TryGetValue(prefix, out Dictionary<int, List<SnapshotNode>> bySequence))
                {
                    bySequence = new Dictionary<int, List<SnapshotNode>>();
                    nodesByRoleAndSequence.Add(prefix, bySequence);
                }

                if (!bySequence.TryGetValue(sequence, out List<SnapshotNode> matches))
                {
                    matches = new List<SnapshotNode>();
                    bySequence.Add(sequence, matches);
                }

                matches.Add(node);
            }

            var candidateSequences = new HashSet<int>();
            foreach (SequenceSlot slot in selectedSlots)
            {
                if (!nodesByRoleAndSequence.TryGetValue(slot.prefix, out Dictionary<int, List<SnapshotNode>> bySequence))
                {
                    continue;
                }

                foreach (int sequence in bySequence.Keys)
                {
                    candidateSequences.Add(sequence - offsetsByPrefix[slot.prefix]);
                }
            }

            var instances = new List<PsdHierarchyCrossParentPrefabInstance>();
            foreach (int candidateSequence in candidateSequences.OrderBy(sequence => sequence))
            {
                var sourceIds = new List<string>();
                bool isComplete = true;
                foreach (string role in roleOrder)
                {
                    int expectedSequence = candidateSequence + offsetsByPrefix[role];
                    if (!nodesByRoleAndSequence.TryGetValue(role, out Dictionary<int, List<SnapshotNode>> bySequence) ||
                        !bySequence.TryGetValue(expectedSequence, out List<SnapshotNode> matches) ||
                        matches.Count != 1)
                    {
                        isComplete = false;
                        break;
                    }

                    sourceIds.Add(matches[0].id);
                }

                if (isComplete)
                {
                    instances.Add(new PsdHierarchyCrossParentPrefabInstance(candidateSequence, sourceIds));
                }
            }

            PsdHierarchyCrossParentPrefabInstance templateInstance = instances.FirstOrDefault(instance =>
                new HashSet<string>(instance.sourceNodeIds, StringComparer.Ordinal).SetEquals(selectedNodeIds));
            if (templateInstance.sourceNodeIds == null || templateInstance.sourceNodeIds.Length != selectedNodeIds.Count)
            {
                error = "The selected nodes do not form a complete repeated local component group.";
                return false;
            }

            if (instances.Count < 2)
            {
                error = "No similar complete groups were found below the shared UI root.";
                return false;
            }

            var usedNodeIds = new HashSet<string>(
                instances.SelectMany(instance => instance.sourceNodeIds),
                StringComparer.Ordinal);
            string[] unmatchedNodeIds = nodesByRoleAndSequence.Values
                .SelectMany(bySequence => bySequence.Values)
                .SelectMany(nodes => nodes)
                .Select(node => node.id)
                .Where(id => !usedNodeIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            string targetPrefabPath = context?.targetPrefabAssetPath ?? string.Empty;
            int separator = targetPrefabPath.LastIndexOf('/');
            if (separator <= 0)
            {
                error = "The target Prefab has no usable asset directory.";
                return false;
            }

            string assetPath = targetPrefabPath.Substring(0, separator) + "/Common/" + normalizedName + ".prefab";
            string fullPath = Path.Combine(
                context.projectRoot ?? string.Empty,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null ||
                File.Exists(fullPath) ||
                File.Exists(fullPath + ".meta"))
            {
                error = "Component Prefab already exists: " + assetPath;
                return false;
            }

            extraction = new PsdHierarchyCrossParentPrefabExtraction(
                normalizedName,
                assetPath,
                crossParentRootNodeId,
                templateInstance.sourceNodeIds,
                instances,
                unmatchedNodeIds);
            return true;
        }

        internal void ValidatePlan(JObject plan)
        {
            if (plan == null)
            {
                throw new InvalidOperationException("局部修复计划为空。");
            }

            JArray selectedPrefabExtractions = plan["selectedPrefabExtractions"] as JArray;
            JArray crossParentPrefabExtractions = plan["crossParentPrefabExtractions"] as JArray;
            bool hasSelectedPrefabExtractions = selectedPrefabExtractions != null && selectedPrefabExtractions.Count > 0;
            bool hasCrossParentPrefabExtractions = crossParentPrefabExtractions != null && crossParentPrefabExtractions.Count > 0;
            if (hasSelectedPrefabExtractions && hasCrossParentPrefabExtractions)
            {
                throw new InvalidOperationException("A local organization plan can contain one extraction mode only.");
            }

            if (hasCrossParentPrefabExtractions)
            {
                ValidateCrossParentPrefabExtractions(plan, crossParentPrefabExtractions);
                return;
            }

            if (hasSelectedPrefabExtractions)
            {
                ValidateSelectedPrefabExtractions(plan, selectedPrefabExtractions);
                return;
            }

            RequireEmptyOperations(plan, "componentFamilyDecisions", "componentExtractions", "stateComponentExtractions", "variantComponentExtractions", "statefulComponentExtractions", "textureRenames", "spriteAtlasRenames", "flatSiblingResolutions");
            ValidateReferences(plan, "wrappers", "parent", true, true);
            ValidateReferences(plan, "moves", "source", false, false);
            ValidateReferences(plan, "moves", "destination", true, false);
            ValidateReferences(plan, "renames", "target", true, false);
            ValidateReferences(plan, "emptyContainerRemovals", "source", false, false);
            ValidateReferences(plan, "tightBounds", "target", true, false);
        }

        private void ValidateSelectedPrefabExtractions(JObject plan, JArray extractions)
        {
            RequireEmptyOperations(
                plan,
                "wrappers",
                "moves",
                "renames",
                "emptyContainerRemovals",
                "tightBounds",
                "componentFamilyDecisions",
                "componentExtractions",
                "stateComponentExtractions",
                "variantComponentExtractions",
                "statefulComponentExtractions",
                "textureRenames",
                "spriteAtlasRenames",
                "flatSiblingResolutions");
            if (extractions.Count != 1 || !(extractions[0] is JObject extraction))
            {
                throw new InvalidOperationException("局部选区抽取必须且只能包含一个 selectedPrefabExtractions 项。");
            }

            string parentReference = extraction.Value<string>("parent");
            if (!TryReadNodeId(parentReference, out string parentId) || !allowedContainerNodeIds.Contains(parentId))
            {
                throw new InvalidOperationException("局部选区抽取的 parent 必须是所有选中节点的直接父节点。");
            }

            if (!(extraction["sources"] is JArray sources) || sources.Count != selectedNodeIds.Count)
            {
                throw new InvalidOperationException("局部选区抽取的 sources 必须与当前选择完全一致。");
            }

            var planSourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken source in sources)
            {
                if (!TryReadNodeId(source.Value<string>(), out string sourceId) || !planSourceIds.Add(sourceId))
                {
                    throw new InvalidOperationException("局部选区抽取的 sources 必须是唯一的当前节点引用。");
                }
            }

            if (!planSourceIds.SetEquals(selectedNodeIds))
            {
                throw new InvalidOperationException("局部选区抽取的 sources 不得扩大或缩小当前选择。");
            }
        }

        private void ValidateCrossParentPrefabExtractions(JObject plan, JArray extractions)
        {
            RequireEmptyOperations(
                plan,
                "wrappers",
                "moves",
                "renames",
                "emptyContainerRemovals",
                "tightBounds",
                "componentFamilyDecisions",
                "componentExtractions",
                "stateComponentExtractions",
                "variantComponentExtractions",
                "statefulComponentExtractions",
                "textureRenames",
                "spriteAtlasRenames",
                "flatSiblingResolutions",
                "selectedPrefabExtractions");
            if (extractions.Count != 1 || !(extractions[0] is JObject extraction))
            {
                throw new InvalidOperationException("A cross-parent local organization plan must contain exactly one extraction.");
            }

            if (!TryReadNodeId(extraction.Value<string>("root"), out string rootId) ||
                !string.Equals(rootId, crossParentRootNodeId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The cross-parent extraction root must be the locked selection's lowest common ancestor.");
            }

            HashSet<string> templateSourceIds = ReadUniqueNodeReferences(
                extraction["templateSources"] as JArray,
                "templateSources");
            if (!templateSourceIds.SetEquals(selectedNodeIds))
            {
                throw new InvalidOperationException("The cross-parent templateSources must exactly match the locked selection.");
            }

            if (!(extraction["instances"] is JArray instances) || instances.Count < 2)
            {
                throw new InvalidOperationException("The cross-parent extraction requires at least two complete reviewed groups.");
            }

            var allInstanceSourceIds = new HashSet<string>(StringComparer.Ordinal);
            bool includesTemplate = false;
            foreach (JObject instance in instances.OfType<JObject>())
            {
                HashSet<string> sourceIds = ReadUniqueNodeReferences(instance["sources"] as JArray, "instances.sources");
                if (sourceIds.Count != selectedNodeIds.Count ||
                    sourceIds.Any(id => !crossParentCandidateNodeIds.Contains(id)) ||
                    sourceIds.Any(id => !allInstanceSourceIds.Add(id)))
                {
                    throw new InvalidOperationException("Each cross-parent group must be unique and remain inside the reviewed common root.");
                }

                includesTemplate |= sourceIds.SetEquals(selectedNodeIds);
            }

            if (!includesTemplate)
            {
                throw new InvalidOperationException("The cross-parent instances must include the selected template group.");
            }

            if (extraction["unmatched"] is JArray unmatched)
            {
                HashSet<string> unmatchedIds = unmatched.Count == 0
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : ReadUniqueNodeReferences(unmatched, "unmatched");
                foreach (string nodeId in unmatchedIds)
                {
                    if (!crossParentCandidateNodeIds.Contains(nodeId) || allInstanceSourceIds.Contains(nodeId))
                    {
                        throw new InvalidOperationException("Unmatched nodes must remain inside the common root and outside all replacement groups.");
                    }
                }
            }
        }

        private static HashSet<string> ReadUniqueNodeReferences(JArray references, string label)
        {
            if (references == null || references.Count == 0)
            {
                throw new InvalidOperationException(label + " must contain node references.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken reference in references)
            {
                if (!TryReadNodeId(reference.Value<string>(), out string nodeId) || !ids.Add(nodeId))
                {
                    throw new InvalidOperationException(label + " must contain unique node references.");
                }
            }

            return ids;
        }

        private static bool TryReadNodeId(string reference, out string nodeId)
        {
            const string prefix = "node:";
            nodeId = string.Empty;
            if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            nodeId = reference.Substring(prefix.Length);
            return !string.IsNullOrWhiteSpace(nodeId);
        }

        private void ValidateReferences(
            JObject plan,
            string arrayName,
            string propertyName,
            bool allowWrapperReference,
            bool allowContainerParent)
        {
            if (!(plan[arrayName] is JArray operations))
            {
                return;
            }

            for (int index = 0; index < operations.Count; index++)
            {
                if (!(operations[index] is JObject operation))
                {
                    throw new InvalidOperationException(arrayName + "[" + index + "] 必须是对象。");
                }

                string reference = operation.Value<string>(propertyName);
                if (string.IsNullOrWhiteSpace(reference))
                {
                    throw new InvalidOperationException(arrayName + "[" + index + "]." + propertyName + " 不能为空。");
                }

                if (allowWrapperReference && reference.StartsWith("@", StringComparison.Ordinal))
                {
                    continue;
                }

                const string prefix = "node:";
                if (!reference.StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(arrayName + "[" + index + "]." + propertyName + " 必须引用当前局部快照节点。");
                }

                string nodeId = reference.Substring(prefix.Length);
                if (editableNodeIds.Contains(nodeId) || (allowContainerParent && allowedContainerNodeIds.Contains(nodeId)))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "局部修复越界：" + arrayName + "[" + index + "]." + propertyName +
                    " 引用了选区外节点 " + nodeId + "。请扩大选区或重新生成计划。");
            }
        }

        private static void RequireEmptyOperations(JObject plan, params string[] names)
        {
            foreach (string name in names)
            {
                if (plan[name] is JArray operations && operations.Count > 0)
                {
                    throw new InvalidOperationException("局部修复不支持 " + name + "；请仅返回层级移动、命名和容器操作。");
                }
            }
        }

        private static void AddDescendants(
            string rootId,
            IReadOnlyDictionary<string, SnapshotNode> nodesById,
            ISet<string> results)
        {
            foreach (SnapshotNode child in nodesById.Values.Where(node => string.Equals(node.parentId, rootId, StringComparison.Ordinal)))
            {
                if (results.Add(child.id))
                {
                    AddDescendants(child.id, nodesById, results);
                }
            }
        }

        private static string FindLowestCommonAncestor(
            IEnumerable<string> selectedIds,
            IReadOnlyDictionary<string, SnapshotNode> nodesById)
        {
            string[] ids = (selectedIds ?? Array.Empty<string>()).ToArray();
            if (ids.Length == 0 || !nodesById.ContainsKey(ids[0]))
            {
                return string.Empty;
            }

            var firstAncestors = new List<string>();
            for (string current = ids[0]; !string.IsNullOrWhiteSpace(current);)
            {
                firstAncestors.Add(current);
                current = nodesById.TryGetValue(current, out SnapshotNode node) ? node.parentId : string.Empty;
            }

            foreach (string candidate in firstAncestors)
            {
                if (ids.All(id => IsDescendantOrSelf(id, candidate, nodesById)))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static bool IsDescendantOrSelf(
            string nodeId,
            string ancestorId,
            IReadOnlyDictionary<string, SnapshotNode> nodesById)
        {
            for (string current = nodeId; !string.IsNullOrWhiteSpace(current);)
            {
                if (string.Equals(current, ancestorId, StringComparison.Ordinal))
                {
                    return true;
                }

                current = nodesById.TryGetValue(current, out SnapshotNode node) ? node.parentId : string.Empty;
            }

            return false;
        }

        private static bool TryParseSequenceSlot(string name, out string prefix, out int sequence)
        {
            prefix = string.Empty;
            sequence = 0;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            int firstDigit = name.Length;
            while (firstDigit > 0 && char.IsDigit(name[firstDigit - 1]))
            {
                firstDigit--;
            }

            return firstDigit > 0 && firstDigit < name.Length &&
                   int.TryParse(name.Substring(firstDigit), out sequence) &&
                   (prefix = name.Substring(0, firstDigit)).Length > 0;
        }

        private static bool IsPascalCaseIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !char.IsUpper(value[0]))
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!char.IsLetterOrDigit(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadNodes(
            string snapshotJson,
            out Dictionary<string, SnapshotNode> nodesById,
            out string error)
        {
            nodesById = new Dictionary<string, SnapshotNode>(StringComparer.Ordinal);
            error = string.Empty;
            try
            {
                JArray nodes = JObject.Parse(snapshotJson ?? string.Empty)["nodes"] as JArray;
                if (nodes == null)
                {
                    error = "当前 Prefab 快照没有节点列表。";
                    return false;
                }

                foreach (JObject node in nodes.OfType<JObject>())
                {
                    string id = node.Value<string>("id");
                    if (string.IsNullOrWhiteSpace(id) || nodesById.ContainsKey(id))
                    {
                        error = "当前 Prefab 快照包含无效或重复节点 ID。";
                        return false;
                    }

                    string path = node.Value<string>("path") ?? string.Empty;
                    string name = node.Value<string>("name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        int separator = path.LastIndexOf('/');
                        name = separator >= 0 ? path.Substring(separator + 1) : path;
                    }

                    nodesById.Add(id, new SnapshotNode(id, node.Value<string>("parentId"), name));
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "无法读取当前 Prefab 快照：" + exception.Message;
                return false;
            }
        }

        private static Transform GetTransform(UnityEngine.Object selected)
        {
            if (selected is GameObject gameObject) return gameObject.transform;
            return selected is Component component ? component.transform : null;
        }

        private static string BuildPlanPath(Transform node)
        {
            var segments = new List<string>();
            for (Transform current = node; current != null; current = current.parent)
            {
                string segment = current.name;
                if (current.parent != null)
                {
                    int occurrence = 0;
                    for (int index = 0; index < current.parent.childCount; index++)
                    {
                        Transform sibling = current.parent.GetChild(index);
                        if (sibling == current) break;
                        if (string.Equals(sibling.name, current.name, StringComparison.Ordinal)) occurrence++;
                    }

                    if (occurrence > 0) segment += "#" + occurrence;
                }

                segments.Add(segment);
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private readonly struct SequenceSlot
        {
            internal SequenceSlot(string prefix, int sequence, string nodeId)
            {
                this.prefix = prefix ?? string.Empty;
                this.sequence = sequence;
                this.nodeId = nodeId ?? string.Empty;
            }

            internal readonly string prefix;
            internal readonly int sequence;
            internal readonly string nodeId;
        }

        private readonly struct SnapshotNode
        {
            internal SnapshotNode(string id, string parentId, string name)
            {
                this.id = id ?? string.Empty;
                this.parentId = parentId ?? string.Empty;
                this.name = name ?? string.Empty;
            }

            internal readonly string id;
            internal readonly string parentId;
            internal readonly string name;
        }
    }
}
