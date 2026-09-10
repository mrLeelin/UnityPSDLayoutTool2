using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class TMPDropdownHelper : UIHelperBase
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

        private static TMPDropdownHelper s_TMPDropdownHelperObfuscationSentinel;

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
            toggleItem = FindOwnedNode(GUIType.TMPToggle, GUIType.Toggle);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            TMP_Dropdown component = uiRoot.GetComponent<TMP_Dropdown>();
            UGUIParser.ApplyNodeRectToUI(background, component);
            Graphic targetGraphic = ((Selectable)component).targetGraphic;
            Image val = (Image)(object)((targetGraphic is Image) ? targetGraphic : null);
            UGUIParser.Instance.ApplyImageSprite(background, val);
            PsdLayerNode psdLayerNode = label;
            TMP_Text captionText = component.captionText;
            UGUIParser.ApplyTMPTextStyle(psdLayerNode, (captionText is TextMeshProUGUI) ? captionText : null);
            UGUIParser.ApplyNodeRectToUI(label, component.captionText);
            Transform obj = ((Component)component).transform.Find("Arrow");
            Image val2 = (((object)obj == null) ? null : ((Component)obj).GetComponent<Image>());
            if ((Object)(object)val2 != (Object)null)
            {
                UGUIParser.ApplyNodeRectToUI(arrow, val2);
                UGUIParser.Instance.ApplyImageSprite(arrow, val2);
            }
            if (!((Object)(object)scrollView != (Object)null))
            {
                ScrollRect componentInChildren = uiRoot.GetComponentInChildren<ScrollRect>(true);
                ((Behaviour)((Component)componentInChildren).GetComponent<Image>()).enabled = false;
                if ((Object)(object)componentInChildren.horizontalScrollbar != (Object)null)
                {
                    Scrollbar horizontalScrollbar = componentInChildren.horizontalScrollbar;
                    componentInChildren.horizontalScrollbar = null;
                    ((Component)horizontalScrollbar).gameObject.SetActive(false);
                }
                if ((Object)(object)componentInChildren.verticalScrollbar != (Object)null)
                {
                    Scrollbar verticalScrollbar = componentInChildren.verticalScrollbar;
                    componentInChildren.verticalScrollbar = null;
                    ((Component)verticalScrollbar).gameObject.SetActive(false);
                }
            }
            else
            {
                ScrollRect componentInChildren2 = uiRoot.GetComponentInChildren<ScrollRect>(true);
                GameObject val3 = ((Component)scrollView).GetComponent<ScrollViewHelper>()?.CreateOrUpdateUIRoot(((Component)componentInChildren2).gameObject);
                if ((Object)(object)val3 != (Object)null)
                {
                    RectTransform component2 = val3.GetComponent<RectTransform>();
                    UGUIParser.ApplyNodeRectToUI(scrollView, component2);
                }
            }
            if ((Object)(object)toggleItem != (Object)null)
            {
                Transform val4 = (((Object)(object)component.itemText != (Object)null) ? component.itemText.transform.parent : null);
                if ((Object)(object)val4 != (Object)null)
                {
                    ((Component)toggleItem).GetComponent<UIHelperBase>()?.CreateOrUpdateUIRoot(((Component)val4).gameObject);
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
                ContentSizeFitter val6 = (((object)content2 == null) ? null : ((Component)content2).GetComponent<ContentSizeFitter>());
                if ((Object)(object)val6 != (Object)null)
                {
                    ((Behaviour)val6).enabled = false;
                }
            }
        }

        internal static bool IsTMPDropdownHelperObfuscationSentinelNull()
        {
            return (object)s_TMPDropdownHelperObfuscationSentinel == null;
        }

        internal static TMPDropdownHelper GetTMPDropdownHelperObfuscationSentinel()
        {
            return s_TMPDropdownHelperObfuscationSentinel;
        }
    }
}
