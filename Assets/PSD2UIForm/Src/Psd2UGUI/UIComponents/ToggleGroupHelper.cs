using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class ToggleGroupHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        internal static ToggleGroupHelper s_ToggleGroupHelperObfuscationSentinel;

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
            if (!((Object)(object)background != (Object)null))
            {
                return CreatePlainRoot(uiInstance);
            }
            return CreateImageRoot(uiInstance);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            if (!((Object)(object)background != (Object)null))
            {
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
            else
            {
                Image val = uiRoot.GetComponent<Image>() ?? uiRoot.AddComponent<Image>();
                UGUIParser.ApplyNodeRectToUI(GetLayerNode(), val);
                UGUIParser.Instance.ApplyImageSprite(background, val);
            }
            if ((Object)(object)uiRoot.GetComponent<ToggleGroup>() == (Object)null)
            {
                uiRoot.AddComponent<ToggleGroup>();
            }
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
                EnsureToggleGroupComponent(gameObject);
                return gameObject;
            }
            return new GameObject(GetLayerNode().GetGeneratedObjectName(), new Type[2]
            {
                typeof(RectTransform),
                typeof(ToggleGroup)
            });
        }

        private GameObject CreateImageRoot(GameObject gameObject)
        {
            if ((Object)(object)gameObject == (Object)null)
            {
                return new GameObject(GetLayerNode().GetGeneratedObjectName(), new Type[4]
                {
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ToggleGroup)
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
            EnsureToggleGroupComponent(gameObject);
            return gameObject;
        }

        private static void EnsureToggleGroupComponent(object value)
        {
            if ((Object)value != (Object)null && (Object)(object)((GameObject)value).GetComponent<ToggleGroup>() == (Object)null)
            {
                ((GameObject)value).AddComponent<ToggleGroup>();
            }
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
                    if (!(type == typeof(Transform)) && !(type == typeof(RectTransform)) && !(type == typeof(UIStringKey)) && !(type == typeof(PsdGeneratedKey)) && !(type == typeof(ToggleGroup)) && (!enabled || (!(type == typeof(CanvasRenderer)) && !(type == typeof(Image)))) && IsManagedVisualComponent(type))
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

        internal static bool IsToggleGroupHelperObfuscationSentinelNull()
        {
            return (object)s_ToggleGroupHelperObfuscationSentinel == null;
        }

        internal static ToggleGroupHelper GetToggleGroupHelperObfuscationSentinel()
        {
            return s_ToggleGroupHelperObfuscationSentinel;
        }
    }
}
