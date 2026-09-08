using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class DropdownHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode background;

	[SerializeField]
	private PsdLayerNode label;

	[SerializeField]
	private PsdLayerNode arrow;

	[SerializeField]
	private PsdLayerNode scrollView;

	[SerializeField]
	private PsdLayerNode toggleItem;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(background, label, arrow, scrollView, toggleItem);
	}

	internal override void ParseAndAttachUIElements()
	{
		background = GetLayerNode().FindChildByUiTypePriority(GUIType.Background, GUIType.Image, GUIType.RawImage);
		label = GetLayerNode().FindChildByUiTypePriority(GUIType.Dropdown_Label, GUIType.Text, GUIType.TMPText);
		arrow = GetLayerNode().FindChildByUiType(GUIType.Dropdown_Arrow);
		scrollView = GetLayerNode().FindChildByUiType(GUIType.ScrollView);
		toggleItem = GetLayerNode().FindChildByUiType(GUIType.Toggle);
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		Dropdown component = uiRoot.GetComponent<Dropdown>();
		UGUIParser.ApplyLayerRectToUiElement(background, component);
		Image image = component.targetGraphic as Image;
		UGUIParser.Instance.ApplyLayerSpriteToImage(background, image);
		UGUIParser.ApplyPsdTextToUguiText(label, component.captionText);
		UGUIParser.ApplyLayerRectToUiElement(label, component.captionText);
		Image image2 = component.transform.Find("Arrow")?.GetComponent<Image>();
		if (image2 != null)
		{
			UGUIParser.ApplyLayerRectToUiElement(arrow, image2);
			UGUIParser.Instance.ApplyLayerSpriteToImage(arrow, image2);
		}
		if (scrollView != null)
		{
			ScrollRect componentInChildren = uiRoot.GetComponentInChildren<ScrollRect>(includeInactive: true);
			GameObject gameObject = scrollView.GetComponent<ScrollViewHelper>()?.CreateOrUpdateUiObject(componentInChildren.gameObject);
			if (gameObject != null)
			{
				RectTransform component2 = gameObject.GetComponent<RectTransform>();
				UGUIParser.ApplyLayerRectToUiElement(scrollView, component2);
			}
		}
		else
		{
			ScrollRect componentInChildren2 = uiRoot.GetComponentInChildren<ScrollRect>(includeInactive: true);
			componentInChildren2.GetComponent<Image>().enabled = false;
			if (componentInChildren2.horizontalScrollbar != null)
			{
				Scrollbar horizontalScrollbar = componentInChildren2.horizontalScrollbar;
				componentInChildren2.horizontalScrollbar = null;
				horizontalScrollbar.gameObject.SetActive(value: false);
			}
			if (componentInChildren2.verticalScrollbar != null)
			{
				Scrollbar verticalScrollbar = componentInChildren2.verticalScrollbar;
				componentInChildren2.verticalScrollbar = null;
				verticalScrollbar.gameObject.SetActive(value: false);
			}
		}
		if (toggleItem != null)
		{
			Transform transform = ((!(component.itemText != null)) ? null : component.itemText.transform.parent);
			if (transform != null)
			{
				toggleItem.GetComponent<ToggleHelper>()?.CreateOrUpdateUiObject(transform.gameObject);
			}
		}
		ScrollRect componentInChildren3 = uiRoot.GetComponentInChildren<ScrollRect>(includeInactive: true);
		if (componentInChildren3 != null)
		{
			LayoutGroup layoutGroup = componentInChildren3.content?.GetComponent<LayoutGroup>();
			if (layoutGroup != null)
			{
				layoutGroup.enabled = false;
			}
			ContentSizeFitter contentSizeFitter = componentInChildren3.content?.GetComponent<ContentSizeFitter>();
			if (contentSizeFitter != null)
			{
				contentSizeFitter.enabled = false;
			}
		}
	}

	public DropdownHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private DropdownHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
