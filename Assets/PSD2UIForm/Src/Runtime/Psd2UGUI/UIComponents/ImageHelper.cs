using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class ImageHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode image;

        internal static ImageHelper s_ImageHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(image);
        }

        internal override void ParseAndAttachUIElements()
        {
            image = GetLayerNode();
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Image componentInChildren = uiRoot.GetComponentInChildren<Image>();
            EditorHost?.ApplyNodeRectToUI(image, componentInChildren);
            EditorHost?.ApplyImageSprite(image, componentInChildren);
        }

        internal static bool IsImageHelperObfuscationSentinelNull()
        {
            return (object)s_ImageHelperObfuscationSentinel == null;
        }

        internal static ImageHelper GetImageHelperObfuscationSentinel()
        {
            return s_ImageHelperObfuscationSentinel;
        }
    }
}
