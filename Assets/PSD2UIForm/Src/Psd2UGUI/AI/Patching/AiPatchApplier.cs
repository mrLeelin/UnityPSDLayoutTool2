using System;
using System.Collections.Generic;
using System.Linq;
using LayerNodeIdUtilityNamespace;
using AiPatchOperationParserNamespace;
using AiPatchValidatorNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using AiPatchLocalNormalizerNamespace;
using AiHierarchyStructureValidatorNamespace;
using AiPatchOperationKindNamespace;

namespace AiPatchApplierNamespace
{
    internal sealed class AiPatchApplier
    {
        private sealed class AiPatchApplyContext
        {
            public readonly Dictionary<string, GameObject> _gameObjectsById = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

            public readonly Dictionary<string, PsdLayerNode> _layerNodesById = new Dictionary<string, PsdLayerNode>(StringComparer.OrdinalIgnoreCase);

            public readonly List<PsdLayerNode> _generatedLayerNodes = new List<PsdLayerNode>();

            public readonly Dictionary<Transform, int> _requestedSiblingIndices = new Dictionary<Transform, int>();

            internal static AiPatchApplyContext s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static AiPatchApplyContext GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static AiPatchApplier s_ObfuscationSentinel;

        internal bool ApplyPatch(Psd2UIFormConverter value5, AiPatchDocument aiPatchDocument, out string result)
        {
            result = null;
            if ((Object)(object)value5 == (Object)null)
            {
                result = "\ufffd";
                return false;
            }
            if (aiPatchDocument != null)
            {
                AiPatchLocalNormalizer.NormalizePatchDocument(aiPatchDocument);
                if (aiPatchDocument.operations == null)
                {
                    aiPatchDocument.operations = new List<AiPatchOperation>();
                }
                if (!new AiPatchValidator().ValidatePatchDocument(aiPatchDocument, out result))
                {
                    Debug.LogWarning((object)("[PSD2UIForm.AI] Patch 基础校验未完全通过，将继续应用可执行项并跳过无效项。error=" + result));
                    result = null;
                }
                AiPatchApplyContext value = BuildApplyContext(value5);
                List<string> list = new List<string>(16);
                HashSet<int> hashSet = new HashSet<int>();
                Undo.IncrementCurrentGroup();
                int currentGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply AI Patch");
                Undo.RegisterFullObjectHierarchyUndo((Object)(object)((Component)value5).gameObject, "Apply AI Patch");
                for (int i = 0; i < aiPatchDocument.operations.Count; i++)
                {
                    AiPatchOperation aiPatchOperation = aiPatchDocument.operations[i];
                    if (!TryGetOperationType(aiPatchOperation, i, list, hashSet, out var value2))
                    {
                        continue;
                    }
                    try
                    {
                        switch (value2)
                        {
                        case (AiPatchOperationKind)1:
                            ApplyCreateGroup(value5, value, aiPatchOperation, list);
                            break;
                        case (AiPatchOperationKind)2:
                            ApplyMoveNode(value, aiPatchOperation, list);
                            break;
                        case (AiPatchOperationKind)3:
                            ApplyFlattenGroup(value, aiPatchOperation, list);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        list.Add($"Skipped operation[{i}] '{aiPatchOperation.op}' on '{GetOperationTargetId(aiPatchOperation)}': {ex.Message}");
                    }
                }
                for (int j = 0; j < aiPatchDocument.operations.Count; j++)
                {
                    AiPatchOperation aiPatchOperation2 = aiPatchDocument.operations[j];
                    if (!TryGetOperationType(aiPatchOperation2, j, list, hashSet, out var value3))
                    {
                        continue;
                    }
                    try
                    {
                        switch (value3)
                        {
                        case (AiPatchOperationKind)5:
                            ApplyRenameNode(value, aiPatchOperation2, list);
                            break;
                        case (AiPatchOperationKind)4:
                            ApplySetUiType(value, aiPatchOperation2, list);
                            break;
                        }
                    }
                    catch (Exception ex2)
                    {
                        list.Add($"Skipped operation[{j}] '{aiPatchOperation2.op}' on '{GetOperationTargetId(aiPatchOperation2)}': {ex2.Message}");
                    }
                }
                for (int k = 0; k < aiPatchDocument.operations.Count; k++)
                {
                    AiPatchOperation aiPatchOperation3 = aiPatchDocument.operations[k];
                    if (TryGetOperationType(aiPatchOperation3, k, list, hashSet, out var value4) && value4 == (AiPatchOperationKind)6)
                    {
                        try
                        {
                            ApplyDeleteGeneratedGroup(value, aiPatchOperation3, list);
                        }
                        catch (Exception ex3)
                        {
                            list.Add($"Skipped operation[{k}] '{aiPatchOperation3.op}' on '{GetOperationTargetId(aiPatchOperation3)}': {ex3.Message}");
                        }
                    }
                }
                RefreshGeneratedGroupBounds(value._generatedLayerNodes);
                new AiHierarchyStructureValidator().ValidateHierarchy(value5, list);
                EditorUtility.SetDirty((Object)(object)((Component)value5).gameObject);
                Undo.CollapseUndoOperations(currentGroup);
                if (list.Count > 0)
                {
                    Debug.LogWarning((object)("Apply AI Patch completed with warnings:\n- " + string.Join("\n- ", list)));
                }
                return true;
            }
            result = "Patch document is null.";
            return false;
        }

        private static bool TryGetOperationType(object value, int value2, List<string> texts, HashSet<int> values, out AiPatchOperationKind result)
        {
            result = (AiPatchOperationKind)0;
            if (value == null)
            {
                AddWarningOnce(value2, values, texts, $"Skipped operation[{value2}]: operation is null.");
                return false;
            }
            if (!AiPatchOperationParser.TryParse(((AiPatchOperation)value).op, out result))
            {
                AddWarningOnce(value2, values, texts, $"Skipped operation[{value2}]: unsupported patch operation '{((AiPatchOperation)value).op}'.");
                return false;
            }
            return true;
        }

        private static void AddWarningOnce(int value, HashSet<int> values, List<string> texts, object value2)
        {
            if (texts != null && values != null && values.Add(value))
            {
                texts.Add((string)value2);
            }
        }

        private static string GetOperationTargetId(object value)
        {
            if (value != null)
            {
                if (string.IsNullOrWhiteSpace(((AiPatchOperation)value).targetId))
                {
                    if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value).id))
                    {
                        return ((AiPatchOperation)value).id;
                    }
                    return string.Empty;
                }
                return ((AiPatchOperation)value).targetId;
            }
            return string.Empty;
        }

