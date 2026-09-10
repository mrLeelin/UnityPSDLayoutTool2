using TMPro;
using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class TMPTextHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode text;

        private static TMPTextHelper s_TMPTextHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(text);
        }

        internal override void ParseAndAttachUIElements()
        {
            if (GetLayerNode().TryGetTextStyleInfo(out var _))
            {
                text = GetLayerNode();
            }
            else
            {
                GetLayerNode().SetUIType(UGUIParser.Instance.GetDefaultImageType());
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            TextMeshProUGUI componentInChildren = uiRoot.GetComponentInChildren<TextMeshProUGUI>();
            UGUIParser.ApplyTMPTextStyle(text, componentInChildren);
            UGUIParser.ApplyNodeRectToUI(text, componentInChildren);
        }

        internal static bool IsTMPTextHelperObfuscationSentinelNull()
        {
            return (object)s_TMPTextHelperObfuscationSentinel == null;
        }

        internal static TMPTextHelper GetTMPTextHelperObfuscationSentinel()
        {
            return s_TMPTextHelperObfuscationSentinel;
        }
    }
}
