using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class ScrollViewHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode background;

	[SerializeField]
	private PsdLayerNode viewport;

	[SerializeField]
	private PsdLayerNode horizontalBarBG;

	[SerializeField]
	private PsdLayerNode horizontalBar;

	[SerializeField]
	private PsdLayerNode verticalBarBG;

	[SerializeField]
	private PsdLayerNode verticalBar;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(background, viewport, horizontalBarBG, horizontalBar, verticalBarBG, verticalBar);
	}

	internal override void ParseAndAttachUIElements()
	{
		background = GetLayerNode().FindChildByUiTypePriority(GUIType.Background, GUIType.Image, GUIType.RawImage);
		viewport = GetLayerNode().FindChildByUiTypePriority(GUIType.ScrollView_Viewport, GUIType.Mask);
		horizontalBarBG = GetLayerNode().FindChildByUiType(GUIType.ScrollView_HorizontalBarBG);
		horizontalBar = GetLayerNode().FindChildByUiType(GUIType.ScrollView_HorizontalBar);
		verticalBarBG = GetLayerNode().FindChildByUiType(GUIType.ScrollView_VerticalBarBG);
		verticalBar = GetLayerNode().FindChildByUiType(GUIType.ScrollView_VerticalBar);
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		ScrollRect component = uiRoot.GetComponent<ScrollRect>();
		UGUIParser.ApplyLayerRectToUiElement(background, component);
		Image component2 = component.GetComponent<Image>();
		if (component2 != null)
		{
			UGUIParser.Instance.ApplyLayerSpriteToImage(background, component2);
			if (viewport == null)
			{
				component.viewport.GetComponent<Image>().sprite = component2.sprite;
			}
		}
		if (viewport != null)
		{
			Image component3 = component.viewport.GetComponent<Image>();
			UGUIParser.Instance.ApplyLayerSpriteToImage(viewport, component3);
		}
		Scrollbar horizontalScrollbar = component.horizontalScrollbar;
		Scrollbar verticalScrollbar = component.verticalScrollbar;
		if (horizontalBarBG != null && horizontalScrollbar != null)
		{
			Image component4 = horizontalScrollbar.GetComponent<Image>();
			UGUIParser.Instance.ApplyLayerSpriteToImage(horizontalBarBG, component4);
			UGUIParser.ApplyLayerRectToUiElement(horizontalBarBG, component4);
			UGUIParser.ApplyLayerRectToUiElement(background, component.content, true, true, false);
		}
		else
		{
			Scrollbar horizontalScrollbar2 = component.horizontalScrollbar;
			component.horizontalScrollbar = null;
			if (horizontalScrollbar2 != null)
			{
				horizontalScrollbar2.gameObject.SetActive(value: false);
			}
		}
		if (verticalBarBG != null && verticalScrollbar != null)
		{
			Image component5 = verticalScrollbar.GetComponent<Image>();
			UGUIParser.Instance.ApplyLayerSpriteToImage(verticalBarBG, component5);
			UGUIParser.ApplyLayerRectToUiElement(verticalBarBG, component5);
			UGUIParser.ApplyLayerRectToUiElement(background, component.content, true, false);
		}
		else
		{
			Scrollbar verticalScrollbar2 = component.verticalScrollbar;
			component.verticalScrollbar = null;
			if (verticalScrollbar2 != null)
			{
				verticalScrollbar2.gameObject.SetActive(value: false);
			}
		}
		if (horizontalBar != null && horizontalScrollbar != null)
		{
			Image image = horizontalScrollbar.targetGraphic as Image;
			UGUIParser.Instance.ApplyLayerSpriteToImage(horizontalBar, image);
			UGUIParser.ApplyLayerRectToUiElement(horizontalBar, image, false, false, false);
		}
		if (verticalBar != null && verticalScrollbar != null)
		{
			Image image2 = verticalScrollbar.targetGraphic as Image;
			UGUIParser.Instance.ApplyLayerSpriteToImage(verticalBar, image2);
			UGUIParser.ApplyLayerRectToUiElement(verticalBar, image2, false, false, false);
		}
	}

	public ScrollViewHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private ScrollViewHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
