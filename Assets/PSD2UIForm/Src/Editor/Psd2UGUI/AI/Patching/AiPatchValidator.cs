using System;
using System.Collections.Generic;
using AiPatchOperationParserNamespace;
using UGF.EditorTools.Psd2UGUI;
using AiPatchOperationKindNamespace;
using UiTypeCompatibilityRulesNamespace;

namespace AiPatchValidatorNamespace
{
    internal sealed class AiPatchValidator
    {
        private sealed class PatchTreeNodeState
        {
            public string _id;

            public string _parentId;

            public string _layerType;

            public GUIType _uiType;

            public bool _isGenerated;

            public bool yFQjkifssS;

            private static PatchTreeNodeState s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static PatchTreeNodeState GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static AiPatchValidator s_ObfuscationSentinel;

        internal bool ValidatePatch(AiPatchDocument aiPatchDocument, AiAnalysisPackageDocument aiAnalysisPackageDocument, out string result)
        {
            if (ValidatePatchDocument(aiPatchDocument, out result))
            {
                if (ValidateUniqueAnalysisTargets(aiPatchDocument, aiAnalysisPackageDocument, out result) && ValidateAnalysisSemantics(aiPatchDocument, aiAnalysisPackageDocument, out result))
                {
                    return ValidateOperationsByDryRun(aiPatchDocument, aiAnalysisPackageDocument, out result);
                }
                return false;
            }
            return false;
        }

