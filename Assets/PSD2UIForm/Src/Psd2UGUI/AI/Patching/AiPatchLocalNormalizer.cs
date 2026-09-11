using System;
using System.Collections.Generic;
using LayerNodeIdUtilityNamespace;
using AiPatchOperationParserNamespace;
using AiPatchValidatorNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using AiPatchOperationKindNamespace;
using UiTypeCompatibilityRulesNamespace;

namespace AiPatchLocalNormalizerNamespace
{
    internal sealed class AiPatchLocalNormalizer
    {
        internal static AiPatchLocalNormalizer s_ObfuscationSentinel;

        internal static bool NormalizePatchDocument(object aiPatchDocument)
        {
            if (aiPatchDocument != null)
            {
                bool result = false;
                if (((AiPatchDocument)aiPatchDocument).analysis == null)
                {
                    ((AiPatchDocument)aiPatchDocument).analysis = new List<AiAuditEntry>();
                    result = true;
                }
                if (((AiPatchDocument)aiPatchDocument).operations == null)
                {
                    ((AiPatchDocument)aiPatchDocument).operations = new List<AiPatchOperation>();
                    result = true;
                }
                for (int num = ((AiPatchDocument)aiPatchDocument).analysis.Count - 1; num >= 0; num--)
                {
                    AiAuditEntry aiAuditEntry = ((AiPatchDocument)aiPatchDocument).analysis[num];
                    if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId))
                    {
                        if (aiAuditEntry.ownerId == null)
                        {
                            aiAuditEntry.ownerId = string.Empty;
                            result = true;
                        }
                        if (!string.IsNullOrWhiteSpace(aiAuditEntry.predictedUIType) && IsValidPredictedUiType(aiAuditEntry.predictedUIType))
                        {
                            if (string.IsNullOrWhiteSpace(aiAuditEntry.currentUIType) || !IsDefinedGuiTypeName(aiAuditEntry.currentUIType))
                            {
                                aiAuditEntry.currentUIType = aiAuditEntry.predictedUIType;
                                result = true;
                            }
                            if (!IsValidVerdict(aiAuditEntry.verdict))
                            {
                                aiAuditEntry.verdict = (string.Equals(aiAuditEntry.currentUIType, aiAuditEntry.predictedUIType, StringComparison.Ordinal) ? "correct" : "corrected");
                                result = true;
                            }
                            if (!(aiAuditEntry.confidence <= 0f) && aiAuditEntry.confidence <= 1f)
                            {
                                if (string.IsNullOrWhiteSpace(aiAuditEntry.reason))
                                {
                                    aiAuditEntry.reason = "本地补齐：AI 未提供 reason";
                                    result = true;
                                }
                            }
                            else
                            {
                                ((AiPatchDocument)aiPatchDocument).analysis.RemoveAt(num);
                                result = true;
                            }
                        }
                        else
                        {
                            ((AiPatchDocument)aiPatchDocument).analysis.RemoveAt(num);
                            result = true;
                        }
                    }
                    else
                    {
                        ((AiPatchDocument)aiPatchDocument).analysis.RemoveAt(num);
                        result = true;
                    }
                }
                for (int num2 = ((AiPatchDocument)aiPatchDocument).operations.Count - 1; num2 >= 0; num2--)
                {
                    AiPatchOperation aiPatchOperation = ((AiPatchDocument)aiPatchDocument).operations[num2];
                    if (NormalizePatchOperation(aiPatchOperation))
                    {
                        result = true;
                        if (aiPatchOperation.confidence <= 0f || aiPatchOperation.confidence > 1f)
                        {
                            aiPatchOperation.confidence = NormalizeConfidence(aiPatchOperation.confidence);
                            result = true;
                        }
                        if (string.IsNullOrWhiteSpace(aiPatchOperation.reason))
                        {
                            aiPatchOperation.reason = "本地补齐：AI 未提供 reason";
                            result = true;
                        }
                    }
                    else
                    {
                        ((AiPatchDocument)aiPatchDocument).operations.RemoveAt(num2);
                        result = true;
                    }
                }
                return result;
            }
            return false;
        }

        internal static bool RepairAnalysisOwnership(object value)
        {
            return RepairAnalysisOwnership(value, null);
        }

        internal static bool RepairAnalysisOwnership(object aiPatchDocument, object value)
        {
            if (aiPatchDocument == null || ((AiPatchDocument)aiPatchDocument).analysis == null)
            {
                return false;
            }
            bool flag = DeduplicateAnalysisEntries(aiPatchDocument);
            Dictionary<string, string> dictionary = BuildParentIdLookup(value);
            Dictionary<string, GUIType> dictionary2 = BuildPredictedUiTypeLookup(aiPatchDocument);
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ((AiPatchDocument)aiPatchDocument).analysis.Count; i++)
            {
                AiAuditEntry aiAuditEntry = ((AiPatchDocument)aiPatchDocument).analysis[i];
                if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId) && string.IsNullOrWhiteSpace(aiAuditEntry.ownerId) && AiPatchValidator.TryParsePatchUiType(aiAuditEntry.predictedUIType, out var gUIType) && IsAuxiliaryUiType(gUIType))
                {
                    if (TryFindCompatibleOwner(aiAuditEntry.targetId, gUIType, dictionary, dictionary2, out var ownerId))
                    {
                        aiAuditEntry.ownerId = ownerId;
                        aiAuditEntry.reason = "按最近兼容父节点补齐 ownerId";
                        flag = true;
                    }
                    else if (gUIType == GUIType.Background)
                    {
                        aiAuditEntry.predictedUIType = "Image";
                        aiAuditEntry.verdict = ((!string.Equals(aiAuditEntry.currentUIType, aiAuditEntry.predictedUIType, StringComparison.Ordinal)) ? "corrected" : "correct");
                        aiAuditEntry.reason = "无兼容 owner 的 Background 修正为独立 Image";
                        hashSet.Add(aiAuditEntry.targetId);
                        flag = true;
                    }
                }
            }
            if (flag |= ResolveDuplicateAuxiliaryRoles(aiPatchDocument, value))
            {
                SynchronizeSetUiTypeOperations(aiPatchDocument);
            }
            if (hashSet.Count == 0)
            {
                return flag;
            }
            if (((AiPatchDocument)aiPatchDocument).operations != null)
            {
                for (int j = 0; j < ((AiPatchDocument)aiPatchDocument).operations.Count; j++)
                {
                    AiPatchOperation aiPatchOperation = ((AiPatchDocument)aiPatchDocument).operations[j];
                    if (aiPatchOperation != null && string.Equals(aiPatchOperation.op, "set_ui_type", StringComparison.Ordinal) && hashSet.Contains(aiPatchOperation.targetId) && string.Equals(aiPatchOperation.uiType, "Background", StringComparison.Ordinal))
                    {
                        aiPatchOperation.uiType = "Image";
                        aiPatchOperation.reason = "无兼容 owner 的 Background 修正为独立 Image";
                    }
                }
            }
            return true;
        }

        private static bool DeduplicateAnalysisEntries(object value2)
        {
            if (value2 != null && ((AiPatchDocument)value2).analysis != null && ((AiPatchDocument)value2).analysis.Count >= 2)
            {
                Dictionary<string, AiAuditEntry> dictionary = new Dictionary<string, AiAuditEntry>(StringComparer.OrdinalIgnoreCase);
                List<string> list = new List<string>(((AiPatchDocument)value2).analysis.Count);
                bool flag = false;
                for (int i = 0; i < ((AiPatchDocument)value2).analysis.Count; i++)
                {
                    AiAuditEntry aiAuditEntry = ((AiPatchDocument)value2).analysis[i];
                    if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId))
                    {
                        if (dictionary.TryGetValue(aiAuditEntry.targetId, out var value))
                        {
                            flag = true;
                            if (ScoreAnalysisEntry(aiAuditEntry) > ScoreAnalysisEntry(value))
                            {
                                dictionary[aiAuditEntry.targetId] = aiAuditEntry;
                            }
                        }
                        else
                        {
                            dictionary.Add(aiAuditEntry.targetId, aiAuditEntry);
                            list.Add(aiAuditEntry.targetId);
                        }
                    }
                    else
                    {
                        flag = true;
                    }
                }
                if (!flag)
                {
                    return false;
                }
                ((AiPatchDocument)value2).analysis.Clear();
                for (int j = 0; j < list.Count; j++)
                {
                    AiAuditEntry aiAuditEntry2 = dictionary[list[j]];
                    aiAuditEntry2.reason = "本地去重后保留的唯一 analysis 结论";
                    ((AiPatchDocument)value2).analysis.Add(aiAuditEntry2);
                }
                return true;
            }
            return false;
        }

        private static int ScoreAnalysisEntry(object value)
        {
            if (value == null)
            {
                return int.MinValue;
            }
            int num = Mathf.RoundToInt(Mathf.Clamp01(((AiAuditEntry)value).confidence) * 100f);
            if (!string.IsNullOrWhiteSpace(((AiAuditEntry)value).ownerId))
            {
                num += 1000;
            }
            if (AiPatchValidator.TryParsePatchUiType(((AiAuditEntry)value).predictedUIType, out var gUIType))
            {
                if (IsAuxiliaryUiType(gUIType))
                {
                    num += 500;
                }
                if (gUIType != GUIType.Panel && gUIType != GUIType.Null)
                {
                    num += 100;
                }
                if (gUIType == GUIType.Text || gUIType == GUIType.Image)
                {
                    num += 20;
                }
            }
            if (string.Equals(((AiAuditEntry)value).verdict, "removed", StringComparison.Ordinal))
            {
                num -= 50;
            }
            return num;
        }

        private static bool ResolveDuplicateAuxiliaryRoles(object value2, object value3)
        {
            if (value2 != null && ((AiPatchDocument)value2).analysis != null && ((AiPatchDocument)value2).analysis.Count >= 2)
            {
                Dictionary<string, AiAnalysisNodeEntry> dictionary = BuildAnalysisNodeLookup(value3);
                Dictionary<string, AiAuditEntry> dictionary2 = new Dictionary<string, AiAuditEntry>(StringComparer.OrdinalIgnoreCase);
                bool result = false;
                for (int i = 0; i < ((AiPatchDocument)value2).analysis.Count; i++)
                {
                    AiAuditEntry aiAuditEntry = ((AiPatchDocument)value2).analysis[i];
                    if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.ownerId) && AiPatchValidator.TryParsePatchUiType(aiAuditEntry.predictedUIType, out var gUIType) && IsAuxiliaryUiType(gUIType))
                    {
                        string key = aiAuditEntry.ownerId + "\n" + aiAuditEntry.predictedUIType;
                        if (dictionary2.TryGetValue(key, out var value))
                        {
                            result = true;
                            AiAuditEntry aiAuditEntry2 = ((ScoreOwnedAnalysisEntry(aiAuditEntry, dictionary) <= ScoreOwnedAnalysisEntry(value, dictionary)) ? value : aiAuditEntry);
                            AiAuditEntry obj = ((aiAuditEntry2 == aiAuditEntry) ? value : aiAuditEntry);
                            dictionary2[key] = aiAuditEntry2;
                            DowngradeDuplicateRoleEntry(obj, gUIType, dictionary);
                        }
                        else
                        {
                            dictionary2.Add(key, aiAuditEntry);
                        }
                    }
                }
                return result;
            }
            return false;
        }

        private static Dictionary<string, AiAnalysisNodeEntry> BuildAnalysisNodeLookup(object value)
        {
            Dictionary<string, AiAnalysisNodeEntry> dictionary = new Dictionary<string, AiAnalysisNodeEntry>(StringComparer.OrdinalIgnoreCase);
            if (value != null && ((AiAnalysisPackageDocument)value).nodes != null)
            {
                for (int i = 0; i < ((AiAnalysisPackageDocument)value).nodes.Count; i++)
                {
                    AiAnalysisNodeEntry aiAnalysisNodeEntry = ((AiAnalysisPackageDocument)value).nodes[i];
                    if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                    {
                        dictionary[aiAnalysisNodeEntry.id] = aiAnalysisNodeEntry;
                    }
                }
                return dictionary;
            }
            return dictionary;
        }

        private static int ScoreOwnedAnalysisEntry(object value2, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value2 == null)
            {
                return int.MinValue;
            }
            int num = Mathf.RoundToInt(Mathf.Clamp01(((AiAuditEntry)value2).confidence) * 100f);
            if (lookup != null && lookup.TryGetValue(((AiAuditEntry)value2).targetId, out var value) && value != null && string.Equals(value.parentId, ((AiAuditEntry)value2).ownerId, StringComparison.OrdinalIgnoreCase))
            {
                num += 1000;
            }
            if (AiPatchValidator.TryParsePatchUiType(((AiAuditEntry)value2).predictedUIType, out var gUIType))
            {
                if (RequiresTextLayer(gUIType))
                {
                    num += 100;
                }
                if (IsTextAnalysisNode(lookup, ((AiAuditEntry)value2).targetId))
                {
                    num += 50;
                }
            }
            return num;
        }

        private static bool IsTextAnalysisNode(Dictionary<string, AiAnalysisNodeEntry> lookup, object value2)
        {
            if (lookup != null && !string.IsNullOrWhiteSpace((string)value2) && lookup.TryGetValue((string)value2, out var value) && value != null)
            {
                return value.isTextLayer;
            }
            return false;
        }

        private static void DowngradeDuplicateRoleEntry(object value, GUIType uiType, Dictionary<string, AiAnalysisNodeEntry> lookup)
        {
            if (value != null)
            {
                ((AiAuditEntry)value).ownerId = string.Empty;
                ((AiAuditEntry)value).predictedUIType = ((!RequiresTextLayer(uiType) || !IsTextAnalysisNode(lookup, ((AiAuditEntry)value).targetId)) ? "Image" : "Text");
                ((AiAuditEntry)value).verdict = (string.Equals(((AiAuditEntry)value).currentUIType, ((AiAuditEntry)value).predictedUIType, StringComparison.Ordinal) ? "correct" : "corrected");
                ((AiAuditEntry)value).reason = "同一 owner 下同类子控件重复，降级为普通元素";
            }
        }

        private static void SynchronizeSetUiTypeOperations(object value2)
        {
            if (value2 == null || ((AiPatchDocument)value2).analysis == null || ((AiPatchDocument)value2).operations == null || ((AiPatchDocument)value2).operations.Count == 0)
            {
                return;
            }
            Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ((AiPatchDocument)value2).analysis.Count; i++)
            {
                AiAuditEntry aiAuditEntry = ((AiPatchDocument)value2).analysis[i];
                if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId) && !string.IsNullOrWhiteSpace(aiAuditEntry.predictedUIType))
                {
                    dictionary[aiAuditEntry.targetId] = aiAuditEntry.predictedUIType;
                }
            }
            for (int j = 0; j < ((AiPatchDocument)value2).operations.Count; j++)
            {
                AiPatchOperation aiPatchOperation = ((AiPatchDocument)value2).operations[j];
                if (aiPatchOperation != null && string.Equals(aiPatchOperation.op, "set_ui_type", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(aiPatchOperation.targetId) && dictionary.TryGetValue(aiPatchOperation.targetId, out var value) && !string.Equals(aiPatchOperation.uiType, value, StringComparison.Ordinal))
                {
                    aiPatchOperation.uiType = value;
                    aiPatchOperation.reason = "同步本地归一化后的 analysis.predictedUIType";
                }
            }
        }

        internal static void AddMissingSetUiTypeOperations(object unityObject, object aiPatchDocument)
        {
            if (Psd2UIFormTargetCompat.IsNull(unityObject) || aiPatchDocument == null || ((AiPatchDocument)aiPatchDocument).analysis == null)
            {
                return;
            }
            if (((AiPatchDocument)aiPatchDocument).operations == null)
            {
                ((AiPatchDocument)aiPatchDocument).operations = new List<AiPatchOperation>();
            }
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ((AiPatchDocument)aiPatchDocument).operations.Count; i++)
            {
                AiPatchOperation aiPatchOperation = ((AiPatchDocument)aiPatchDocument).operations[i];
                if (aiPatchOperation != null && AiPatchOperationParser.TryParse(aiPatchOperation.op, out var value) && value == (AiPatchOperationKind)4 && !string.IsNullOrWhiteSpace(aiPatchOperation.targetId))
                {
                    hashSet.Add(aiPatchOperation.targetId);
                }
            }
            for (int j = 0; j < ((AiPatchDocument)aiPatchDocument).analysis.Count; j++)
            {
                AiAuditEntry aiAuditEntry = ((AiPatchDocument)aiPatchDocument).analysis[j];
                if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId) && AiPatchValidator.TryParsePatchUiType(aiAuditEntry.predictedUIType, out var _) && !string.Equals(aiAuditEntry.currentUIType, aiAuditEntry.predictedUIType, StringComparison.Ordinal) && !hashSet.Contains(aiAuditEntry.targetId))
                {
                    ((AiPatchDocument)aiPatchDocument).operations.Add(new AiPatchOperation
                    {
                        op = "set_ui_type",
                        targetId = aiAuditEntry.targetId,
                        uiType = aiAuditEntry.predictedUIType,
                        confidence = NormalizeConfidence(aiAuditEntry.confidence),
                        reason = "由 analysis.predictedUIType 自动补充类型修正"
                    });
                    hashSet.Add(aiAuditEntry.targetId);
                }
            }
            AiPatchValidator.NormalizeOperationOrder(aiPatchDocument);
        }

        private static void AddFlattenGroupOperations(object value, object value2, Dictionary<string, PsdLayerNode> lookup, HashSet<string> texts)
        {
            if (Psd2UIFormTargetCompat.IsNull(value) || value2 == null || lookup == null || lookup.Count == 0)
            {
                return;
            }
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (((AiPatchDocument)value2).analysis != null)
            {
                for (int i = 0; i < ((AiPatchDocument)value2).analysis.Count; i++)
                {
                    AiAuditEntry aiAuditEntry = ((AiPatchDocument)value2).analysis[i];
                    if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.ownerId))
                    {
                        hashSet2.Add(aiAuditEntry.ownerId);
                    }
                }
            }
            if (((AiPatchDocument)value2).operations != null)
            {
                for (int j = 0; j < ((AiPatchDocument)value2).operations.Count; j++)
                {
                    AiPatchOperation aiPatchOperation = ((AiPatchDocument)value2).operations[j];
                    if (aiPatchOperation != null && AiPatchOperationParser.TryParse(aiPatchOperation.op, out var value3))
                    {
                        if (value3 == (AiPatchOperationKind)3 && !string.IsNullOrWhiteSpace(aiPatchOperation.targetId))
                        {
                            hashSet.Add(aiPatchOperation.targetId);
                        }
                        if ((value3 == (AiPatchOperationKind)1 || value3 == (AiPatchOperationKind)2) && !string.IsNullOrWhiteSpace(aiPatchOperation.newParentId))
                        {
                            hashSet2.Add(aiPatchOperation.newParentId);
                        }
                        if (value3 == (AiPatchOperationKind)1 && !string.IsNullOrWhiteSpace(aiPatchOperation.parentId))
                        {
                            hashSet2.Add(aiPatchOperation.parentId);
                        }
                    }
                }
            }
            List<PsdLayerNode> list = new List<PsdLayerNode>(lookup.Values);
            list.Sort((PsdLayerNode a, PsdLayerNode b) => GetTransformDepth((!(!Psd2UIFormTargetCompat.IsNull(a))) ? null : Psd2UIFormTargetCompat.TransformOf(a)).CompareTo(GetTransformDepth((!(!Psd2UIFormTargetCompat.IsNull(b))) ? null : Psd2UIFormTargetCompat.TransformOf(b))));
            for (int num = 0; num < list.Count; num++)
            {
                PsdLayerNode psdLayerNode = list[num];
                if (!IsFlattenableContainerNode(value, psdLayerNode))
                {
                    continue;
                }
                string text = LayerNodeIdUtility.GetStableNodeId(value, psdLayerNode);
                if (string.IsNullOrWhiteSpace(text) || hashSet.Contains(text) || texts.Contains(text) || hashSet2.Contains(text))
                {
                    continue;
                }
                string text2;
                if (Psd2UIFormTargetCompat.TransformOf(psdLayerNode).childCount == 0)
                {
                    text2 = "本地结构归一化：删除无图像意义的空 Null/Panel 节点";
                }
                else
                {
                    if (!IsSingleChildRedundantWrapper(psdLayerNode))
                    {
                        continue;
                    }
                    text2 = "本地结构归一化：消除单子节点无意义 Null/Panel 嵌套";
                }
                ((AiPatchDocument)value2).operations.Add(new AiPatchOperation
                {
                    op = "flatten_group",
                    targetId = text,
                    confidence = 0.95f,
                    reason = text2
                });
                hashSet.Add(text);
                MarkAnalysisEntryRemoved(value2, text, text2);
            }
        }

        private static bool IsFlattenableContainerNode(object value, object value2)
        {
            if (!Psd2UIFormTargetCompat.IsNull(value) && !Psd2UIFormTargetCompat.IsNull(value2) && !((Object)(object)Psd2UIFormTargetCompat.TransformOf(value2) == (Object)null) && !((Object)(object)Psd2UIFormTargetCompat.TransformOf(value2) == (Object)(object)Psd2UIFormTargetCompat.TransformOf(value)))
            {
                if (((PsdLayerNode)value2).LayerType == PsdLayerType.LayerGroup && (((PsdLayerNode)value2).UIType == GUIType.Panel || ((PsdLayerNode)value2).UIType == GUIType.Null))
                {
                    if (!((PsdLayerNode)value2).ShouldExportImage() && !((PsdLayerNode)value2).HasAssetReference() && !((PsdLayerNode)value2).HasPrefabReference())
                    {
                        if (HasChildOfUiType(Psd2UIFormTargetCompat.TransformOf(value2), GUIType.Background))
                        {
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

        private static bool IsSingleChildRedundantWrapper(object value)
        {
            if (!Psd2UIFormTargetCompat.IsNull(value) && !((Object)(object)Psd2UIFormTargetCompat.TransformOf(value) == (Object)null) && Psd2UIFormTargetCompat.TransformOf(value).childCount == 1)
            {
                Transform child = Psd2UIFormTargetCompat.TransformOf(value).GetChild(0);
                PsdLayerNode psdLayerNode = (((object)child != null) ? Psd2UIFormTargetCompat.GameObjectOf(child).GetComponent<PsdLayerNode>() : null);
                if (!Psd2UIFormTargetCompat.IsNull(psdLayerNode))
                {
                    if (AreRectsApproximatelyEqual(((PsdLayerNode)value).GetLayerRect(), psdLayerNode.GetLayerRect()))
                    {
                        return true;
                    }
                    Rect val = ((PsdLayerNode)value).GetLayerRect();
                    return val.size == Vector2.zero;
                }
                return false;
            }
            return false;
        }

        private static bool HasChildOfUiType(object value, GUIType uiType)
        {
            if (!Psd2UIFormTargetCompat.IsNull(value))
            {
                int num = 0;
                while (true)
                {
                    if (num < ((Transform)value).childCount)
                    {
                        Transform child = ((Transform)value).GetChild(num);
                        PsdLayerNode psdLayerNode = (((object)child != null) ? Psd2UIFormTargetCompat.GameObjectOf(child).GetComponent<PsdLayerNode>() : null);
                        if (!Psd2UIFormTargetCompat.IsNull(psdLayerNode) && psdLayerNode.UIType == uiType)
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

        private static bool AreRectsApproximatelyEqual(Rect rect, Rect rect2)
        {
            if (Mathf.Abs(rect.x - rect2.x) <= 0.5f && Mathf.Abs(rect.y - rect2.y) <= 0.5f && Mathf.Abs(rect.width - rect2.width) <= 0.5f)
            {
                return Mathf.Abs(rect.height - rect2.height) <= 0.5f;
            }
            return false;
        }

        private static int GetTransformDepth(object value)
        {
            int num = 0;
            Transform val = (Transform)value;
            while (!Psd2UIFormTargetCompat.IsNull(val))
            {
                num++;
                val = val.parent;
            }
            return num;
        }

        private static void MarkAnalysisEntryRemoved(object value, object value2, object value3)
        {
            if (value == null || ((AiPatchDocument)value).analysis == null || string.IsNullOrWhiteSpace((string)value2))
            {
                return;
            }
            int num = 0;
            AiAuditEntry aiAuditEntry;
            while (true)
            {
                if (num < ((AiPatchDocument)value).analysis.Count)
                {
                    aiAuditEntry = ((AiPatchDocument)value).analysis[num];
                    if (aiAuditEntry != null && string.Equals(aiAuditEntry.targetId, (string)value2, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return;
            }
            aiAuditEntry.verdict = "removed";
            aiAuditEntry.predictedUIType = (string.IsNullOrWhiteSpace(aiAuditEntry.predictedUIType) ? "Null" : aiAuditEntry.predictedUIType);
            aiAuditEntry.confidence = NormalizeConfidence(aiAuditEntry.confidence);
            aiAuditEntry.reason = (string)value3;
        }

        private static Dictionary<string, string> BuildParentIdLookup(object value)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (value != null && ((AiAnalysisPackageDocument)value).nodes != null)
            {
                for (int i = 0; i < ((AiAnalysisPackageDocument)value).nodes.Count; i++)
                {
                    AiAnalysisNodeEntry aiAnalysisNodeEntry = ((AiAnalysisPackageDocument)value).nodes[i];
                    if (aiAnalysisNodeEntry != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.id))
                    {
                        dictionary[aiAnalysisNodeEntry.id] = (string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.parentId) ? "root" : aiAnalysisNodeEntry.parentId);
                    }
                }
                return dictionary;
            }
            return dictionary;
        }

        private static Dictionary<string, GUIType> BuildPredictedUiTypeLookup(object value2)
        {
            Dictionary<string, GUIType> dictionary = new Dictionary<string, GUIType>(StringComparer.OrdinalIgnoreCase);
            if (value2 != null && ((AiPatchDocument)value2).analysis != null)
            {
                for (int i = 0; i < ((AiPatchDocument)value2).analysis.Count; i++)
                {
                    AiAuditEntry aiAuditEntry = ((AiPatchDocument)value2).analysis[i];
                    if (aiAuditEntry != null && !string.IsNullOrWhiteSpace(aiAuditEntry.targetId) && AiPatchValidator.TryParsePatchUiType(aiAuditEntry.predictedUIType, out var value))
                    {
                        dictionary[aiAuditEntry.targetId] = value;
                    }
                }
                return dictionary;
            }
            return dictionary;
        }

        private static bool TryFindCompatibleOwner(object value3, GUIType uiType, Dictionary<string, string> lookup, Dictionary<string, GUIType> lookup2, out string result)
        {
            result = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)value3) && lookup != null && lookup2 != null)
            {
                string key = (string)value3;
                int num = 0;
                while (true)
                {
                    if (num < 64)
                    {
                        if (!lookup.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value) || string.Equals(value, "root", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                        if (!lookup2.TryGetValue(value, out var value2) || !UiTypeCompatibilityRules.IsRoleAllowed(value2, uiType))
                        {
                            key = value;
                            num++;
                            continue;
                        }
                        result = value;
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static Dictionary<string, PsdLayerNode> BuildLayerNodeLookup(object value)
        {
            Dictionary<string, PsdLayerNode> dictionary = new Dictionary<string, PsdLayerNode>(StringComparer.OrdinalIgnoreCase);
            if (Psd2UIFormTargetCompat.IsNull(value))
            {
                return dictionary;
            }
            PsdLayerNode[] componentsInChildren = Psd2UIFormTargetCompat.GameObjectOf(value).GetComponentsInChildren<PsdLayerNode>(true);
            if (componentsInChildren == null)
            {
                return dictionary;
            }
            foreach (PsdLayerNode psdLayerNode in componentsInChildren)
            {
                if (!Psd2UIFormTargetCompat.IsNull(psdLayerNode))
                {
                    string text = LayerNodeIdUtility.GetStableNodeId(value, psdLayerNode);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        dictionary[text] = psdLayerNode;
                    }
                }
            }
            return dictionary;
        }

        private static string GetParentNodeId(object value, object value2)
        {
            if (!Psd2UIFormTargetCompat.IsNull(value) && !Psd2UIFormTargetCompat.IsNull(value2) && !((Object)value2 == (Object)(object)Psd2UIFormTargetCompat.TransformOf(value)))
            {
                PsdLayerNode component = Psd2UIFormTargetCompat.GameObjectOf(value2).GetComponent<PsdLayerNode>();
                if (!Psd2UIFormTargetCompat.IsNull(component))
                {
                    return LayerNodeIdUtility.GetStableNodeId(value, component);
                }
                return "root";
            }
            return "root";
        }

        private static bool NormalizePatchOperation(object value)
        {
            if (value != null && AiPatchOperationParser.TryParse(((AiPatchOperation)value).op, out var value2))
            {
                switch (value2)
                {
                default:
                    return false;
                case (AiPatchOperationKind)1:
                    if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value).id) && ((AiPatchOperation)value).id.StartsWith("gen:", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(((AiPatchOperation)value).parentId) && !string.IsNullOrWhiteSpace(((AiPatchOperation)value).name))
                    {
                        if (!AiPatchValidator.TryParsePatchUiType(((AiPatchOperation)value).uiType, out var gUIType2) || UGUIParser.IsAuxiliaryUIType(gUIType2))
                        {
                            ((AiPatchOperation)value).uiType = "Null";
                        }
                        if (((AiPatchOperation)value).insertIndex < -1)
                        {
                            ((AiPatchOperation)value).insertIndex = -1;
                        }
                        return true;
                    }
                    return false;
                case (AiPatchOperationKind)2:
                    if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value).targetId) && !string.IsNullOrWhiteSpace(((AiPatchOperation)value).newParentId))
                    {
                        ((AiPatchOperation)value).id = string.Empty;
                        ((AiPatchOperation)value).parentId = string.Empty;
                        ((AiPatchOperation)value).name = string.Empty;
                        ((AiPatchOperation)value).uiType = string.Empty;
                        if (((AiPatchOperation)value).insertIndex < -1)
                        {
                            ((AiPatchOperation)value).insertIndex = -1;
                        }
                        return true;
                    }
                    return false;
                case (AiPatchOperationKind)3:
                    if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value).targetId))
                    {
                        ((AiPatchOperation)value).id = string.Empty;
                        ((AiPatchOperation)value).parentId = string.Empty;
                        ((AiPatchOperation)value).newParentId = string.Empty;
                        ((AiPatchOperation)value).name = string.Empty;
                        ((AiPatchOperation)value).uiType = string.Empty;
                        ((AiPatchOperation)value).insertIndex = -1;
                        return true;
                    }
                    return false;
                case (AiPatchOperationKind)4:
                {
                    GUIType gUIType;
                    if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value).targetId))
                    {
                        return AiPatchValidator.TryParsePatchUiType(((AiPatchOperation)value).uiType, out gUIType);
                    }
                    return false;
                }
                case (AiPatchOperationKind)5:
                    if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value).targetId) && !string.IsNullOrWhiteSpace(((AiPatchOperation)value).name))
                    {
                        ((AiPatchOperation)value).id = string.Empty;
                        ((AiPatchOperation)value).parentId = string.Empty;
                        ((AiPatchOperation)value).newParentId = string.Empty;
                        ((AiPatchOperation)value).uiType = string.Empty;
                        ((AiPatchOperation)value).insertIndex = -1;
                        return true;
                    }
                    return false;
                case (AiPatchOperationKind)6:
                    if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value).targetId) && ((AiPatchOperation)value).targetId.StartsWith("gen:", StringComparison.OrdinalIgnoreCase))
                    {
                        ((AiPatchOperation)value).id = string.Empty;
                        ((AiPatchOperation)value).parentId = string.Empty;
                        ((AiPatchOperation)value).newParentId = string.Empty;
                        ((AiPatchOperation)value).name = string.Empty;
                        ((AiPatchOperation)value).uiType = string.Empty;
                        ((AiPatchOperation)value).insertIndex = -1;
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        private static bool IsDefinedGuiTypeName(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return false;
            }
            if (Enum.TryParse<GUIType>((string)value, ignoreCase: true, out var result))
            {
                return Enum.IsDefined(typeof(GUIType), result);
            }
            return false;
        }

        private static bool IsValidPredictedUiType(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return false;
            }
            if (string.Equals((string)value, "Null", StringComparison.Ordinal))
            {
                return true;
            }
            GUIType gUIType;
            return AiPatchValidator.TryParsePatchUiType(value, out gUIType);
        }

        private static bool IsValidVerdict(object value)
        {
            if (!string.Equals((string)value, "correct", StringComparison.Ordinal) && !string.Equals((string)value, "corrected", StringComparison.Ordinal))
            {
                return string.Equals((string)value, "removed", StringComparison.Ordinal);
            }
            return true;
        }

        private static bool IsAuxiliaryUiType(GUIType uiType)
        {
            return uiType > (GUIType)100;
        }

        private static bool RequiresTextLayer(GUIType uiType)
        {
            return UiTypeCompatibilityRules.RoleRequiresTextLayer(uiType);
        }

        private static float NormalizeConfidence(float value)
        {
            if (value <= 0f)
            {
                return 0.5f;
            }
            return Mathf.Clamp01(value);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchLocalNormalizer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
