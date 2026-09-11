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
            if (!Psd2UIFormTargetCompat.IsNull(value))
            {
                int num = -1;
                if (enabled)
                {
                    num = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("PSD2UIForm Normalize Structure");
                    Undo.RegisterCompleteObjectUndo((Object)(object)Psd2UIFormTargetCompat.GameObjectOf(value), "PSD2UIForm Normalize Structure");
                }
                try
                {
                    ((Psd2UIFormConverterEditor)value).NormalizeGroupGenerationState();
                    PsdLayerNode[] componentsInChildren = Psd2UIFormTargetCompat.GameObjectOf(value).GetComponentsInChildren<PsdLayerNode>(true);
                    if (componentsInChildren != null && componentsInChildren.Length != 0)
                    {
                        MoveDependencyNodesToOwners(componentsInChildren, result, enabled);
                        FlattenEmptyNullGroups(value, result, enabled);
                        ((Psd2UIFormConverterEditor)value).RefreshAllHelperComponents();
                        EditorUtility.SetDirty((Object)(object)Psd2UIFormTargetCompat.GameObjectOf(value));
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
                if (Psd2UIFormTargetCompat.IsNull(psdLayerNode) || !UGUIParser.CanOwnDependencyNodes(psdLayerNode.UIType))
                {
                    continue;
                }
                PsdLayerNode psdLayerNode2 = psdLayerNode.FindOwnerNode();
                if (Psd2UIFormTargetCompat.IsNull(psdLayerNode2))
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
                else if (!((Object)(object)Psd2UIFormTargetCompat.TransformOf(psdLayerNode).parent == (Object)(object)Psd2UIFormTargetCompat.TransformOf(psdLayerNode2)))
                {
                    list.Add(new NodeMovePlan
                    {
                        _node = psdLayerNode,
                        _ownerNode = psdLayerNode2,
                        _originalOrder = GetOriginalTransformOrder(dictionary, Psd2UIFormTargetCompat.TransformOf(psdLayerNode))
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
                if (Psd2UIFormTargetCompat.IsNull(val))
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
            if (!Psd2UIFormTargetCompat.IsNull(value) && lookup != null)
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
            if (Psd2UIFormTargetCompat.IsNull(value) || Psd2UIFormTargetCompat.IsNull(value2))
            {
                return;
            }
            int num = ((Transform)value2).childCount - 1;
            for (int i = 0; i < ((Transform)value2).childCount; i++)
            {
                Transform child = ((Transform)value2).GetChild(i);
                if (!Psd2UIFormTargetCompat.IsNull(child) && !((Object)(object)child == (Object)value) && value3 < GetOriginalTransformOrder(lookup, child))
                {
                    num = i;
                    break;
                }
            }
            ((Transform)value).SetSiblingIndex(Mathf.Clamp(num, 0, Mathf.Max(0, ((Transform)value2).childCount - 1)));
        }

        private static int GetOriginalTransformOrder(Dictionary<Transform, int> lookup, object value2)
        {
            if (Psd2UIFormTargetCompat.IsNull(value2))
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
                PsdLayerNode[] componentsInChildren = Psd2UIFormTargetCompat.GameObjectOf(value).GetComponentsInChildren<PsdLayerNode>(true);
                for (int num = componentsInChildren.Length - 1; num >= 0; num--)
                {
                    PsdLayerNode psdLayerNode = componentsInChildren[num];
                    if (CanFlattenNullGroup(value, psdLayerNode))
                    {
                        Transform parent = Psd2UIFormTargetCompat.TransformOf(psdLayerNode).parent;
                        if (!Psd2UIFormTargetCompat.IsNull(parent))
                        {
                            int siblingIndex = Psd2UIFormTargetCompat.TransformOf(psdLayerNode).GetSiblingIndex();
                            while (Psd2UIFormTargetCompat.TransformOf(psdLayerNode).childCount > 0)
                            {
                                Transform child = Psd2UIFormTargetCompat.TransformOf(psdLayerNode).GetChild(0);
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
                                Object.DestroyImmediate((Object)(object)Psd2UIFormTargetCompat.GameObjectOf(psdLayerNode));
                            }
                            else
                            {
                                Undo.DestroyObjectImmediate((Object)(object)Psd2UIFormTargetCompat.GameObjectOf(psdLayerNode));
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
            if (!Psd2UIFormTargetCompat.IsNull(value) && !Psd2UIFormTargetCompat.IsNull(value2) && !((Object)(object)Psd2UIFormTargetCompat.TransformOf(value2) == (Object)(object)Psd2UIFormTargetCompat.TransformOf(value)))
            {
                if (((PsdLayerNode)value2).UIType == GUIType.Null && ((PsdLayerNode)value2).LayerType == PsdLayerType.LayerGroup)
                {
                    if (!((PsdLayerNode)value2).HasAssetReference() && !((PsdLayerNode)value2).HasPrefabReference() && !((PsdLayerNode)value2).ShouldCollapseChildrenForGeneration())
                    {
                        if (Psd2UIFormTargetCompat.TransformOf(value2).childCount > 1)
                        {
                            return false;
                        }
                        for (int i = 0; i < Psd2UIFormTargetCompat.TransformOf(value2).childCount; i++)
                        {
                            PsdLayerNode component = ((Component)Psd2UIFormTargetCompat.TransformOf(value2).GetChild(i)).GetComponent<PsdLayerNode>();
                            if (!Psd2UIFormTargetCompat.IsNull(component) && component.UIType == GUIType.Background)
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
