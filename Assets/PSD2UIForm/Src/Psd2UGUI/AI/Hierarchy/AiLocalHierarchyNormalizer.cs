using System;
using System.Collections.Generic;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using LocalHierarchyNormalizationReportNamespace;

namespace AiLocalHierarchyNormalizerNamespace
{
    internal sealed class AiLocalHierarchyNormalizer
    {
        private sealed class NodeMovePlan
        {
            internal PsdLayerNode _node;

            internal PsdLayerNode _ownerNode;

            internal int _originalOrder;

            private static NodeMovePlan s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static NodeMovePlan GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        internal static AiLocalHierarchyNormalizer s_ObfuscationSentinel;

        internal static bool TryNormalizeHierarchy(object value, bool enabled, out LocalHierarchyNormalizationReport result)
        {
            result = new LocalHierarchyNormalizationReport();
            if (!((Object)value == (Object)null))
            {
                int num = -1;
                if (enabled)
                {
                    num = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("PSD2UIForm Normalize Structure");
                    Undo.RegisterCompleteObjectUndo((Object)(object)((Component)value).gameObject, "PSD2UIForm Normalize Structure");
                }
                try
                {
                    ((Psd2UIFormConverter)value).NormalizeGroupGenerationState();
                    PsdLayerNode[] componentsInChildren = ((Component)value).GetComponentsInChildren<PsdLayerNode>(true);
                    if (componentsInChildren != null && componentsInChildren.Length != 0)
                    {
                        MoveDependencyNodesToOwners(componentsInChildren, result, enabled);
                        FlattenEmptyNullGroups(value, result, enabled);
                        ((Psd2UIFormConverter)value).RefreshAllHelperComponents();
                        EditorUtility.SetDirty((Object)(object)((Component)value).gameObject);
                        return true;
                    }
                    return true;
                }
                finally
                {
                    if (enabled && num >= 0)
                    {
                        Undo.CollapseUndoOperations(num);
                    }
                }
            }
            result.Warnings.Add("Converter 为空，无法执行本地归一化。");
            return false;
        }

        private static void MoveDependencyNodesToOwners(object value, object value2, bool enabled)
        {
            Dictionary<Transform, int> dictionary = BuildTransformOrderLookup(value);
            List<NodeMovePlan> list = new List<NodeMovePlan>(16);
            for (int i = 0; i < ((Array)value).Length; i++)
            {
                PsdLayerNode psdLayerNode = (PsdLayerNode)((object[])value)[i];
                if ((Object)(object)psdLayerNode == (Object)null || !UGUIParser.CanOwnDependencyNodes(psdLayerNode.UIType))
                {
                    continue;
                }
                PsdLayerNode psdLayerNode2 = psdLayerNode.FindOwnerNode();
                if ((Object)(object)psdLayerNode2 == (Object)null)
                {
                    if (psdLayerNode.UIType == GUIType.Background)
                    {
                        if (enabled)
                        {
                            Undo.RecordObject((Object)(object)psdLayerNode, "Normalize Background Role");
                        }
                        psdLayerNode.SetUIType(GUIType.Image, false);
                        EditorUtility.SetDirty((Object)(object)psdLayerNode);
                        ((LocalHierarchyNormalizationReport)value2).BackgroundDowngradeCount++;
                        ((LocalHierarchyNormalizationReport)value2).Actions.Add("background_to_image:" + ((Object)psdLayerNode).name);
                    }
                    else
                    {
                        ((LocalHierarchyNormalizationReport)value2).Warnings.Add($"节点 '{((Object)psdLayerNode).name}' ({psdLayerNode.UIType}) 未找到兼容 owner，跳过结构修正。");
                    }
                }
                else if (!((Object)(object)((Component)psdLayerNode).transform.parent == (Object)(object)((Component)psdLayerNode2).transform))
                {
                    list.Add(new NodeMovePlan
                    {
                        _node = psdLayerNode,
                        _ownerNode = psdLayerNode2,
                        _originalOrder = GetOriginalTransformOrder(dictionary, ((Component)psdLayerNode).transform)
                    });
                }
            }
            for (int j = 0; j < list.Count; j++)
            {
                NodeMovePlan value3 = list[j];
                if ((Object)(object)value3?._node == (Object)null || (Object)(object)value3._ownerNode == (Object)null)
                {
                    continue;
                }
                if (((Component)value3._ownerNode).transform.IsChildOf(((Component)value3._node).transform))
                {
                    ((LocalHierarchyNormalizationReport)value2).Warnings.Add("节点 '" + ((Object)value3._node).name + "' 不能移动到自己的子层级 '" + ((Object)value3._ownerNode).name + "' 下，已跳过。");
                    continue;
                }
                if (!enabled)
                {
                    ((Component)value3._node).transform.SetParent(((Component)value3._ownerNode).transform, true);
                }
                else
                {
                    Undo.SetTransformParent(((Component)value3._node).transform, ((Component)value3._ownerNode).transform, "Normalize PSD Structure");
                }
                SetSiblingIndexByOriginalOrder(((Component)value3._node).transform, ((Component)value3._ownerNode).transform, value3._originalOrder, dictionary);
                dictionary[((Component)value3._node).transform] = value3._originalOrder;
                EditorUtility.SetDirty((Object)(object)value3._node);
                ((LocalHierarchyNormalizationReport)value2).MovedNodeCount++;
                ((LocalHierarchyNormalizationReport)value2).Actions.Add("move:" + ((Object)value3._node).name + "->" + ((Object)value3._ownerNode).name);
            }
        }