        internal bool ValidatePatchDocument(AiPatchDocument aiPatchDocument, out string result)
        {
            result = null;
            if (aiPatchDocument != null)
            {
                if (string.Equals(aiPatchDocument.version, "2.0", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(aiPatchDocument.treeHash))
                    {
                        if (aiPatchDocument.analysis == null)
                        {
                            aiPatchDocument.analysis = new List<AiAuditEntry>();
                        }
                        if (aiPatchDocument.operations == null)
                        {
                            aiPatchDocument.operations = new List<AiPatchOperation>();
                        }
                        NormalizeOperationOrder(aiPatchDocument);
                        if (aiPatchDocument.analysis.Count < 1)
                        {
                            result = "Patch document requires at least one analysis entry.";
                            return false;
                        }
                        int num = 0;
                        while (true)
                        {
                            if (num < aiPatchDocument.analysis.Count)
                            {
                                AiAuditEntry aiAuditEntry = aiPatchDocument.analysis[num];
                                if (aiAuditEntry != null)
                                {
                                    if (string.IsNullOrWhiteSpace(aiAuditEntry.targetId))
                                    {
                                        break;
                                    }
                                    if (aiAuditEntry.ownerId == null)
                                    {
                                        aiAuditEntry.ownerId = string.Empty;
                                    }
                                    if (!string.IsNullOrWhiteSpace(aiAuditEntry.verdict))
                                    {
                                        if (IsValidAuditVerdict(aiAuditEntry.verdict))
                                        {
                                            if (!string.IsNullOrWhiteSpace(aiAuditEntry.reason))
                                            {
                                                if (!(aiAuditEntry.confidence <= 0f) && aiAuditEntry.confidence <= 1f)
                                                {
                                                    if (!string.IsNullOrWhiteSpace(aiAuditEntry.currentUIType) && IsValidCurrentUiType(aiAuditEntry.currentUIType))
                                                    {
                                                        if (!string.IsNullOrWhiteSpace(aiAuditEntry.predictedUIType) && IsValidPredictedUiType(aiAuditEntry.predictedUIType))
                                                        {
                                                            num++;
                                                            continue;
                                                        }
                                                        result = $"Analysis[{num}] predictedUIType is invalid: '{aiAuditEntry.predictedUIType}'.";
                                                        return false;
                                                    }
                                                    result = $"Analysis[{num}] currentUIType is invalid: '{aiAuditEntry.currentUIType}'.";
                                                    return false;
                                                }
                                                result = $"Analysis[{num}] confidence must be within (0, 1].";
                                                return false;
                                            }
                                            result = $"Analysis[{num}] requires reason.";
                                            return false;
                                        }
                                        result = $"Analysis[{num}] verdict is invalid: '{aiAuditEntry.verdict}'.";
                                        return false;
                                    }
                                    result = $"Analysis[{num}] requires verdict.";
                                    return false;
                                }
                                result = $"Analysis[{num}] is null.";
                                return false;
                            }
                            if (aiPatchDocument.operations.Count < 1)
                            {
                                return true;
                            }
                            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            int num2 = 0;
                            AiPatchOperation aiPatchOperation;
                            while (true)
                            {
                                if (num2 < aiPatchDocument.operations.Count)
                                {
                                    aiPatchOperation = aiPatchDocument.operations[num2];
                                    if (aiPatchOperation != null)
                                    {
                                        if (!AiPatchOperationParser.TryParse(aiPatchOperation.op, out var value))
                                        {
                                            break;
                                        }
                                        if (!(aiPatchOperation.confidence <= 0f) && aiPatchOperation.confidence <= 1f)
                                        {
                                            switch (value)
                                            {
                                            case (AiPatchOperationKind)1:
                                                if (!string.IsNullOrWhiteSpace(aiPatchOperation.id) && aiPatchOperation.id.StartsWith("gen:", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    if (hashSet.Add(aiPatchOperation.id))
                                                    {
                                                        if (!string.IsNullOrWhiteSpace(aiPatchOperation.parentId))
                                                        {
                                                            if (!string.IsNullOrWhiteSpace(aiPatchOperation.name))
                                                            {
                                                                if (aiPatchOperation.insertIndex >= -1)
                                                                {
                                                                    if (TryParsePatchUiType(aiPatchOperation.uiType, out var gUIType2) && (UGUIParser.IsPrimaryUIType(gUIType2) || gUIType2 == GUIType.Null) && !UGUIParser.IsAuxiliaryUIType(gUIType2))
                                                                    {
                                                                        goto default;
                                                                    }
                                                                    result = $"Operation[{num2}] create_group uiType must be a main UI type or Null wrapper.";
                                                                    return false;
                                                                }
                                                                result = $"Operation[{num2}] create_group insertIndex must be >= -1.";
                                                                return false;
                                                            }
                                                            result = $"Operation[{num2}] create_group requires name.";
                                                            return false;
                                                        }
                                                        result = $"Operation[{num2}] create_group requires parentId.";
                                                        return false;
                                                    }
                                                    result = $"Operation[{num2}] create_group duplicates generated id '{aiPatchOperation.id}'.";
                                                    return false;
                                                }
                                                result = $"Operation[{num2}] create_group requires a generated id with 'gen:' prefix.";
                                                return false;
                                            case (AiPatchOperationKind)2:
                                                if (!string.IsNullOrWhiteSpace(aiPatchOperation.targetId))
                                                {
                                                    if (!string.IsNullOrWhiteSpace(aiPatchOperation.newParentId))
                                                    {
                                                        if (aiPatchOperation.insertIndex >= -1)
                                                        {
                                                            if (!string.IsNullOrWhiteSpace(aiPatchOperation.uiType))
                                                            {
                                                                result = $"Operation[{num2}] move_node must not set uiType.";
                                                                return false;
                                                            }
                                                            goto default;
                                                        }
                                                        result = $"Operation[{num2}] move_node insertIndex must be >= -1.";
                                                        return false;
                                                    }
                                                    result = $"Operation[{num2}] move_node requires newParentId.";
                                                    return false;
                                                }
                                                result = $"Operation[{num2}] move_node requires targetId.";
                                                return false;
                                            case (AiPatchOperationKind)3:
                                                if (!string.IsNullOrWhiteSpace(aiPatchOperation.targetId))
                                                {
                                                    if (!string.IsNullOrWhiteSpace(aiPatchOperation.id) || !string.IsNullOrWhiteSpace(aiPatchOperation.parentId) || !string.IsNullOrWhiteSpace(aiPatchOperation.newParentId) || !string.IsNullOrWhiteSpace(aiPatchOperation.name) || !string.IsNullOrWhiteSpace(aiPatchOperation.uiType))
                                                    {
                                                        result = $"Operation[{num2}] flatten_group only supports targetId, confidence, and reason.";
                                                        return false;
                                                    }
                                                    goto default;
                                                }
                                                result = $"Operation[{num2}] flatten_group requires targetId.";
                                                return false;
                                            case (AiPatchOperationKind)4:
                                                if (!string.IsNullOrWhiteSpace(aiPatchOperation.targetId))
                                                {
                                                    if (!TryParsePatchUiType(aiPatchOperation.uiType, out var _))
                                                    {
                                                        result = $"Operation[{num2}] set_ui_type has invalid uiType '{aiPatchOperation.uiType}'.";
                                                        return false;
                                                    }
                                                    goto default;
                                                }
                                                result = $"Operation[{num2}] set_ui_type requires targetId.";
                                                return false;
                                            case (AiPatchOperationKind)5:
                                                if (!string.IsNullOrWhiteSpace(aiPatchOperation.targetId) && !string.IsNullOrWhiteSpace(aiPatchOperation.name))
                                                {
                                                    if (string.IsNullOrWhiteSpace(aiPatchOperation.uiType))
                                                    {
                                                        goto default;
                                                    }
                                                    result = $"Operation[{num2}] rename_node must not set uiType.";
                                                    return false;
                                                }
                                                result = $"Operation[{num2}] rename_node requires valid targetId and name.";
                                                return false;
                                            case (AiPatchOperationKind)6:
                                                if (string.IsNullOrWhiteSpace(aiPatchOperation.targetId) || !aiPatchOperation.targetId.StartsWith("gen:", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    break;
                                                }
                                                if (!string.IsNullOrWhiteSpace(aiPatchOperation.uiType))
                                                {
                                                    result = $"Operation[{num2}] delete_generated_group must not set uiType.";
                                                    return false;
                                                }
                                                goto default;
                                            default:
                                                if (!string.IsNullOrWhiteSpace(aiPatchOperation.reason))
                                                {
                                                    goto IL_03f0;
                                                }
                                                result = $"Operation[{num2}] requires reason.";
                                                return false;
                                            }
                                            result = $"Operation[{num2}] delete_generated_group only supports generated group ids.";
                                            return false;
                                        }
                                        result = $"Operation[{num2}] confidence must be within (0, 1].";
                                        return false;
                                    }
                                    result = $"Operation[{num2}] is null.";
                                    return false;
                                }
                                return true;
                                IL_03f0:
                                num2++;
                            }
                            result = $"Operation[{num2}] has unsupported op '{aiPatchOperation.op}'.";
                            return false;
                        }
                        result = $"Analysis[{num}] requires targetId.";
                        return false;
                    }
                    result = "Patch document requires treeHash.";
                    return false;
                }
                result = "Patch document version must be '2.0'.";
                return false;
            }
            result = "Patch document is null.";
            return false;
        }

        private static bool ValidateUniqueAnalysisTargets(object value, object value2, out string result)
        {
            result = null;
            if (value != null && value2 != null)
            {
                HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (((AiPatchDocument)value).analysis != null)
                {
                    for (int i = 0; i < ((AiPatchDocument)value).analysis.Count; i++)
                    {
                        AiAuditEntry aiAuditEntry = ((AiPatchDocument)value).analysis[i];
                        if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId) && !hashSet.Add(aiAuditEntry.targetId))
                        {
                            result = "Analysis contains duplicate targetId '" + aiAuditEntry.targetId + "'.";
                            return false;
                        }
                    }
                }
                return true;
            }
            return true;
        }

