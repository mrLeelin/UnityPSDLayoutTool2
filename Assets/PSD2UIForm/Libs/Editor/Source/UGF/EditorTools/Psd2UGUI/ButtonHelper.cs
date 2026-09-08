using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class ButtonHelper : UIHelperBase
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

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(background, text, highlight, press, select, disable);
	}

	internal override void ParseAndAttachUIElements()
	{
		if (GetLayerNode().LayerType == PsdLayerType.LayerGroup)
		{
			background = GetLayerNode().FindChildByUiTypePriority(GUIType.Background, GUIType.Image, GUIType.RawImage);
			text = GetLayerNode().FindChildByUiTypePriority(GUIType.Button_Text, GUIType.Text, GUIType.TMPText);
			highlight = GetLayerNode().FindChildByUiType(GUIType.Button_Highlight);
			press = GetLayerNode().FindChildByUiType(GUIType.Button_Press);
			select = GetLayerNode().FindChildByUiType(GUIType.Button_Select);
			disable = GetLayerNode().FindChildByUiType(GUIType.Button_Disable);
		}
		else
		{
			background = GetLayerNode();
		}
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		Button component = uiRoot.GetComponent<Button>();
		Image component2 = component.GetComponent<Image>();
		UGUIParser.Instance.ApplyLayerSpriteToImage(background, component2);
		UGUIParser.ApplyLayerRectToUiElement(GetLayerNode(), component);
		Text componentInChildren = uiRoot.GetComponentInChildren<Text>(includeInactive: true);
		if (!(text == null))
		{
			componentInChildren = componentInChildren ?? InstantiateTemplateText(uiRoot);
			UGUIParser.ApplyPsdTextToUguiText(text, componentInChildren);
			UGUIParser.ApplyLayerRectToUiElement(text, componentInChildren);
		}
		else if (componentInChildren != null)
		{
			Object.DestroyImmediate(componentInChildren.gameObject);
		}
		bool flag = highlight != null || press != null || select != null || disable != null;
		component.transition = ((!flag) ? Selectable.Transition.ColorTint : Selectable.Transition.SpriteSwap);
		if (component.transition == Selectable.Transition.SpriteSwap)
		{
			bool flag2 = component2.type == Image.Type.Sliced || component2.type == Image.Type.Tiled;
			component.spriteState = new SpriteState
			{
				highlightedSprite = UGUIParser.ExportAndLoadLayerSprite(highlight, flag2),
				pressedSprite = UGUIParser.ExportAndLoadLayerSprite(press, flag2),
				selectedSprite = UGUIParser.ExportAndLoadLayerSprite(select, flag2),
				disabledSprite = UGUIParser.ExportAndLoadLayerSprite(disable, flag2)
			};
		}
	}

	private Text InstantiateTemplateText(GameObject P_0)
	{
		GameObject gameObject = UGUIParser.Instance?.GetRuleForUiType(GetLayerNode().UIType)?.UIPrefab;
		Text text = ((!(gameObject != null)) ? null : gameObject.GetComponentInChildren<Text>(includeInactive: true));
		if (text == null)
		{
			return null;
		}
		GameObject obj = Object.Instantiate(text.gameObject, P_0.transform, worldPositionStays: false);
		obj.name = text.gameObject.name;
		obj.transform.SetAsLastSibling();
		return obj.GetComponent<Text>();
	}

	public ButtonHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private ButtonHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
