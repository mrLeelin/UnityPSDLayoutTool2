using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class RawImageHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode rawImage;

        private static RawImageHelper s_RawImageHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(rawImage);
        }

        internal override void ParseAndAttachUIElements()
        {
            rawImage = GetLayerNode();
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            RawImage componentInChildren = uiRoot.GetComponentInChildren<RawImage>();
            UGUIParser.ApplyNodeRectToUI(rawImage, componentInChildren);
            componentInChildren.texture = (Texture)(object)UGUIParser.ExportAndLoadTexture(rawImage);
        }

        internal static bool IsRawImageHelperObfuscationSentinelNull()
        {
            return (object)s_RawImageHelperObfuscationSentinel == null;
        }

        internal static RawImageHelper GetRawImageHelperObfuscationSentinel()
        {
            return s_RawImageHelperObfuscationSentinel;
        }
    }
}
