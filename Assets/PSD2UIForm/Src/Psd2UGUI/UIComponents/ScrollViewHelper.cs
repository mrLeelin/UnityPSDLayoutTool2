using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class ScrollViewHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode viewport;

        [SerializeField]
        private PsdLayerNode horizontalBarBG;

        [SerializeField]
        private PsdLayerNode horizontalBar;

        [SerializeField]
        private PsdLayerNode verticalBarBG;

        [SerializeField]
        private PsdLayerNode verticalBar;

        private static ScrollViewHelper s_ScrollViewHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, viewport, horizontalBarBG, horizontalBar, verticalBarBG, verticalBar);
        }

        internal override void ParseAndAttachUIElements()
        {
            background = FindOwnedNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
            viewport = FindOwnedNode(GUIType.ScrollView_Viewport);
            horizontalBarBG = FindOwnedNode(GUIType.ScrollView_HorizontalBarBG);
            horizontalBar = FindOwnedNode(GUIType.ScrollView_HorizontalBar);
            verticalBarBG = FindOwnedNode(GUIType.ScrollView_VerticalBarBG);
            verticalBar = FindOwnedNode(GUIType.ScrollView_VerticalBar);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            ScrollRect component = uiRoot.GetComponent<ScrollRect>();
            UGUIParser.ApplyNodeRectToUI(background, component);
            Image component2 = ((Component)component).GetComponent<Image>();
            if ((Object)(object)component2 != (Object)null)
            {
                UGUIParser.Instance.ApplyImageSprite(background, component2);
                if ((Object)(object)viewport == (Object)null)
                {
                    ((Component)component.viewport).GetComponent<Image>().sprite = component2.sprite;
                }
            }
            if ((Object)(object)viewport != (Object)null)
            {
                Image component3 = ((Component)component.viewport).GetComponent<Image>();
                UGUIParser.Instance.ApplyImageSprite(viewport, component3);
            }
            Scrollbar horizontalScrollbar = component.horizontalScrollbar;
            Scrollbar verticalScrollbar = component.verticalScrollbar;
            if ((Object)(object)horizontalBarBG != (Object)null && (Object)(object)horizontalScrollbar != (Object)null)
            {
                Image component4 = ((Component)horizontalScrollbar).GetComponent<Image>();
                UGUIParser.Instance.ApplyImageSprite(horizontalBarBG, component4);
                UGUIParser.ApplyNodeRectToUI(horizontalBarBG, component4);
                UGUIParser.ApplyNodeRectToUI(background, component.content, true, true, false);
            }
            else
            {
                Scrollbar horizontalScrollbar2 = component.horizontalScrollbar;
                component.horizontalScrollbar = null;
                if ((Object)(object)horizontalScrollbar2 != (Object)null)
                {
                    ((Component)horizontalScrollbar2).gameObject.SetActive(false);
                }
            }
            if ((Object)(object)verticalBarBG != (Object)null && (Object)(object)verticalScrollbar != (Object)null)
            {
                Image component5 = ((Component)verticalScrollbar).GetComponent<Image>();
                UGUIParser.Instance.ApplyImageSprite(verticalBarBG, component5);
                UGUIParser.ApplyNodeRectToUI(verticalBarBG, component5);
                UGUIParser.ApplyNodeRectToUI(background, component.content, true, false);
            }
            else
            {
                Scrollbar verticalScrollbar2 = component.verticalScrollbar;
                component.verticalScrollbar = null;
                if ((Object)(object)verticalScrollbar2 != (Object)null)
                {
                    ((Component)verticalScrollbar2).gameObject.SetActive(false);
                }
            }
            if ((Object)(object)horizontalBar != (Object)null && (Object)(object)horizontalScrollbar != (Object)null)
            {
                Graphic targetGraphic = ((Selectable)horizontalScrollbar).targetGraphic;
                Image val = (Image)(object)((targetGraphic is Image) ? targetGraphic : null);
                UGUIParser.Instance.ApplyImageSprite(horizontalBar, val);
                UGUIParser.ApplyNodeRectToUI(horizontalBar, val, false, false, false);
            }
            if ((Object)(object)verticalBar != (Object)null && (Object)(object)verticalScrollbar != (Object)null)
            {
                Graphic targetGraphic2 = ((Selectable)verticalScrollbar).targetGraphic;
                Image val2 = (Image)(object)((targetGraphic2 is Image) ? targetGraphic2 : null);
                UGUIParser.Instance.ApplyImageSprite(verticalBar, val2);
                UGUIParser.ApplyNodeRectToUI(verticalBar, val2, false, false, false);
            }
        }

        internal static bool IsScrollViewHelperObfuscationSentinelNull()
        {
            return (object)s_ScrollViewHelperObfuscationSentinel == null;
        }

        internal static ScrollViewHelper GetScrollViewHelperObfuscationSentinel()
        {
            return s_ScrollViewHelperObfuscationSentinel;
        }
    }
}
