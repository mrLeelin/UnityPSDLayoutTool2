using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class DropdownHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode label;

        [SerializeField]
        private PsdLayerNode arrow;

        [SerializeField]
        private PsdLayerNode scrollView;

        [SerializeField]
        private PsdLayerNode toggleItem;

        internal static DropdownHelper s_DropdownHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, label, arrow, scrollView, toggleItem);
        }

        internal override void ParseAndAttachUIElements()
        {
            background = FindOwnedNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
            label = FindOwnedNode(GUIType.Dropdown_Label, GUIType.Text);
            arrow = FindOwnedNode(GUIType.Dropdown_Arrow);
            scrollView = FindOwnedNode(GUIType.ScrollView);
            toggleItem = FindOwnedNode(GUIType.Toggle, GUIType.TMPToggle);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Dropdown component = uiRoot.GetComponent<Dropdown>();
            UGUIParser.ApplyNodeRectToUI(background, component);
            Graphic targetGraphic = ((Selectable)component).targetGraphic;
            Image val = (Image)(object)((targetGraphic is Image) ? targetGraphic : null);
            UGUIParser.Instance.ApplyImageSprite(background, val);
            UGUIParser.ApplyLegacyTextStyle(label, component.captionText);
            UGUIParser.ApplyNodeRectToUI(label, component.captionText);
            Transform obj = ((Component)component).transform.Find("Arrow");
            Image val2 = (((object)obj == null) ? null : ((Component)obj).GetComponent<Image>());
            if ((Object)(object)val2 != (Object)null)
            {
                UGUIParser.ApplyNodeRectToUI(arrow, val2);
                UGUIParser.Instance.ApplyImageSprite(arrow, val2);
            }
            if ((Object)(object)scrollView != (Object)null)
            {
                ScrollRect componentInChildren = uiRoot.GetComponentInChildren<ScrollRect>(true);
                GameObject val3 = ((Component)scrollView).GetComponent<ScrollViewHelper>()?.CreateOrUpdateUIRoot(((Component)componentInChildren).gameObject);
                if ((Object)(object)val3 != (Object)null)
                {
                    RectTransform component2 = val3.GetComponent<RectTransform>();
                    UGUIParser.ApplyNodeRectToUI(scrollView, component2);
                }
            }
            else
            {
                ScrollRect componentInChildren2 = uiRoot.GetComponentInChildren<ScrollRect>(true);
                ((Behaviour)((Component)componentInChildren2).GetComponent<Image>()).enabled = false;
                if ((Object)(object)componentInChildren2.horizontalScrollbar != (Object)null)
                {
                    Scrollbar horizontalScrollbar = componentInChildren2.horizontalScrollbar;
                    componentInChildren2.horizontalScrollbar = null;
                    ((Component)horizontalScrollbar).gameObject.SetActive(false);
                }
                if ((Object)(object)componentInChildren2.verticalScrollbar != (Object)null)
                {
                    Scrollbar verticalScrollbar = componentInChildren2.verticalScrollbar;
                    componentInChildren2.verticalScrollbar = null;
                    ((Component)verticalScrollbar).gameObject.SetActive(false);
                }
            }
            if ((Object)(object)toggleItem != (Object)null)
            {
                Transform val4 = (((Object)(object)component.itemText != (Object)null) ? ((Component)component.itemText).transform.parent : null);
                if ((Object)(object)val4 != (Object)null)
                {
                    ((Component)toggleItem).GetComponent<ToggleHelper>()?.CreateOrUpdateUIRoot(((Component)val4).gameObject);
                }
            }
            ScrollRect componentInChildren3 = uiRoot.GetComponentInChildren<ScrollRect>(true);
            if ((Object)(object)componentInChildren3 != (Object)null)
            {
                RectTransform content = componentInChildren3.content;
                LayoutGroup val5 = (((object)content == null) ? null : ((Component)content).GetComponent<LayoutGroup>());
                if ((Object)(object)val5 != (Object)null)
                {
                    ((Behaviour)val5).enabled = false;
                }
                RectTransform content2 = componentInChildren3.content;
                ContentSizeFitter val6 = (((object)content2 != null) ? ((Component)content2).GetComponent<ContentSizeFitter>() : null);
                if ((Object)(object)val6 != (Object)null)
                {
                    ((Behaviour)val6).enabled = false;
                }
            }
        }

        internal static bool IsDropdownHelperObfuscationSentinelNull()
        {
            return (object)s_DropdownHelperObfuscationSentinel == null;
        }

        internal static DropdownHelper GetDropdownHelperObfuscationSentinel()
        {
            return s_DropdownHelperObfuscationSentinel;
        }
    }
}
