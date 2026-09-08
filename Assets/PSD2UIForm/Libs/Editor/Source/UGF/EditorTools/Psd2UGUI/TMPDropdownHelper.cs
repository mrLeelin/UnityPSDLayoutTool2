using PsdProtectionGuards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class TMPDropdownHelper : UIHelperBase
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
		label = GetLayerNode().FindChildByUiTypePriority(GUIType.Dropdown_Label, GUIType.TMPText);
		arrow = GetLayerNode().FindChildByUiType(GUIType.Dropdown_Arrow);
		scrollView = GetLayerNode().FindChildByUiType(GUIType.ScrollView);
		toggleItem = GetLayerNode().FindChildByUiTypePriority(GUIType.TMPToggle, GUIType.Toggle);
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		TMP_Dropdown component = uiRoot.GetComponent<TMP_Dropdown>();
		UGUIParser.ApplyLayerRectToUiElement(background, component);
		Image image = component.targetGraphic as Image;
		UGUIParser.Instance.ApplyLayerSpriteToImage(background, image);
		UGUIParser.ApplyPsdTextToTmpText(label, component.captionText as TextMeshProUGUI);
		UGUIParser.ApplyLayerRectToUiElement(label, component.captionText);
		Image image2 = component.transform.Find("Arrow")?.GetComponent<Image>();
		if (image2 != null)
		{
			UGUIParser.ApplyLayerRectToUiElement(arrow, image2);
			UGUIParser.Instance.ApplyLayerSpriteToImage(arrow, image2);
		}
		if (!(scrollView != null))
		{
			ScrollRect componentInChildren = uiRoot.GetComponentInChildren<ScrollRect>(includeInactive: true);
			componentInChildren.GetComponent<Image>().enabled = false;
			if (componentInChildren.horizontalScrollbar != null)
			{
				Scrollbar horizontalScrollbar = componentInChildren.horizontalScrollbar;
				componentInChildren.horizontalScrollbar = null;
				horizontalScrollbar.gameObject.SetActive(value: false);
			}
			if (componentInChildren.verticalScrollbar != null)
			{
				Scrollbar verticalScrollbar = componentInChildren.verticalScrollbar;
				componentInChildren.verticalScrollbar = null;
				verticalScrollbar.gameObject.SetActive(value: false);
			}
		}
		else
		{
			ScrollRect componentInChildren2 = uiRoot.GetComponentInChildren<ScrollRect>(includeInactive: true);
			GameObject gameObject = scrollView.GetComponent<ScrollViewHelper>()?.CreateOrUpdateUiObject(componentInChildren2.gameObject);
			if (gameObject != null)
			{
				RectTransform component2 = gameObject.GetComponent<RectTransform>();
				UGUIParser.ApplyLayerRectToUiElement(scrollView, component2);
			}
		}
		if (toggleItem != null)
		{
			Transform transform = ((component.itemText != null) ? component.itemText.transform.parent : null);
			if (transform != null)
			{
				toggleItem.GetComponent<UIHelperBase>()?.CreateOrUpdateUiObject(transform.gameObject);
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

	public TMPDropdownHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private TMPDropdownHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
