using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class TextHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode text;

        internal static TextHelper s_TextHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(text);
        }

        internal override void ParseAndAttachUIElements()
        {
            IPsd2UIFormEditorHost host = EditorHost;
            if (host == null)
            {
                return;
            }
            if (!GetLayerNode().TryGetTextStyleInfo(out var _))
            {
                GetLayerNode().SetUIType(host.GetDefaultImageType());
            }
            else
            {
                text = GetLayerNode();
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Text componentInChildren = uiRoot.GetComponentInChildren<Text>();
            EditorHost?.ApplyLegacyTextStyle(text, componentInChildren);
            EditorHost?.ApplyNodeRectToUI(text, componentInChildren);
        }

        internal static bool IsTextHelperObfuscationSentinelNull()
        {
            return (object)s_TextHelperObfuscationSentinel == null;
        }

        internal static TextHelper GetTextHelperObfuscationSentinel()
        {
            return s_TextHelperObfuscationSentinel;
        }
    }
}
