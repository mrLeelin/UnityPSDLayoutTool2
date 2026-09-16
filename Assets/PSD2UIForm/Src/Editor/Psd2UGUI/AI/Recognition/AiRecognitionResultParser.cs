using System;
using System.Collections.Generic;
using AiJobFileStoreNamespace;
using AiPatchValidatorNamespace;
using UGF.EditorTools.Psd2UGUI;
using UiTypeCompatibilityRulesNamespace;

namespace AiRecognitionResultParserNamespace
{
    internal sealed class AiRecognitionResultParser
    {
        private static AiRecognitionResultParser s_ObfuscationSentinel;

        internal bool TryLoadMainTypeResult(AiJobContext aiJobContext, AiAnalysisPackageDocument aiAnalysisPackageDocument, out AiMainTypeResultDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (aiJobContext == null || aiAnalysisPackageDocument == null || aiAnalysisPackageDocument.nodes == null)
            {
                result2 = "MainType load context is invalid.";
                return false;
            }
            if (AiJobFileStore.TryReadJson<AiMainTypeResultDocument>(aiJobContext.MainTypePath, out var aiMainTypeResultDocument) && aiMainTypeResultDocument != null)
            {
                return TryNormalizeMainTypeResult(aiAnalysisPackageDocument, aiMainTypeResultDocument.treeHash, aiMainTypeResultDocument.nodes, out result, out result2);
            }
            result2 = "MainType result not found: " + aiJobContext.MainTypePath;
            return false;
        }

        internal bool TryLoadChildRelationResult(AiJobContext aiJobContext, AiAnalysisPackageDocument aiAnalysisPackageDocument, out AiChildRelationResultDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (aiJobContext != null && aiAnalysisPackageDocument != null && aiAnalysisPackageDocument.nodes != null)
            {
                if (AiJobFileStore.TryReadJson<AiChildRelationResultDocument>(aiJobContext.ChildRelationPath, out var aiChildRelationResultDocument) && aiChildRelationResultDocument != null)
                {
                    return TryNormalizeChildRelationResult(aiAnalysisPackageDocument, aiChildRelationResultDocument.treeHash, aiChildRelationResultDocument.relations, out result, out result2);
                }
                result2 = "ChildRelation result not found: " + aiJobContext.ChildRelationPath;
                return false;
            }
            result2 = "ChildRelation load context is invalid.";
            return false;
        }

        internal bool TryLoadCombinedRecognitionResult(AiJobContext aiJobContext, AiAnalysisPackageDocument aiAnalysisPackageDocument, out AiRecognitionCombinedResultDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (aiJobContext != null && aiAnalysisPackageDocument != null && aiAnalysisPackageDocument.nodes != null)
            {
                if (!AiJobFileStore.TryReadJson<AiRecognitionCombinedResultDocument>(aiJobContext.RecognitionCombinedPath, out var aiRecognitionCombinedResultDocument) || aiRecognitionCombinedResultDocument == null)
                {
                    result2 = "RecognitionCombined result not found: " + aiJobContext.RecognitionCombinedPath;
                    return false;
                }
                return TryNormalizeCombinedRecognitionResult(aiAnalysisPackageDocument, aiRecognitionCombinedResultDocument, out result, out result2);
            }
            result2 = "RecognitionCombined load context is invalid.";
            return false;
        }

