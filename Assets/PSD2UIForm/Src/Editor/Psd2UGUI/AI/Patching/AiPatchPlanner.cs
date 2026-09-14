using System;
using System.Collections.Generic;
using System.Linq;
using AiPatchValidatorNamespace;
using UGF.EditorTools.Psd2UGUI;
using UiTypeCompatibilityRulesNamespace;

namespace AiPatchPlannerNamespace
{
    internal sealed class AiPatchPlanner
    {
        private sealed class NodeTypeChange
        {
            public AiAnalysisNodeEntry _node;

            public GUIType _currentUiType;

            public GUIType _plannedUiType;

            private static NodeTypeChange s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static NodeTypeChange GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class OwnerPlan
        {
            public string _ownerId;

            public GUIType _ownerType;

            public string _carrierNodeId;

            public string _targetNodeId;

            public string _generatedNodeName;

            public string _generatedParentId;

            public int _insertIndex;

            public string[] _memberNodeIds;

            public string[] _memberRootIds;

            public float _confidence;

            public string _reason;

            public bool _requiresGeneratedNode;

            internal static OwnerPlan s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static OwnerPlan GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class RolePlan
        {
            public OwnerPlan _owner;

            public GUIType _roleType;

            public string _carrierNodeId;

            public string _targetNodeId;

            public string _generatedNodeName;

            public int _insertIndex;

            public string[] _memberNodeIds;

            public string[] _memberRootIds;

            public float _confidence;

            public string _reason;

            public bool _requiresGeneratedNode;

            internal static RolePlan s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static RolePlan GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class NodePatchPlan
        {
            public string _nodeId;

            public string _newParentId;

            public int _insertIndex;

            public float _confidence;

            public string _reason;

            internal static NodePatchPlan s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static NodePatchPlan GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        internal static AiPatchPlanner s_ObfuscationSentinel;

        internal bool TryBuildPatchFromCombinedRecognition(AiAnalysisPackageDocument aiAnalysisPackageDocument, AiRecognitionCombinedResultDocument aiRecognitionCombinedResultDocument, out AiPatchDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (aiAnalysisPackageDocument != null && aiAnalysisPackageDocument.nodes != null && aiRecognitionCombinedResultDocument != null && aiRecognitionCombinedResultDocument.nodeLabels != null)
            {
                Dictionary<string, AiAnalysisNodeEntry> dictionary = BuildNodeLookup(aiAnalysisPackageDocument.nodes);
                Dictionary<string, NodeTypeChange> dictionary2 = BuildNodeTypeChanges(aiAnalysisPackageDocument.nodes, aiRecognitionCombinedResultDocument.nodeLabels);
                if (!TryBuildOwnerPlans(aiRecognitionCombinedResultDocument.owners, dictionary, out var list, out result2))
                {
                    return false;
                }
                if (TryBuildRolePlans(aiRecognitionCombinedResultDocument.roles, list, dictionary, out var list2, out result2))
                {
                    ApplyRoleCarrierConflictCorrections(dictionary, list, list2);
                    ApplyPlannedNodeTypes(dictionary2, dictionary, list, list2);
                    List<NodePatchPlan> list3 = BuildNodeMovePlans(dictionary, list, list2);
                    result = new AiPatchDocument
                    {
                        version = "2.0",
                        treeHash = (aiAnalysisPackageDocument.treeHash ?? string.Empty),
                        analysis = new List<AiAuditEntry>((list?.Count ?? 0) + (list2?.Count ?? 0)),
                        operations = new List<AiPatchOperation>(64)
                    };
                    AddGroupCreationOperations(result.operations, list, list2);
                    AddNodeMoveOperations(result.operations, list3);
                    AddUiTypeOperations(result.operations, dictionary2, list, list2);
                    AddAuditEntries(result.analysis, dictionary2, list, list2);
                    if (!string.IsNullOrEmpty(aiRecognitionCombinedResultDocument.organizerVersion))
                    {
                        if (aiRecognitionCombinedResultDocument.organizerVersion != "1.0")
                        { result2 = "不支持的整理方案版本。"; return false; }
                        var owners = list.ToDictionary(owner => "owner:" + owner._ownerId, owner => owner._targetNodeId, StringComparer.Ordinal);
                        Func<string, string> resolve = id => id != null && owners.TryGetValue(id, out var target) ? target : id;
                        foreach (var rename in aiRecognitionCombinedResultDocument.renames ?? new List<AiOrganizerRename>())
                        {
                            if (rename == null) { result2 = "改名建议为空。"; return false; }
                            result.operations.Add(new AiPatchOperation { op = "rename_node", targetId = resolve(rename.nodeId), name = rename.name, confidence = 1, reason = rename.reason ?? "整理命名" });
                        }
                        foreach (var component in aiRecognitionCombinedResultDocument.components ?? new List<AiOrganizerComponent>())
                        {
                            if (component == null) { result2 = "公共组件建议为空。"; return false; }
                            result.components.Add(new AiOrganizerComponent { name = component.name, mode = component.mode,
                                rootIds = component.rootIds?.Select(resolve).ToArray(), reason = component.reason });
                        }
                    }
                    AiPatchValidator.NormalizeOperationOrder(result);
                    return true;
                }
                return false;
            }
            result2 = "Recognition synthesis input is invalid.";
            return false;
        }

        internal bool TryBuildPatchFromLegacyRecognition(AiAnalysisPackageDocument aiAnalysisPackageDocument, AiMainTypeResultDocument aiMainTypeResultDocument, AiChildRelationResultDocument aiChildRelationResultDocument, out AiPatchDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (aiAnalysisPackageDocument != null && aiMainTypeResultDocument != null && aiChildRelationResultDocument != null)
            {
                AiRecognitionCombinedResultDocument aiRecognitionCombinedResultDocument = new AiRecognitionCombinedResultDocument
                {
                    version = "2.0",
                    treeHash = (aiAnalysisPackageDocument.treeHash ?? string.Empty),
                    owners = new List<AiRecognitionOwnerEntry>((aiMainTypeResultDocument.nodes != null) ? aiMainTypeResultDocument.nodes.Count : 0),
                    roles = new List<AiRecognitionRoleEntry>((aiChildRelationResultDocument.relations != null) ? aiChildRelationResultDocument.relations.Count : 0),
                    nodeLabels = new List<AiRecognitionNodeLabelEntry>((aiAnalysisPackageDocument.nodes != null) ? aiAnalysisPackageDocument.nodes.Count : 0)
                };
                if (aiAnalysisPackageDocument.nodes != null)
                {
                    for (int i = 0; i < aiAnalysisPackageDocument.nodes.Count; i++)
                    {
                        AiAnalysisNodeEntry aiAnalysisNodeEntry = aiAnalysisPackageDocument.nodes[i];
                        if (aiAnalysisNodeEntry == null || string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                        {
                            continue;
                        }
                        GUIType gUIType = UiTypeCompatibilityRules.InferBaseUiType(aiAnalysisNodeEntry);
                        if (aiMainTypeResultDocument.nodes != null)
                        {
                            for (int j = 0; j < aiMainTypeResultDocument.nodes.Count; j++)
                            {
                                AiMainTypeEntry aiMainTypeEntry = aiMainTypeResultDocument.nodes[j];
                                if (aiMainTypeEntry == null || !string.Equals(aiMainTypeEntry.targetId, aiAnalysisNodeEntry.id, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                                if (UiTypeCompatibilityRules.TryParseOwnerType(aiMainTypeEntry.predictedUIType, out var gUIType2))
                                {
                                    gUIType2 = UiTypeCompatibilityRules.NormalizeUiTypeAlias(gUIType2);
                                    if (gUIType2 == GUIType.Image || gUIType2 == GUIType.Text || gUIType2 == GUIType.Mask || gUIType2 == GUIType.FillColor || gUIType2 == GUIType.Panel || gUIType2 == GUIType.Null)
                                    {
                                        gUIType = gUIType2;
                                    }
                                }
                                break;
                            }
                        }
                        aiRecognitionCombinedResultDocument.nodeLabels.Add(new AiRecognitionNodeLabelEntry
                        {
                            nodeId = aiAnalysisNodeEntry.id,
                            currentUIType = GetCurrentUiType(aiAnalysisNodeEntry).ToString(),
                            labelType = gUIType.ToString(),
                            confidence = 0.9f,
                            reason = "旧版识别结果升级的 nodeLabel。"
                        });
                    }
                }
                if (aiMainTypeResultDocument.nodes != null)
                {
                    for (int k = 0; k < aiMainTypeResultDocument.nodes.Count; k++)
                    {
                        AiMainTypeEntry aiMainTypeEntry2 = aiMainTypeResultDocument.nodes[k];
                        if (aiMainTypeEntry2 != null && !string.IsNullOrWhiteSpace(aiMainTypeEntry2.targetId) && UiTypeCompatibilityRules.TryParseOwnerType(aiMainTypeEntry2.predictedUIType, out var gUIType3))
                        {
                            aiRecognitionCombinedResultDocument.owners.Add(new AiRecognitionOwnerEntry
                            {
                                ownerId = aiMainTypeEntry2.targetId,
                                ownerType = gUIType3.ToString(),
                                carrierNodeId = aiMainTypeEntry2.targetId,
                                memberNodeIds = new string[1] { aiMainTypeEntry2.targetId },
                                confidence = NormalizeConfidence(aiMainTypeEntry2.confidence),
                                reason = (aiMainTypeEntry2.reason ?? "旧版主类型识别结果升级的 owner。")
                            });
                        }
                    }
                }
                if (aiChildRelationResultDocument.relations != null)
                {
                    for (int l = 0; l < aiChildRelationResultDocument.relations.Count; l++)
                    {
                        AiChildRelationEntry aiChildRelationEntry = aiChildRelationResultDocument.relations[l];
                        if (aiChildRelationEntry != null)
                        {
                            aiRecognitionCombinedResultDocument.roles.Add(new AiRecognitionRoleEntry
                            {
                                ownerId = (aiChildRelationEntry.ownerId ?? string.Empty),
                                roleType = (aiChildRelationEntry.roleType ?? string.Empty),
                                carrierNodeId = (aiChildRelationEntry.targetId ?? string.Empty),
                                memberNodeIds = CloneStringArray(aiChildRelationEntry.memberNodeIds),
                                confidence = NormalizeConfidence(aiChildRelationEntry.confidence),
                                reason = (aiChildRelationEntry.reason ?? "旧版子控件关系升级的 role。")
                            });
                        }
                    }
                }
                return TryBuildPatchFromCombinedRecognition(aiAnalysisPackageDocument, aiRecognitionCombinedResultDocument, out result, out result2);
            }
            result2 = "Legacy recognition synthesis input is invalid.";
            return false;
        }

        private static Dictionary<string, NodeTypeChange> BuildNodeTypeChanges(List<AiAnalysisNodeEntry> values, List<AiRecognitionNodeLabelEntry> values2)
        {
            Dictionary<string, NodeTypeChange> dictionary = new Dictionary<string, NodeTypeChange>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, AiRecognitionNodeLabelEntry> dictionary2 = new Dictionary<string, AiRecognitionNodeLabelEntry>(StringComparer.OrdinalIgnoreCase);
            if (values2 != null)
            {
                for (int i = 0; i < values2.Count; i++)
                {
                    AiRecognitionNodeLabelEntry aiRecognitionNodeLabelEntry = values2[i];
                    if (aiRecognitionNodeLabelEntry != null && !string.IsNullOrWhiteSpace(aiRecognitionNodeLabelEntry.nodeId))
                    {
                        dictionary2[aiRecognitionNodeLabelEntry.nodeId] = aiRecognitionNodeLabelEntry;
                    }
                }
            }
            if (values == null)
            {
                return dictionary;
            }
            for (int j = 0; j < values.Count; j++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = values[j];
                if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                {
                    GUIType currentUiType = GetCurrentUiType(aiAnalysisNodeEntry);
                    AiRecognitionNodeLabelEntry value;
                    GUIType gUIType;
                    GUIType plannedUiType = ((!dictionary2.TryGetValue(aiAnalysisNodeEntry.id, out value) || value == null || !UiTypeCompatibilityRules.TryParseBaseLayerType(value.labelType, out gUIType)) ? UiTypeCompatibilityRules.InferBaseUiType(aiAnalysisNodeEntry) : gUIType);
                    dictionary[aiAnalysisNodeEntry.id] = new NodeTypeChange
                    {
                        _node = aiAnalysisNodeEntry,
                        _currentUiType = currentUiType,
                        _plannedUiType = plannedUiType
                    };
                }
            }
            return dictionary;
        }

        private static bool TryBuildOwnerPlans(List<AiRecognitionOwnerEntry> values, Dictionary<string, AiAnalysisNodeEntry> lookup, out List<OwnerPlan> result, out string result2)
        {
            result = new List<OwnerPlan>(values?.Count ?? 0);
            result2 = null;
            if (values == null)
            {
                return true;
            }
            int num = 0;
            while (true)
            {
                if (num < values.Count)
                {
                    AiRecognitionOwnerEntry aiRecognitionOwnerEntry = values[num];
                    if (aiRecognitionOwnerEntry == null || string.IsNullOrWhiteSpace(aiRecognitionOwnerEntry.ownerId) || !UiTypeCompatibilityRules.TryParseOwnerType(aiRecognitionOwnerEntry.ownerType, out var gUIType))
                    {
                        break;
                    }
                    string[] array = NormalizeMemberRootIds(aiRecognitionOwnerEntry.memberNodeIds, lookup);
                    if (array.Length >= 1)
                    {
                        string text = ResolveOwnerCarrierNodeId(aiRecognitionOwnerEntry, gUIType, lookup);
                        bool flag;
                        string text2 = ((flag = string.IsNullOrWhiteSpace(text)) ? BuildGeneratedNodeId("gen:owner:", gUIType, aiRecognitionOwnerEntry.ownerId) : text);
                        string text3 = (flag ? ResolveGeneratedOwnerParentId(array, lookup) : string.Empty);
                        result.Add(new OwnerPlan
                        {
                            _ownerId = aiRecognitionOwnerEntry.ownerId,
                            _ownerType = gUIType,
                            _carrierNodeId = text,
                            _targetNodeId = text2,
                            _generatedNodeName = (flag ? BuildGeneratedNodeName("gen_owner", gUIType, aiRecognitionOwnerEntry.ownerId) : string.Empty),
                            _generatedParentId = text3,
                            _insertIndex = (flag ? GetMinimumSiblingIndex(array, lookup) : (-1)),
                            _memberNodeIds = CloneStringArray(aiRecognitionOwnerEntry.memberNodeIds),
                            _memberRootIds = array,
                            _confidence = NormalizeConfidence(aiRecognitionOwnerEntry.confidence),
                            _reason = (string.IsNullOrWhiteSpace(aiRecognitionOwnerEntry.reason) ? "AI owner 识别结果。" : aiRecognitionOwnerEntry.reason),
                            _requiresGeneratedNode = flag
                        });
                        num++;
                        continue;
                    }
                    result2 = $"Owner[{num}] has no valid member roots.";
                    return false;
                }
                return true;
            }
            result2 = $"Owner[{num}] is invalid.";
            return false;
        }

        private static bool TryBuildRolePlans(List<AiRecognitionRoleEntry> values, List<OwnerPlan> values2, Dictionary<string, AiAnalysisNodeEntry> lookup, out List<RolePlan> result, out string result2)
        {
            result = new List<RolePlan>(values?.Count ?? 0);
            result2 = null;
            if (values == null)
            {
                return true;
            }
            Dictionary<string, OwnerPlan> dictionary = new Dictionary<string, OwnerPlan>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < values2.Count; i++)
            {
                OwnerPlan ownerPlan = values2[i];
                if (ownerPlan != null && !string.IsNullOrWhiteSpace(ownerPlan._ownerId))
                {
                    dictionary[ownerPlan._ownerId] = ownerPlan;
                }
            }
            int num = 0;
            while (true)
            {
                if (num < values.Count)
                {
                    AiRecognitionRoleEntry aiRecognitionRoleEntry = values[num];
                    if (aiRecognitionRoleEntry == null || string.IsNullOrWhiteSpace(aiRecognitionRoleEntry.ownerId))
                    {
                        break;
                    }
                    if (dictionary.TryGetValue(aiRecognitionRoleEntry.ownerId, out var value) && value != null)
                    {
                        if (UiTypeCompatibilityRules.TryParseAuxiliaryRoleType(aiRecognitionRoleEntry.roleType, out var gUIType))
                        {
                            string[] array = NormalizeMemberRootIds(aiRecognitionRoleEntry.memberNodeIds, lookup);
                            if (array.Length >= 1)
                            {
                                string text = ResolveRoleCarrierNodeId(aiRecognitionRoleEntry, gUIType, array, value, lookup);
                                bool flag;
                                string targetNodeId = ((flag = string.IsNullOrWhiteSpace(text)) ? BuildGeneratedNodeId("gen:role:", gUIType, value._ownerId + ":" + aiRecognitionRoleEntry.roleType) : text);
                                if (flag || !UiTypeCompatibilityRules.RoleRequiresTextLayer(gUIType) || (lookup.TryGetValue(text, out var value2) && value2 != null && value2.isTextLayer))
                                {
                                    result.Add(new RolePlan
                                    {
                                        _owner = value,
                                        _roleType = gUIType,
                                        _carrierNodeId = text,
                                        _targetNodeId = targetNodeId,
                                        _generatedNodeName = (flag ? BuildGeneratedRoleNodeName(gUIType, value, lookup) : string.Empty),
                                        _insertIndex = (flag ? GetMinimumSiblingIndex(array, lookup) : (-1)),
                                        _memberNodeIds = CloneStringArray(aiRecognitionRoleEntry.memberNodeIds),
                                        _memberRootIds = array,
                                        _confidence = NormalizeConfidence(aiRecognitionRoleEntry.confidence),
                                        _reason = (string.IsNullOrWhiteSpace(aiRecognitionRoleEntry.reason) ? "AI role 识别结果。" : aiRecognitionRoleEntry.reason),
                                        _requiresGeneratedNode = flag
                                    });
                                    num++;
                                    continue;
                                }
                                result2 = $"Role[{num}] text role carrier is invalid.";
                                return false;
                            }
                            result2 = $"Role[{num}] has no valid member roots.";
                            return false;
                        }
                        result2 = $"Role[{num}] has invalid roleType.";
                        return false;
                    }
                    result2 = $"Role[{num}] references unknown owner.";
                    return false;
                }
                return true;
            }
            result2 = $"Role[{num}] is invalid.";
            return false;
        }

        private static void ApplyRoleCarrierConflictCorrections(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup != null && values != null && values2 != null)
            {
                ResolveRoleCarrierConflicts(lookup, values, values2);
            }
        }

        private static void DowngradeUnsupportedToggleGroups(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || values == null || values2 == null)
            {
                return;
            }
            for (int i = 0; i < values.Count; i++)
            {
                OwnerPlan ownerPlan = values[i];
                if (ownerPlan == null || ownerPlan._ownerType != GUIType.ToggleGroup)
                {
                    continue;
                }
                bool flag = false;
                bool flag2 = false;
                for (int j = 0; j < values2.Count; j++)
                {
                    RolePlan rolePlan = values2[j];
                    if (rolePlan != null && rolePlan._owner == ownerPlan && (rolePlan._roleType == GUIType.Toggle_Checkmark || rolePlan._roleType == GUIType.Toggle_Label))
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    for (int k = 0; k < values.Count; k++)
                    {
                        OwnerPlan ownerPlan2 = values[k];
                        if (ownerPlan2 != null && ownerPlan2 != ownerPlan && ownerPlan2._ownerType == GUIType.Toggle && !string.IsNullOrWhiteSpace(ownerPlan2._carrierNodeId) && IsWithinOwnerSubtree(ownerPlan2._carrierNodeId, ownerPlan, lookup))
                        {
                            flag2 = true;
                            break;
                        }
                    }
                }
                if (flag || flag2)
                {
                    continue;
                }
                ownerPlan._ownerType = GUIType.Null;
                ownerPlan._reason = "本地契约修正：无 Toggle 语义支撑，ToggleGroup 降级为 Null 容器。";
                ownerPlan._confidence = Math.Max(ownerPlan._confidence, 0.92f);
                for (int num = values2.Count - 1; num >= 0; num--)
                {
                    if (values2[num] != null && values2[num]._owner == ownerPlan)
                    {
                        values2.RemoveAt(num);
                    }
                }
            }
        }

        private static bool HasRole(object value, List<RolePlan> values, GUIType uiType)
        {
            return FindRole(value, values, uiType) != null;
        }

        private static RolePlan FindRole(object value, List<RolePlan> values, GUIType uiType)
        {
            if (value != null && values != null)
            {
                uiType = UiTypeCompatibilityRules.NormalizeUiTypeAlias(uiType);
                int num = 0;
                RolePlan rolePlan;
                while (true)
                {
                    if (num < values.Count)
                    {
                        rolePlan = values[num];
                        if (rolePlan != null && rolePlan._owner == value && rolePlan._roleType == uiType)
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return null;
                }
                return rolePlan;
            }
            return null;
        }

        private static void AddRoleIfMissing(List<RolePlan> values, object value, GUIType uiType, object value2, float value3, object value4)
        {
            if (values != null && value != null && !string.IsNullOrWhiteSpace((string)value2) && FindRole(value, values, uiType) == null)
            {
                values.Add(new RolePlan
                {
                    _owner = (OwnerPlan)value,
                    _roleType = uiType,
                    _carrierNodeId = (string)value2,
                    _targetNodeId = (string)value2,
                    _generatedNodeName = string.Empty,
                    _insertIndex = -1,
                    _memberNodeIds = new string[1] { (string)value2 },
                    _memberRootIds = new string[1] { (string)value2 },
                    _confidence = NormalizeConfidence(value3),
                    _reason = (string)value4,
                    _requiresGeneratedNode = false
                });
            }
        }

        private static string FindUniqueTextDescendant(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && lookup != null)
            {
                string result = string.Empty;
                int num = int.MinValue;
                int num2 = 0;
                foreach (KeyValuePair<string, AiAnalysisNodeEntry> item in lookup)
                {
                    AiAnalysisNodeEntry value = item.Value;
                    if (value != null && value.isTextLayer && IsWithinOwnerSubtree(value.id, value2, lookup))
                    {
                        num2++;
                        int num3 = 300;
                        if (IsDirectChildOfOwner(value2, value.id, lookup))
                        {
                            num3 += 200;
                        }
                        if (NodeNameContainsAnyToken(value, "text", "label", "title", "name"))
                        {
                            num3 += 120;
                        }
                        if (num3 > num)
                        {
                            num = num3;
                            result = value.id;
                        }
                    }
                }
                if (num2 != 1 && num < 500)
                {
                    return string.Empty;
                }
                return result;
            }
            return string.Empty;
        }

        private static string FindBestRoleCarrier(object value3, GUIType uiType, object value4, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value3 != null && lookup != null)
            {
                string text = string.Empty;
                int num = int.MinValue;
                foreach (KeyValuePair<string, AiAnalysisNodeEntry> item in lookup)
                {
                    AiAnalysisNodeEntry value = item.Value;
                    if (value != null && !string.IsNullOrWhiteSpace(value.id) && !string.Equals(value.id, (string)value4, StringComparison.OrdinalIgnoreCase) && !string.Equals(value.id, ((OwnerPlan)value3)._targetNodeId, StringComparison.OrdinalIgnoreCase) && IsWithinOwnerSubtree(value.id, value3, lookup) && UiTypeCompatibilityRules.IsRoleNodeCompatible(uiType, value))
                    {
                        int num2 = ScoreRoleCarrier(value3, value, uiType, lookup);
                        if (num2 > num)
                        {
                            num = num2;
                            text = value.id;
                        }
                    }
                }
                if (num < 300)
                {
                    return string.Empty;
                }
                if (TryPromoteRoleCarrierToWrapper(text, uiType, value3, lookup, out var text2) && !string.IsNullOrWhiteSpace(text2) && ((!lookup.TryGetValue(text2, out var value2) || value2 == null) ? int.MinValue : ScoreRoleCarrier(value3, value2, uiType, lookup)) >= num)
                {
                    text = text2;
                }
                return text;
            }
            return string.Empty;
        }

        private static int ScoreRoleCarrier(object value, object value2, GUIType uiType, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && value2 != null)
            {
                int num = 0;
                if (IsDirectChildOfOwner(value, ((AiAnalysisNodeEntry)value2).id, lookup))
                {
                    num += 220;
                }
                if (((AiAnalysisNodeEntry)value2).isGroupLayer)
                {
                    num += 120;
                }
                if (((AiAnalysisNodeEntry)value2).childCount == 1 || (!string.IsNullOrWhiteSpace(((AiAnalysisNodeEntry)value2).onlyChildId) && ((AiAnalysisNodeEntry)value2).childCount <= 2))
                {
                    num += 80;
                }
                switch (uiType)
                {
                case GUIType.Slider_Handle:
                    if (NodeNameContainsAnyToken(value2, "handle", "thumb", "knob", "arrow"))
                    {
                        num += 460;
                    }
                    break;
                case GUIType.Slider_Fill:
                    if (NodeNameContainsAnyToken(value2, "bar", "fill", "progress", "loading", "exp"))
                    {
                        num += 440;
                    }
                    if (NodeNameContainsAnyToken(value2, "bg", "background", "levelbg", "barbg"))
                    {
                        num -= 180;
                    }
                    break;
                case GUIType.Background:
                    if (NodeNameContainsAnyToken(value2, "bg", "background", "back", "base", "frame", "levelbg", "barbg", "btn"))
                    {
                        num += 420;
                    }
                    if (NodeNameContainsAnyToken(value2, "bar", "fill", "progress", "loading", "exp"))
                    {
                        num -= 160;
                    }
                    break;
                }
                return num;
            }
            return int.MinValue;
        }

        private static bool HasSliderTrackAndFillEvidence(object value4, object value5, object value6, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value4 != null && !string.IsNullOrWhiteSpace((string)value5) && !string.IsNullOrWhiteSpace((string)value6))
            {
                bool flag = false;
                if (!string.IsNullOrWhiteSpace(((OwnerPlan)value4)._carrierNodeId) && lookup.TryGetValue(((OwnerPlan)value4)._carrierNodeId, out var value) && value != null)
                {
                    flag = NodeNameContainsAnyToken(value, "slider", "bar", "progress", "exp", "loading", "level");
                }
                AiAnalysisNodeEntry value2;
                bool flag2 = lookup.TryGetValue((string)value5, out value2) && value2 != null && NodeNameContainsAnyToken(value2, "bg", "background", "back", "base", "levelbg", "barbg");
                AiAnalysisNodeEntry value3;
                bool flag3 = lookup.TryGetValue((string)value6, out value3) && value3 != null && NodeNameContainsAnyToken(value3, "bar", "fill", "progress", "loading", "exp");
                if (flag2 && flag3)
                {
                    if (flag)
                    {
                        return true;
                    }
                    if (!ShareSliderSemanticContainer(value4, value5, value6, lookup))
                    {
                        return false;
                    }
                    return !HasCompetingSliderGroup(value4, value5, value6, lookup);
                }
                return false;
            }
            return false;
        }