        private static bool ValidateAnalysisSemantics(object value4, object value5, out string result2)
        {
            result2 = null;
            if (value4 != null && value5 != null && ((AiPatchDocument)value4).analysis != null)
            {
                Dictionary<string, AiAnalysisNodeEntry> dictionary = new Dictionary<string, AiAnalysisNodeEntry>(StringComparer.OrdinalIgnoreCase);
                if (((AiAnalysisPackageDocument)value5).nodes != null)
                {
                    for (int i = 0; i < ((AiAnalysisPackageDocument)value5).nodes.Count; i++)
                    {
                        AiAnalysisNodeEntry aiAnalysisNodeEntry = ((AiAnalysisPackageDocument)value5).nodes[i];
                        if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                        {
                            dictionary[aiAnalysisNodeEntry.id] = aiAnalysisNodeEntry;
                        }
                    }
                }
                Dictionary<string, GUIType> dictionary2 = new Dictionary<string, GUIType>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < ((AiPatchDocument)value4).analysis.Count; j++)
                {
                    AiAuditEntry aiAuditEntry = ((AiPatchDocument)value4).analysis[j];
                    if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId) && IsValidPredictedUiType(aiAuditEntry.predictedUIType) && Enum.TryParse<GUIType>(aiAuditEntry.predictedUIType, ignoreCase: true, out var result))
                    {
                        dictionary2[aiAuditEntry.targetId] = result;
                    }
                }
                Dictionary<string, string> dictionary3 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int k = 0; k < ((AiPatchDocument)value4).analysis.Count; k++)
                {
                    AiAuditEntry aiAuditEntry2 = ((AiPatchDocument)value4).analysis[k];
                    if (aiAuditEntry2 == null || !TryParsePatchUiType(aiAuditEntry2.predictedUIType, out var gUIType))
                    {
                        continue;
                    }
                    if ((!IsTextSemanticType(gUIType) && gUIType != GUIType.Text) || (dictionary.TryGetValue(aiAuditEntry2.targetId, out var value) && value != null && value.isTextLayer))
                    {
                        if (UGUIParser.IsAuxiliaryUIType(gUIType))
                        {
                            if (string.IsNullOrWhiteSpace(aiAuditEntry2.ownerId))
                            {
                                result2 = $"Analysis[{k}] semantic child '{aiAuditEntry2.targetId}' with uiType '{gUIType}' requires ownerId.";
                                return false;
                            }
                            if (!dictionary2.TryGetValue(aiAuditEntry2.ownerId, out var value2))
                            {
                                result2 = $"Analysis[{k}] ownerId '{aiAuditEntry2.ownerId}' for semantic child '{aiAuditEntry2.targetId}' is not present in analysis.";
                                return false;
                            }
                            if (!UiTypeCompatibilityRules.IsRoleAllowed(value2, gUIType))
                            {
                                result2 = $"Analysis[{k}] owner '{aiAuditEntry2.ownerId}' with uiType '{value2}' cannot own semantic child '{gUIType}'.";
                                return false;
                            }
                            string key = aiAuditEntry2.ownerId + "\n" + gUIType;
                            if (dictionary3.TryGetValue(key, out var value3))
                            {
                                result2 = $"Analysis[{k}] owner '{aiAuditEntry2.ownerId}' has duplicate semantic child '{gUIType}' on '{value3}' and '{aiAuditEntry2.targetId}'.";
                                return false;
                            }
                            dictionary3.Add(key, aiAuditEntry2.targetId);
                        }
                        continue;
                    }
                    result2 = $"Analysis[{k}] marks '{aiAuditEntry2.targetId}' as text semantic '{gUIType}', but the target is not an isTextLayer node.";
                    return false;
                }
                return true;
            }
            return true;
        }

        private static bool ValidateOperationsByDryRun(object value4, object value5, out string result)
        {
            result = null;
            if (value4 == null || value5 == null || ((AiAnalysisPackageDocument)value5).nodes == null)
            {
                return true;
            }
            Dictionary<string, PatchTreeNodeState> dictionary = BuildPatchTree(value5);
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> hashSet3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AiPatchOperationKind value6 = (AiPatchOperationKind)1;
            int num = 0;
            AiPatchOperation aiPatchOperation;
            while (true)
            {
                if (num < ((AiPatchDocument)value4).operations.Count)
                {
                    aiPatchOperation = ((AiPatchDocument)value4).operations[num];
                    AiPatchOperationParser.TryParse(aiPatchOperation.op, out var value7);
                    if (!IsOperationOrderValid(value6, value7))
                    {
                        break;
                    }
                    value6 = value7;
                    switch (value7)
                    {
                    case (AiPatchOperationKind)1:
                    {
                        GUIType uiType = GUIType.Null;
                        TryParsePatchUiType(aiPatchOperation.uiType, out uiType);
                        if (dictionary.ContainsKey(aiPatchOperation.parentId))
                        {
                            dictionary[aiPatchOperation.id] = new PatchTreeNodeState
                            {
                                _id = aiPatchOperation.id,
                                _parentId = aiPatchOperation.parentId,
                                _layerType = PsdLayerType.LayerGroup.ToString(),
                                _uiType = uiType,
                                _isGenerated = true,
                                yFQjkifssS = false
                            };
                            hashSet.Add(aiPatchOperation.id);
                            break;
                        }
                        result = $"Operation[{num}] create_group parent '{aiPatchOperation.parentId}' does not exist in analysis package or generated operations.";
                        return false;
                    }
                    case (AiPatchOperationKind)2:
                        if (dictionary.ContainsKey(aiPatchOperation.targetId))
                        {
                            if (dictionary.ContainsKey(aiPatchOperation.newParentId))
                            {
                                if (hashSet3.Add(aiPatchOperation.targetId))
                                {
                                    if (!WouldCreateParentCycle(dictionary, aiPatchOperation.targetId, aiPatchOperation.newParentId))
                                    {
                                        dictionary[aiPatchOperation.targetId]._parentId = aiPatchOperation.newParentId;
                                        if (hashSet.Contains(aiPatchOperation.newParentId))
                                        {
                                            hashSet2.Add(aiPatchOperation.newParentId);
                                        }
                                        break;
                                    }
                                    result = $"Operation[{num}] move_node would parent '{aiPatchOperation.targetId}' into its own descendant chain.";
                                    return false;
                                }
                                result = $"Operation[{num}] moves target '{aiPatchOperation.targetId}' more than once.";
                                return false;
                            }
                            result = $"Operation[{num}] move_node newParentId '{aiPatchOperation.newParentId}' does not exist.";
                            return false;
                        }
                        result = $"Operation[{num}] move_node target '{aiPatchOperation.targetId}' does not exist.";
                        return false;
                    case (AiPatchOperationKind)3:
                    {
                        if (dictionary.TryGetValue(aiPatchOperation.targetId, out var value2))
                        {
                            if (!string.Equals(aiPatchOperation.targetId, "root", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value2._parentId))
                            {
                                if (value2._uiType == GUIType.Panel || value2._uiType == GUIType.Null)
                                {
                                    if (!HasDirectChildOfType(dictionary, aiPatchOperation.targetId, GUIType.Background))
                                    {
                                        FlattenNodeInTree(dictionary, aiPatchOperation.targetId, value2._parentId);
                                        if (hashSet.Contains(aiPatchOperation.targetId))
                                        {
                                            hashSet2.Add(aiPatchOperation.targetId);
                                        }
                                        break;
                                    }
                                    result = $"Operation[{num}] flatten_group target '{aiPatchOperation.targetId}' has a direct Background child.";
                                    return false;
                                }
                                result = $"Operation[{num}] flatten_group target '{aiPatchOperation.targetId}' must be a Null/Panel wrapper.";
                                return false;
                            }
                            result = $"Operation[{num}] flatten_group cannot target the root.";
                            return false;
                        }
                        result = $"Operation[{num}] flatten_group target '{aiPatchOperation.targetId}' does not exist.";
                        return false;
                    }
                    case (AiPatchOperationKind)4:
                    {
                        if (dictionary.TryGetValue(aiPatchOperation.targetId, out var value3))
                        {
                            TryParsePatchUiType(aiPatchOperation.uiType, out var gUIType);
                            if (IsUiTypeCompatibleWithLayer(dictionary, value3, gUIType))
                            {
                                value3._uiType = gUIType;
                                if (hashSet.Contains(aiPatchOperation.targetId))
                                {
                                    hashSet2.Add(aiPatchOperation.targetId);
                                }
                                break;
                            }
                            result = $"Operation[{num}] set_ui_type '{aiPatchOperation.targetId}' -> '{gUIType}' is incompatible with layerType '{value3._layerType}'.";
                            return false;
                        }
                        result = $"Operation[{num}] set_ui_type target '{aiPatchOperation.targetId}' does not exist.";
                        return false;
                    }
                    case (AiPatchOperationKind)5:
                        if (dictionary.ContainsKey(aiPatchOperation.targetId))
                        {
                            break;
                        }
                        result = $"Operation[{num}] rename_node target '{aiPatchOperation.targetId}' does not exist.";
                        return false;
                    case (AiPatchOperationKind)6:
                    {
                        if (dictionary.TryGetValue(aiPatchOperation.targetId, out var value))
                        {
                            if (value._isGenerated)
                            {
                                if (!HasChild(dictionary, aiPatchOperation.targetId))
                                {
                                    dictionary.Remove(aiPatchOperation.targetId);
                                    hashSet2.Add(aiPatchOperation.targetId);
                                    break;
                                }
                                result = $"Operation[{num}] delete_generated_group target '{aiPatchOperation.targetId}' still has children in dry-run tree.";
                                return false;
                            }
                            result = $"Operation[{num}] delete_generated_group target '{aiPatchOperation.targetId}' is not a generated group.";
                            return false;
                        }
                        result = $"Operation[{num}] delete_generated_group target '{aiPatchOperation.targetId}' does not exist.";
                        return false;
                    }
                    }
                    num++;
                    continue;
                }
                foreach (string item in hashSet)
                {
                    if (!hashSet2.Contains(item))
                    {
                        result = "Generated group '" + item + "' is never used by move_node, flatten_group, set_ui_type, or delete_generated_group.";
                        return false;
                    }
                }
                return ValidateAuxiliaryNodeOwnership(dictionary, value4, out result);
            }
            result = $"Operation[{num}] '{aiPatchOperation.op}' is out of order. Expected create_group, then move_node/flatten_group, then set_ui_type/rename_node, then delete_generated_group.";
            return false;
        }

        private static Dictionary<string, PatchTreeNodeState> BuildPatchTree(object value)
        {
            Dictionary<string, PatchTreeNodeState> dictionary = new Dictionary<string, PatchTreeNodeState>(StringComparer.OrdinalIgnoreCase) { ["root"] = new PatchTreeNodeState
            {
                _id = "root",
                _parentId = string.Empty,
                _layerType = PsdLayerType.LayerGroup.ToString(),
                _uiType = GUIType.Null,
                _isGenerated = false,
                yFQjkifssS = false
            } };
            for (int i = 0; i < ((AiAnalysisPackageDocument)value).nodes.Count; i++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = ((AiAnalysisPackageDocument)value).nodes[i];
                if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                {
                    GUIType result = GUIType.Null;
                    Enum.TryParse<GUIType>(aiAnalysisNodeEntry.uiType, ignoreCase: true, out result);
                    dictionary[aiAnalysisNodeEntry.id] = new PatchTreeNodeState
                    {
                        _id = aiAnalysisNodeEntry.id,
                        _parentId = (string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.parentId) ? "root" : aiAnalysisNodeEntry.parentId),
                        _layerType = (aiAnalysisNodeEntry.layerType ?? string.Empty),
                        _uiType = result,
                        _isGenerated = aiAnalysisNodeEntry.isGeneratedNode,
                        yFQjkifssS = false
                    };
                }
            }
            return dictionary;
        }

        private static bool IsOperationOrderValid(AiPatchOperationKind value, AiPatchOperationKind value2)
        {
            return GetOperationPhase(value2) >= GetOperationPhase(value);
        }

        internal static bool NormalizeOperationOrder(object aiPatchDocument)
        {
            if (aiPatchDocument != null && ((AiPatchDocument)aiPatchDocument).operations != null && ((AiPatchDocument)aiPatchDocument).operations.Count >= 2)
            {
                int num = int.MinValue;
                bool flag = false;
                int num2 = 0;
                while (true)
                {
                    if (num2 < ((AiPatchDocument)aiPatchDocument).operations.Count)
                    {
                        AiPatchOperation aiPatchOperation = ((AiPatchDocument)aiPatchDocument).operations[num2];
                        if (aiPatchOperation == null || !AiPatchOperationParser.TryParse(aiPatchOperation.op, out var value))
                        {
                            break;
                        }
                        int num3 = GetOperationPhase(value);
                        if (num3 >= num)
                        {
                            num = num3;
                            num2++;
                            continue;
                        }
                        flag = true;
                    }
                    if (!flag)
                    {
                        return false;
                    }
                    List<AiPatchOperation> list = new List<AiPatchOperation>();
                    List<AiPatchOperation> list2 = new List<AiPatchOperation>();
                    List<AiPatchOperation> list3 = new List<AiPatchOperation>();
                    List<AiPatchOperation> list4 = new List<AiPatchOperation>();
                    for (int i = 0; i < ((AiPatchDocument)aiPatchDocument).operations.Count; i++)
                    {
                        AiPatchOperation aiPatchOperation2 = ((AiPatchDocument)aiPatchDocument).operations[i];
                        AiPatchOperationParser.TryParse(aiPatchOperation2.op, out var value2);
                        switch (GetOperationPhase(value2))
                        {
                        case 0:
                            list.Add(aiPatchOperation2);
                            break;
                        case 1:
                            list2.Add(aiPatchOperation2);
                            break;
                        case 2:
                            list3.Add(aiPatchOperation2);
                            break;
                        case 3:
                            list4.Add(aiPatchOperation2);
                            break;
                        }
                    }
                    ((AiPatchDocument)aiPatchDocument).operations.Clear();
                    ((AiPatchDocument)aiPatchDocument).operations.AddRange(list);
                    ((AiPatchDocument)aiPatchDocument).operations.AddRange(list2);
                    ((AiPatchDocument)aiPatchDocument).operations.AddRange(list3);
                    ((AiPatchDocument)aiPatchDocument).operations.AddRange(list4);
                    return true;
                }
                return false;
            }
            return false;
        }

        internal static int GetOperationPhase(AiPatchOperationKind aiPatchOperationKind)
        {
            switch (aiPatchOperationKind)
            {
            default:
                return 0;
            case (AiPatchOperationKind)1:
                return 0;
            case (AiPatchOperationKind)2:
            case (AiPatchOperationKind)3:
                return 1;
            case (AiPatchOperationKind)4:
            case (AiPatchOperationKind)5:
                return 2;
            case (AiPatchOperationKind)6:
                return 3;
            }
        }

        private static bool WouldCreateParentCycle(Dictionary<string, PatchTreeNodeState> lookup, object value2, object value3)
        {
            string text = (string)value3;
            int num = 0;
            while (true)
            {
                if (num < lookup.Count + 1)
                {
                    if (!string.Equals(text, (string)value2, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!lookup.TryGetValue(text, out var value) || string.IsNullOrWhiteSpace(value._parentId))
                        {
                            break;
                        }
                        text = value._parentId;
                        num++;
                        continue;
                    }
                    return true;
                }
                return true;
            }
            return false;
        }

        private static bool HasChild(Dictionary<string, PatchTreeNodeState> lookup, object value)
        {
            foreach (KeyValuePair<string, PatchTreeNodeState> item in lookup)
            {
                if (string.Equals(item.Value._parentId, (string)value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasDirectChildOfType(Dictionary<string, PatchTreeNodeState> lookup, object value2, GUIType uiType)
        {
            foreach (KeyValuePair<string, PatchTreeNodeState> item in lookup)
            {
                PatchTreeNodeState value = item.Value;
                if (value != null && string.Equals(value._parentId, (string)value2, StringComparison.OrdinalIgnoreCase) && value._uiType == uiType)
                {
                    return true;
                }
            }
            return false;
        }

        private static void FlattenNodeInTree(Dictionary<string, PatchTreeNodeState> lookup, object value2, object value3)
        {
            foreach (KeyValuePair<string, PatchTreeNodeState> item in lookup)
            {
                PatchTreeNodeState value = item.Value;
                if (value != null && string.Equals(value._parentId, (string)value2, StringComparison.OrdinalIgnoreCase))
                {
                    value._parentId = (string)value3;
                }
            }
            lookup.Remove((string)value2);
        }

        private static bool IsUiTypeCompatibleWithLayer(Dictionary<string, PatchTreeNodeState> lookup, object value, GUIType uiType)
        {
            if (value == null)
            {
                return false;
            }
            if (uiType == GUIType.Null)
            {
                return true;
            }
            if (UGUIParser.IsCompositeControlType(uiType))
            {
                return string.Equals(((PatchTreeNodeState)value)._layerType, PsdLayerType.LayerGroup.ToString(), StringComparison.Ordinal);
            }
            if (IsTextSemanticType(uiType))
            {
                return string.Equals(((PatchTreeNodeState)value)._layerType, PsdLayerType.TextLayer.ToString(), StringComparison.Ordinal);
            }
            if (uiType == GUIType.Text)
            {
                return string.Equals(((PatchTreeNodeState)value)._layerType, PsdLayerType.TextLayer.ToString(), StringComparison.Ordinal);
            }
            if (IsGraphicSemanticType(uiType))
            {
                if (!string.Equals(((PatchTreeNodeState)value)._layerType, PsdLayerType.TextLayer.ToString(), StringComparison.Ordinal))
                {
                    return !string.Equals(((PatchTreeNodeState)value)._layerType, PsdLayerType.Unknown.ToString(), StringComparison.Ordinal);
                }
                return false;
            }
            return true;
        }

        private static bool HasTextDescendant(Dictionary<string, PatchTreeNodeState> lookup, object value2)
        {
            foreach (KeyValuePair<string, PatchTreeNodeState> item in lookup)
            {
                PatchTreeNodeState value = item.Value;
                if (string.Equals(value._parentId, (string)value2, StringComparison.OrdinalIgnoreCase) && (string.Equals(value._layerType, PsdLayerType.TextLayer.ToString(), StringComparison.Ordinal) || HasTextDescendant(lookup, value._id)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ValidateAuxiliaryNodeOwnership(Dictionary<string, PatchTreeNodeState> lookup, object value3, out string result)
        {
            result = null;
            List<string> list = new List<string>();
            foreach (KeyValuePair<string, PatchTreeNodeState> item in lookup)
            {
                PatchTreeNodeState value = item.Value;
                if (value != null && UGUIParser.IsAuxiliaryUIType(value._uiType) && (!lookup.TryGetValue(value._parentId, out var value2) || !UiTypeCompatibilityRules.IsRoleAllowed(value2._uiType, value._uiType)))
                {
                    list.Add($"Semantic node '{value._id}' with uiType '{value._uiType}' has no compatible owner after dry-run (parent is '{value2?._uiType}').");
                }
            }
            if (list.Count > 0)
            {
                result = string.Join("\n", list);
            }
            return true;
        }

        private static bool IsTextSemanticType(GUIType uiType)
        {
            return UiTypeCompatibilityRules.RoleRequiresTextLayer(uiType);
        }

        private static bool IsGraphicSemanticType(GUIType uiType)
        {
            switch (uiType)
            {
            default:
                return false;
            case GUIType.Background:
            case GUIType.Button_Highlight:
            case GUIType.Button_Press:
            case GUIType.Button_Select:
            case GUIType.Button_Disable:
            case GUIType.Dropdown_Arrow:
            case GUIType.Toggle_Checkmark:
            case GUIType.Slider_Fill:
            case GUIType.Slider_Handle:
            case GUIType.ScrollView_Viewport:
            case GUIType.ScrollView_HorizontalBarBG:
            case GUIType.ScrollView_HorizontalBar:
            case GUIType.ScrollView_VerticalBarBG:
            case GUIType.ScrollView_VerticalBar:
                return true;
            }
        }

        internal static bool TryParsePatchUiType(object text, out GUIType result)
        {
            result = GUIType.Null;
            if (!string.IsNullOrWhiteSpace((string)text) && Enum.TryParse<GUIType>((string)text, ignoreCase: true, out result) && Enum.IsDefined(typeof(GUIType), result))
            {
                if (result != GUIType.Null)
                {
                    return IsSupportedPatchUiType(result);
                }
                return true;
            }
            return false;
        }

        private static bool IsValidCurrentUiType(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return false;
            }
            if (!string.Equals((string)value, GUIType.Null.ToString(), StringComparison.Ordinal))
            {
                if (!Enum.TryParse<GUIType>((string)value, ignoreCase: true, out var result))
                {
                    return false;
                }
                return Enum.IsDefined(typeof(GUIType), result);
            }
            return true;
        }

        private static bool IsValidPredictedUiType(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                GUIType gUIType = GUIType.Null;
                if (!string.Equals((string)value, gUIType.ToString(), StringComparison.Ordinal))
                {
                    return TryParsePatchUiType(value, out gUIType);
                }
                return true;
            }
            return false;
        }

        private static bool IsValidAuditVerdict(object value)
        {
            if (!string.Equals((string)value, "correct", StringComparison.Ordinal) && !string.Equals((string)value, "corrected", StringComparison.Ordinal))
            {
                return string.Equals((string)value, "removed", StringComparison.Ordinal);
            }
            return true;
        }

        private static bool IsSupportedPatchUiType(GUIType uiType)
        {
            if ((uint)(uiType - 12) <= 4u)
            {
                return false;
            }
            return true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchValidator GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
