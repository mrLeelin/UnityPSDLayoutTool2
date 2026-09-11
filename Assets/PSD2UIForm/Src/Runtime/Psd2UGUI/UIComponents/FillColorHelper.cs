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
            IPsd2UIFormEditorHost host = EditorHost;
            if (host == null)
            {
                return;
            }
            if (GetLayerNode().UIType != GUIType.FillColor && GetLayerNode().LayerType != PsdLayerType.FillLayer)
            {
                GetLayerNode().SetUIType(host.GetDefaultImageType());
            }
            else
            {
                fillColor = GetLayerNode();
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            RawImage componentInChildren = uiRoot.GetComponentInChildren<RawImage>();
            IPsd2UIFormEditorHost host = EditorHost;
            host?.ApplyNodeRectToUI(fillColor, componentInChildren);
            if (host != null)
            {
                ((Graphic)componentInChildren).color = host.GetRepresentativeColor(fillColor, ((Graphic)componentInChildren).color);
            }
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