        internal bool TrySplitCombinedRecognitionResult(AiAnalysisPackageDocument aiAnalysisPackageDocument, AiRecognitionCombinedResultDocument aiRecognitionCombinedResultDocument2, out AiMainTypeResultDocument result, out AiChildRelationResultDocument result2, out string result3)
        {
            result = null;
            result2 = null;
            result3 = null;
            if (aiAnalysisPackageDocument != null && aiAnalysisPackageDocument.nodes != null && aiRecognitionCombinedResultDocument2 != null)
            {
                if (!TryNormalizeCombinedRecognitionResult(aiAnalysisPackageDocument, aiRecognitionCombinedResultDocument2, out var aiRecognitionCombinedResultDocument, out result3))
                {
                    return false;
                }
                Dictionary<string, GUIType> dictionary = BuildCurrentUiTypeLookup(aiAnalysisPackageDocument.nodes);
                List<AiMainTypeEntry> list = new List<AiMainTypeEntry>(aiAnalysisPackageDocument.nodes.Count);
                Dictionary<string, AiRecognitionNodeLabelEntry> dictionary2 = new Dictionary<string, AiRecognitionNodeLabelEntry>(StringComparer.OrdinalIgnoreCase);
                if (aiRecognitionCombinedResultDocument.nodeLabels != null)
                {
                    for (int i = 0; i < aiRecognitionCombinedResultDocument.nodeLabels.Count; i++)
                    {
                        AiRecognitionNodeLabelEntry aiRecognitionNodeLabelEntry = aiRecognitionCombinedResultDocument.nodeLabels[i];
                        if (aiRecognitionNodeLabelEntry != null && !string.IsNullOrWhiteSpace(aiRecognitionNodeLabelEntry.nodeId))
                        {
                            dictionary2[aiRecognitionNodeLabelEntry.nodeId] = aiRecognitionNodeLabelEntry;
                        }
                    }
                }
                Dictionary<string, GUIType> dictionary3 = BuildOwnerUiTypeLookup(aiRecognitionCombinedResultDocument);
                for (int j = 0; j < aiAnalysisPackageDocument.nodes.Count; j++)
                {
                    AiAnalysisNodeEntry aiAnalysisNodeEntry = aiAnalysisPackageDocument.nodes[j];
                    if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id) && dictionary2.TryGetValue(aiAnalysisNodeEntry.id, out var value) && value != null)
                    {
                        string predictedUIType = value.labelType;
                        if (dictionary3.TryGetValue(aiAnalysisNodeEntry.id, out var value2))
                        {
                            predictedUIType = value2.ToString();
                        }
                        list.Add(new AiMainTypeEntry
                        {
                            targetId = aiAnalysisNodeEntry.id,
                            currentUIType = (dictionary.TryGetValue(aiAnalysisNodeEntry.id, out var value3) ? value3.ToString() : GUIType.Null.ToString()),
                            predictedUIType = predictedUIType,
                            confidence = NormalizeConfidence(value.confidence),
                            reason = NormalizeReason(value.reason, "AI 节点保底标签。")
                        });
                    }
                }
                List<AiChildRelationEntry> list2 = new List<AiChildRelationEntry>((aiRecognitionCombinedResultDocument.roles != null) ? aiRecognitionCombinedResultDocument.roles.Count : 0);
                Dictionary<string, string> dictionary4 = BuildOwnerCarrierLookup(aiRecognitionCombinedResultDocument);
                if (aiRecognitionCombinedResultDocument.roles != null)
                {
                    for (int k = 0; k < aiRecognitionCombinedResultDocument.roles.Count; k++)
                    {
                        AiRecognitionRoleEntry aiRecognitionRoleEntry = aiRecognitionCombinedResultDocument.roles[k];
                        if (aiRecognitionRoleEntry != null)
                        {
                            list2.Add(new AiChildRelationEntry
                            {
                                ownerId = (dictionary4.TryGetValue(aiRecognitionRoleEntry.ownerId, out var value4) ? value4 : (aiRecognitionRoleEntry.ownerId ?? string.Empty)),
                                targetId = (aiRecognitionRoleEntry.carrierNodeId ?? string.Empty),
                                roleType = (aiRecognitionRoleEntry.roleType ?? string.Empty),
                                memberNodeIds = CopyMemberNodeIds(aiRecognitionRoleEntry.memberNodeIds),
                                confidence = NormalizeConfidence(aiRecognitionRoleEntry.confidence),
                                reason = NormalizeReason(aiRecognitionRoleEntry.reason, "AI 子控件归属识别结果。")
                            });
                        }
                    }
                }
                result = new AiMainTypeResultDocument
                {
                    version = "2.0",
                    treeHash = (aiAnalysisPackageDocument.treeHash ?? string.Empty),
                    nodes = list
                };
                result2 = new AiChildRelationResultDocument
                {
                    version = "2.0",
                    treeHash = (aiAnalysisPackageDocument.treeHash ?? string.Empty),
                    relations = list2
                };
                return true;
            }
            result3 = "RecognitionCombined split context is invalid.";
            return false;
        }

        internal bool TryCreateEmptyStructuralResult(AiJobContext aiJobContext, AiAnalysisPackageDocument aiAnalysisPackageDocument, out AiStructuralResultDocument result, out string result2)
        {
            result2 = null;
            result = new AiStructuralResultDocument
            {
                version = "1.0",
                treeHash = ((aiAnalysisPackageDocument != null) ? (aiAnalysisPackageDocument.treeHash ?? string.Empty) : string.Empty),
                operations = new List<AiPatchOperation>()
            };
            return true;
        }

        private static bool TryNormalizeCombinedRecognitionResult(object value, object value2, out AiRecognitionCombinedResultDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (value != null && ((AiAnalysisPackageDocument)value).nodes != null && value2 != null)
            {
                var organizer = (AiRecognitionCombinedResultDocument)value2;
                if (!string.IsNullOrEmpty(organizer.organizerVersion) &&
                    (organizer.organizerVersion != "1.0" || string.IsNullOrWhiteSpace(organizer.treeHash)))
                { result2 = "整理方案版本或输入指纹无效。"; return false; }
                if (!string.IsNullOrWhiteSpace(((AiAnalysisPackageDocument)value).treeHash) && !string.IsNullOrWhiteSpace(((AiRecognitionCombinedResultDocument)value2).treeHash) && !string.Equals(((AiAnalysisPackageDocument)value).treeHash, ((AiRecognitionCombinedResultDocument)value2).treeHash, StringComparison.OrdinalIgnoreCase))
                {
                    result2 = "RecognitionCombined result treeHash mismatch.";
                    return false;
                }
                Dictionary<string, AiAnalysisNodeEntry> dictionary = BuildNodeLookup(((AiAnalysisPackageDocument)value).nodes);
                if (!TryNormalizeOwners(dictionary, ((AiRecognitionCombinedResultDocument)value2).owners, out var list, out result2))
                {
                    return false;
                }
                if (!TryNormalizeRoles(dictionary, list, ((AiRecognitionCombinedResultDocument)value2).roles, out var list2, out result2))
                {
                    return false;
                }
                if (!TryNormalizeNodeLabels(((AiAnalysisPackageDocument)value).nodes, ((AiRecognitionCombinedResultDocument)value2).nodeLabels, true, out var nodeLabels, out result2))
                {
                    return false;
                }
                if (list.Count < 1)
                {
                    result2 = "RecognitionCombined result does not contain any valid owners.";
                    return false;
                }
                if (((AiRecognitionCombinedResultDocument)value2).roles != null && ((AiRecognitionCombinedResultDocument)value2).roles.Count > 0 && list2.Count < 1)
                {
                    result2 = "RecognitionCombined roles are incompatible with the current protocol.";
                    return false;
                }
                result = new AiRecognitionCombinedResultDocument
                {
                    version = "2.0",
                    treeHash = (((AiAnalysisPackageDocument)value).treeHash ?? string.Empty),
                    owners = list,
                    roles = list2,
                    nodeLabels = nodeLabels,
                    organizerVersion = organizer.organizerVersion,
                    renames = organizer.renames,
                    components = organizer.components
                };
                return true;
            }
            result2 = "RecognitionCombined normalize context is invalid.";
            return false;
        }

        private static bool TryNormalizeOwners(Dictionary<string, AiAnalysisNodeEntry> lookup, List<AiRecognitionOwnerEntry> values, out List<AiRecognitionOwnerEntry> result, out string result2)
        {
            result = new List<AiRecognitionOwnerEntry>(values?.Count ?? 0);
            result2 = null;
            if (values == null)
            {
                return true;
            }
            Dictionary<string, AiRecognitionOwnerEntry> dictionary = new Dictionary<string, AiRecognitionOwnerEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < values.Count; i++)
            {
                AiRecognitionOwnerEntry aiRecognitionOwnerEntry = values[i];
                if (aiRecognitionOwnerEntry == null || string.IsNullOrWhiteSpace(aiRecognitionOwnerEntry.ownerId))
                {
                    continue;
                }
                string text = GetKnownNodeId(aiRecognitionOwnerEntry.carrierNodeId, lookup);
                string[] array = NormalizeMemberNodeIds(aiRecognitionOwnerEntry.memberNodeIds, text, lookup);
                if (array.Length < 1 || !TryResolveOwnerType(aiRecognitionOwnerEntry.ownerType, text, array, lookup, out var gUIType))
                {
                    continue;
                }
                gUIType = UiTypeCompatibilityRules.NormalizeUiTypeAlias(gUIType);
                if (gUIType == GUIType.Text)
                {
                    if (!TryResolveTextNodeId(array, text, lookup, out var text2))
                    {
                        continue;
                    }
                    text = text2;
                    array = new string[1] { text2 };
                }
                AiRecognitionOwnerEntry aiRecognitionOwnerEntry2 = new AiRecognitionOwnerEntry
                {
                    ownerId = aiRecognitionOwnerEntry.ownerId.Trim(),
                    ownerType = gUIType.ToString(),
                    carrierNodeId = text,
                    memberNodeIds = array,
                    confidence = NormalizeConfidenceOrDefault(aiRecognitionOwnerEntry.confidence),
                    reason = NormalizeReason(aiRecognitionOwnerEntry.reason, "AI owner 识别结果。")
                };
                if (!dictionary.TryGetValue(aiRecognitionOwnerEntry2.ownerId, out var value) || ScoreOwnerCandidate(aiRecognitionOwnerEntry2) > ScoreOwnerCandidate(value))
                {
                    dictionary[aiRecognitionOwnerEntry2.ownerId] = aiRecognitionOwnerEntry2;
                }
            }
            result.AddRange(dictionary.Values);
            result.Sort((AiRecognitionOwnerEntry left, AiRecognitionOwnerEntry right) => ScoreOwnerCandidate(right).CompareTo(ScoreOwnerCandidate(left)));
            return true;
        }

        private static bool TryNormalizeRoles(Dictionary<string, AiAnalysisNodeEntry> lookup, List<AiRecognitionOwnerEntry> values, List<AiRecognitionRoleEntry> values2, out List<AiRecognitionRoleEntry> result, out string result2)
        {
            result = new List<AiRecognitionRoleEntry>(values2?.Count ?? 0);
            result2 = null;
            if (values2 == null)
            {
                return true;
            }
            Dictionary<string, AiRecognitionOwnerEntry> dictionary = new Dictionary<string, AiRecognitionOwnerEntry>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    AiRecognitionOwnerEntry aiRecognitionOwnerEntry = values[i];
                    if (aiRecognitionOwnerEntry != null && !string.IsNullOrWhiteSpace(aiRecognitionOwnerEntry.ownerId))
                    {
                        dictionary[aiRecognitionOwnerEntry.ownerId] = aiRecognitionOwnerEntry;
                    }
                }
            }
            Dictionary<string, AiRecognitionRoleEntry> dictionary2 = new Dictionary<string, AiRecognitionRoleEntry>(StringComparer.OrdinalIgnoreCase);
            for (int j = 0; j < values2.Count; j++)
            {
                AiRecognitionRoleEntry aiRecognitionRoleEntry = values2[j];
                if (aiRecognitionRoleEntry == null || string.IsNullOrWhiteSpace(aiRecognitionRoleEntry.ownerId) || !dictionary.TryGetValue(aiRecognitionRoleEntry.ownerId.Trim(), out var value) || value == null)
                {
                    continue;
                }
                string text = GetKnownNodeId(aiRecognitionRoleEntry.carrierNodeId, lookup);
                string[] array = NormalizeMemberNodeIds(aiRecognitionRoleEntry.memberNodeIds, text, lookup);
                if (array.Length < 1)
                {
                    continue;
                }
                GUIType gUIType = ParseOwnerTypeOrNull(value.ownerType);
                if (!TryResolveRoleType(aiRecognitionRoleEntry.roleType, gUIType, text, array, lookup, out var gUIType2) || !UiTypeCompatibilityRules.IsRoleAllowed(gUIType, gUIType2))
                {
                    continue;
                }
                if (UiTypeCompatibilityRules.RoleRequiresTextLayer(gUIType2))
                {
                    if (!TryResolveTextNodeId(array, text, lookup, out var text2))
                    {
                        continue;
                    }
                    text = text2;
                    array = new string[1] { text2 };
                }
                else
                {
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (lookup.TryGetValue(array[k], out var value2) && value2 != null && value2.isTextLayer)
                        {
                            text = null;
                            array = Array.Empty<string>();
                            break;
                        }
                    }
                    if (array.Length < 1 || (!string.IsNullOrWhiteSpace(text) && lookup.TryGetValue(text, out var value3) && value3 != null && value3.isTextLayer) || (array.Length > 1 && !UiTypeCompatibilityRules.RoleAcceptsMultipleImageMembers(gUIType2)))
                    {
                        continue;
                    }
                }
                string key = value.ownerId + "\n" + gUIType2;
                AiRecognitionRoleEntry aiRecognitionRoleEntry2 = new AiRecognitionRoleEntry
                {
                    ownerId = value.ownerId,
                    roleType = gUIType2.ToString(),
                    carrierNodeId = text,
                    memberNodeIds = array,
                    confidence = NormalizeConfidenceOrDefault(aiRecognitionRoleEntry.confidence),
                    reason = NormalizeReason(aiRecognitionRoleEntry.reason, "AI role 识别结果。")
                };
                if (!dictionary2.TryGetValue(key, out var value4) || ScoreRoleCandidate(aiRecognitionRoleEntry2) > ScoreRoleCandidate(value4))
                {
                    dictionary2[key] = aiRecognitionRoleEntry2;
                }
            }
            List<AiRecognitionRoleEntry> list = new List<AiRecognitionRoleEntry>(dictionary2.Values);
            list.Sort((AiRecognitionRoleEntry left, AiRecognitionRoleEntry right) => ScoreRoleCandidate(right).CompareTo(ScoreRoleCandidate(left)));
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int num = 0; num < list.Count; num++)
            {
                AiRecognitionRoleEntry aiRecognitionRoleEntry3 = list[num];
                if (aiRecognitionRoleEntry3 == null)
                {
                    continue;
                }
                bool flag = false;
                for (int num2 = 0; num2 < aiRecognitionRoleEntry3.memberNodeIds.Length; num2++)
                {
                    if (!hashSet.Add(aiRecognitionRoleEntry3.memberNodeIds[num2]))
                    {
                        flag = true;
                        break;
                    }
                }
                if (flag)
                {
                    for (int num3 = 0; num3 < aiRecognitionRoleEntry3.memberNodeIds.Length; num3++)
                    {
                        hashSet.Remove(aiRecognitionRoleEntry3.memberNodeIds[num3]);
                    }
                }
                else
                {
                    result.Add(aiRecognitionRoleEntry3);
                }
            }
            return true;
        }

        private static bool TryNormalizeNodeLabels(List<AiAnalysisNodeEntry> values, List<AiRecognitionNodeLabelEntry> values2, bool enabled, out List<AiRecognitionNodeLabelEntry> result, out string result2)
        {
            result = new List<AiRecognitionNodeLabelEntry>(values?.Count ?? 0);
            result2 = null;
            if (values == null)
            {
                result2 = "RecognitionCombined nodeLabel normalization requires package nodes.";
                return false;
            }
            Dictionary<string, AiAnalysisNodeEntry> dictionary = BuildNodeLookup(values);
            Dictionary<string, AiRecognitionNodeLabelEntry> dictionary2 = new Dictionary<string, AiRecognitionNodeLabelEntry>(StringComparer.OrdinalIgnoreCase);
            if (values2 != null)
            {
                for (int i = 0; i < values2.Count; i++)
                {
                    AiRecognitionNodeLabelEntry aiRecognitionNodeLabelEntry = values2[i];
                    if (aiRecognitionNodeLabelEntry != null && !string.IsNullOrWhiteSpace(aiRecognitionNodeLabelEntry.nodeId) && dictionary.TryGetValue(aiRecognitionNodeLabelEntry.nodeId.Trim(), out var value) && value != null)
                    {
                        AiRecognitionNodeLabelEntry aiRecognitionNodeLabelEntry2 = new AiRecognitionNodeLabelEntry
                        {
                            nodeId = aiRecognitionNodeLabelEntry.nodeId.Trim(),
                            currentUIType = GetCurrentUiType(value).ToString(),
                            labelType = ResolveNodeLabelType(aiRecognitionNodeLabelEntry.labelType, value).ToString(),
                            confidence = NormalizeConfidenceOrDefault(aiRecognitionNodeLabelEntry.confidence),
                            reason = NormalizeReason(aiRecognitionNodeLabelEntry.reason, "AI 节点保底标签。")
                        };
                        if (!dictionary2.TryGetValue(aiRecognitionNodeLabelEntry2.nodeId, out var value2) || aiRecognitionNodeLabelEntry2.confidence > value2.confidence)
                        {
                            dictionary2[aiRecognitionNodeLabelEntry2.nodeId] = aiRecognitionNodeLabelEntry2;
                        }
                    }
                }
            }
            for (int j = 0; j < values.Count; j++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = values[j];
                if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                {
                    if (!dictionary2.TryGetValue(aiAnalysisNodeEntry.id, out var value3) || value3 == null)
                    {
                        value3 = new AiRecognitionNodeLabelEntry
                        {
                            nodeId = aiAnalysisNodeEntry.id,
                            currentUIType = GetCurrentUiType(aiAnalysisNodeEntry).ToString(),
                            labelType = UiTypeCompatibilityRules.InferBaseUiType(aiAnalysisNodeEntry).ToString(),
                            confidence = (enabled ? 0.8f : 0.7f),
                            reason = "本地补齐 nodeLabel。"
                        };
                    }
                    result.Add(value3);
                }
            }
            return true;
        }

        private static bool TryNormalizeMainTypeResult(object value3, object value4, List<AiMainTypeEntry> values, out AiMainTypeResultDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (value3 != null && ((AiAnalysisPackageDocument)value3).nodes != null)
            {
                if (!string.IsNullOrWhiteSpace(((AiAnalysisPackageDocument)value3).treeHash) && !string.IsNullOrWhiteSpace((string)value4) && !string.Equals(((AiAnalysisPackageDocument)value3).treeHash, (string)value4, StringComparison.OrdinalIgnoreCase))
                {
                    result2 = "MainType result treeHash mismatch.";
                    return false;
                }
                Dictionary<string, AiMainTypeEntry> dictionary = new Dictionary<string, AiMainTypeEntry>(StringComparer.OrdinalIgnoreCase);
                if (values != null)
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        AiMainTypeEntry aiMainTypeEntry = values[i];
                        if (aiMainTypeEntry != null && !string.IsNullOrWhiteSpace(aiMainTypeEntry.targetId) && UiTypeCompatibilityRules.TryParseOwnerType(aiMainTypeEntry.predictedUIType, out var gUIType))
                        {
                            if (aiMainTypeEntry.confidence <= 0f || !(aiMainTypeEntry.confidence <= 1f))
                            {
                                result2 = "MainType entry '" + aiMainTypeEntry.targetId + "' has invalid confidence.";
                                return false;
                            }
                            aiMainTypeEntry.currentUIType = NormalizeUiTypeName(aiMainTypeEntry.currentUIType);
                            aiMainTypeEntry.predictedUIType = gUIType.ToString();
                            aiMainTypeEntry.reason = NormalizeReason(aiMainTypeEntry.reason, "AI 主类型识别结果。");
                            if (!dictionary.TryGetValue(aiMainTypeEntry.targetId, out var value) || aiMainTypeEntry.confidence > value.confidence)
                            {
                                dictionary[aiMainTypeEntry.targetId] = aiMainTypeEntry;
                            }
                        }
                    }
                }
                List<AiMainTypeEntry> list = new List<AiMainTypeEntry>(((AiAnalysisPackageDocument)value3).nodes.Count);
                int num = 0;
                AiAnalysisNodeEntry aiAnalysisNodeEntry;
                while (true)
                {
                    if (num < ((AiAnalysisPackageDocument)value3).nodes.Count)
                    {
                        aiAnalysisNodeEntry = ((AiAnalysisPackageDocument)value3).nodes[num];
                        if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                        {
                            if (!dictionary.TryGetValue(aiAnalysisNodeEntry.id, out var value2))
                            {
                                break;
                            }
                            list.Add(value2);
                        }
                        num++;
                        continue;
                    }
                    result = new AiMainTypeResultDocument
                    {
                        version = "2.0",
                        treeHash = (((AiAnalysisPackageDocument)value3).treeHash ?? string.Empty),
                        nodes = list
                    };
                    return true;
                }
                result2 = "MainType result is missing node '" + aiAnalysisNodeEntry.id + "'.";
                return false;
            }
            result2 = "MainType normalize context is invalid.";
            return false;
        }

        private static bool TryNormalizeChildRelationResult(object value2, object value3, List<AiChildRelationEntry> values, out AiChildRelationResultDocument result, out string result2)
        {
            result = null;
            result2 = null;
            if (value2 != null && ((AiAnalysisPackageDocument)value2).nodes != null)
            {
                if (!string.IsNullOrWhiteSpace(((AiAnalysisPackageDocument)value2).treeHash) && !string.IsNullOrWhiteSpace((string)value3) && !string.Equals(((AiAnalysisPackageDocument)value2).treeHash, (string)value3, StringComparison.OrdinalIgnoreCase))
                {
                    result2 = "ChildRelation result treeHash mismatch.";
                    return false;
                }
                Dictionary<string, AiAnalysisNodeEntry> dictionary = BuildNodeLookup(((AiAnalysisPackageDocument)value2).nodes);
                Dictionary<string, AiChildRelationEntry> dictionary2 = new Dictionary<string, AiChildRelationEntry>(StringComparer.OrdinalIgnoreCase);
                if (values != null)
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        AiChildRelationEntry aiChildRelationEntry = values[i];
                        if (aiChildRelationEntry == null || (!(aiChildRelationEntry.confidence <= 0f) && aiChildRelationEntry.confidence <= 1f))
                        {
                            if (TryNormalizeChildRelationEntry(aiChildRelationEntry, dictionary, out var aiChildRelationEntry2))
                            {
                                string key = aiChildRelationEntry2.ownerId + "\n" + aiChildRelationEntry2.roleType;
                                if (!dictionary2.TryGetValue(key, out var value) || ScoreChildRelationCandidate(aiChildRelationEntry2) > ScoreChildRelationCandidate(value))
                                {
                                    dictionary2[key] = aiChildRelationEntry2;
                                }
                            }
                            continue;
                        }
                        result2 = $"ChildRelation entry[{i}] has invalid confidence.";
                        return false;
                    }
                }
                List<AiChildRelationEntry> list = new List<AiChildRelationEntry>(dictionary2.Values);
                list.Sort((AiChildRelationEntry left, AiChildRelationEntry right) => ScoreChildRelationCandidate(right).CompareTo(ScoreChildRelationCandidate(left)));
                HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int num = list.Count - 1; num >= 0; num--)
                {
                    AiChildRelationEntry aiChildRelationEntry3 = list[num];
                    if (aiChildRelationEntry3 != null && aiChildRelationEntry3.memberNodeIds != null)
                    {
                        bool flag = false;
                        for (int num2 = 0; num2 < aiChildRelationEntry3.memberNodeIds.Length; num2++)
                        {
                            if (!hashSet.Add(aiChildRelationEntry3.memberNodeIds[num2]))
                            {
                                flag = true;
                                break;
                            }
                        }
                        if (flag)
                        {
                            for (int num3 = 0; num3 < aiChildRelationEntry3.memberNodeIds.Length; num3++)
                            {
                                hashSet.Remove(aiChildRelationEntry3.memberNodeIds[num3]);
                            }
                            list.RemoveAt(num);
                        }
                    }
                    else
                    {
                        list.RemoveAt(num);
                    }
                }
                result = new AiChildRelationResultDocument
                {
                    version = "2.0",
                    treeHash = (((AiAnalysisPackageDocument)value2).treeHash ?? string.Empty),
                    relations = list
                };
                return true;
            }
            result2 = "ChildRelation normalize context is invalid.";
            return false;
        }

        private static bool TryNormalizeChildRelationEntry(object value, Dictionary<string, AiAnalysisNodeEntry> lookup, out AiChildRelationEntry result)
        {
            result = null;
            if (value != null && !string.IsNullOrWhiteSpace(((AiChildRelationEntry)value).ownerId) && lookup != null && lookup.ContainsKey(((AiChildRelationEntry)value).ownerId) && UiTypeCompatibilityRules.TryParseAuxiliaryRoleType(((AiChildRelationEntry)value).roleType, out var gUIType))
            {
                string[] array = NormalizeMemberNodeIds(((AiChildRelationEntry)value).memberNodeIds, GetKnownNodeId(((AiChildRelationEntry)value).targetId, lookup), lookup);
                if (array.Length < 1)
                {
                    return false;
                }
                result = new AiChildRelationEntry
                {
                    ownerId = ((AiChildRelationEntry)value).ownerId.Trim(),
                    targetId = GetKnownNodeId(((AiChildRelationEntry)value).targetId, lookup),
                    roleType = gUIType.ToString(),
                    memberNodeIds = array,
                    confidence = NormalizeConfidence(((AiChildRelationEntry)value).confidence),
                    reason = NormalizeReason(((AiChildRelationEntry)value).reason, "AI 子控件归属识别结果。")
                };
                return true;
            }
            return false;
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

        private static Dictionary<string, GUIType> BuildCurrentUiTypeLookup(List<AiAnalysisNodeEntry> values)
        {
            Dictionary<string, GUIType> dictionary = new Dictionary<string, GUIType>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return dictionary;
            }
            for (int i = 0; i < values.Count; i++)
            {
                AiAnalysisNodeEntry aiAnalysisNodeEntry = values[i];
                if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                {
                    dictionary[aiAnalysisNodeEntry.id] = GetCurrentUiType(aiAnalysisNodeEntry);
                }
            }
            return dictionary;
        }

        private static Dictionary<string, GUIType> BuildOwnerUiTypeLookup(object value2)
        {
            Dictionary<string, GUIType> dictionary = new Dictionary<string, GUIType>(StringComparer.OrdinalIgnoreCase);
            if (value2 != null && ((AiRecognitionCombinedResultDocument)value2).owners != null)
            {
                for (int i = 0; i < ((AiRecognitionCombinedResultDocument)value2).owners.Count; i++)
                {
                    AiRecognitionOwnerEntry aiRecognitionOwnerEntry = ((AiRecognitionCombinedResultDocument)value2).owners[i];
                    if (aiRecognitionOwnerEntry != null && !string.IsNullOrWhiteSpace(aiRecognitionOwnerEntry.carrierNodeId) && UiTypeCompatibilityRules.TryParseOwnerType(aiRecognitionOwnerEntry.ownerType, out var value))
                    {
                        dictionary[aiRecognitionOwnerEntry.carrierNodeId] = value;
                    }
                }
                return dictionary;
            }
            return dictionary;
        }

        private static Dictionary<string, string> BuildOwnerCarrierLookup(object value)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (value != null && ((AiRecognitionCombinedResultDocument)value).owners != null)
            {
                for (int i = 0; i < ((AiRecognitionCombinedResultDocument)value).owners.Count; i++)
                {
                    AiRecognitionOwnerEntry aiRecognitionOwnerEntry = ((AiRecognitionCombinedResultDocument)value).owners[i];
                    if (aiRecognitionOwnerEntry != null && !string.IsNullOrWhiteSpace(aiRecognitionOwnerEntry.ownerId) && !string.IsNullOrWhiteSpace(aiRecognitionOwnerEntry.carrierNodeId))
                    {
                        dictionary[aiRecognitionOwnerEntry.ownerId] = aiRecognitionOwnerEntry.carrierNodeId;
                    }
                }
                return dictionary;
            }
            return dictionary;
        }

        private static string GetKnownNodeId(object value, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && lookup != null && lookup.ContainsKey(((string)value).Trim()))
            {
                return ((string)value).Trim();
            }
            return string.Empty;
        }

        private static string[] NormalizeMemberNodeIds(object value, object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            List<string> list = new List<string>((value == null) ? 1 : (((Array)value).Length + 1));
            if (!string.IsNullOrWhiteSpace((string)value2))
            {
                list.Add((string)value2);
            }
            if (value != null)
            {
                for (int i = 0; i < ((Array)value).Length; i++)
                {
                    string text = GetKnownNodeId(((object[])value)[i], lookup);
                    if (!string.IsNullOrWhiteSpace(text) && !list.Contains(text))
                    {
                        list.Add(text);
                    }
                }
            }
            return list.ToArray();
        }

        private static bool TryResolveTextNodeId(object value3, object value4, Dictionary<string, AiAnalysisNodeEntry> lookup, out string result)
        {
            result = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)value4) && lookup.TryGetValue((string)value4, out var value) && value != null && value.isTextLayer)
            {
                result = (string)value4;
                return true;
            }
            if (value3 != null && ((Array)value3).Length == 1)
            {
                string text = (string)((object[])value3)[0];
                if (lookup.TryGetValue(text, out var value2) && value2 != null && value2.isTextLayer)
                {
                    result = text;
                    return true;
                }
                return false;
            }
            return false;
        }

        private static int ScoreOwnerCandidate(object value)
        {
            if (value == null)
            {
                return int.MinValue;
            }
            int num = (int)(((AiRecognitionOwnerEntry)value).confidence * 100f);
            if (!string.IsNullOrWhiteSpace(((AiRecognitionOwnerEntry)value).carrierNodeId))
            {
                num += 20;
            }
            if (((AiRecognitionOwnerEntry)value).memberNodeIds != null)
            {
                num += ((((AiRecognitionOwnerEntry)value).memberNodeIds.Length == 1) ? 10 : 5);
            }
            return num;
        }

        private static int ScoreRoleCandidate(object value)
        {
            if (value == null)
            {
                return int.MinValue;
            }
            int num = (int)(((AiRecognitionRoleEntry)value).confidence * 100f);
            if (!string.IsNullOrWhiteSpace(((AiRecognitionRoleEntry)value).carrierNodeId))
            {
                num += 25;
            }
            if (((AiRecognitionRoleEntry)value).memberNodeIds != null)
            {
                num += ((((AiRecognitionRoleEntry)value).memberNodeIds.Length == 1) ? 20 : 5);
            }
            if (UiTypeCompatibilityRules.TryParseAuxiliaryRoleType(((AiRecognitionRoleEntry)value).roleType, out var gUIType) && UiTypeCompatibilityRules.RoleRequiresTextLayer(gUIType))
            {
                num += 10;
            }
            return num;
        }

        private static int ScoreChildRelationCandidate(object value)
        {
            if (value != null)
            {
                int num = (int)(((AiChildRelationEntry)value).confidence * 100f);
                if (!string.IsNullOrWhiteSpace(((AiChildRelationEntry)value).targetId))
                {
                    num += 20;
                }
                if (((AiChildRelationEntry)value).memberNodeIds != null)
                {
                    num += ((((AiChildRelationEntry)value).memberNodeIds.Length == 1) ? 10 : 5);
                }
                return num;
            }
            return int.MinValue;
        }

        private static GUIType ParseOwnerTypeOrNull(object value)
        {
            if (UiTypeCompatibilityRules.TryParseOwnerType(value, out var result))
            {
                return result;
            }
            return GUIType.Null;
        }

        private static bool TryResolveOwnerType(object value, object value2, object value3, Dictionary<string, AiAnalysisNodeEntry> lookup, out GUIType result)
        {
            if (UiTypeCompatibilityRules.TryParseOwnerType(value, out result))
            {
                result = UiTypeCompatibilityRules.NormalizeUiTypeAlias(result);
                return true;
            }
            if (UiTypeCompatibilityRules.TryParseAuxiliaryRoleType(value, out var gUIType))
            {
                if (gUIType == GUIType.Background)
                {
                    result = InferBasicOwnerType(value2, value3, lookup);
                    return result != GUIType.Null;
                }
                if (UiTypeCompatibilityRules.RoleRequiresTextLayer(gUIType) && TryResolveTextNodeId(value3, value2, lookup, out var _))
                {
                    result = GUIType.Text;
                    return true;
                }
            }
            result = GUIType.Null;
            return false;
        }

        private static bool TryResolveRoleType(object value, GUIType uiType, object value2, object value3, Dictionary<string, AiAnalysisNodeEntry> lookup, out GUIType result)
        {
            if (UiTypeCompatibilityRules.TryParseAuxiliaryRoleType(value, out result))
            {
                result = UiTypeCompatibilityRules.NormalizeUiTypeAlias(result);
                return true;
            }
            if (!AiPatchValidator.TryParsePatchUiType(value, out var gUIType))
            {
                result = GUIType.Null;
                return false;
            }
            gUIType = UiTypeCompatibilityRules.NormalizeUiTypeAlias(gUIType);
            if (gUIType == GUIType.Text)
            {
                if (TryGetTextRoleType(uiType, out result) && TryResolveTextNodeId(value3, value2, lookup, out var _))
                {
                    return true;
                }
                result = GUIType.Null;
                return false;
            }
            if (UiTypeCompatibilityRules.IsRoleAllowed(uiType, GUIType.Background))
            {
                result = GUIType.Background;
                return true;
            }
            result = GUIType.Null;
            return false;
        }

        private static GUIType ResolveNodeLabelType(object value, object value2)
        {
            return UiTypeCompatibilityRules.ResolveBaseUiType((string)value, (AiAnalysisNodeEntry)value2);
        }

        private static GUIType InferBasicOwnerType(object value3, object value4, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            bool flag = false;
            bool flag2 = false;
            if (!string.IsNullOrWhiteSpace((string)value3) && lookup.TryGetValue((string)value3, out var value) && value != null)
            {
                flag |= value.isTextLayer;
                flag2 |= !value.isTextLayer;
            }
            if (value4 != null)
            {
                for (int i = 0; i < ((Array)value4).Length; i++)
                {
                    if (lookup.TryGetValue((string)((object[])value4)[i], out var value2) && value2 != null)
                    {
                        flag |= value2.isTextLayer;
                        flag2 |= !value2.isTextLayer;
                    }
                }
            }
            if (flag2)
            {
                return GUIType.Image;
            }
            if (!flag)
            {
                return GUIType.Null;
            }
            return GUIType.Text;
        }

        private static bool TryGetTextRoleType(GUIType uiType, out GUIType result)
        {
            switch (uiType)
            {
            default:
                result = GUIType.Null;
                return false;
            case GUIType.Button:
                result = GUIType.Button_Text;
                return true;
            case GUIType.Dropdown:
                result = GUIType.Dropdown_Label;
                return true;
            case GUIType.InputField:
                result = GUIType.InputField_Text;
                return true;
            case GUIType.Toggle:
                result = GUIType.Toggle_Label;
                return true;
            }
        }

        private static GUIType GetCurrentUiType(object value)
        {
            if (value == null)
            {
                return GUIType.Null;
            }
            if (!AiPatchValidator.TryParsePatchUiType(((AiAnalysisNodeEntry)value).uiType, out var gUIType))
            {
                return UiTypeCompatibilityRules.InferBaseUiType(value);
            }
            return UiTypeCompatibilityRules.NormalizeUiTypeAlias(gUIType);
        }

        private static string NormalizeUiTypeName(object value)
        {
            if (AiPatchValidator.TryParsePatchUiType(value, out var gUIType))
            {
                return UiTypeCompatibilityRules.NormalizeUiTypeAlias(gUIType).ToString();
            }
            return GUIType.Null.ToString();
        }

        private static string NormalizeReason(object value, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                return ((string)value).Trim();
            }
            return (string)value2;
        }

        private static float NormalizeConfidence(float value)
        {
            if (value > 0f && !(value > 1f))
            {
                return value;
            }
            return -1f;
        }

        private static float NormalizeConfidenceOrDefault(float value)
        {
            float num = NormalizeConfidence(value);
            if (!(num > 0f))
            {
                return 0.5f;
            }
            return num;
        }

        private static string[] CopyMemberNodeIds(object value)
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

        internal static AiRecognitionResultParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
