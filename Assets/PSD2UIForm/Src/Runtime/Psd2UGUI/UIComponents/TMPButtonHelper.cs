using TMPro;
using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class TMPButtonHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode text;

        [Header("Sprite Swap:")]
        [SerializeField]
        private PsdLayerNode highlight;

        [SerializeField]
        private PsdLayerNode press;

        [SerializeField]
        private PsdLayerNode select;

        [SerializeField]
        private PsdLayerNode disable;

        internal static TMPButtonHelper s_TMPButtonHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, text, highlight, press, select, disable);
        }

        internal override void ParseAndAttachUIElements()
        {
            if (GetLayerNode().LayerType == PsdLayerType.LayerGroup)
            {
                background = FindOwnedNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
                text = FindOwnedNode(GUIType.Button_Text, GUIType.Text);
                highlight = FindOwnedNode(GUIType.Button_Highlight);
                press = FindOwnedNode(GUIType.Button_Press);
                select = FindOwnedNode(GUIType.Button_Select);
                disable = FindOwnedNode(GUIType.Button_Disable);
            }
            else
            {
                background = GetLayerNode();
                text = null;
                highlight = null;
                press = null;
                select = null;
                disable = null;
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Button component = uiRoot.GetComponent<Button>();
            Image component2 = ((Component)component).GetComponent<Image>();
            PsdLayerNode obj = (((Object)(object)background != (Object)null) ? background : GetLayerNode());
            EditorHost?.ApplyImageSprite(background, component2);
            EditorHost?.ApplyNodeRectToUI(obj, component);
            TextMeshProUGUI val = FindReusableDirectTextChild(uiRoot);
            if (!((Object)(object)text == (Object)null))
            {
                val = val ?? CloneTemplateText(uiRoot);
                EditorHost?.ApplyTMPTextStyle(text, val);
                EditorHost?.ApplyNodeRectToUI(text, val);
            }
            else if ((Object)(object)val != (Object)null)
            {
                Object.DestroyImmediate((Object)(object)((Component)val).gameObject);
            }
            bool flag = (Object)(object)highlight != (Object)null || (Object)(object)press != (Object)null || (Object)(object)select != (Object)null || (Object)(object)disable != (Object)null;
            ((Selectable)component).transition = (Selectable.Transition)((!flag) ? 1 : 2);
            if ((int)((Selectable)component).transition == 2)
            {
                bool flag2 = (int)component2.type == 1 || (int)component2.type == 2;
                SpriteState spriteState = default(SpriteState);
                spriteState.highlightedSprite = EditorHost?.ExportAndLoadSprite(highlight, flag2);
                spriteState.pressedSprite = EditorHost?.ExportAndLoadSprite(press, flag2);
                spriteState.selectedSprite = EditorHost?.ExportAndLoadSprite(select, flag2);
                spriteState.disabledSprite = EditorHost?.ExportAndLoadSprite(disable, flag2);
                ((Selectable)component).spriteState = spriteState;
            }
        }

        private TextMeshProUGUI CloneTemplateText(GameObject gameObject)
        {
            GameObject val = EditorHost?.FindRule(GetLayerNode().UIType)?.UIPrefab;
            TextMeshProUGUI val2 = ((!((Object)(object)val != (Object)null)) ? null : val.GetComponentInChildren<TextMeshProUGUI>(true));
            if ((Object)(object)val2 == (Object)null)
            {
                return null;
            }
            GameObject obj = Object.Instantiate<GameObject>(((Component)val2).gameObject, gameObject.transform, false);
            ((Object)obj).name = ((Object)((Component)val2).gameObject).name;
            obj.transform.SetAsLastSibling();
            return obj.GetComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI FindReusableDirectTextChild(object value)
        {
            if (!((Object)value == (Object)null))
            {
                Transform transform = ((GameObject)value).transform;
                int num = 0;
                TextMeshProUGUI component;
                while (true)
                {
                    if (num < transform.childCount)
                    {
                        Transform child = transform.GetChild(num);
                        if (!((Object)(object)child == (Object)null) && !((Object)(object)((Component)child).GetComponent<PsdGeneratedKey>() != (Object)null))
                        {
                            component = ((Component)child).GetComponent<TextMeshProUGUI>();
                            if ((Object)(object)component != (Object)null)
                            {
                                break;
                            }
                        }
                        num++;
                        continue;
                    }
                    return null;
                }
                return component;
            }
            return null;
        }

        internal static bool IsTMPButtonHelperObfuscationSentinelNull()
        {
            return (object)s_TMPButtonHelperObfuscationSentinel == null;
        }

        internal static TMPButtonHelper GetTMPButtonHelperObfuscationSentinel()
        {
            return s_TMPButtonHelperObfuscationSentinel;
        }
    }
}
