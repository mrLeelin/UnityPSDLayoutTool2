using System.Collections.Generic;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

using Object = UnityEngine.Object;
using UiTypeCompatibilityRulesNamespace;

namespace AiHierarchyStructureValidatorNamespace
{
    internal sealed class AiHierarchyStructureValidator
    {
        private enum RoleBackingKind
        {

        }

        private static AiHierarchyStructureValidator s_ObfuscationSentinel;

        internal void ValidateHierarchy(Psd2UIFormConverterEditor value, List<string> ids)
        {
            if ((Object)(object)value == (Object)null)
            {
                ids?.Add("Skipped AI structure validation: converter is null.");
                return;
            }
            PsdLayerNode[] componentsInChildren = value.GetComponentsInChildren<PsdLayerNode>(true);
            if (componentsInChildren == null || componentsInChildren.Length < 1)
            {
                return;
            }
            foreach (PsdLayerNode psdLayerNode in componentsInChildren)
            {
                if (!((Object)(object)psdLayerNode == (Object)null))
                {
                    ValidateUnsupportedMainType(psdLayerNode, ids);
                    ValidateCompositeStructure(psdLayerNode, ids);
                }
            }
        }

        private static void ValidateUnsupportedMainType(object value, List<string> texts)
        {
            if (!((Object)value == (Object)null) && UiTypeCompatibilityRules.IsUiTypeAlias(((PsdLayerNode)value).UIType))
            {
                texts?.Add($"Node '{((Object)value).name}' still uses unsupported AI main type '{((PsdLayerNode)value).UIType}'. The patch was kept, but this node may not parse as ordinary uGUI.");
            }
        }

        private static void ValidateCompositeStructure(object value, List<string> texts)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            GUIType gUIType = UiTypeCompatibilityRules.NormalizeUiTypeAlias(((PsdLayerNode)value).UIType);
            UiTypeCompatibilityRules.UiTypeRule value2 = UiTypeCompatibilityRules.GetRule(gUIType);
            if (value2 != null)
            {
                if ((gUIType == GUIType.Panel || gUIType == GUIType.ToggleGroup) && IsLayerGroup(value))
                {
                    ValidateBackgroundStructure(value, texts);
                }
                if (gUIType != GUIType.Null && gUIType != GUIType.Image && gUIType != GUIType.Text && gUIType != GUIType.Mask && gUIType != GUIType.FillColor)
                {
                    ValidateNestedDuplicateMainControl(value, texts, gUIType, gUIType.ToString());
                }
                for (int i = 0; i < value2.AllowedRoleTypes.Length; i++)
                {
                    GUIType gUIType2 = value2.AllowedRoleTypes[i];
                    ValidateRequiredRoleNode(value, texts, gUIType2, gUIType2.ToString(), GetRoleBackingKind(gUIType2));
                }
                for (int j = 0; j < value2.AllowedDirectChildControlTypes.Length; j++)
                {
                    GUIType gUIType3 = value2.AllowedDirectChildControlTypes[j];
                    ValidateRequiredChildControl(value, texts, gUIType3, gUIType3.ToString());
                }
            }
        }

        private static void ValidateBackgroundStructure(object value, List<string> texts)
        {
            if (IsLayerGroup(value))
            {
                int num = CountDescendantsByUiType(value, GUIType.Background);
                int num2 = CountDirectChildrenByUiType(value, GUIType.Background);
                if (num2 > 1)
                {
                    texts?.Add($"Protocol violation: {((PsdLayerNode)value).UIType} '{((Object)value).name}' contains {num2} direct Background nodes. AI should keep one Background and leave other artwork as ordinary Image/Panel.");
                }
                if (num > num2)
                {
                    texts?.Add($"Protocol violation: {((PsdLayerNode)value).UIType} '{((Object)value).name}' has nested Background semantics. AI should move_node the Background under '{((Object)value).name}', tag the direct outer Panel as Background, or leave non-primary artwork as ordinary Image/Panel.");
                }
            }
        }

