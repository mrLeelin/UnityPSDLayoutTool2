using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class TMPToggleHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode checkmark;

        [SerializeField]
        private PsdLayerNode label;

        private static TMPToggleHelper s_TMPToggleHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, checkmark, label);
        }

        internal override void ParseAndAttachUIElements()
        {
            background = FindOwnedNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
            checkmark = FindOwnedNode(GUIType.Toggle_Checkmark);
            label = FindOwnedNode(GUIType.Toggle_Label, GUIType.Text);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Toggle component = uiRoot.GetComponent<Toggle>();
            UGUIParser.ApplyNodeRectToUI(GetLayerNode(), component);
            Graphic targetGraphic = ((Selectable)component).targetGraphic;
            Image val = (Image)(object)((targetGraphic is Image) ? targetGraphic : null);
            if ((Object)(object)val != (Object)null)
            {
                UGUIParser.ApplyNodeRectToUI(background, val);
                UGUIParser.Instance.ApplyImageSprite(background, val);
            }
            Graphic graphic = component.graphic;
            Image val2 = (Image)(object)((graphic is Image) ? graphic : null);
            if ((Object)(object)val2 != (Object)null)
            {
                UGUIParser.ApplyNodeRectToUI(checkmark, val2);
                UGUIParser.Instance.ApplyImageSprite(checkmark, val2);
            }
            Transform obj = ((Component)component).transform.Find("Label");
            TextMeshProUGUI val3 = (((object)obj == null) ? null : ((Component)obj).GetComponent<TextMeshProUGUI>());
            if ((Object)(object)val3 != (Object)null)
            {
                ((Component)val3).gameObject.SetActive((Object)(object)label != (Object)null);
            }
            UGUIParser.ApplyTMPTextStyle(label, val3);
            UGUIParser.ApplyNodeRectToUI(label, val3);
        }

        internal override void OnUIParented(GameObject uiRoot)
        {
            Toggle val = ((!((Object)(object)uiRoot != (Object)null)) ? null : uiRoot.GetComponent<Toggle>());
            if (!((Object)(object)val == (Object)null))
            {
                val.group = UIHelperBase.FindParentComponent<ToggleGroup>(uiRoot.transform.parent);
            }
        }

        internal static bool IsTMPToggleHelperObfuscationSentinelNull()
        {
            return (object)s_TMPToggleHelperObfuscationSentinel == null;
        }

        internal static TMPToggleHelper GetTMPToggleHelperObfuscationSentinel()
        {
            return s_TMPToggleHelperObfuscationSentinel;
        }
    }
}
