using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    public abstract class UIHelperBase : MonoBehaviour
    {
        private static UIHelperBase s_UIHelperBaseObfuscationSentinel;

        /// <summary>
        /// 编辑器侧实现的当前实例。播放器构建中恒为 null（注册方只在编辑器程序集里），
        /// 因此所有生成期/导出期动作必须经 <c>?.</c> 或判空调用，绝不能假定它存在。
        /// </summary>
        internal static IPsd2UIFormEditorHost EditorHost
        {
            get { return Psd2UIFormEditorHost.Current; }
        }

        [SpecialName]
        internal PsdLayerNode GetLayerNode()
        {
            return ((Component)this).GetComponent<PsdLayerNode>();
        }

        private void OnEnable()
        {
            if (EditorHost == null)
            {
                return;
            }
            ParseAndAttachUIElements();
        }

        internal abstract void ParseAndAttachUIElements();

        internal abstract PsdLayerNode[] GetDependencies();

        internal virtual void ResolveDependencyClaims(Dictionary<int, HashSet<int>> dependencyOwnerIds)
        {
        }

        protected abstract void InitUIElements(GameObject uiRoot);

        internal virtual void OnUIParented(GameObject uiRoot)
        {
        }

        internal virtual void OnGeneratedHierarchyReady(GameObject uiRoot)
        {
        }

        protected PsdLayerNode[] CalculateDependencies(params PsdLayerNode[] nodes)
        {
            if (nodes != null && nodes.Length != 0)
            {
                for (int num = nodes.Length - 1; num >= 0; num--)
                {
                    PsdLayerNode psdLayerNode = nodes[num];
                    if ((Object)(object)psdLayerNode == (Object)null || (Object)(object)psdLayerNode == (Object)(object)GetLayerNode())
                    {
                        List<PsdLayerNode> remaining = new List<PsdLayerNode>(nodes);
                        remaining.RemoveAt(num);
                        nodes = remaining.ToArray();
                    }
                }
                return nodes;
            }
            return null;
        }

        protected PsdLayerNode FindOwnedNode(params GUIType[] uiTypes)
        {
            if ((Object)(object)GetLayerNode() != (Object)null)
            {
                return GetLayerNode().FindFirstOwnedNode(uiTypes);
            }
            return null;
        }

        protected PsdLayerNode[] FindOwnedNodes(params GUIType[] uiTypes)
        {
            if ((Object)(object)GetLayerNode() != (Object)null)
            {
                return GetLayerNode().FindOwnedNodes(uiTypes);
            }
            return null;
        }

        protected bool IsDependencyClaimedByOtherHelper(PsdLayerNode node, Dictionary<int, HashSet<int>> dependencyOwnerIds)
        {
            if (!((Object)(object)node == (Object)null) && dependencyOwnerIds != null)
            {
                if (dependencyOwnerIds.TryGetValue(GetLayerNodeGameObjectId(node), out var value) && value != null)
                {
                    int instanceID = ((Object)this).GetInstanceID();
                    foreach (int item in value)
                    {
                        if (item != instanceID)
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

        internal static int GetLayerNodeGameObjectId(object value)
        {
            if ((Object)value != (Object)null && (Object)(object)((Component)value).gameObject != (Object)null)
            {
                return ((Object)((Component)value).gameObject).GetInstanceID();
            }
            return 0;
        }

        internal GameObject CreateOrUpdateUIRoot(GameObject gameObject = null)
        {
            if (EditorHost == null)
            {
                return null;
            }
            if (!GetLayerNode().IsPrimaryUIType() || GetLayerNode().UIType == GUIType.Null)
            {
                return null;
            }
            gameObject = CreateOrReuseRoot(gameObject);
            if (!((Object)(object)gameObject == (Object)null))
            {
                if (GetLayerNode().IsPrimaryUIType())
                {
                    ((Object)gameObject).name = GetLayerNode().GetGeneratedObjectName();
                }
                InitUIElements(gameObject);
                return gameObject;
            }
            return null;
        }

        protected virtual GameObject CreateOrReuseRoot(GameObject uiInstance)
        {
            UGUIParseRule uGUIParseRule = EditorHost?.FindRule(GetLayerNode().UIType);
            if (uGUIParseRule != null && !((Object)(object)uGUIParseRule.UIPrefab == (Object)null))
            {
                if ((Object)(object)uiInstance == (Object)null)
                {
                    uiInstance = Object.Instantiate<GameObject>(uGUIParseRule.UIPrefab, Vector3.zero, Quaternion.identity);
                }
                else if (!IsCompatibleRoot(uiInstance, uGUIParseRule.UIPrefab))
                {
                    SyncRootComponentsFromPrefab(uiInstance, uGUIParseRule.UIPrefab);
                }
                return uiInstance;
            }
            Debug.LogWarning((object)$"创建UI类型{GetLayerNode().UIType}失败:Rule配置项不存在或UIPrefab为空");
            return null;
        }

        private static void SyncRootComponentsFromPrefab(object value, object value2)
        {
            if ((Object)value == (Object)null || (Object)value2 == (Object)null)
            {
                return;
            }
            RemoveObsoleteManagedComponents(value, value2);
            Component[] components = ((GameObject)value2).GetComponents<Component>();
            foreach (Component val in components)
            {
                if ((Object)(object)val == (Object)null)
                {
                    continue;
                }
                Type type = ((object)val).GetType();
                if (type == typeof(Transform) || type == typeof(RectTransform))
                {
                    continue;
                }
                if (type == typeof(CanvasRenderer))
                {
                    if ((Object)(object)((GameObject)value).GetComponent<CanvasRenderer>() == (Object)null)
                    {
                        ((GameObject)value).AddComponent<CanvasRenderer>();
                    }
                    continue;
                }
                Component component = ((GameObject)value).GetComponent(type);
#if UNITY_EDITOR
                ComponentUtility.CopyComponent(val);
                if ((Object)(object)component != (Object)null)
                {
                    ComponentUtility.PasteComponentValues(component);
                }
                else
                {
                    ComponentUtility.PasteComponentAsNew((GameObject)value);
                }
#else
                if ((Object)(object)component == (Object)null)
                {
                    ((GameObject)value).AddComponent(type);
                }
#endif
            }
        }

        private static void RemoveObsoleteManagedComponents(object value, object value2)
        {
            Component[] components = ((GameObject)value2).GetComponents<Component>();
            HashSet<Type> hashSet = new HashSet<Type>();
            foreach (Component val in components)
            {
                if ((Object)(object)val != (Object)null)
                {
                    hashSet.Add(((object)val).GetType());
                }
            }
            Component[] components2 = ((GameObject)value).GetComponents<Component>();
            for (int num = components2.Length - 1; num >= 0; num--)
            {
                Component val2 = components2[num];
                if (!((Object)(object)val2 == (Object)null))
                {
                    Type type = ((object)val2).GetType();
                    if (!hashSet.Contains(type) && IsManagedUIComponentType(type))
                    {
                        Object.DestroyImmediate((Object)(object)val2);
                    }
                }
            }
        }

        protected static bool IsCompatibleRoot(GameObject existing, GameObject prefabRoot)
        {
            Type primaryRootComponentType = GetPrimaryRootComponentType(prefabRoot);
            Type primaryRootComponentType2 = GetPrimaryRootComponentType(existing);
            if (!(primaryRootComponentType == null) && !(primaryRootComponentType2 == null))
            {
                return primaryRootComponentType == primaryRootComponentType2;
            }
            return false;
        }

        protected static Type GetPrimaryRootComponentType(GameObject target)
        {
            if (!((Object)(object)target == (Object)null))
            {
                Component[] components = target.GetComponents<Component>();
                foreach (Component val in components)
                {
                    if (!((Object)(object)val == (Object)null) && !IsInfrastructureComponentType(((object)val).GetType()) && (val is ScrollRect || val is Selectable || val is Mask))
                    {
                        return ((object)val).GetType();
                    }
                }
                foreach (Component val2 in components)
                {
                    if (!((Object)(object)val2 == (Object)null) && !IsInfrastructureComponentType(((object)val2).GetType()) && val2 is Graphic)
                    {
                        return ((object)val2).GetType();
                    }
                }
                foreach (Component val3 in components)
                {
                    if (!((Object)(object)val3 == (Object)null))
                    {
                        Type type = ((object)val3).GetType();
                        if (!IsInfrastructureComponentType(type))
                        {
                            return type;
                        }
                    }
                }
                return null;
            }
            return null;
        }

        private static bool IsInfrastructureComponentType(Type type)
        {
            if (!(type == typeof(Transform)) && !(type == typeof(RectTransform)) && !(type == typeof(CanvasRenderer)) && !(type == typeof(UIStringKey)))
            {
                return type == typeof(PsdGeneratedKey);
            }
            return true;
        }

        private static bool IsManagedUIComponentType(Type type)
        {
            if (!typeof(Selectable).IsAssignableFrom(type) && !typeof(Graphic).IsAssignableFrom(type) && !(type == typeof(Mask)) && !(type == typeof(ScrollRect)))
            {
                return type == typeof(CanvasRenderer);
            }
            return true;
        }

        protected static T FindParentComponent<T>(Transform current) where T : Component
        {
            T component;
            while (true)
            {
                if ((Object)(object)current != (Object)null)
                {
                    component = ((Component)current).GetComponent<T>();
                    if ((Object)(object)component != (Object)null)
                    {
                        break;
                    }
                    current = current.parent;
                    continue;
                }
                return default(T);
            }
            return component;
        }

        internal static bool IsUIHelperBaseObfuscationSentinelNull()
        {
            return (object)s_UIHelperBaseObfuscationSentinel == null;
        }

        internal static UIHelperBase GetUIHelperBaseObfuscationSentinel()
        {
            return s_UIHelperBaseObfuscationSentinel;
        }
    }
}