        private static void ValidateRequiredRoleNode(object value, List<string> texts, GUIType uiType, object value2, RoleBackingKind value3)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            int num = CountDescendantsByUiType(value, uiType);
            if (num > 1)
            {
                texts?.Add($"Protocol violation: {((PsdLayerNode)value).UIType} '{((Object)value).name}' contains {num} '{value2}' nodes. AI should keep exactly one primary role candidate and leave other content as ordinary Image/Text/Panel.");
            }
            PsdLayerNode psdLayerNode = FindDescendantByUiType(value, uiType);
            if ((Object)(object)psdLayerNode == (Object)null)
            {
                return;
            }
            if ((Object)(object)((Component)psdLayerNode).transform.parent != (Object)(object)((Component)value).transform)
            {
                texts?.Add($"Protocol violation: {((PsdLayerNode)value).UIType} '{((Object)value).name}' has '{value2}' at '{GetRelativeNodePath(value, psdLayerNode)}', but composite role nodes must be direct children. AI should move_node it under '{((Object)value).name}' or tag the direct outer Panel as '{value2}'.");
            }
            if (value3 == (RoleBackingKind)1)
            {
                if (!HasTextBacking(psdLayerNode))
                {
                    texts?.Add($"{((PsdLayerNode)value).UIType} '{((Object)value).name}' uses '{value2}' on node '{((Object)psdLayerNode).name}' without text backing. That node was kept, but it may be ignored or downgraded later.");
                }
                else if (psdLayerNode.LayerType != PsdLayerType.TextLayer)
                {
                    texts?.Add($"{((PsdLayerNode)value).UIType} '{((Object)value).name}' uses '{value2}' on node '{((Object)psdLayerNode).name}' through a wrapper Panel/Null. Prefer the actual TextLayer as the final direct role node and clear intermediate text wrappers.");
                }
            }
            else if (!HasImageBacking(psdLayerNode))
            {
                texts?.Add($"{((PsdLayerNode)value).UIType} '{((Object)value).name}' uses '{value2}' on node '{((Object)psdLayerNode).name}' without image backing. That node was kept, but it may be ignored or export unexpectedly.");
            }
        }

        private static void ValidateRequiredChildControl(object value, List<string> texts, GUIType uiType, object value2)
        {
            int num = CountDescendantsByUiType(value, uiType);
            if (num > 1)
            {
                texts?.Add($"Protocol violation: {((PsdLayerNode)value).UIType} '{((Object)value).name}' contains {num} '{value2}' nodes. AI should keep exactly one primary child candidate and leave other content as ordinary Image/Text/Panel.");
            }
            PsdLayerNode psdLayerNode = FindDescendantByUiType(value, uiType);
            if ((Object)(object)psdLayerNode != (Object)null && (Object)(object)((Component)psdLayerNode).transform.parent != (Object)(object)((Component)value).transform)
            {
                texts?.Add($"Protocol violation: {((PsdLayerNode)value).UIType} '{((Object)value).name}' has '{value2}' at '{GetRelativeNodePath(value, psdLayerNode)}', but composite child controls must be direct children. AI should move_node it under '{((Object)value).name}'.");
            }
        }

        private static void ValidateNestedDuplicateMainControl(object value, List<string> texts, GUIType uiType, object value2)
        {
            if (!((Object)value == (Object)null))
            {
                PsdLayerNode psdLayerNode = FindNestedNodeByUiType(value, uiType);
                if (!((Object)(object)psdLayerNode == (Object)null))
                {
                    texts?.Add("Protocol suspicion: " + (string)value2 + " '" + ((Object)value).name + "' still contains nested " + (string)value2 + " '" + ((Object)psdLayerNode).name + "' at '" + GetRelativeNodePath(value, psdLayerNode) + "'. Prefer the nearest complete main-control boundary and avoid tagging both parent and child as the same main control.");
                }
            }
        }

        private static int CountDescendantsByUiType(object value, GUIType uiType)
        {
            if (!((Object)value == (Object)null) && uiType != GUIType.Null)
            {
                int result = 0;
                CountDescendantsRecursive(((Component)value).transform, uiType, ref result);
                return result;
            }
            return 0;
        }

        private static PsdLayerNode FindDescendantByUiType(object value, GUIType uiType)
        {
            if (!((Object)value == (Object)null) && uiType != GUIType.Null)
            {
                return FindDescendantRecursive(((Component)value).transform, uiType);
            }
            return null;
        }

        private static PsdLayerNode FindNestedNodeByUiType(object value, GUIType uiType)
        {
            if (!((Object)value == (Object)null))
            {
                return FindNestedNodeRecursive(((Component)value).transform, uiType);
            }
            return null;
        }

        private static void CountDescendantsRecursive(object value, GUIType uiType, ref int value2)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            for (int i = 0; i < ((Transform)value).childCount; i++)
            {
                Transform child = ((Transform)value).GetChild(i);
                PsdLayerNode psdLayerNode = (((Object)(object)child != (Object)null) ? ((Component)child).GetComponent<PsdLayerNode>() : null);
                if (!((Object)(object)psdLayerNode == (Object)null))
                {
                    if (psdLayerNode.UIType == uiType)
                    {
                        value2++;
                    }
                    if (!IsControlBoundary(psdLayerNode))
                    {
                        CountDescendantsRecursive(child, uiType, ref value2);
                    }
                }
                else
                {
                    CountDescendantsRecursive(child, uiType, ref value2);
                }
            }
        }

        private static PsdLayerNode FindDescendantRecursive(object value, GUIType uiType)
        {
            if ((Object)value == (Object)null)
            {
                return null;
            }
            int num = 0;
            PsdLayerNode psdLayerNode3;
            while (true)
            {
                if (num < ((Transform)value).childCount)
                {
                    Transform child = ((Transform)value).GetChild(num);
                    PsdLayerNode psdLayerNode = ((!((Object)(object)child != (Object)null)) ? null : ((Component)child).GetComponent<PsdLayerNode>());
                    if ((Object)(object)psdLayerNode == (Object)null)
                    {
                        PsdLayerNode psdLayerNode2 = FindDescendantRecursive(child, uiType);
                        if ((Object)(object)psdLayerNode2 != (Object)null)
                        {
                            return psdLayerNode2;
                        }
                    }
                    else
                    {
                        if (psdLayerNode.UIType == uiType)
                        {
                            return psdLayerNode;
                        }
                        if (!IsControlBoundary(psdLayerNode))
                        {
                            psdLayerNode3 = FindDescendantRecursive(child, uiType);
                            if ((Object)(object)psdLayerNode3 != (Object)null)
                            {
                                break;
                            }
                        }
                    }
                    num++;
                    continue;
                }
                return null;
            }
            return psdLayerNode3;
        }

        private static PsdLayerNode FindNestedNodeRecursive(object value, GUIType uiType)
        {
            if (!((Object)value == (Object)null))
            {
                for (int i = 0; i < ((Transform)value).childCount; i++)
                {
                    Transform child = ((Transform)value).GetChild(i);
                    PsdLayerNode psdLayerNode = ((!((Object)(object)child != (Object)null)) ? null : ((Component)child).GetComponent<PsdLayerNode>());
                    if (!((Object)(object)psdLayerNode == (Object)null))
                    {
                        if (psdLayerNode.UIType == uiType)
                        {
                            return psdLayerNode;
                        }
                        if (!IsControlBoundary(psdLayerNode))
                        {
                            PsdLayerNode psdLayerNode2 = FindNestedNodeRecursive(child, uiType);
                            if ((Object)(object)psdLayerNode2 != (Object)null)
                            {
                                return psdLayerNode2;
                            }
                        }
                    }
                    else
                    {
                        PsdLayerNode psdLayerNode3 = FindNestedNodeRecursive(child, uiType);
                        if ((Object)(object)psdLayerNode3 != (Object)null)
                        {
                            return psdLayerNode3;
                        }
                    }
                }
                return null;
            }
            return null;
        }

        private static bool IsControlBoundary(object value)
        {
            if (!((Object)value != (Object)null) || !((PsdLayerNode)value).IsPrimaryUIType() || ((PsdLayerNode)value).UIType == GUIType.Null)
            {
                return false;
            }
            return ((PsdLayerNode)value).UIType != GUIType.Panel;
        }

        private static string GetRelativeNodePath(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null))
            {
                Stack<string> stack = new Stack<string>();
                Transform transform = ((Component)value).transform;
                Transform val = ((Component)value2).transform;
                while ((Object)(object)val != (Object)null && (Object)(object)val != (Object)(object)transform)
                {
                    stack.Push(((Object)val).name);
                    val = val.parent;
                }
                if (stack.Count > 0)
                {
                    return string.Join("/", stack.ToArray());
                }
                return ((Object)value2).name;
            }
            return string.Empty;
        }

        private static int CountDirectChildrenByUiType(object value, GUIType uiType)
        {
            if ((Object)value == (Object)null)
            {
                return 0;
            }
            int num = 0;
            Transform transform = ((Component)value).transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                PsdLayerNode component = ((Component)transform.GetChild(i)).GetComponent<PsdLayerNode>();
                if ((Object)(object)component != (Object)null && component.UIType == uiType)
                {
                    num++;
                }
            }
            return num;
        }

        private static bool IsLayerGroup(object value)
        {
            if ((Object)value != (Object)null)
            {
                return ((PsdLayerNode)value).LayerType == PsdLayerType.LayerGroup;
            }
            return false;
        }

        private static bool HasImageBacking(object value)
        {
            if ((Object)value == (Object)null || ((PsdLayerNode)value).LayerType == PsdLayerType.Unknown || ((PsdLayerNode)value).LayerType == PsdLayerType.TextLayer)
            {
                return false;
            }
            return true;
        }

        private static bool HasTextBacking(object value)
        {
            if (!((Object)value == (Object)null))
            {
                if (((PsdLayerNode)value).LayerType == PsdLayerType.TextLayer)
                {
                    return true;
                }
                if (((PsdLayerNode)value).LayerType != PsdLayerType.LayerGroup)
                {
                    return false;
                }
                PsdLayerNode[] componentsInChildren = ((Component)value).GetComponentsInChildren<PsdLayerNode>(true);
                if (componentsInChildren != null && componentsInChildren.Length >= 1)
                {
                    foreach (PsdLayerNode psdLayerNode in componentsInChildren)
                    {
                        if ((Object)(object)psdLayerNode != (Object)null && (Object)(object)psdLayerNode != (Object)value && psdLayerNode.LayerType == PsdLayerType.TextLayer)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static RoleBackingKind GetRoleBackingKind(GUIType uiType)
        {
            if (UiTypeCompatibilityRules.GetRoleContentKind(uiType) == (UiTypeCompatibilityRules.UiRoleContentKind)2)
            {
                return (RoleBackingKind)1;
            }
            return (RoleBackingKind)0;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiHierarchyStructureValidator GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
