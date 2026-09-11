using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class MaskHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode mask;

        internal static MaskHelper s_MaskHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(mask);
        }

        internal override void ParseAndAttachUIElements()
        {
            mask = GetLayerNode();
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Image componentInChildren = uiRoot.GetComponentInChildren<Image>();
            EditorHost?.ApplyNodeRectToUI(mask, componentInChildren);
            EditorHost?.ApplyImageSprite(mask, componentInChildren);
        }

        internal static bool IsMaskHelperObfuscationSentinelNull()
        {
            return (object)s_MaskHelperObfuscationSentinel == null;
        }

        internal static MaskHelper GetMaskHelperObfuscationSentinel()
        {
            return s_MaskHelperObfuscationSentinel;
        }
    }
}
