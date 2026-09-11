using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class InputFieldHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode placeholder;

        [SerializeField]
        private PsdLayerNode text;

        internal static InputFieldHelper s_InputFieldHelperObfuscationSentinel;

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
            InputField component = uiRoot.GetComponent<InputField>();
            EditorHost?.ApplyNodeRectToUI(background, component);
            Graphic targetGraphic = ((Selectable)component).targetGraphic;
            Image val = (Image)(object)((targetGraphic is Image) ? targetGraphic : null);
            EditorHost?.ApplyImageSprite(background, val);
            EditorHost?.ApplyNodeRectToUI(placeholder, component.placeholder);
            EditorHost?.ApplyNodeRectToUI(text, component.textComponent);
            PsdLayerNode psdLayerNode = placeholder;
            Graphic obj = component.placeholder;
            IPsd2UIFormEditorHost host = EditorHost;
            if (host != null)
            {
                host.ApplyLegacyTextStyle(psdLayerNode, (obj is Text) ? obj : null);
                component.text = host.ApplyLegacyTextStyle(text, component.textComponent).Text;
            }
        }

        internal static bool IsInputFieldHelperObfuscationSentinelNull()
        {
            return (object)s_InputFieldHelperObfuscationSentinel == null;
        }

        internal static InputFieldHelper GetInputFieldHelperObfuscationSentinel()
        {
            return s_InputFieldHelperObfuscationSentinel;
        }
    }
}
