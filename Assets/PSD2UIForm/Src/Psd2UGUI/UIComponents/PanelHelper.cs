using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class PanelHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        private static PanelHelper s_PanelHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            ResolveBackgroundNode();
            return CalculateDependencies(background);
        }

        internal override void ResolveDependencyClaims(Dictionary<int, HashSet<int>> dependencyOwnerIds)
        {
            if (IsDependencyClaimedByOtherHelper(background, dependencyOwnerIds))
            {
                background = null;
            }
        }

        internal override void ParseAndAttachUIElements()
        {
            ResolveBackgroundNode();
        }

        protected override GameObject CreateOrReuseRoot(GameObject uiInstance)
        {
            if ((Object)(object)background != (Object)null)
            {
                return CreateImageRoot(uiInstance);
            }
            return CreatePlainRoot(uiInstance);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            if ((Object)(object)background != (Object)null)
            {
                Image val = uiRoot.GetComponent<Image>() ?? uiRoot.AddComponent<Image>();
                UGUIParser.ApplyNodeRectToUI(GetLayerNode(), val);
                UGUIParser.Instance.ApplyImageSprite(background, val);
                return;
            }
            Image component = uiRoot.GetComponent<Image>();
            if ((Object)(object)component != (Object)null)
            {
                Object.DestroyImmediate((Object)(object)component);
            }
            CanvasRenderer component2 = uiRoot.GetComponent<CanvasRenderer>();
            if ((Object)(object)component2 != (Object)null)
            {
                Object.DestroyImmediate((Object)(object)component2);
            }
            UGUIParser.ApplyNodeRectToUI(GetLayerNode(), uiRoot.transform);
        }

        private void ResolveBackgroundNode()
        {
            background = (((Object)(object)GetLayerNode() != (Object)null && GetLayerNode().LayerType == PsdLayerType.LayerGroup) ? FindOwnedNode(GUIType.Background) : null);
        }

        private GameObject CreatePlainRoot(GameObject gameObject)
        {
            if (!((Object)(object)gameObject == (Object)null))
            {
                RemoveIncompatibleComponents(gameObject, false);
                return gameObject;
            }
            return new GameObject(GetLayerNode().GetGeneratedObjectName(), new Type[1] { typeof(RectTransform) });
        }

        private GameObject CreateImageRoot(GameObject gameObject)
        {
            if ((Object)(object)gameObject == (Object)null)
            {
                return new GameObject(GetLayerNode().GetGeneratedObjectName(), new Type[3]
                {
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                });
            }
            RemoveIncompatibleComponents(gameObject, true);
            if ((Object)(object)gameObject.GetComponent<CanvasRenderer>() == (Object)null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }
            if ((Object)(object)gameObject.GetComponent<Image>() == (Object)null)
            {
                gameObject.AddComponent<Image>();
            }
            return gameObject;
        }

        private static void RemoveIncompatibleComponents(object value, bool enabled)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            Component[] components = ((GameObject)value).GetComponents<Component>();
            for (int num = components.Length - 1; num >= 0; num--)
            {
                Component val = components[num];
                if (!((Object)(object)val == (Object)null))
                {
                    Type type = ((object)val).GetType();
                    if (!(type == typeof(Transform)) && !(type == typeof(RectTransform)) && !(type == typeof(UIStringKey)) && !(type == typeof(PsdGeneratedKey)) && (!enabled || (!(type == typeof(CanvasRenderer)) && !(type == typeof(Image)))) && IsManagedVisualComponent(type))
                    {
                        Object.DestroyImmediate((Object)(object)val);
                    }
                }
            }
        }

        private static bool IsManagedVisualComponent(Type type)
        {
            if (!typeof(Selectable).IsAssignableFrom(type) && !typeof(Graphic).IsAssignableFrom(type) && !(type == typeof(Mask)) && !(type == typeof(ScrollRect)))
            {
                return type == typeof(CanvasRenderer);
            }
            return true;
        }

        internal static bool IsPanelHelperObfuscationSentinelNull()
        {
            return (object)s_PanelHelperObfuscationSentinel == null;
        }

        internal static PanelHelper GetPanelHelperObfuscationSentinel()
        {
            return s_PanelHelperObfuscationSentinel;
        }
    }
}
