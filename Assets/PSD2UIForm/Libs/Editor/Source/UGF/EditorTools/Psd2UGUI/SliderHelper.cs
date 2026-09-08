using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class SliderHelper : UIHelperBase
{
	private readonly struct CenteredLayerBounds
	{
		[CompilerGenerated]
		private readonly Vector2 _center;

		[CompilerGenerated]
		private readonly Vector2 _size;

		private static object iTOMrUZj1JpVC2PUZhan;

		public Vector2 Size
		{
			[CompilerGenerated]
			get
			{
				return _size;
			}
		}

		public float Left => GetCenter().x - GetWidth() * 0.5f;

		public float Right => GetCenter().x + GetWidth() * 0.5f;

		public float Bottom => GetCenter().y - GetHeight() * 0.5f;

		public float Top => GetCenter().y + GetHeight() * 0.5f;

		public CenteredLayerBounds(Rect P_0)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			_center = P_0.position;
			_size = P_0.size;
		}

		[SpecialName]
		[CompilerGenerated]
		public Vector2 GetCenter()
		{
			return _center;
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

		internal static bool Pve7KLZjZ9Swviau1ba4()
		{
			return iTOMrUZj1JpVC2PUZhan == null;
		}

		internal static object vkDjhQZjOUiNwO91uSYA()
		{
			return iTOMrUZj1JpVC2PUZhan;
		}
	}

	[SerializeField]
	private PsdLayerNode background;

	[SerializeField]
	private PsdLayerNode fill;

	[SerializeField]
	private PsdLayerNode handle;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(background, fill, handle);
	}

	internal override void ParseAndAttachUIElements()
	{
		background = GetLayerNode().FindChildByUiTypePriority(GUIType.Background, GUIType.Image, GUIType.RawImage);
		fill = GetLayerNode().FindChildByUiType(GUIType.Slider_Fill);
		handle = GetLayerNode().FindChildByUiType(GUIType.Slider_Handle);
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		Slider component = uiRoot.GetComponent<Slider>();
		if (!(component == null))
		{
			UGUIParser.ApplyLayerRectToUiElement(GetLayerNode(), component);
			CenteredLayerBounds CenteredLayerBounds = new CenteredLayerBounds(GetLayerNode().GetLayerBounds());
			CenteredLayerBounds e7afxMUvDbkFI0K4NTQ2 = ((background != null) ? new CenteredLayerBounds(background.GetLayerBounds()) : CenteredLayerBounds);
			CenteredLayerBounds? e7afxMUvDbkFI0K4NTQ3 = ((!(fill != null)) ? ((CenteredLayerBounds?)null) : new CenteredLayerBounds?(new CenteredLayerBounds(fill.GetLayerBounds())));
			CenteredLayerBounds? e7afxMUvDbkFI0K4NTQ4 = ((!(handle != null)) ? ((CenteredLayerBounds?)null) : new CenteredLayerBounds?(new CenteredLayerBounds(handle.GetLayerBounds())));
			bool flag = e7afxMUvDbkFI0K4NTQ2.GetWidth() >= e7afxMUvDbkFI0K4NTQ2.GetHeight();
			ApplySliderBackground(component);
			LayoutSliderTrackContainers(component, CenteredLayerBounds, e7afxMUvDbkFI0K4NTQ2);
			ApplySliderFill(component, e7afxMUvDbkFI0K4NTQ2, e7afxMUvDbkFI0K4NTQ3, flag);
			ApplySliderHandle(component, e7afxMUvDbkFI0K4NTQ2, e7afxMUvDbkFI0K4NTQ4, flag);
			InitializeSliderDirectionAndValue(component, e7afxMUvDbkFI0K4NTQ2, e7afxMUvDbkFI0K4NTQ3, e7afxMUvDbkFI0K4NTQ4, flag);
		}
	}

	private void ApplySliderBackground(Slider P_0)
	{
		Image image = P_0.transform.Find("Background")?.GetComponent<Image>();
		if (!(image == null))
		{
			PsdLayerNode psdLayerNode = background ?? GetLayerNode();
			UGUIParser.ApplyLayerRectToUiElement(psdLayerNode, image);
			UGUIParser.Instance.ApplyLayerSpriteToImage(psdLayerNode, image);
		}
	}

	private void LayoutSliderTrackContainers(Slider P_0, CenteredLayerBounds P_1, CenteredLayerBounds P_2)
	{
		LayoutCenteredTrackContainer(P_0.fillRect?.parent as RectTransform, P_1, P_2);
		LayoutCenteredTrackContainer(P_0.handleRect?.parent as RectTransform, P_1, P_2);
	}

	private void ApplySliderFill(Slider P_0, CenteredLayerBounds P_1, CenteredLayerBounds? P_2, bool P_3)
	{
		RectTransform fillRect = P_0.fillRect;
		if (!(fillRect == null))
		{
			Image component = fillRect.GetComponent<Image>();
			if (component != null)
			{
				UGUIParser.Instance.ApplyLayerSpriteToImage(fill, component);
			}
			if (P_2.HasValue)
			{
				AlignSliderFillCrossAxis(fillRect, P_1, P_2.Value, P_3);
			}
		}
	}

	private void ApplySliderHandle(Slider P_0, CenteredLayerBounds P_1, CenteredLayerBounds? P_2, bool P_3)
	{
		RectTransform handleRect = P_0.handleRect;
		Image image = handleRect?.GetComponent<Image>();
		if (handleRect == null || image == null)
		{
			return;
		}
		bool flag = handle == null;
		image.gameObject.SetActive(!flag);
		P_0.transition = ((!flag) ? Selectable.Transition.ColorTint : Selectable.Transition.None);
		P_0.interactable = !flag;
		UGUIParser.Instance.ApplyLayerSpriteToImage(handle, image);
		if (P_2.HasValue)
		{
			CenteredLayerBounds value = P_2.Value;
			handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, value.GetWidth());
			handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, value.GetHeight());
			Vector2 anchoredPosition = handleRect.anchoredPosition;
			if (P_3)
			{
				anchoredPosition.y = value.GetCenter().y - P_1.GetCenter().y;
			}
			else
			{
				anchoredPosition.x = value.GetCenter().x - P_1.GetCenter().x;
			}
			handleRect.anchoredPosition = anchoredPosition;
		}
	}

	private void InitializeSliderDirectionAndValue(Slider P_0, CenteredLayerBounds P_1, CenteredLayerBounds? P_2, CenteredLayerBounds? P_3, bool P_4)
	{
		Slider.Direction direction;
		float t = CalculateSliderNormalizedValue(P_1, P_2, P_3, P_4, out direction);
		P_0.direction = direction;
		float valueWithoutNotify = Mathf.Lerp(P_0.minValue, P_0.maxValue, t);
		P_0.SetValueWithoutNotify(valueWithoutNotify);
	}

	private static void LayoutCenteredTrackContainer(object P_0, CenteredLayerBounds P_1, CenteredLayerBounds P_2)
	{
		if (!((Object)P_0 == null))
		{
			Vector2 anchorMin = (((RectTransform)P_0).anchorMax = new Vector2(0.5f, 0.5f));
			((RectTransform)P_0).anchorMin = anchorMin;
			((RectTransform)P_0).pivot = new Vector2(0.5f, 0.5f);
			((RectTransform)P_0).anchoredPosition = P_2.GetCenter() - P_1.GetCenter();
			((RectTransform)P_0).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, P_2.GetWidth());
			((RectTransform)P_0).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, P_2.GetHeight());
		}
	}

	private static void AlignSliderFillCrossAxis(object P_0, CenteredLayerBounds P_1, CenteredLayerBounds P_2, bool P_3)
	{
		Vector2 anchoredPosition = ((RectTransform)P_0).anchoredPosition;
		if (!P_3)
		{
			anchoredPosition.x = P_2.GetCenter().x - P_1.GetCenter().x;
			((RectTransform)P_0).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, P_2.GetWidth());
		}
		else
		{
			anchoredPosition.y = P_2.GetCenter().y - P_1.GetCenter().y;
			((RectTransform)P_0).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, P_2.GetHeight());
		}
		((RectTransform)P_0).anchoredPosition = anchoredPosition;
	}

	private static float CalculateSliderNormalizedValue(CenteredLayerBounds P_0, CenteredLayerBounds? P_1, CenteredLayerBounds? P_2, bool P_3, out Slider.Direction P_4)
	{
		P_4 = ((!P_3) ? Slider.Direction.BottomToTop : Slider.Direction.LeftToRight);
		float num = (P_3 ? P_0.GetWidth() : P_0.GetHeight());
		if (num <= Mathf.Epsilon)
		{
			return 1f;
		}
		if (P_1.HasValue)
		{
			CenteredLayerBounds value = P_1.Value;
			float num2 = (P_3 ? P_0.Left : P_0.Bottom);
			float num3 = (P_3 ? P_0.Right : P_0.Top);
			float num4 = (P_3 ? value.Left : value.Bottom);
			float num5 = (P_3 ? value.Right : value.Top);
			bool flag = Mathf.Abs(num4 - num2) <= Mathf.Abs(num3 - num5);
			P_4 = ((!P_3) ? (flag ? Slider.Direction.BottomToTop : Slider.Direction.TopToBottom) : ((!flag) ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight));
			return Mathf.Clamp01((flag ? (num5 - num2) : (num3 - num4)) / num);
		}
		if (P_2.HasValue)
		{
			CenteredLayerBounds value2 = P_2.Value;
			float num6 = (P_3 ? P_0.Left : P_0.Bottom);
			return Mathf.Clamp01(((P_3 ? value2.GetCenter().x : value2.GetCenter().y) - num6) / num);
		}
		return 1f;
	}

	public SliderHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private SliderHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
