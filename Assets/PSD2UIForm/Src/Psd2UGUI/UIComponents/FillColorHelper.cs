using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class FillColorHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode fillColor;

        internal static FillColorHelper s_FillColorHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(fillColor);
        }

        internal override void ParseAndAttachUIElements()
        {
            if (GetLayerNode().UIType != GUIType.FillColor && GetLayerNode().LayerType != PsdLayerType.FillLayer)
            {
                GetLayerNode().SetUIType(UGUIParser.Instance.GetDefaultImageType());
            }
            else
            {
                fillColor = GetLayerNode();
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            RawImage componentInChildren = uiRoot.GetComponentInChildren<RawImage>();
            UGUIParser.ApplyNodeRectToUI(fillColor, componentInChildren);
            ((Graphic)componentInChildren).color = UGUIParser.GetRepresentativeColor(fillColor, ((Graphic)componentInChildren).color);
        }

        internal static bool IsFillColorHelperObfuscationSentinelNull()
        {
            return (object)s_FillColorHelperObfuscationSentinel == null;
        }

        internal static FillColorHelper GetFillColorHelperObfuscationSentinel()
        {
            return s_FillColorHelperObfuscationSentinel;
        }
    }
}