        private static bool LooksLikeButtonSubtree(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && lookup != null && ((AiAnalysisNodeEntry)value2).isGroupLayer)
            {
                bool flag;
                if (!(flag = NodeNameContainsAnyToken(value2, "button", "btn", "close", "next", "prev", "play") || NodeNameContainsAnyToken(value2, "card", "slot", "avatar", "equip", "frame")))
                {
                    return false;
                }
                if (((AiAnalysisNodeEntry)value2).childCount <= 6 && ((AiAnalysisNodeEntry)value2).renderLeafCount <= 18)
                {
                    bool flag2 = false;
                    bool flag3 = false;
                    bool flag4 = false;
                    foreach (KeyValuePair<string, AiAnalysisNodeEntry> item in lookup)
                    {
                        AiAnalysisNodeEntry value = item.Value;
                        if (value != null && !string.Equals(value.id, ((AiAnalysisNodeEntry)value2).id, StringComparison.OrdinalIgnoreCase) && IsSameOrDescendant(value.id, ((AiAnalysisNodeEntry)value2).id, lookup) && GetDescendantDistance(value.id, ((AiAnalysisNodeEntry)value2).id, lookup) <= 2)
                        {
                            if (!flag2 && !value.isTextLayer && NodeNameContainsAnyToken(value, "bg", "background", "back", "btn"))
                            {
                                flag2 = true;
                            }
                            if (!flag3 && value.isTextLayer)
                            {
                                flag3 = true;
                            }
                            if (!flag4 && !value.isTextLayer && NodeNameContainsAnyToken(value, "icon", "flag", "frame"))
                            {
                                flag4 = true;
                            }
                            if (flag2 && (flag3 || flag4) && (flag || NodeNameContainsAnyToken(value, "btn", "button")))
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static bool HasNestedInteractiveOwner(object value, List<OwnerPlan> values, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            return CountDirectChildInteractiveOwners(value, values, lookup) > 0;
        }

        private static int CountDirectChildInteractiveOwners(object value2, List<OwnerPlan> values, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && values != null && lookup != null && !string.IsNullOrWhiteSpace(((OwnerPlan)value2)._targetNodeId))
            {
                int num = 0;
                for (int i = 0; i < values.Count; i++)
                {
                    OwnerPlan ownerPlan = values[i];
                    if (ownerPlan != null && ownerPlan != value2 && !string.IsNullOrWhiteSpace(ownerPlan._carrierNodeId) && IsInteractiveOwnerType(ownerPlan._ownerType) && lookup.TryGetValue(ownerPlan._carrierNodeId, out var value) && value != null && string.Equals(value.parentId, ((OwnerPlan)value2)._targetNodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        num++;
                    }
                }
                return num;
            }
            return 0;
        }

        private static bool IsInteractiveOwnerType(GUIType uiType)
        {
            if ((uint)(uiType - 4) > 5u && uiType != GUIType.ToggleGroup)
            {
                return false;
            }
            return true;
        }

        private static bool ShareSliderSemanticContainer(object value4, object value5, object value6, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value4 != null && lookup != null)
            {
                if (IsDirectChildOfOwner(value4, value5, lookup) & IsDirectChildOfOwner(value4, value6, lookup))
                {
                    return true;
                }
                if (lookup.TryGetValue((string)value5, out var value) && value != null && lookup.TryGetValue((string)value6, out var value2) && value2 != null)
                {
                    if (!string.IsNullOrWhiteSpace(value.parentId) && string.Equals(value.parentId, value2.parentId, StringComparison.OrdinalIgnoreCase) && lookup.TryGetValue(value.parentId, out var value3) && value3 != null && IsWithinOwnerSubtree(value3.id, value4, lookup) && NodeNameContainsAnyToken(value3, "slider", "bar", "progress", "exp", "loading", "track"))
                    {
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static bool HasCompetingSliderGroup(object value2, object value3, object value4, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && lookup != null)
            {
                foreach (KeyValuePair<string, AiAnalysisNodeEntry> item in lookup)
                {
                    AiAnalysisNodeEntry value = item.Value;
                    if (value != null && value.isGroupLayer && !string.IsNullOrWhiteSpace(value.id) && !string.Equals(value.id, (string)value3, StringComparison.OrdinalIgnoreCase) && !string.Equals(value.id, (string)value4, StringComparison.OrdinalIgnoreCase) && IsDirectChildOfOwner(value2, value.id, lookup) && LooksLikeInformationPanel(value, lookup))
                    {
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        private static bool TryPromoteRoleCarrierToWrapper(object value3, GUIType uiType, object value4, Dictionary<string, AiAnalysisNodeEntry> lookup, out string result)
        {
            result = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)value3) && value4 != null && lookup != null && !UiTypeCompatibilityRules.RoleRequiresTextLayer(uiType))
            {
                string key = (string)value3;
                string text = (string)value3;
                AiAnalysisNodeEntry value;
                AiAnalysisNodeEntry value2;
                while (lookup.TryGetValue(key, out value) && value != null && !string.IsNullOrWhiteSpace(value.parentId) && lookup.TryGetValue(value.parentId, out value2) && value2 != null && IsCompatibleRoleWrapper(value2, value, uiType) && IsWithinOwnerSubtree(value2.id, value4, lookup) && !string.Equals(value2.id, ((OwnerPlan)value4)._targetNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    text = value2.id;
                    key = value2.id;
                }
                result = text;
                return !string.Equals(result, (string)value3, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static bool IsCompatibleRoleWrapper(object value, object value2, GUIType uiType)
        {
            if (value != null && value2 != null && ((AiAnalysisNodeEntry)value).isGroupLayer && !((AiAnalysisNodeEntry)value).isTextLayer)
            {
                if (((AiAnalysisNodeEntry)value).childCount != 1 && !string.Equals(((AiAnalysisNodeEntry)value).onlyChildId, ((AiAnalysisNodeEntry)value2).id, StringComparison.OrdinalIgnoreCase))
                {
                    return uiType switch
                    {
                        GUIType.Slider_Handle => NodeNameContainsAnyToken(value, "handle", "thumb", "knob", "arrow"), 
                        GUIType.Slider_Fill => NodeNameContainsAnyToken(value, "bar", "fill", "progress", "loading", "exp"), 
                        GUIType.Background => NodeNameContainsAnyToken(value, "bg", "background", "back", "base", "frame", "levelbg", "barbg", "btn"), 
                        _ => false, 
                    };
                }
                return true;
            }
            return false;
        }

        private static bool ContainsInteractiveOwner(object value, List<OwnerPlan> values, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && values != null && lookup != null)
            {
                int num = 0;
                while (true)
                {
                    if (num < values.Count)
                    {
                        OwnerPlan ownerPlan = values[num];
                        if (ownerPlan != null && !string.IsNullOrWhiteSpace(ownerPlan._carrierNodeId) && ownerPlan._ownerType != GUIType.Null && ownerPlan._ownerType != GUIType.Panel && ownerPlan._ownerType != GUIType.ToggleGroup && (string.Equals(ownerPlan._carrierNodeId, (string)value, StringComparison.OrdinalIgnoreCase) || IsStrictDescendant(value, ownerPlan._carrierNodeId, lookup)))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool IsDirectChildOfOwner(object value2, object value3, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && !string.IsNullOrWhiteSpace((string)value3) && lookup != null && lookup.TryGetValue((string)value3, out var value) && value != null)
            {
                if (!((OwnerPlan)value2)._requiresGeneratedNode && !string.IsNullOrWhiteSpace(((OwnerPlan)value2)._targetNodeId))
                {
                    return string.Equals(value.parentId, ((OwnerPlan)value2)._targetNodeId, StringComparison.OrdinalIgnoreCase);
                }
                int num = 0;
                while (true)
                {
                    if (num < ((OwnerPlan)value2)._memberRootIds.Length)
                    {
                        if (string.Equals(((OwnerPlan)value2)._memberRootIds[num], (string)value3, StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool IsWithinOwnerSubtree(object value, object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && value2 != null && lookup != null)
            {
                if (!((OwnerPlan)value2)._requiresGeneratedNode && !string.IsNullOrWhiteSpace(((OwnerPlan)value2)._carrierNodeId))
                {
                    if (!string.Equals((string)value, ((OwnerPlan)value2)._carrierNodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return IsStrictDescendant(value, ((OwnerPlan)value2)._carrierNodeId, lookup);
                    }
                    return true;
                }
                int num = 0;
                while (true)
                {
                    if (num < ((OwnerPlan)value2)._memberRootIds.Length)
                    {
                        string text = ((OwnerPlan)value2)._memberRootIds[num];
                        if (string.Equals((string)value, text, StringComparison.OrdinalIgnoreCase) || IsStrictDescendant(value, text, lookup))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool NodeNameContainsAnyToken(object value, params string[] expectedTokens)
        {
            if (value != null && expectedTokens != null && expectedTokens.Length >= 1)
            {
                if (((AiAnalysisNodeEntry)value).nameTokens != null)
                {
                    for (int i = 0; i < ((AiAnalysisNodeEntry)value).nameTokens.Length; i++)
                    {
                        if (ContainsAnyToken(((AiAnalysisNodeEntry)value).nameTokens[i], expectedTokens))
                        {
                            return true;
                        }
                    }
                }
                if (ContainsAnyToken(((AiAnalysisNodeEntry)value).name, expectedTokens))
                {
                    return true;
                }
                return ContainsAnyToken(((AiAnalysisNodeEntry)value).layerName, expectedTokens);
            }
            return false;
        }

        private static bool ContainsAnyToken(object value2, object value3)
        {
            if (string.IsNullOrWhiteSpace((string)value2) || value3 == null)
            {
                return false;
            }
            int num = 0;
            while (true)
            {
                if (num < ((Array)value3).Length)
                {
                    string value = (string)((object[])value3)[num];
                    if (!string.IsNullOrWhiteSpace(value) && ((string)value2).IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return false;
            }
            return true;
        }

        private static void AddInferredButtonOwners(Dictionary<string, AiAnalysisNodeEntry> lookup, Dictionary<string, NodeTypeChange> lookup2, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || values == null)
            {
                return;
            }
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < values.Count; i++)
            {
                OwnerPlan ownerPlan = values[i];
                if (ownerPlan != null && !string.IsNullOrWhiteSpace(ownerPlan._carrierNodeId))
                {
                    hashSet.Add(ownerPlan._carrierNodeId);
                }
            }
            List<AiAnalysisNodeEntry> list = new List<AiAnalysisNodeEntry>(lookup.Values);
            list.Sort((AiAnalysisNodeEntry left, AiAnalysisNodeEntry right) => GetSiblingIndex(lookup, left?.id).CompareTo(GetSiblingIndex(lookup, right?.id)));
            for (int num = 0; num < list.Count; num++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = list[num];
                if (aiAnalysisNodeEntry != null && aiAnalysisNodeEntry.isGroupLayer && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id) && !hashSet.Contains(aiAnalysisNodeEntry.id) && !ContainsInteractiveOwner(aiAnalysisNodeEntry.id, values, lookup) && LooksLikeButtonSubtree(aiAnalysisNodeEntry, lookup) && CountDirectChildInteractiveOwners(new OwnerPlan
                {
                    _targetNodeId = aiAnalysisNodeEntry.id
                }, values, lookup) < 2 && (lookup2 == null || !lookup2.TryGetValue(aiAnalysisNodeEntry.id, out var value) || value == null || value._plannedUiType == GUIType.Null || value._plannedUiType == GUIType.Panel))
                {
                    OwnerPlan ownerPlan2 = new OwnerPlan();
                    ownerPlan2._ownerId = aiAnalysisNodeEntry.id;
                    ownerPlan2._ownerType = GUIType.Button;
                    ownerPlan2._carrierNodeId = aiAnalysisNodeEntry.id;
                    ownerPlan2._targetNodeId = aiAnalysisNodeEntry.id;
                    ownerPlan2._generatedNodeName = string.Empty;
                    ownerPlan2._generatedParentId = string.Empty;
                    ownerPlan2._insertIndex = -1;
                    ownerPlan2._memberNodeIds = new string[1] { aiAnalysisNodeEntry.id };
                    ownerPlan2._memberRootIds = new string[1] { aiAnalysisNodeEntry.id };
                    ownerPlan2._confidence = 0.9f;
                    ownerPlan2._reason = "本地契约修正：检测到稳定按钮子树，补建 Button owner。";
                    ownerPlan2._requiresGeneratedNode = false;
                    OwnerPlan item = ownerPlan2;
                    values.Add(item);
                    hashSet.Add(aiAnalysisNodeEntry.id);
                }
            }
        }

        private static void DowngradeWeakButtonOwnersToPanels(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || values == null || values2 == null)
            {
                return;
            }
            for (int i = 0; i < values.Count; i++)
            {
                OwnerPlan ownerPlan = values[i];
                if (ownerPlan == null || ownerPlan._ownerType != GUIType.Button || string.IsNullOrWhiteSpace(ownerPlan._carrierNodeId) || !lookup.TryGetValue(ownerPlan._carrierNodeId, out var value) || value == null || !value.isGroupLayer || LooksLikeButtonSubtree(value, lookup) || !LooksLikeResourcePanelWithChildButton(ownerPlan, values, lookup))
                {
                    continue;
                }
                ownerPlan._ownerType = GUIType.Panel;
                ownerPlan._confidence = Math.Max(ownerPlan._confidence, 0.93f);
                ownerPlan._reason = "本地契约修正：资源/状态信息区含独立子按钮，弱 Button owner 降级为 Panel。";
                for (int num = values2.Count - 1; num >= 0; num--)
                {
                    RolePlan rolePlan = values2[num];
                    if (rolePlan != null && rolePlan._owner == ownerPlan && rolePlan._roleType != GUIType.Background)
                    {
                        values2.RemoveAt(num);
                    }
                }
            }
        }

        private static void AddInferredPanelOwners(Dictionary<string, AiAnalysisNodeEntry> lookup, Dictionary<string, NodeTypeChange> lookup2, List<OwnerPlan> values)
        {
            if (lookup == null || values == null)
            {
                return;
            }
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < values.Count; i++)
            {
                OwnerPlan ownerPlan = values[i];
                if (ownerPlan != null && !string.IsNullOrWhiteSpace(ownerPlan._carrierNodeId))
                {
                    hashSet.Add(ownerPlan._carrierNodeId);
                }
            }
            List<AiAnalysisNodeEntry> list = new List<AiAnalysisNodeEntry>(lookup.Values);
            list.Sort((AiAnalysisNodeEntry left, AiAnalysisNodeEntry right) => GetSiblingIndex(lookup, left?.id).CompareTo(GetSiblingIndex(lookup, right?.id)));
            for (int num = 0; num < list.Count; num++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = list[num];
                if (aiAnalysisNodeEntry != null && aiAnalysisNodeEntry.isGroupLayer && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id) && !hashSet.Contains(aiAnalysisNodeEntry.id) && (lookup2 == null || !lookup2.TryGetValue(aiAnalysisNodeEntry.id, out var value) || value == null || value._plannedUiType == GUIType.Null) && LooksLikeInformationPanel(aiAnalysisNodeEntry, lookup) && (!TryFindNearestContainingOwner(aiAnalysisNodeEntry.id, values, lookup, out var ownerPlan2) || ownerPlan2 == null || (ownerPlan2._ownerType != GUIType.Button && ownerPlan2._ownerType != GUIType.Toggle && ownerPlan2._ownerType != GUIType.Dropdown && ownerPlan2._ownerType != GUIType.InputField)))
                {
                    values.Add(new OwnerPlan
                    {
                        _ownerId = aiAnalysisNodeEntry.id,
                        _ownerType = GUIType.Panel,
                        _carrierNodeId = aiAnalysisNodeEntry.id,
                        _targetNodeId = aiAnalysisNodeEntry.id,
                        _generatedNodeName = string.Empty,
                        _generatedParentId = string.Empty,
                        _insertIndex = -1,
                        _memberNodeIds = new string[1] { aiAnalysisNodeEntry.id },
                        _memberRootIds = new string[1] { aiAnalysisNodeEntry.id },
                        _confidence = 0.84f,
                        _reason = "本地契约修正：检测到稳定的非交互信息子树，补建 Panel owner。",
                        _requiresGeneratedNode = false
                    });
                    hashSet.Add(aiAnalysisNodeEntry.id);
                }
            }
        }

        private static void UpgradePanelsToSliders(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || values == null || values2 == null)
            {
                return;
            }
            for (int i = 0; i < values.Count; i++)
            {
                OwnerPlan ownerPlan = values[i];
                if (ownerPlan == null || ownerPlan._ownerType != GUIType.Panel || HasRole(ownerPlan, values2, GUIType.Slider_Fill))
                {
                    continue;
                }
                string text = FindBestRoleCarrier(ownerPlan, GUIType.Background, null, lookup);
                string text2 = FindBestRoleCarrier(ownerPlan, GUIType.Slider_Fill, text, lookup);
                if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && HasSliderTrackAndFillEvidence(ownerPlan, text, text2, lookup))
                {
                    ownerPlan._ownerType = GUIType.Slider;
                    ownerPlan._confidence = Math.Max(ownerPlan._confidence, 0.94f);
                    ownerPlan._reason = "本地契约修正：检测到轨道与填充闭环，Panel 升级为 Slider。";
                    AddRoleIfMissing(values2, ownerPlan, GUIType.Background, text, ownerPlan._confidence, "本地契约补齐：Slider 的主轨道背景。");
                    AddRoleIfMissing(values2, ownerPlan, GUIType.Slider_Fill, text2, ownerPlan._confidence, "本地契约补齐：Slider 的填充条。");
                    string text3 = FindBestRoleCarrier(ownerPlan, GUIType.Slider_Handle, text2, lookup);
                    if (!string.IsNullOrWhiteSpace(text3))
                    {
                        AddRoleIfMissing(values2, ownerPlan, GUIType.Slider_Handle, text3, 0.88f, "本地契约补齐：Slider 的可见手柄。");
                    }
                }
            }
        }

        private static void AddMissingOwnerRoles(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || values == null || values2 == null)
            {
                return;
            }
            for (int i = 0; i < values.Count; i++)
            {
                OwnerPlan ownerPlan = values[i];
                if (ownerPlan == null)
                {
                    continue;
                }
                if (ownerPlan._ownerType == GUIType.Button)
                {
                    if (!HasRole(ownerPlan, values2, GUIType.Background))
                    {
                        string text = FindBestRoleCarrier(ownerPlan, GUIType.Background, null, lookup);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            AddRoleIfMissing(values2, ownerPlan, GUIType.Background, text, 0.9f, "本地契约补齐：Button 的主背景。");
                        }
                    }
                    if (!HasRole(ownerPlan, values2, GUIType.Button_Text))
                    {
                        string text2 = FindUniqueTextDescendant(ownerPlan, lookup);
                        if (!string.IsNullOrWhiteSpace(text2))
                        {
                            AddRoleIfMissing(values2, ownerPlan, GUIType.Button_Text, text2, 0.88f, "本地契约补齐：Button 的主标题文字。");
                        }
                    }
                }
                else if ((ownerPlan._ownerType == GUIType.Panel || ownerPlan._ownerType == GUIType.ToggleGroup) && !HasRole(ownerPlan, values2, GUIType.Background))
                {
                    string text3 = FindBestRoleCarrier(ownerPlan, GUIType.Background, null, lookup);
                    if (!string.IsNullOrWhiteSpace(text3))
                    {
                        AddRoleIfMissing(values2, ownerPlan, GUIType.Background, text3, 0.82f, "本地契约补齐：容器的唯一底板。");
                    }
                }
            }
        }

        private static bool LooksLikeInformationPanel(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && lookup != null && ((AiAnalysisNodeEntry)value2).isGroupLayer)
            {
                if (((AiAnalysisNodeEntry)value2).childCount >= 1 && ((AiAnalysisNodeEntry)value2).childCount <= 5 && ((AiAnalysisNodeEntry)value2).renderLeafCount >= 2 && ((AiAnalysisNodeEntry)value2).renderLeafCount <= 12)
                {
                    if (NodeNameContainsAnyToken(value2, "btn", "button", "play", "next", "prev", "close", "slider", "fill", "handle", "toggle", "dropdown", "input"))
                    {
                        return false;
                    }
                    bool flag = NodeNameContainsAnyToken(value2, "level", "num", "value", "badge", "info", "status");
                    bool flag2 = false;
                    bool flag3 = false;
                    bool flag4 = false;
                    foreach (KeyValuePair<string, AiAnalysisNodeEntry> item in lookup)
                    {
                        AiAnalysisNodeEntry value = item.Value;
                        if (value != null && !string.Equals(value.id, ((AiAnalysisNodeEntry)value2).id, StringComparison.OrdinalIgnoreCase) && IsSameOrDescendant(value.id, ((AiAnalysisNodeEntry)value2).id, lookup) && GetDescendantDistance(value.id, ((AiAnalysisNodeEntry)value2).id, lookup) <= 3)
                        {
                            if (!flag2 && value.isTextLayer)
                            {
                                flag2 = true;
                            }
                            if (!flag3 && !value.isTextLayer && NodeNameContainsAnyToken(value, "bg", "background", "back", "base", "frame", "levelbg"))
                            {
                                flag3 = true;
                            }
                            if (!flag4 && NodeNameContainsAnyToken(value, "fill", "bar", "progress", "loading", "exp", "handle"))
                            {
                                flag4 = true;
                            }
                        }
                    }
                    if (flag && flag2 && flag3)
                    {
                        return !flag4;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static bool LooksLikeResourcePanelWithChildButton(object value3, List<OwnerPlan> values, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value3 != null && values != null && lookup != null && !string.IsNullOrWhiteSpace(((OwnerPlan)value3)._carrierNodeId) && lookup.TryGetValue(((OwnerPlan)value3)._carrierNodeId, out var value) && value != null && value.isGroupLayer)
            {
                if (value.childCount >= 2 && value.childCount <= 6 && value.renderLeafCount >= 3 && value.renderLeafCount <= 12)
                {
                    if (!HasNestedInteractiveOwner(value3, values, lookup))
                    {
                        return false;
                    }
                    if (NodeNameContainsAnyToken(value, "button", "btn", "play", "next", "prev", "close"))
                    {
                        return false;
                    }
                    bool flag = false;
                    bool flag2 = false;
                    bool flag3 = false;
                    foreach (KeyValuePair<string, AiAnalysisNodeEntry> item in lookup)
                    {
                        AiAnalysisNodeEntry value2 = item.Value;
                        if (value2 != null && !string.Equals(value2.id, value.id, StringComparison.OrdinalIgnoreCase) && IsSameOrDescendant(value2.id, value.id, lookup) && GetDescendantDistance(value2.id, value.id, lookup) <= 3)
                        {
                            if (!flag && value2.isTextLayer)
                            {
                                flag = true;
                            }
                            if (!flag2 && !value2.isTextLayer && NodeNameContainsAnyToken(value2, "bg", "background", "back", "base", "frame", "status"))
                            {
                                flag2 = true;
                            }
                            if (!flag3 && !value2.isTextLayer && NodeNameContainsAnyToken(value2, "icon", "gem", "gold", "coin", "energy", "flag", "resource", "currency"))
                            {
                                flag3 = true;
                            }
                        }
                    }
                    return flag && flag2 && flag3;
                }
                return false;
            }
            return false;
        }

        private static bool TryFindNearestContainingOwner(object value, List<OwnerPlan> values, Dictionary<string, AiAnalysisNodeEntry> lookup, out OwnerPlan result)
        {
            result = null;
            if (!string.IsNullOrWhiteSpace((string)value) && values != null && lookup != null)
            {
                OwnerPlan ownerPlan = null;
                int num = int.MaxValue;
                for (int i = 0; i < values.Count; i++)
                {
                    OwnerPlan ownerPlan2 = values[i];
                    if (ownerPlan2 != null && !string.IsNullOrWhiteSpace(ownerPlan2._carrierNodeId) && IsStrictDescendant(value, ownerPlan2._carrierNodeId, lookup))
                    {
                        int num2 = GetDescendantDistance(value, ownerPlan2._carrierNodeId, lookup);
                        if (num2 >= 0 && num2 < num)
                        {
                            num = num2;
                            ownerPlan = ownerPlan2;
                        }
                    }
                }
                result = ownerPlan;
                return result != null;
            }
            return false;
        }

        private static void PromoteImageRoleCarriersToWrappers(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || values2 == null)
            {
                return;
            }
            for (int i = 0; i < values2.Count; i++)
            {
                RolePlan rolePlan = values2[i];
                if (rolePlan != null && !rolePlan._requiresGeneratedNode && !string.IsNullOrWhiteSpace(rolePlan._carrierNodeId) && !UiTypeCompatibilityRules.RoleRequiresTextLayer(rolePlan._roleType) && TryPromoteRoleCarrierToWrapper(rolePlan._carrierNodeId, rolePlan._roleType, rolePlan._owner, lookup, out var text) && !string.IsNullOrWhiteSpace(text) && !string.Equals(text, rolePlan._carrierNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    rolePlan._carrierNodeId = text;
                    rolePlan._targetNodeId = text;
                    rolePlan._memberRootIds = new string[1] { text };
                    rolePlan._reason = "本地契约修正：将图片 role 提升到最近自然 wrapper 承载。";
                    rolePlan._confidence = Math.Max(rolePlan._confidence, 0.92f);
                }
            }
        }

        private static void ResolveRoleCarrierConflicts(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || values2 == null)
            {
                return;
            }
            for (int i = 0; i < values2.Count; i++)
            {
                RolePlan rolePlan = values2[i];
                if (rolePlan != null && !rolePlan._requiresGeneratedNode && !string.IsNullOrWhiteSpace(rolePlan._carrierNodeId) && rolePlan._owner != null && string.Equals(rolePlan._carrierNodeId, rolePlan._owner._targetNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    ReassignConflictingRoleCarrier(rolePlan, null, lookup);
                }
            }
            Dictionary<string, List<RolePlan>> dictionary = new Dictionary<string, List<RolePlan>>(StringComparer.OrdinalIgnoreCase);
            for (int j = 0; j < values2.Count; j++)
            {
                RolePlan rolePlan2 = values2[j];
                if (rolePlan2 != null && !rolePlan2._requiresGeneratedNode && rolePlan2._owner != null && !string.IsNullOrWhiteSpace(rolePlan2._carrierNodeId))
                {
                    string key = rolePlan2._owner._targetNodeId + "\n" + rolePlan2._carrierNodeId;
                    if (!dictionary.TryGetValue(key, out var value))
                    {
                        value = new List<RolePlan>(2);
                        dictionary.Add(key, value);
                    }
                    value.Add(rolePlan2);
                }
            }
            foreach (KeyValuePair<string, List<RolePlan>> item in dictionary)
            {
                List<RolePlan> value2 = item.Value;
                if (value2 == null || value2.Count < 2)
                {
                    continue;
                }
                value2.Sort(delegate(RolePlan left, RolePlan right)
                {
                    int value3 = ScoreRolePlanCarrier(left, lookup);
                    return ScoreRolePlanCarrier(right, lookup).CompareTo(value3);
                });
                HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { value2[0]._carrierNodeId };
                for (int num = 1; num < value2.Count; num++)
                {
                    ReassignConflictingRoleCarrier(value2[num], hashSet, lookup);
                    if (!value2[num]._requiresGeneratedNode && !string.IsNullOrWhiteSpace(value2[num]._carrierNodeId))
                    {
                        hashSet.Add(value2[num]._carrierNodeId);
                    }
                }
            }
        }

        private static int ScoreRolePlanCarrier(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && ((RolePlan)value2)._owner != null && lookup != null && !string.IsNullOrWhiteSpace(((RolePlan)value2)._carrierNodeId) && lookup.TryGetValue(((RolePlan)value2)._carrierNodeId, out var value) && value != null)
            {
                return ScoreRoleCarrier(((RolePlan)value2)._owner, value, ((RolePlan)value2)._roleType, lookup);
            }
            return int.MinValue;
        }

        private static void ReassignConflictingRoleCarrier(object value, HashSet<string> texts, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && ((RolePlan)value)._owner != null && lookup != null)
            {
                string[] array = GetValidRoleMemberRoots(value, lookup);
                if (TryResolveIndependentRoleCarrier(value, array, texts, lookup, out var text))
                {
                    ((RolePlan)value)._carrierNodeId = text;
                    ((RolePlan)value)._targetNodeId = text;
                    ((RolePlan)value)._memberRootIds = ((array != null && array.Length != 0) ? array : new string[1] { text });
                    ((RolePlan)value)._reason = "本地契约修正：role carrier 与 owner/其他 role 冲突，回退到更具体的独立承载层。";
                    ((RolePlan)value)._confidence = Math.Max(((RolePlan)value)._confidence, 0.93f);
                    ((RolePlan)value)._requiresGeneratedNode = false;
                }
                else if (array != null && array.Length > 1)
                {
                    ((RolePlan)value)._carrierNodeId = string.Empty;
                    ((RolePlan)value)._targetNodeId = BuildGeneratedNodeId("gen:role:", ((RolePlan)value)._roleType, ((RolePlan)value)._owner._ownerId + ":" + ((RolePlan)value)._roleType);
                    ((RolePlan)value)._generatedNodeName = BuildGeneratedRoleNodeName(((RolePlan)value)._roleType, ((RolePlan)value)._owner, lookup);
                    ((RolePlan)value)._memberRootIds = array;
                    ((RolePlan)value)._insertIndex = GetMinimumSiblingIndex(array, lookup);
                    ((RolePlan)value)._requiresGeneratedNode = true;
                    ((RolePlan)value)._reason = "本地契约修正：role 无法与 owner/其他 role 共用同一 carrier，改为生成独立 role 节点。";
                    ((RolePlan)value)._confidence = Math.Max(((RolePlan)value)._confidence, 0.93f);
                }
            }
        }

        private static bool TryResolveIndependentRoleCarrier(object value2, object value3, HashSet<string> texts, Dictionary<string, AiAnalysisNodeEntry> lookup, out string result)
        {
            result = string.Empty;
            if (value2 != null && ((RolePlan)value2)._owner != null && lookup != null)
            {
                if (value3 != null && ((Array)value3).Length >= 1)
                {
                    if (((Array)value3).Length == 1)
                    {
                        string text = (string)((object[])value3)[0];
                        if (!IsReservedRoleCarrier(text, ((RolePlan)value2)._owner, texts) && lookup.TryGetValue(text, out var value) && value != null && UiTypeCompatibilityRules.IsRoleNodeCompatible(((RolePlan)value2)._roleType, value))
                        {
                            result = text;
                            return true;
                        }
                        if (TryFindCompatibleAncestorCarrier(text, value2, texts, lookup, out var text2))
                        {
                            result = text2;
                            return true;
                        }
                        return false;
                    }
                    if (TryUseCommonMemberParentCarrier(value3, ((RolePlan)value2)._owner, texts, lookup, out var text3))
                    {
                        result = text3;
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static string[] GetValidRoleMemberRoots(object value, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && ((RolePlan)value)._owner != null && lookup != null)
            {
                if (((RolePlan)value)._memberNodeIds != null && ((RolePlan)value)._memberNodeIds.Length >= 1)
                {
                    List<string> list = new List<string>(((RolePlan)value)._memberNodeIds.Length);
                    for (int i = 0; i < ((RolePlan)value)._memberNodeIds.Length; i++)
                    {
                        string text = ((RolePlan)value)._memberNodeIds[i];
                        if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, ((RolePlan)value)._owner._targetNodeId, StringComparison.OrdinalIgnoreCase) && lookup.ContainsKey(text) && IsWithinOwnerSubtree(text, ((RolePlan)value)._owner, lookup))
                        {
                            list.Add(text);
                        }
                    }
                    string[] array = NormalizeMemberRootIds(list.ToArray(), lookup);
                    if (array.Length != 0)
                    {
                        return array;
                    }
                    return ((RolePlan)value)._memberRootIds ?? Array.Empty<string>();
                }
                return ((RolePlan)value)._memberRootIds ?? Array.Empty<string>();
            }
            return Array.Empty<string>();
        }

        private static bool TryFindCompatibleAncestorCarrier(object value3, object value4, HashSet<string> texts, Dictionary<string, AiAnalysisNodeEntry> lookup, out string result)
        {
            result = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)value3) && value4 != null && ((RolePlan)value4)._owner != null && lookup != null && !UiTypeCompatibilityRules.RoleRequiresTextLayer(((RolePlan)value4)._roleType))
            {
                string key = (string)value3;
                string text = string.Empty;
                int num = int.MinValue;
                AiAnalysisNodeEntry value;
                AiAnalysisNodeEntry value2;
                while (lookup.TryGetValue(key, out value) && value != null && !string.IsNullOrWhiteSpace(value.parentId) && lookup.TryGetValue(value.parentId, out value2) && value2 != null && IsCompatibleRoleWrapper(value2, value, ((RolePlan)value4)._roleType) && IsWithinOwnerSubtree(value2.id, ((RolePlan)value4)._owner, lookup) && !IsReservedRoleCarrier(value2.id, ((RolePlan)value4)._owner, texts))
                {
                    int num2 = ScoreRoleCarrier(((RolePlan)value4)._owner, value2, ((RolePlan)value4)._roleType, lookup);
                    if (num2 > num)
                    {
                        num = num2;
                        text = value2.id;
                    }
                    key = value2.id;
                }
                result = text;
                return !string.IsNullOrWhiteSpace(result);
            }
            return false;
        }

        private static bool TryUseCommonMemberParentCarrier(object value, object value2, HashSet<string> texts, Dictionary<string, AiAnalysisNodeEntry> lookup, out string result)
        {
            result = string.Empty;
            if (TryFindCommonMemberParent(value, value2, lookup, out var text) && !IsReservedRoleCarrier(text, value2, texts))
            {
                result = text;
                return true;
            }
            return false;
        }

        private static bool IsReservedRoleCarrier(object value, object value2, HashSet<string> texts)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && value2 != null)
            {
                if (!string.Equals((string)value, ((OwnerPlan)value2)._targetNodeId, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(((OwnerPlan)value2)._carrierNodeId) || !string.Equals((string)value, ((OwnerPlan)value2)._carrierNodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    return texts?.Contains((string)value) ?? false;
                }
                return true;
            }
            return true;
        }

        private static void InferUnassignedIconGroupsAsImages(Dictionary<string, AiAnalysisNodeEntry> lookup, Dictionary<string, NodeTypeChange> lookup2, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null || lookup2 == null)
            {
                return;
            }
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    OwnerPlan ownerPlan = values[i];
                    if (ownerPlan != null && !ownerPlan._requiresGeneratedNode && !string.IsNullOrWhiteSpace(ownerPlan._carrierNodeId))
                    {
                        hashSet.Add(ownerPlan._carrierNodeId);
                    }
                }
            }
            if (values2 != null)
            {
                for (int j = 0; j < values2.Count; j++)
                {
                    RolePlan rolePlan = values2[j];
                    if (rolePlan != null && !rolePlan._requiresGeneratedNode && !string.IsNullOrWhiteSpace(rolePlan._carrierNodeId))
                    {
                        hashSet.Add(rolePlan._carrierNodeId);
                    }
                }
            }
            foreach (KeyValuePair<string, NodeTypeChange> item in lookup2)
            {
                NodeTypeChange value = item.Value;
                if (value != null && value._node != null && value._plannedUiType == GUIType.Null && !hashSet.Contains(item.Key) && LooksLikeStandaloneIconGroup(value._node, lookup))
                {
                    value._plannedUiType = GUIType.Image;
                }
            }
        }

        private static bool LooksLikeStandaloneIconGroup(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && lookup != null && ((AiAnalysisNodeEntry)value2).isGroupLayer && !((AiAnalysisNodeEntry)value2).isTextLayer)
            {
                if (((AiAnalysisNodeEntry)value2).childCount >= 1 && ((AiAnalysisNodeEntry)value2).childCount <= 4 && ((AiAnalysisNodeEntry)value2).renderLeafCount >= 1 && ((AiAnalysisNodeEntry)value2).renderLeafCount <= 6)
                {
                    if (NodeNameContainsAnyToken(value2, "btn", "button", "background", "bg", "text", "label", "title", "bar", "fill", "handle"))
                    {
                        return false;
                    }
                    if (!NodeNameContainsAnyToken(value2, "icon", "flag", "gem", "coin", "energy", "avatar", "badge", "close"))
                    {
                        return false;
                    }
                    bool result = false;
                    {
                        foreach (KeyValuePair<string, AiAnalysisNodeEntry> item in lookup)
                        {
                            AiAnalysisNodeEntry value = item.Value;
                            if (value != null && !string.Equals(value.id, ((AiAnalysisNodeEntry)value2).id, StringComparison.OrdinalIgnoreCase) && IsSameOrDescendant(value.id, ((AiAnalysisNodeEntry)value2).id, lookup) && GetDescendantDistance(value.id, ((AiAnalysisNodeEntry)value2).id, lookup) <= 3)
                            {
                                if (value.isTextLayer)
                                {
                                    return false;
                                }
                                result = true;
                            }
                        }
                        return result;
                    }
                }
                return false;
            }
            return false;
        }

        private static void ApplyPlannedNodeTypes(Dictionary<string, NodeTypeChange> lookup, Dictionary<string, AiAnalysisNodeEntry> lookup2, List<OwnerPlan> values, List<RolePlan> values2)
        {
            if (lookup == null)
            {
                return;
            }
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    OwnerPlan ownerPlan = values[i];
                    if (ownerPlan != null && !ownerPlan._requiresGeneratedNode && !string.IsNullOrWhiteSpace(ownerPlan._carrierNodeId) && lookup.TryGetValue(ownerPlan._carrierNodeId, out var value) && value != null)
                    {
                        value._plannedUiType = ownerPlan._ownerType;
                    }
                }
            }
            if (values2 == null)
            {
                return;
            }
            for (int j = 0; j < values2.Count; j++)
            {
                RolePlan rolePlan = values2[j];
                if (rolePlan != null && !rolePlan._requiresGeneratedNode && !string.IsNullOrWhiteSpace(rolePlan._carrierNodeId) && lookup.TryGetValue(rolePlan._carrierNodeId, out var value2) && value2 != null)
                {
                    value2._plannedUiType = rolePlan._roleType;
                }
            }
        }

        private static List<NodePatchPlan> BuildNodeMovePlans(Dictionary<string, AiAnalysisNodeEntry> lookup, List<OwnerPlan> values, List<RolePlan> values2)
        {
            Dictionary<string, NodePatchPlan> dictionary = new Dictionary<string, NodePatchPlan>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values2 != null)
            {
                for (int i = 0; i < values2.Count; i++)
                {
                    RolePlan rolePlan = values2[i];
                    if (rolePlan == null || !rolePlan._requiresGeneratedNode)
                    {
                        continue;
                    }
                    for (int j = 0; j < rolePlan._memberRootIds.Length; j++)
                    {
                        string text = rolePlan._memberRootIds[j];
                        if (!string.IsNullOrWhiteSpace(text) && hashSet.Add(text))
                        {
                            dictionary[text] = new NodePatchPlan
                            {
                                _nodeId = text,
                                _newParentId = rolePlan._targetNodeId,
                                _insertIndex = j,
                                _confidence = rolePlan._confidence,
                                _reason = "多个节点共同构成一个 role，本地移动到生成的 role 节点下。"
                            };
                        }
                    }
                }
            }
            if (values != null)
            {
                for (int k = 0; k < values.Count; k++)
                {
                    OwnerPlan ownerPlan = values[k];
                    if (ownerPlan == null || !ownerPlan._requiresGeneratedNode)
                    {
                        continue;
                    }
                    int num = 0;
                    for (int l = 0; l < ownerPlan._memberRootIds.Length; l++)
                    {
                        string text2 = ownerPlan._memberRootIds[l];
                        if (!string.IsNullOrWhiteSpace(text2) && !hashSet.Contains(text2) && !dictionary.ContainsKey(text2))
                        {
                            dictionary[text2] = new NodePatchPlan
                            {
                                _nodeId = text2,
                                _newParentId = ownerPlan._targetNodeId,
                                _insertIndex = num++,
                                _confidence = ownerPlan._confidence,
                                _reason = "owner 由多个节点共同构成，本地移动到生成的 owner 节点下。"
                            };
                        }
                    }
                }
            }
            if (values2 != null)
            {
                for (int m = 0; m < values2.Count; m++)
                {
                    RolePlan rolePlan2 = values2[m];
                    if (rolePlan2 != null && !rolePlan2._requiresGeneratedNode && !string.IsNullOrWhiteSpace(rolePlan2._carrierNodeId))
                    {
                        string text3 = GetRoleCarrierNodeId(rolePlan2, lookup);
                        if (!string.IsNullOrWhiteSpace(text3) && !dictionary.ContainsKey(text3) && !string.Equals(text3, rolePlan2._owner._targetNodeId, StringComparison.OrdinalIgnoreCase) && !IsNodeWithinOwner(rolePlan2._owner, text3, lookup))
                        {
                            dictionary[text3] = new NodePatchPlan
                            {
                                _nodeId = text3,
                                _newParentId = rolePlan2._owner._targetNodeId,
                                _insertIndex = GetSiblingIndex(lookup, text3),
                                _confidence = rolePlan2._confidence,
                                _reason = "role 当前不在 owner 语义子树内，本地移动到 owner 下。"
                            };
                        }
                    }
                }
            }
            List<NodePatchPlan> list = new List<NodePatchPlan>(dictionary.Values);
            list.Sort((NodePatchPlan left, NodePatchPlan right) => left._insertIndex.CompareTo(right._insertIndex));
            return list;
        }

        private static void AddGroupCreationOperations(List<AiPatchOperation> values, List<OwnerPlan> values2, List<RolePlan> values3)
        {
            if (values == null)
            {
                return;
            }
            if (values2 != null)
            {
                for (int i = 0; i < values2.Count; i++)
                {
                    OwnerPlan ownerPlan = values2[i];
                    if (ownerPlan != null && ownerPlan._requiresGeneratedNode)
                    {
                        values.Add(new AiPatchOperation
                        {
                            op = "create_group",
                            id = ownerPlan._targetNodeId,
                            parentId = ownerPlan._generatedParentId,
                            insertIndex = ownerPlan._insertIndex,
                            name = ownerPlan._generatedNodeName,
                            uiType = GUIType.Null.ToString(),
                            confidence = ownerPlan._confidence,
                            reason = "多个图层共同构成一个 owner，本地补建 owner 节点。"
                        });
                    }
                }
            }
            if (values3 == null)
            {
                return;
            }
            for (int j = 0; j < values3.Count; j++)
            {
                RolePlan rolePlan = values3[j];
                if (rolePlan != null && rolePlan._requiresGeneratedNode)
                {
                    values.Add(new AiPatchOperation
                    {
                        op = "create_group",
                        id = rolePlan._targetNodeId,
                        parentId = rolePlan._owner._targetNodeId,
                        insertIndex = rolePlan._insertIndex,
                        name = rolePlan._generatedNodeName,
                        uiType = GUIType.Null.ToString(),
                        confidence = rolePlan._confidence,
                        reason = "多个图层共同构成一个 role，本地补建 role 节点。"
                    });
                }
            }
        }

        private static void AddNodeMoveOperations(List<AiPatchOperation> values, List<NodePatchPlan> values2)
        {
            if (values == null || values2 == null)
            {
                return;
            }
            for (int i = 0; i < values2.Count; i++)
            {
                NodePatchPlan nodePatchPlan = values2[i];
                if (nodePatchPlan != null && !string.IsNullOrWhiteSpace(nodePatchPlan._nodeId) && !string.IsNullOrWhiteSpace(nodePatchPlan._newParentId))
                {
                    values.Add(new AiPatchOperation
                    {
                        op = "move_node",
                        targetId = nodePatchPlan._nodeId,
                        newParentId = nodePatchPlan._newParentId,
                        insertIndex = nodePatchPlan._insertIndex,
                        confidence = nodePatchPlan._confidence,
                        reason = nodePatchPlan._reason
                    });
                }
            }
        }

        private static void AddUiTypeOperations(List<AiPatchOperation> values, Dictionary<string, NodeTypeChange> lookup, List<OwnerPlan> values2, List<RolePlan> values3)
        {
            if (values == null)
            {
                return;
            }
            if (values2 != null)
            {
                for (int i = 0; i < values2.Count; i++)
                {
                    OwnerPlan ownerPlan = values2[i];
                    if (ownerPlan != null && ownerPlan._requiresGeneratedNode)
                    {
                        values.Add(new AiPatchOperation
                        {
                            op = "set_ui_type",
                            targetId = ownerPlan._targetNodeId,
                            uiType = ownerPlan._ownerType.ToString(),
                            confidence = ownerPlan._confidence,
                            reason = ownerPlan._reason
                        });
                    }
                }
            }
            if (values3 != null)
            {
                for (int j = 0; j < values3.Count; j++)
                {
                    RolePlan rolePlan = values3[j];
                    if (rolePlan != null && rolePlan._requiresGeneratedNode)
                    {
                        values.Add(new AiPatchOperation
                        {
                            op = "set_ui_type",
                            targetId = rolePlan._targetNodeId,
                            uiType = rolePlan._roleType.ToString(),
                            confidence = rolePlan._confidence,
                            reason = rolePlan._reason
                        });
                    }
                }
            }
            if (lookup == null)
            {
                return;
            }
            foreach (KeyValuePair<string, NodeTypeChange> item in lookup)
            {
                NodeTypeChange value = item.Value;
                if (value != null && value._currentUiType != value._plannedUiType)
                {
                    values.Add(new AiPatchOperation
                    {
                        op = "set_ui_type",
                        targetId = item.Key,
                        uiType = value._plannedUiType.ToString(),
                        confidence = 0.9f,
                        reason = "应用 nodeLabel 与 owner/role 语义图后的最终节点类型。"
                    });
                }
            }
        }

        private static void AddAuditEntries(List<AiAuditEntry> values, Dictionary<string, NodeTypeChange> lookup, List<OwnerPlan> values2, List<RolePlan> values3)
        {
            if (values == null)
            {
                return;
            }
            if (values2 != null)
            {
                for (int i = 0; i < values2.Count; i++)
                {
                    OwnerPlan ownerPlan = values2[i];
                    if (ownerPlan != null)
                    {
                        values.Add(new AiAuditEntry
                        {
                            targetId = ownerPlan._targetNodeId,
                            ownerId = string.Empty,
                            semanticKind = "owner",
                            currentUIType = GetCurrentUiTypeName(ownerPlan._targetNodeId, lookup),
                            predictedUIType = ownerPlan._ownerType.ToString(),
                            verdict = (string.Equals(GetCurrentUiTypeName(ownerPlan._targetNodeId, lookup), ownerPlan._ownerType.ToString(), StringComparison.Ordinal) ? "correct" : "corrected"),
                            confidence = ownerPlan._confidence,
                            reason = ownerPlan._reason
                        });
                    }
                }
            }
            if (values3 == null)
            {
                return;
            }
            for (int j = 0; j < values3.Count; j++)
            {
                RolePlan rolePlan = values3[j];
                if (rolePlan != null)
                {
                    values.Add(new AiAuditEntry
                    {
                        targetId = rolePlan._targetNodeId,
                        ownerId = ((rolePlan._owner != null) ? rolePlan._owner._targetNodeId : string.Empty),
                        semanticKind = "role",
                        currentUIType = GetCurrentUiTypeName(rolePlan._targetNodeId, lookup),
                        predictedUIType = rolePlan._roleType.ToString(),
                        verdict = (string.Equals(GetCurrentUiTypeName(rolePlan._targetNodeId, lookup), rolePlan._roleType.ToString(), StringComparison.Ordinal) ? "correct" : "corrected"),
                        confidence = rolePlan._confidence,
                        reason = rolePlan._reason
                    });
                }
            }
        }

        private static Dictionary<string, AiAnalysisNodeEntry> BuildNodeLookup(List<AiAnalysisNodeEntry> values)
        {
            Dictionary<string, AiAnalysisNodeEntry> dictionary = new Dictionary<string, AiAnalysisNodeEntry>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return dictionary;
            }
            for (int i = 0; i < values.Count; i++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = values[i];
                if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                {
                    dictionary[aiAnalysisNodeEntry.id] = aiAnalysisNodeEntry;
                }
            }
            return dictionary;
        }

        private static string ResolveOwnerCarrierNodeId(object value3, GUIType uiType, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value3 != null && lookup != null)
            {
                if (!string.IsNullOrWhiteSpace(((AiRecognitionOwnerEntry)value3).carrierNodeId) && lookup.TryGetValue(((AiRecognitionOwnerEntry)value3).carrierNodeId, out var value) && value != null && UiTypeCompatibilityRules.IsOwnerNodeCompatible(uiType, value))
                {
                    return ((AiRecognitionOwnerEntry)value3).carrierNodeId;
                }
                if (lookup.TryGetValue(((AiRecognitionOwnerEntry)value3).ownerId, out var value2) && value2 != null && UiTypeCompatibilityRules.IsOwnerNodeCompatible(uiType, value2))
                {
                    return ((AiRecognitionOwnerEntry)value3).ownerId;
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private static string ResolveRoleCarrierNodeId(object value3, GUIType uiType, object value4, object value5, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value3 != null && value5 != null && lookup != null)
            {
                if (!string.IsNullOrWhiteSpace(((AiRecognitionRoleEntry)value3).carrierNodeId) && lookup.TryGetValue(((AiRecognitionRoleEntry)value3).carrierNodeId, out var value) && value != null && UiTypeCompatibilityRules.IsRoleNodeCompatible(uiType, value))
                {
                    return ((AiRecognitionRoleEntry)value3).carrierNodeId;
                }
                if (value4 != null && ((Array)value4).Length == 1 && lookup.TryGetValue((string)((object[])value4)[0], out var value2) && value2 != null && UiTypeCompatibilityRules.IsRoleNodeCompatible(uiType, value2))
                {
                    return (string)((object[])value4)[0];
                }
                if (!UiTypeCompatibilityRules.RoleRequiresTextLayer(uiType) && TryFindCommonMemberParent(value4, value5, lookup, out var text))
                {
                    if (!string.Equals(text, ((OwnerPlan)value5)._targetNodeId, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(((OwnerPlan)value5)._carrierNodeId) || !string.Equals(text, ((OwnerPlan)value5)._carrierNodeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return text;
                    }
                    return string.Empty;
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private static bool TryFindCommonMemberParent(object value3, object value4, Dictionary<string, AiAnalysisNodeEntry> lookup, out string result)
        {
            result = string.Empty;
            if (value3 != null && ((Array)value3).Length >= 2 && value4 != null && lookup != null)
            {
                string text = null;
                int num = 0;
                while (true)
                {
                    if (num < ((Array)value3).Length)
                    {
                        if (!lookup.TryGetValue((string)((object[])value3)[num], out var value) || value == null || string.IsNullOrWhiteSpace(value.parentId))
                        {
                            break;
                        }
                        if (text == null)
                        {
                            text = value.parentId;
                        }
                        else if (!string.Equals(text, value.parentId, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                        num++;
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(text) && lookup.TryGetValue(text, out var value2) && value2 != null && value2.isGroupLayer)
                    {
                        if (!((OwnerPlan)value4)._requiresGeneratedNode && string.Equals(text, ((OwnerPlan)value4)._targetNodeId, StringComparison.OrdinalIgnoreCase))
                        {
                            result = text;
                            return true;
                        }
                        if (((OwnerPlan)value4)._requiresGeneratedNode && ContainsNodeId(((OwnerPlan)value4)._memberRootIds, text))
                        {
                            result = text;
                            return true;
                        }
                        return false;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static string[] NormalizeMemberRootIds(object value, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && ((Array)value).Length >= 1 && lookup != null)
            {
                List<string> list = new List<string>(((Array)value).Length);
                for (int i = 0; i < ((Array)value).Length; i++)
                {
                    string text = (string)((object[])value)[i];
                    if (!string.IsNullOrWhiteSpace(text) && lookup.ContainsKey(text) && !list.Contains(text))
                    {
                        list.Add(text);
                    }
                }
                for (int num = list.Count - 1; num >= 0; num--)
                {
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (num != j && IsStrictDescendant(list[num], list[j], lookup))
                        {
                            list.RemoveAt(num);
                            break;
                        }
                    }
                }
                list.Sort((string left, string right) => GetSiblingIndex(lookup, left).CompareTo(GetSiblingIndex(lookup, right)));
                return list.ToArray();
            }
            return Array.Empty<string>();
        }

        private static string ResolveGeneratedOwnerParentId(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 != null && ((Array)value2).Length >= 1 && lookup != null)
            {
                if (((Array)value2).Length == 1 && lookup.TryGetValue((string)((object[])value2)[0], out var value) && value != null)
                {
                    if (!string.IsNullOrWhiteSpace(value.parentId))
                    {
                        return value.parentId;
                    }
                    return "root";
                }
                string text = FindLowestCommonAncestorId(value2, lookup);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
                return "root";
            }
            return "root";
        }

        private static string FindLowestCommonAncestorId(object value, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && ((Array)value).Length >= 1 && lookup != null)
            {
                List<string> list = GetAncestorIds(((object[])value)[0], lookup);
                int num = 0;
                string text;
                while (true)
                {
                    if (num < list.Count)
                    {
                        text = list[num];
                        bool flag = true;
                        for (int i = 1; i < ((Array)value).Length; i++)
                        {
                            if (!IsSameOrDescendant(((object[])value)[i], text, lookup))
                            {
                                flag = false;
                                break;
                            }
                        }
                        if (flag)
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return "root";
                }
                return text;
            }
            return "root";
        }

        private static List<string> GetAncestorIds(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            List<string> list = new List<string>(8);
            if (string.IsNullOrWhiteSpace((string)value2))
            {
                list.Add("root");
                return list;
            }
            string text = (string)value2;
            for (int i = 0; i < 256; i++)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    list.Add(text);
                    if (lookup.TryGetValue(text, out var value) && value != null && !string.IsNullOrWhiteSpace(value.parentId) && !string.Equals(value.parentId, "root", StringComparison.OrdinalIgnoreCase))
                    {
                        text = value.parentId;
                        continue;
                    }
                    list.Add("root");
                    break;
                }
                list.Add("root");
                break;
            }
            return list;
        }

        private static bool IsNodeWithinOwner(object value, object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && !string.IsNullOrWhiteSpace((string)value2))
            {
                if (((OwnerPlan)value)._requiresGeneratedNode)
                {
                    return IsDescendantOfAnyRoot(((OwnerPlan)value)._memberRootIds, value2, lookup);
                }
                return IsSameOrDescendant(value2, ((OwnerPlan)value)._targetNodeId, lookup);
            }
            return false;
        }

        private static string GetRoleCarrierNodeId(object value, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && !string.IsNullOrWhiteSpace(((RolePlan)value)._carrierNodeId))
            {
                return ((RolePlan)value)._carrierNodeId;
            }
            return string.Empty;
        }

        private static bool ContainsNodeId(object value, object value2)
        {
            if (value == null || string.IsNullOrWhiteSpace((string)value2))
            {
                return false;
            }
            for (int i = 0; i < ((Array)value).Length; i++)
            {
                if (string.Equals((string)((object[])value)[i], (string)value2, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDescendantOfAnyRoot(object value, object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && ((Array)value).Length >= 1 && !string.IsNullOrWhiteSpace((string)value2) && lookup != null)
            {
                int num = 0;
                while (true)
                {
                    if (num < ((Array)value).Length)
                    {
                        string text = (string)((object[])value)[num];
                        if (!string.IsNullOrWhiteSpace(text) && IsSameOrDescendant(value2, text, lookup))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool IsStrictDescendant(object value2, object value3, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (!string.IsNullOrWhiteSpace((string)value2) && !string.IsNullOrWhiteSpace((string)value3) && lookup != null)
            {
                string key = (string)value2;
                int num = 0;
                while (true)
                {
                    if (num < 256)
                    {
                        if (!lookup.TryGetValue(key, out var value) || value == null || string.IsNullOrWhiteSpace(value.parentId))
                        {
                            break;
                        }
                        if (!string.Equals(value.parentId, (string)value3, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.Equals(value.parentId, "root", StringComparison.OrdinalIgnoreCase))
                            {
                                key = value.parentId;
                                num++;
                                continue;
                            }
                            return false;
                        }
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static bool IsSameOrDescendant(object value, object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (!string.Equals((string)value, (string)value2, StringComparison.OrdinalIgnoreCase))
            {
                return IsStrictDescendant(value, value2, lookup);
            }
            return true;
        }

        private static int GetDescendantDistance(object value2, object value3, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (!string.IsNullOrWhiteSpace((string)value2) && !string.IsNullOrWhiteSpace((string)value3) && lookup != null)
            {
                if (string.Equals((string)value2, (string)value3, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }
                string key = (string)value2;
                int num = 1;
                while (true)
                {
                    if (num < 256)
                    {
                        if (!lookup.TryGetValue(key, out var value) || value == null || string.IsNullOrWhiteSpace(value.parentId))
                        {
                            break;
                        }
                        if (!string.Equals(value.parentId, (string)value3, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.Equals(value.parentId, "root", StringComparison.OrdinalIgnoreCase))
                            {
                                key = value.parentId;
                                num++;
                                continue;
                            }
                            return -1;
                        }
                        return num;
                    }
                    return -1;
                }
                return -1;
            }
            return -1;
        }

        private static int GetMinimumSiblingIndex(object value, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null && ((Array)value).Length >= 1)
            {
                int num = int.MaxValue;
                for (int i = 0; i < ((Array)value).Length; i++)
                {
                    int num2 = GetSiblingIndex(lookup, ((object[])value)[i]);
                    if (num2 < num)
                    {
                        num = num2;
                    }
                }
                if (num != int.MaxValue)
                {
                    return num;
                }
                return 0;
            }
            return 0;
        }

        private static int GetSiblingIndex(Dictionary<string, AiAnalysisNodeEntry> lookup, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value2) && lookup != null && lookup.TryGetValue((string)value2, out var value) && value != null)
            {
                return value.siblingIndex;
            }
            return int.MaxValue;
        }

        private static string GetCurrentUiTypeName(object value2, Dictionary<string, NodeTypeChange> lookup)
        {
            if (!string.IsNullOrWhiteSpace((string)value2) && lookup != null && lookup.TryGetValue((string)value2, out var value) && value != null)
            {
                return value._currentUiType.ToString();
            }
            return GUIType.Null.ToString();
        }

        private static GUIType GetCurrentUiType(object value)
        {
            if (value == null)
            {
                return GUIType.Null;
            }
            if (AiPatchValidator.TryParsePatchUiType(((AiAnalysisNodeEntry)value).uiType, out var gUIType))
            {
                return UiTypeCompatibilityRules.NormalizeUiTypeAlias(gUIType);
            }
            return UiTypeCompatibilityRules.InferBaseUiType(value);
        }

        private static float NormalizeConfidence(float value)
        {
            if (value > 0f && !(value > 1f))
            {
                return value;
            }
            return 0.8f;
        }

        private static string BuildGeneratedNodeId(object value, GUIType uiType, object value2)
        {
            return string.Concat(value, uiType, ":", SanitizeIdentifierComponent(value2));
        }

        private static string BuildGeneratedNodeName(object value, GUIType uiType, object value2)
        {
            if (!string.Equals((string)value, "gen_role", StringComparison.Ordinal))
            {
                if (!string.Equals((string)value, "gen_owner", StringComparison.Ordinal))
                {
                    string text = SanitizeIdentifierComponent(value2);
                    if (text.Length > 16)
                    {
                        text = text.Substring(0, 16);
                    }
                    return string.Concat(uiType, "_", text);
                }
                return string.Concat(uiType, "_", ComputeStableShortHash(value2));
            }
            return uiType.ToString();
        }

        private static string BuildGeneratedRoleNodeName(GUIType uiType, object value, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (uiType == GUIType.Background)
            {
                string text = GetOwnerNodeNameBase(value, lookup);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text + "_bg";
                }
            }
            return uiType.ToString();
        }

        private static string GetOwnerNodeNameBase(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 == null)
            {
                return string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(((OwnerPlan)value2)._carrierNodeId) && lookup != null && lookup.TryGetValue(((OwnerPlan)value2)._carrierNodeId, out var value) && value != null)
            {
                string text = SanitizeIdentifierComponent((!string.IsNullOrWhiteSpace(value.name)) ? value.name : value.layerName);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            if (!string.IsNullOrWhiteSpace(((OwnerPlan)value2)._generatedNodeName))
            {
                string text2 = SanitizeIdentifierComponent(((OwnerPlan)value2)._generatedNodeName);
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    return text2;
                }
            }
            return string.Empty;
        }

        private static string ComputeStableShortHash(object value)
        {
            uint num = 2166136261u;
            string text = ((!string.IsNullOrWhiteSpace((string)value)) ? ((string)value).Trim() : "auto");
            for (int i = 0; i < text.Length; i++)
            {
                num ^= text[i];
                num *= 16777619;
            }
            return (num & 0xFFFF).ToString("X4");
        }

        private static string SanitizeIdentifierComponent(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return "auto";
            }
            char[] array = ((string)value).Trim().ToCharArray();
            for (int i = 0; i < array.Length; i++)
            {
                if (!char.IsLetterOrDigit(array[i]))
                {
                    array[i] = '_';
                }
            }
            return new string(array);
        }

        private static string[] CloneStringArray(object value)
        {
            if (value != null && ((Array)value).Length >= 1)
            {
                string[] array = new string[((Array)value).Length];
                Array.Copy((Array)value, array, ((Array)value).Length);
                return array;
            }
            return Array.Empty<string>();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchPlanner GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
