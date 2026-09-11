using TMPro;
using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class TMPInputFieldHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode placeholder;

        [SerializeField]
        private PsdLayerNode text;

        internal static TMPInputFieldHelper s_TMPInputFieldHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, placeholder, text);
        }

        internal override void ParseAndAttachUIElements()
        {
            background = FindOwnedNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
            placeholder = FindOwnedNode(GUIType.InputField_Placeholder);
            text = FindOwnedNode(GUIType.InputField_Text, GUIType.Text);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            TMP_InputField component = uiRoot.GetComponent<TMP_InputField>();
            if (!((Object)(object)component == (Object)null))
            {
                EditorHost?.ApplyNodeRectToUI(background, component);
                Graphic targetGraphic = ((Selectable)component).targetGraphic;
                Image val = (Image)(((object)((targetGraphic is Image) ? targetGraphic : null)) ?? ((object)uiRoot.GetComponent<Image>()));
                if ((Object)(object)val != (Object)null)
                {
                    ((Selectable)component).targetGraphic = (Graphic)(object)val;
                    EditorHost?.ApplyImageSprite(background, val);
                }
                Graphic obj = component.placeholder;
                TextMeshProUGUI val2 = (TextMeshProUGUI)(object)((obj is TextMeshProUGUI) ? obj : null);
                if ((Object)(object)val2 == (Object)null)
                {
                    Transform obj2 = uiRoot.transform.Find("Text Area/Placeholder");
                    val2 = (((object)obj2 != null) ? ((Component)obj2).GetComponent<TextMeshProUGUI>() : null);
                    if ((Object)(object)val2 != (Object)null)
                    {
                        component.placeholder = (Graphic)(object)val2;
                    }
                }
                TMP_Text textComponent = component.textComponent;
                TextMeshProUGUI val3 = (TextMeshProUGUI)(object)((textComponent is TextMeshProUGUI) ? textComponent : null);
                if ((Object)(object)val3 == (Object)null)
                {
                    Transform obj3 = uiRoot.transform.Find("Text Area/Text");
                    val3 = (((object)obj3 == null) ? null : ((Component)obj3).GetComponent<TextMeshProUGUI>());
                    if ((Object)(object)val3 != (Object)null)
                    {
                        component.textComponent = (TMP_Text)(object)val3;
                    }
                }
                EditorHost?.ApplyNodeRectToUI(placeholder, val2);
                EditorHost?.ApplyNodeRectToUI(text, val3);
                EditorHost?.ApplyTMPTextStyle(placeholder, val2);
                EditorHost?.ApplyTMPTextStyle(text, val3);
                if ((Object)(object)text != (Object)null && text.TryGetTextStyleInfo(out var value))
                {
                    component.text = value.Text ?? string.Empty;
                }
            }
            else
            {
                Debug.LogWarning((object)("TMPInputField缺少TMP_InputField组件, 已跳过初始化: " + ((Object)uiRoot).name));
            }
        }

        internal static bool IsTMPInputFieldHelperObfuscationSentinelNull()
        {
            return (object)s_TMPInputFieldHelperObfuscationSentinel == null;
        }

        internal static TMPInputFieldHelper GetTMPInputFieldHelperObfuscationSentinel()
        {
            return s_TMPInputFieldHelperObfuscationSentinel;
        }
    }
}
