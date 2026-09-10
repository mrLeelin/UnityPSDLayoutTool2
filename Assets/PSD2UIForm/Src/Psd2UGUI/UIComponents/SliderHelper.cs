using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    public sealed class SliderHelper : UIHelperBase
    {
        private readonly struct LayerRectMetrics
        {
            [CompilerGenerated]
            private readonly Vector2 m_Position;

            [CompilerGenerated]
            private readonly Vector2 m_Size;

            private static object s_ObfuscationSentinel;

            public Vector2 Size
            {
                [CompilerGenerated]
                get
                {
                    return m_Size;
                }
            }

            public float Left => GetPosition().x - GetWidth() * 0.5f;

            public float Right => GetPosition().x + GetWidth() * 0.5f;

            public float Bottom => GetPosition().y - GetHeight() * 0.5f;

            public float Top => GetPosition().y + GetHeight() * 0.5f;

            public LayerRectMetrics(Rect rect)
            {
                m_Position = rect.position;
                m_Size = rect.size;
            }

            [SpecialName]
            [CompilerGenerated]
            public Vector2 GetPosition()
            {
                return m_Position;
            }

            [SpecialName]
            public float GetWidth()
            {
                return Size.x;
            }

            [SpecialName]
            public float GetHeight()
            {
                return Size.y;
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static object GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private readonly struct SiblingOrderEntry
        {
            [CompilerGenerated]
            private readonly RectTransform m_RectTransform;

            [CompilerGenerated]
            private readonly int m_SiblingIndex;

            [CompilerGenerated]
            private readonly int m_TieBreaker;

            internal static object s_ObfuscationSentinel;

            public SiblingOrderEntry(RectTransform rectTransform, int value, int value2)
            {
                m_RectTransform = rectTransform;
                m_SiblingIndex = value;
                m_TieBreaker = value2;
            }

            [SpecialName]
            [CompilerGenerated]
            public RectTransform GetRectTransform()
            {
                return m_RectTransform;
            }

            [SpecialName]
            [CompilerGenerated]
            public int GetSiblingIndex()
            {
                return m_SiblingIndex;
            }

            [SpecialName]
            [CompilerGenerated]
            public int GetTieBreaker()
            {
                return m_TieBreaker;
            }

            public int CompareSiblingOrder(SiblingOrderEntry siblingOrderEntry)
            {
                int num = GetSiblingIndex().CompareTo(siblingOrderEntry.GetSiblingIndex());
                if (num == 0)
                {
                    return GetTieBreaker().CompareTo(siblingOrderEntry.GetTieBreaker());
                }
                return num;
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static object GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [SerializeField]
        private PsdLayerNode background;

        [SerializeField]
        private PsdLayerNode fill;

        [SerializeField]
        private PsdLayerNode handle;

        internal static SliderHelper s_SliderHelperObfuscationSentinel;

        internal override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, fill, handle);
        }

        internal override void ParseAndAttachUIElements()
        {
            background = FindOwnedNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
            fill = FindOwnedNode(GUIType.Slider_Fill);
            handle = FindOwnedNode(GUIType.Slider_Handle);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            Slider component = uiRoot.GetComponent<Slider>();
            if (!((Object)(object)component == (Object)null))
            {
                UGUIParser.ApplyNodeRectToUI(GetLayerNode(), component);
                LayerRectMetrics value = new LayerRectMetrics(GetLayerNode().GetLayerRect());
                LayerRectMetrics value2 = (((Object)(object)background != (Object)null) ? new LayerRectMetrics(background.GetLayerRect()) : value);
                LayerRectMetrics? value3 = ((!((Object)(object)fill != (Object)null)) ? ((LayerRectMetrics?)null) : new LayerRectMetrics?(new LayerRectMetrics(fill.GetLayerRect())));
                LayerRectMetrics? value4 = (((Object)(object)handle != (Object)null) ? new LayerRectMetrics?(new LayerRectMetrics(handle.GetLayerRect())) : ((LayerRectMetrics?)null));
                bool flag = value2.GetWidth() >= value2.GetHeight();
                ConfigureBackground(component);
                ConfigureTrackContainers(component, value, value2);
                ConfigureFill(component, value2, value3, flag);
                ConfigureHandle(component, value2, value4, flag);
                ConfigureDirectionAndValue(component, value2, value3, value4, flag);
            }
        }

        internal override void OnGeneratedHierarchyReady(GameObject uiRoot)
        {
            Slider val = (((Object)(object)uiRoot != (Object)null) ? uiRoot.GetComponent<Slider>() : null);
            if (!((Object)(object)val == (Object)null))
            {
                RestoreVisualSiblingOrder(val);
            }
        }

        private void ConfigureBackground(Slider value)
        {
            Transform obj = ((Component)value).transform.Find("Background");
            Image val = (((object)obj != null) ? ((Component)obj).GetComponent<Image>() : null);
            if (!((Object)(object)val == (Object)null))
            {
                PsdLayerNode psdLayerNode = background ?? GetLayerNode();
                UGUIParser.ApplyNodeRectToUI(psdLayerNode, val);
                UGUIParser.Instance.ApplyImageSprite(psdLayerNode, val);
            }
        }

        private void ConfigureTrackContainers(Slider value, LayerRectMetrics value2, LayerRectMetrics value3)
        {
            RectTransform fillRect = value.fillRect;
            Transform obj = (((object)fillRect == null) ? null : ((Transform)fillRect).parent);
            ApplyRectMetrics((obj is RectTransform) ? obj : null, value2, value3);
            RectTransform handleRect = value.handleRect;
            Transform obj2 = (((object)handleRect == null) ? null : ((Transform)handleRect).parent);
            ApplyRectMetrics((obj2 is RectTransform) ? obj2 : null, value2, value3);
        }

        private void ConfigureFill(Slider value, LayerRectMetrics value2, LayerRectMetrics? value3, bool enabled)
        {
            RectTransform fillRect = value.fillRect;
            if (!((Object)(object)fillRect == (Object)null))
            {
                Image component = ((Component)fillRect).GetComponent<Image>();
                if ((Object)(object)component != (Object)null)
                {
                    UGUIParser.Instance.ApplyImageSprite(fill, component);
                }
                if (value3.HasValue)
                {
                    AlignRectAlongSliderAxis(fillRect, value2, value3.Value, enabled);
                }
            }
        }

        private void ConfigureHandle(Slider value2, LayerRectMetrics value3, LayerRectMetrics? value4, bool enabled)
        {
            RectTransform handleRect = value2.handleRect;
            Image val = (((object)handleRect != null) ? ((Component)handleRect).GetComponent<Image>() : null);
            if ((Object)(object)handleRect == (Object)null || (Object)(object)val == (Object)null)
            {
                return;
            }
            bool flag = (Object)(object)handle == (Object)null;
            ((Component)val).gameObject.SetActive(!flag);
            ((Selectable)value2).transition = flag ? Selectable.Transition.None : Selectable.Transition.ColorTint;
            ((Selectable)value2).interactable = !flag;
            UGUIParser.Instance.ApplyImageSprite(handle, val);
            if (value4.HasValue)
            {
                LayerRectMetrics value = value4.Value;
                handleRect.SetSizeWithCurrentAnchors((RectTransform.Axis)0, value.GetWidth());
                handleRect.SetSizeWithCurrentAnchors((RectTransform.Axis)1, value.GetHeight());
                Vector2 anchoredPosition = handleRect.anchoredPosition;
                if (enabled)
                {
                    anchoredPosition.y = value.GetPosition().y - value3.GetPosition().y;
                }
                else
                {
                    anchoredPosition.x = value.GetPosition().x - value3.GetPosition().x;
                }
                handleRect.anchoredPosition = anchoredPosition;
            }
        }

        private void ConfigureDirectionAndValue(Slider value, LayerRectMetrics value2, LayerRectMetrics? value3, LayerRectMetrics? value4, bool enabled)
        {
            Slider.Direction direction;
            float num = CalculateDirectionAndNormalizedValue(value2, value3, value4, enabled, out direction);
            value.direction = direction;
            float valueWithoutNotify = Mathf.Lerp(value.minValue, value.maxValue, num);
            value.SetValueWithoutNotify(valueWithoutNotify);
        }

        private static void ApplyRectMetrics(object value, LayerRectMetrics value2, LayerRectMetrics value3)
        {
            if (!((Object)value == (Object)null))
            {
                Vector2 val = default(Vector2);
                val = new Vector2(0.5f, 0.5f);
                ((RectTransform)value).anchorMax = val;
                ((RectTransform)value).anchorMin = val;
                ((RectTransform)value).pivot = new Vector2(0.5f, 0.5f);
                ((RectTransform)value).anchoredPosition = value3.GetPosition() - value2.GetPosition();
                ((RectTransform)value).SetSizeWithCurrentAnchors((RectTransform.Axis)0, value3.GetWidth());
                ((RectTransform)value).SetSizeWithCurrentAnchors((RectTransform.Axis)1, value3.GetHeight());
            }
        }

        private static void AlignRectAlongSliderAxis(object value, LayerRectMetrics value2, LayerRectMetrics value3, bool enabled)
        {
            Vector2 anchoredPosition = ((RectTransform)value).anchoredPosition;
            if (enabled)
            {
                anchoredPosition.y = value3.GetPosition().y - value2.GetPosition().y;
                ((RectTransform)value).SetSizeWithCurrentAnchors((RectTransform.Axis)1, value3.GetHeight());
            }
            else
            {
                anchoredPosition.x = value3.GetPosition().x - value2.GetPosition().x;
                ((RectTransform)value).SetSizeWithCurrentAnchors((RectTransform.Axis)0, value3.GetWidth());
            }
            ((RectTransform)value).anchoredPosition = anchoredPosition;
        }

        private static float CalculateDirectionAndNormalizedValue(LayerRectMetrics value3, LayerRectMetrics? value4, LayerRectMetrics? value5, bool enabled, out Slider.Direction result)
        {
            result = (Slider.Direction)((!enabled) ? 2 : 0);
            float num = (enabled ? value3.GetWidth() : value3.GetHeight());
            if (num <= Mathf.Epsilon)
            {
                return 1f;
            }
            if (value4.HasValue)
            {
                LayerRectMetrics value = value4.Value;
                float num2 = (enabled ? value3.Left : value3.Bottom);
                float num3 = (enabled ? value3.Right : value3.Top);
                float num4 = (enabled ? value.Left : value.Bottom);
                float num5 = (enabled ? value.Right : value.Top);
                bool flag = Mathf.Abs(num4 - num2) <= Mathf.Abs(num3 - num5);
                result = (Slider.Direction)((!enabled) ? (flag ? 2 : 3) : ((!flag) ? 1 : 0));
                return Mathf.Clamp01((flag ? (num5 - num2) : (num3 - num4)) / num);
            }
            if (value5.HasValue)
            {
                LayerRectMetrics value2 = value5.Value;
                float num6 = (enabled ? value3.Left : value3.Bottom);
                return Mathf.Clamp01(((enabled ? value2.GetPosition().x : value2.GetPosition().y) - num6) / num);
            }
            return 1f;
        }

        private void RestoreVisualSiblingOrder(Slider value)
        {
            if ((Object)(object)value == (Object)null)
            {
                return;
            }
            SiblingOrderEntry[] array = new SiblingOrderEntry[3];
            int num = AppendSiblingOrderEntry(array, 0, FindDirectChildRectTransform(((Component)value).transform, "Background"), background, 0);
            int num2 = num;
            RectTransform fillRect = value.fillRect;
            Transform obj = (((object)fillRect != null) ? ((Transform)fillRect).parent : null);
            num = AppendSiblingOrderEntry(array, num2, (RectTransform)(object)((obj is RectTransform) ? obj : null), fill, 1);
            int num3 = num;
            RectTransform handleRect = value.handleRect;
            Transform obj2 = (((object)handleRect == null) ? null : ((Transform)handleRect).parent);
            num = AppendSiblingOrderEntry(array, num3, (RectTransform)(object)((obj2 is RectTransform) ? obj2 : null), handle, 2);
            SortSiblingOrderEntries(array, num);
            for (int i = 0; i < num; i++)
            {
                int num4 = 0;
                for (int j = 0; j < i; j++)
                {
                    if (array[j].GetSiblingIndex() == array[i].GetSiblingIndex())
                    {
                        num4++;
                    }
                }
                ((Transform)array[i].GetRectTransform()).SetSiblingIndex(array[i].GetSiblingIndex() + num4);
            }
        }

        private int AppendSiblingOrderEntry(SiblingOrderEntry[] values, int value, RectTransform rectTransform, PsdLayerNode layerNode, int value2)
        {
            if (values != null && value < values.Length && !((Object)(object)rectTransform == (Object)null) && !((Object)(object)layerNode == (Object)null))
            {
                Transform val = FindDirectOwnedChildTransform(layerNode);
                if (!((Object)(object)val == (Object)null))
                {
                    values[value++] = new SiblingOrderEntry(rectTransform, val.GetSiblingIndex(), value2);
                    return value;
                }
                return value;
            }
            return value;
        }

        private static void SortSiblingOrderEntries(object value, int value2)
        {
            for (int i = 0; i < value2 - 1; i++)
            {
                int num = i;
                for (int j = i + 1; j < value2; j++)
                {
                    if (((SiblingOrderEntry[])value)[j].CompareSiblingOrder(((SiblingOrderEntry[])value)[num]) < 0)
                    {
                        num = j;
                    }
                }
                if (num != i)
                {
                    SiblingOrderEntry value3 = ((SiblingOrderEntry[])value)[i];
                    ((SiblingOrderEntry[])value)[i] = ((SiblingOrderEntry[])value)[num];
                    ((SiblingOrderEntry[])value)[num] = value3;
                }
            }
        }

        private Transform FindDirectOwnedChildTransform(PsdLayerNode layerNode)
        {
            if (!((Object)(object)layerNode == (Object)null) && !((Object)(object)GetLayerNode() == (Object)null))
            {
                Transform transform = ((Component)GetLayerNode()).transform;
                Transform val = ((Component)layerNode).transform;
                while ((Object)(object)val.parent != (Object)null && (Object)(object)val.parent != (Object)(object)transform)
                {
                    val = val.parent;
                }
                if (!((Object)(object)val.parent == (Object)(object)transform))
                {
                    return null;
                }
                return val;
            }
            return null;
        }

        private static RectTransform FindDirectChildRectTransform(object value, object value2)
        {
            if ((Object)value == (Object)null || string.IsNullOrEmpty((string)value2))
            {
                return null;
            }
            int num = 0;
            Transform child;
            while (true)
            {
                if (num < ((Transform)value).childCount)
                {
                    child = ((Transform)value).GetChild(num);
                    if ((Object)(object)child != (Object)null && ((Object)child).name == (string)value2)
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return null;
            }
            return (RectTransform)(object)((child is RectTransform) ? child : null);
        }

        internal static bool IsSliderHelperObfuscationSentinelNull()
        {
            return (object)s_SliderHelperObfuscationSentinel == null;
        }

        internal static SliderHelper GetSliderHelperObfuscationSentinel()
        {
            return s_SliderHelperObfuscationSentinel;
        }
    }
}