        private static Dictionary<Transform, int> BuildTransformOrderLookup(object value)
        {
            Dictionary<Transform, int> dictionary = new Dictionary<Transform, int>((value != null) ? ((Array)value).Length : 0);
            if (value != null && ((Array)value).Length != 0)
            {
                Transform val = (((Object)((object[])value)[0] != (Object)null) ? ((Component)((object[])value)[0]).transform.root : null);
                if ((Object)(object)val == (Object)null)
                {
                    return dictionary;
                }
                int num = 0;
                PopulateTransformOrderLookup(val, dictionary, ref num);
                return dictionary;
            }
            return dictionary;
        }

        private static void PopulateTransformOrderLookup(object value, Dictionary<Transform, int> lookup, ref int value2)
        {
            if (!((Object)value == (Object)null) && lookup != null)
            {
                lookup[(Transform)value] = value2++;
                for (int i = 0; i < ((Transform)value).childCount; i++)
                {
                    PopulateTransformOrderLookup(((Transform)value).GetChild(i), lookup, ref value2);
                }
            }
        }

        private static void SetSiblingIndexByOriginalOrder(object value, object value2, int value3, Dictionary<Transform, int> lookup)
        {
            if ((Object)value == (Object)null || (Object)value2 == (Object)null)
            {
                return;
            }
            int num = ((Transform)value2).childCount - 1;
            for (int i = 0; i < ((Transform)value2).childCount; i++)
            {
                Transform child = ((Transform)value2).GetChild(i);
                if (!((Object)(object)child == (Object)null) && !((Object)(object)child == (Object)value) && value3 < GetOriginalTransformOrder(lookup, child))
                {
                    num = i;
                    break;
                }
            }
            ((Transform)value).SetSiblingIndex(Mathf.Clamp(num, 0, Mathf.Max(0, ((Transform)value2).childCount - 1)));
        }

        private static int GetOriginalTransformOrder(Dictionary<Transform, int> lookup, object value2)
        {
            if ((Object)value2 == (Object)null)
            {
                return int.MaxValue;
            }
            if (lookup != null && lookup.TryGetValue((Transform)value2, out var value))
            {
                return value;
            }
            return int.MaxValue;
        }

        private static void FlattenEmptyNullGroups(object value, object value2, bool enabled)
        {
            bool flag;
            do
            {
                flag = false;
                PsdLayerNode[] componentsInChildren = ((Component)value).GetComponentsInChildren<PsdLayerNode>(true);
                for (int num = componentsInChildren.Length - 1; num >= 0; num--)
                {
                    PsdLayerNode psdLayerNode = componentsInChildren[num];
                    if (CanFlattenNullGroup(value, psdLayerNode))
                    {
                        Transform parent = ((Component)psdLayerNode).transform.parent;
                        if (!((Object)(object)parent == (Object)null))
                        {
                            int siblingIndex = ((Component)psdLayerNode).transform.GetSiblingIndex();
                            while (((Component)psdLayerNode).transform.childCount > 0)
                            {
                                Transform child = ((Component)psdLayerNode).transform.GetChild(0);
                                if (enabled)
                                {
                                    Undo.SetTransformParent(child, parent, "Normalize PSD Structure");
                                }
                                else
                                {
                                    child.SetParent(parent, true);
                                }
                                child.SetSiblingIndex(siblingIndex++);
                            }
                            if (!enabled)
                            {
                                Object.DestroyImmediate((Object)(object)((Component)psdLayerNode).gameObject);
                            }
                            else
                            {
                                Undo.DestroyObjectImmediate((Object)(object)((Component)psdLayerNode).gameObject);
                            }
                            ((LocalHierarchyNormalizationReport)value2).FlattenedNullGroupCount++;
                            ((LocalHierarchyNormalizationReport)value2).Actions.Add("flatten_null");
                            flag = true;
                            break;
                        }
                    }
                }
            }
            while (flag);
        }

        private static bool CanFlattenNullGroup(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null) && !((Object)(object)((Component)value2).transform == (Object)(object)((Component)value).transform))
            {
                if (((PsdLayerNode)value2).UIType == GUIType.Null && ((PsdLayerNode)value2).LayerType == PsdLayerType.LayerGroup)
                {
                    if (!((PsdLayerNode)value2).HasAssetReference() && !((PsdLayerNode)value2).HasPrefabReference() && !((PsdLayerNode)value2).ShouldCollapseChildrenForGeneration())
                    {
                        if (((Component)value2).transform.childCount > 1)
                        {
                            return false;
                        }
                        for (int i = 0; i < ((Component)value2).transform.childCount; i++)
                        {
                            PsdLayerNode component = ((Component)((Component)value2).transform.GetChild(i)).GetComponent<PsdLayerNode>();
                            if ((Object)(object)component != (Object)null && component.UIType == GUIType.Background)
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiLocalHierarchyNormalizer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