        private static AiPatchApplyContext BuildApplyContext(object value)
        {
            AiPatchApplyContext value2 = new AiPatchApplyContext();
            value2._gameObjectsById["root"] = ((Component)value).gameObject;
            PsdLayerNode[] componentsInChildren = ((Component)value).GetComponentsInChildren<PsdLayerNode>(true);
            foreach (PsdLayerNode psdLayerNode in componentsInChildren)
            {
                string text = LayerNodeIdUtility.GetStableNodeId(value, psdLayerNode);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    value2._gameObjectsById[text] = ((Component)psdLayerNode).gameObject;
                    value2._layerNodesById[text] = psdLayerNode;
                }
            }
            return value2;
        }

        private static void ApplyCreateGroup(object value2, object value3, object value4, List<string> texts)
        {
            if (!string.IsNullOrWhiteSpace(((AiPatchOperation)value4).id) && !((AiPatchApplyContext)value3)._gameObjectsById.ContainsKey(((AiPatchOperation)value4).id))
            {
                if (((AiPatchApplyContext)value3)._gameObjectsById.TryGetValue(((AiPatchOperation)value4).parentId, out var value) && !((Object)(object)value == (Object)null))
                {
                    GUIType gUIType = GUIType.Null;
                    AiPatchValidator.TryParsePatchUiType(((AiPatchOperation)value4).uiType, out gUIType);
                    string text = (string.IsNullOrWhiteSpace(((AiPatchOperation)value4).name) ? ((AiPatchOperation)value4).id : ((AiPatchOperation)value4).name);
                    if (TryReuseGeneratedGroup(value3, value.transform, ((AiPatchOperation)value4).id, text, gUIType, out var psdLayerNode))
                    {
                        if (((AiPatchOperation)value4).insertIndex >= 0)
                        {
                            ApplyRequestedSiblingIndex(value3, ((Component)psdLayerNode).transform, value.transform, ((AiPatchOperation)value4).insertIndex);
                            ((AiPatchApplyContext)value3)._requestedSiblingIndices[((Component)psdLayerNode).transform] = ((AiPatchOperation)value4).insertIndex;
                        }
                        ((AiPatchApplyContext)value3)._gameObjectsById[((AiPatchOperation)value4).id] = ((Component)psdLayerNode).gameObject;
                        ((AiPatchApplyContext)value3)._layerNodesById[((AiPatchOperation)value4).id] = psdLayerNode;
                        return;
                    }
                    GameObject val = new GameObject(text, new Type[1] { typeof(RectTransform) });
                    val.transform.SetParent(value.transform, false);
                    if (((AiPatchOperation)value4).insertIndex >= 0)
                    {
                        ApplyRequestedSiblingIndex(value3, val.transform, value.transform, ((AiPatchOperation)value4).insertIndex);
                    }
                    PsdLayerNode psdLayerNode2 = val.AddComponent<PsdLayerNode>();
                    psdLayerNode2.InitializeGeneratedGroup(text, Rect.zero, ((AiPatchOperation)value4).id);
                    psdLayerNode2.SetUIType(gUIType);
                    ((AiPatchApplyContext)value3)._gameObjectsById[((AiPatchOperation)value4).id] = val;
                    ((AiPatchApplyContext)value3)._layerNodesById[((AiPatchOperation)value4).id] = psdLayerNode2;
                    ((AiPatchApplyContext)value3)._generatedLayerNodes.Add(psdLayerNode2);
                    ((AiPatchApplyContext)value3)._requestedSiblingIndices[val.transform] = ((AiPatchOperation)value4).insertIndex;
                }
                else
                {
                    texts?.Add("Skipped create_group '" + ((AiPatchOperation)value4).id + "': parent '" + ((AiPatchOperation)value4).parentId + "' does not exist.");
                }
            }
            else
            {
                texts?.Add("Skipped create_group '" + ((AiPatchOperation)value4).id + "': generated id is empty or already exists.");
            }
        }

        private static bool TryReuseGeneratedGroup(object value, object value2, object value3, object value4, GUIType uiType, out PsdLayerNode result)
        {
            result = null;
            if ((Object)value2 == (Object)null || string.IsNullOrWhiteSpace((string)value3))
            {
                return false;
            }
            for (int i = 0; i < ((Transform)value2).childCount; i++)
            {
                Transform child = ((Transform)value2).GetChild(i);
                PsdLayerNode psdLayerNode = ((!((Object)(object)child != (Object)null)) ? null : ((Component)child).GetComponent<PsdLayerNode>());
                if ((Object)(object)psdLayerNode == (Object)null || !LayerNodeIdUtility.IsGeneratedLayerGroup(psdLayerNode))
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(psdLayerNode.GetGeneratedNodeId()))
                {
                    if (!string.Equals(psdLayerNode.GetGeneratedNodeId(), (string)value3, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                else if (!string.Equals(((Object)child).name, (string)value4, StringComparison.Ordinal) || psdLayerNode.UIType != uiType)
                {
                    continue;
                }
                psdLayerNode.SetGeneratedNodeId((string)value3);
                ((AiPatchApplyContext)value)._gameObjectsById[(string)value3] = ((Component)child).gameObject;
                ((AiPatchApplyContext)value)._layerNodesById[(string)value3] = psdLayerNode;
                result = psdLayerNode;
                return true;
            }
            return false;
        }

        private static void ApplyMoveNode(object value3, object value4, List<string> texts)
        {
            if (((AiPatchApplyContext)value3)._gameObjectsById.TryGetValue(((AiPatchOperation)value4).targetId, out var value) && !((Object)(object)value == (Object)null))
            {
                if (((AiPatchApplyContext)value3)._gameObjectsById.TryGetValue(((AiPatchOperation)value4).newParentId, out var value2) && !((Object)(object)value2 == (Object)null))
                {
                    if (value2.transform.IsChildOf(value.transform))
                    {
                        texts?.Add("Skipped move_node '" + ((AiPatchOperation)value4).targetId + "': target cannot be parent of its own ancestor chain.");
                        return;
                    }
                    value.transform.SetParent(value2.transform, true);
                    if (((AiPatchOperation)value4).insertIndex >= 0)
                    {
                        ApplyRequestedSiblingIndex(value3, value.transform, value2.transform, ((AiPatchOperation)value4).insertIndex);
                        ((AiPatchApplyContext)value3)._requestedSiblingIndices[value.transform] = ((AiPatchOperation)value4).insertIndex;
                    }
                }
                else
                {
                    texts?.Add("Skipped move_node '" + ((AiPatchOperation)value4).targetId + "': new parent '" + ((AiPatchOperation)value4).newParentId + "' does not exist.");
                }
            }
            else
            {
                texts?.Add("Skipped move_node '" + ((AiPatchOperation)value4).targetId + "': target does not exist.");
            }
        }

        private static void ApplyRequestedSiblingIndex(object value, object value2, object value3, int value4)
        {
            if ((Object)value2 == (Object)null || (Object)value3 == (Object)null)
            {
                return;
            }
            int num = ((Transform)value3).childCount - 1;
            for (int i = 0; i < ((Transform)value3).childCount; i++)
            {
                Transform child = ((Transform)value3).GetChild(i);
                if (!((Object)(object)child == (Object)null) && !((Object)(object)child == (Object)value2))
                {
                    int num2 = GetRequestedSiblingIndex(value, child, i);
                    if (value4 < num2)
                    {
                        num = i;
                        break;
                    }
                }
            }
            ((Transform)value2).SetSiblingIndex(Mathf.Clamp(num, 0, Mathf.Max(0, ((Transform)value3).childCount - 1)));
        }

        private static int GetRequestedSiblingIndex(object value2, object value3, int value4)
        {
            if (!((Object)value3 == (Object)null))
            {
                if (value2 != null && ((AiPatchApplyContext)value2)._requestedSiblingIndices.TryGetValue((Transform)value3, out var value))
                {
                    return value;
                }
                return value4;
            }
            return int.MaxValue;
        }

        private static void ApplyFlattenGroup(object value2, object value3, List<string> texts)
        {
            if (((AiPatchApplyContext)value2)._gameObjectsById.TryGetValue(((AiPatchOperation)value3).targetId, out var value) && !((Object)(object)value == (Object)null))
            {
                PsdLayerNode component = value.GetComponent<PsdLayerNode>();
                if (!((Object)(object)component == (Object)null) && (component.UIType == GUIType.Panel || component.UIType == GUIType.Null))
                {
                    if (HasDirectChildWithUiType(value.transform, GUIType.Background))
                    {
                        texts?.Add("Skipped flatten_group '" + ((AiPatchOperation)value3).targetId + "': target has a direct Background child.");
                        return;
                    }
                    Transform parent = value.transform.parent;
                    if ((Object)(object)parent == (Object)null)
                    {
                        texts?.Add("Skipped flatten_group '" + ((AiPatchOperation)value3).targetId + "': target has no parent.");
                        return;
                    }
                    int siblingIndex = value.transform.GetSiblingIndex();
                    int childCount = value.transform.childCount;
                    Transform[] array = (Transform[])(object)new Transform[childCount];
                    for (int i = 0; i < childCount; i++)
                    {
                        array[i] = value.transform.GetChild(i);
                    }
                    for (int j = 0; j < array.Length; j++)
                    {
                        Transform val = array[j];
                        if (!((Object)(object)val == (Object)null))
                        {
                            val.SetParent(parent, true);
                            val.SetSiblingIndex(Mathf.Min(siblingIndex + j, parent.childCount - 1));
                        }
                    }
                    ((AiPatchApplyContext)value2)._gameObjectsById.Remove(((AiPatchOperation)value3).targetId);
                    ((AiPatchApplyContext)value2)._layerNodesById.Remove(((AiPatchOperation)value3).targetId);
                    if ((Object)(object)component != (Object)null)
                    {
                        ((AiPatchApplyContext)value2)._generatedLayerNodes.Remove(component);
                    }
                    Object.DestroyImmediate((Object)(object)value);
                }
                else
                {
                    texts?.Add("Skipped flatten_group '" + ((AiPatchOperation)value3).targetId + "': target is not a Null/Panel wrapper.");
                }
            }
            else
            {
                texts?.Add("Skipped flatten_group '" + ((AiPatchOperation)value3).targetId + "': target does not exist.");
            }
        }

        private static void ApplySetUiType(object value2, object value3, List<string> texts)
        {
            PsdLayerNode value;
            if (string.IsNullOrWhiteSpace(((AiPatchOperation)value3).targetId))
            {
                texts?.Add("Skipped set_ui_type: targetId is empty.");
            }
            else if (((AiPatchApplyContext)value2)._layerNodesById.TryGetValue(((AiPatchOperation)value3).targetId, out value) && !((Object)(object)value == (Object)null))
            {
                if (!AiPatchValidator.TryParsePatchUiType(((AiPatchOperation)value3).uiType, out var gUIType))
                {
                    texts?.Add("Skipped set_ui_type '" + ((AiPatchOperation)value3).targetId + "': invalid uiType '" + ((AiPatchOperation)value3).uiType + "'.");
                }
                else if (!IsUiTypeCompatibleWithLayer(value, gUIType))
                {
                    texts?.Add($"Skipped set_ui_type '{((AiPatchOperation)value3).targetId}' -> '{gUIType}': incompatible with current layer type.");
                }
                else
                {
                    value.SetUIType(gUIType);
                }
            }
            else
            {
                texts?.Add("Skipped set_ui_type '" + ((AiPatchOperation)value3).targetId + "': target does not exist.");
            }
        }

        private static void ApplyRenameNode(object value2, object value3, List<string> texts)
        {
            if (((AiPatchApplyContext)value2)._gameObjectsById.TryGetValue(((AiPatchOperation)value3).targetId, out var value) && !((Object)(object)value == (Object)null))
            {
                if (string.IsNullOrWhiteSpace(((AiPatchOperation)value3).name))
                {
                    texts?.Add("Skipped rename_node '" + ((AiPatchOperation)value3).targetId + "': name is empty.");
                }
                else
                {
                    ((Object)value).name = ((AiPatchOperation)value3).name.Trim();
                }
            }
            else
            {
                texts?.Add("Skipped rename_node '" + ((AiPatchOperation)value3).targetId + "': target does not exist.");
            }
        }

        private static void ApplyDeleteGeneratedGroup(object value2, object value3, List<string> texts)
        {
            if (((AiPatchApplyContext)value2)._gameObjectsById.TryGetValue(((AiPatchOperation)value3).targetId, out var value) && !((Object)(object)value == (Object)null))
            {
                PsdLayerNode component = value.GetComponent<PsdLayerNode>();
                if (!LayerNodeIdUtility.IsGeneratedLayerGroup(component))
                {
                    texts?.Add("Skipped delete_generated_group '" + ((AiPatchOperation)value3).targetId + "': target is not a generated group.");
                    return;
                }
                if (value.transform.childCount > 0)
                {
                    texts?.Add("Skipped delete_generated_group '" + ((AiPatchOperation)value3).targetId + "': target still has children.");
                    return;
                }
                ((AiPatchApplyContext)value2)._gameObjectsById.Remove(((AiPatchOperation)value3).targetId);
                ((AiPatchApplyContext)value2)._layerNodesById.Remove(((AiPatchOperation)value3).targetId);
                if ((Object)(object)component != (Object)null)
                {
                    ((AiPatchApplyContext)value2)._generatedLayerNodes.Remove(component);
                }
                Object.DestroyImmediate((Object)(object)value);
            }
            else
            {
                texts?.Add("Skipped delete_generated_group '" + ((AiPatchOperation)value3).targetId + "': target does not exist.");
            }
        }

        private static bool IsUiTypeCompatibleWithLayer(object value, GUIType uiType)
        {
            if ((Object)value == (Object)null)
            {
                return false;
            }
            if (uiType != GUIType.Null)
            {
                if (!IsTextChildUiType(uiType))
                {
                    if (uiType == GUIType.Text)
                    {
                        return ((PsdLayerNode)value).LayerType == PsdLayerType.TextLayer;
                    }
                    if (IsGraphicChildUiType(uiType))
                    {
                        if (((PsdLayerNode)value).LayerType != PsdLayerType.TextLayer)
                        {
                            return ((PsdLayerNode)value).LayerType != PsdLayerType.Unknown;
                        }
                        return false;
                    }
                    return true;
                }
                return ((PsdLayerNode)value).LayerType == PsdLayerType.TextLayer;
            }
            return true;
        }

        private static bool IsTextChildUiType(GUIType uiType)
        {
            switch (uiType)
            {
            default:
                return false;
            case GUIType.Button_Text:
            case GUIType.Dropdown_Label:
            case GUIType.InputField_Placeholder:
            case GUIType.InputField_Text:
            case GUIType.Toggle_Label:
                return true;
            }
        }

        private static bool IsGraphicChildUiType(GUIType uiType)
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

        private static bool HasTextLayerDescendant(object value)
        {
            if ((Object)value == (Object)null)
            {
                return false;
            }
            int num = 0;
            while (true)
            {
                if (num < ((Transform)value).childCount)
                {
                    Transform child = ((Transform)value).GetChild(num);
                    PsdLayerNode psdLayerNode = (((Object)(object)child != (Object)null) ? ((Component)child).GetComponent<PsdLayerNode>() : null);
                    if (!((Object)(object)psdLayerNode != (Object)null) || psdLayerNode.LayerType != PsdLayerType.TextLayer)
                    {
                        if (HasTextLayerDescendant(child))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return true;
                }
                return false;
            }
            return true;
        }

        private static bool HasDirectChildWithUiType(object value, GUIType uiType)
        {
            if (!((Object)value == (Object)null))
            {
                int num = 0;
                while (true)
                {
                    if (num < ((Transform)value).childCount)
                    {
                        Transform child = ((Transform)value).GetChild(num);
                        PsdLayerNode psdLayerNode = (((object)child != null) ? ((Component)child).GetComponent<PsdLayerNode>() : null);
                        if ((Object)(object)psdLayerNode != (Object)null && psdLayerNode.UIType == uiType)
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

        private static void RefreshGeneratedGroupBounds(List<PsdLayerNode> layerNodes)
        {
            if (layerNodes == null || layerNodes.Count < 1)
            {
                return;
            }
            layerNodes.Sort((PsdLayerNode a, PsdLayerNode b) => GetTransformDepth((b == null) ? null : ((Component)b).transform).CompareTo(GetTransformDepth((a != null) ? ((Component)a).transform : null)));
            for (int num = 0; num < layerNodes.Count; num++)
            {
                PsdLayerNode psdLayerNode = layerNodes[num];
                if (!((Object)(object)psdLayerNode == (Object)null))
                {
                    if (!TryCalculateChildBounds(((Component)psdLayerNode).transform, out var zero))
                    {
                        zero = Rect.zero;
                    }
                    psdLayerNode.SetLayerRect(zero);
                }
            }
        }

        private static int GetTransformDepth(object value)
        {
            int num = 0;
            Transform val = (Transform)value;
            while ((Object)(object)val != (Object)null)
            {
                num++;
                val = val.parent;
            }
            return num;
        }

        private static bool TryCalculateChildBounds(object value, out Rect result)
        {
            Transform transform = (Transform)value;
            result = Rect.zero;
            PsdLayerNode[] array = (from node in ((Component)transform).GetComponentsInChildren<PsdLayerNode>(true)
                where (Object)(object)node != (Object)null && (Object)(object)((Component)node).transform.parent == (Object)(object)transform
                select node).ToArray();
            if (array.Length < 1)
            {
                return false;
            }
            bool flag = false;
            float num = 0f;
            float num2 = 0f;
            float num3 = 0f;
            float num4 = 0f;
            for (int num5 = 0; num5 < array.Length; num5++)
            {
                Rect val = array[num5].GetLayerRect();
                if (!flag)
                {
                    num = val.xMin;
                    num2 = val.yMin;
                    num3 = val.xMax;
                    num4 = val.yMax;
                    flag = true;
                }
                else
                {
                    num = Mathf.Min(num, val.xMin);
                    num2 = Mathf.Min(num2, val.yMin);
                    num3 = Mathf.Max(num3, val.xMax);
                    num4 = Mathf.Max(num4, val.yMax);
                }
            }
            if (!flag)
            {
                return false;
            }
            result = Rect.MinMaxRect(num, num2, num3, num4);
            return true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchApplier GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
