using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class ButtonHelper : UIHelperBase
    {
        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode text;

        [SerializeField]
        [Header("Sprite Swap:")]
        private PsdLayerNode highlight;

        [SerializeField]
        private PsdLayerNode press;

        [SerializeField]
        private PsdLayerNode select;

        [SerializeField]
        private PsdLayerNode disable;

        private static ButtonHelper s_ButtonHelperObfuscationSentinel;

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
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Button component = uiRoot.GetComponent<Button>();
            Image component2 = ((Component)component).GetComponent<Image>();
            PsdLayerNode obj = (((Object)(object)background != (Object)null) ? background : GetLayerNode());
            UGUIParser.Instance.ApplyImageSprite(background, component2);
            UGUIParser.ApplyNodeRectToUI(obj, component);
            Text val = FindReusableDirectTextChild(uiRoot);
            if ((Object)(object)text == (Object)null)
            {
                if ((Object)(object)val != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)((Component)val).gameObject);
                }
            }
            else
            {
                val = val ?? CloneTemplateText(uiRoot);
                UGUIParser.ApplyLegacyTextStyle(text, val);
                UGUIParser.ApplyNodeRectToUI(text, val);
            }
            bool flag = (Object)(object)highlight != (Object)null || (Object)(object)press != (Object)null || (Object)(object)select != (Object)null || (Object)(object)disable != (Object)null;
            ((Selectable)component).transition = (Selectable.Transition)((!flag) ? 1 : 2);
            if ((int)((Selectable)component).transition == 2)
            {
                bool flag2 = (int)component2.type == 1 || (int)component2.type == 2;
                SpriteState spriteState = default(SpriteState);
                spriteState.highlightedSprite = UGUIParser.ExportAndLoadSprite(highlight, flag2);
                spriteState.pressedSprite = UGUIParser.ExportAndLoadSprite(press, flag2);
                spriteState.selectedSprite = UGUIParser.ExportAndLoadSprite(select, flag2);
                spriteState.disabledSprite = UGUIParser.ExportAndLoadSprite(disable, flag2);
                ((Selectable)component).spriteState = spriteState;
            }
        }

        private Text CloneTemplateText(GameObject gameObject)
        {
            GameObject val = UGUIParser.Instance?.FindRule(GetLayerNode().UIType)?.UIPrefab;
            Text val2 = ((!((Object)(object)val != (Object)null)) ? null : val.GetComponentInChildren<Text>(true));
            if (!((Object)(object)val2 == (Object)null))
            {
                GameObject obj = Object.Instantiate<GameObject>(((Component)val2).gameObject, gameObject.transform, false);
                ((Object)obj).name = ((Object)((Component)val2).gameObject).name;
                obj.transform.SetAsLastSibling();
                return obj.GetComponent<Text>();
            }
            return null;
        }

        private static Text FindReusableDirectTextChild(object value)
        {
            if ((Object)value == (Object)null)
            {
                return null;
            }
            Transform transform = ((GameObject)value).transform;
            int num = 0;
            Text component;
            while (true)
            {
                if (num < transform.childCount)
                {
                    Transform child = transform.GetChild(num);
                    if (!((Object)(object)child == (Object)null) && !((Object)(object)((Component)child).GetComponent<PsdGeneratedKey>() != (Object)null))
                    {
                        component = ((Component)child).GetComponent<Text>();
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

        internal static bool IsButtonHelperObfuscationSentinelNull()
        {
            return (object)s_ButtonHelperObfuscationSentinel == null;
        }

        internal static ButtonHelper GetButtonHelperObfuscationSentinel()
        {
            return s_ButtonHelperObfuscationSentinel;
        }
    }
}
