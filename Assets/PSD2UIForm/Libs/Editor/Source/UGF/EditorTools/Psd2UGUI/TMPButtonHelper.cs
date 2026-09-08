using PsdProtectionGuards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

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

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(background, text, highlight, press, select, disable);
	}

	internal override void ParseAndAttachUIElements()
	{
		if (GetLayerNode().LayerType == PsdLayerType.LayerGroup)
		{
			background = GetLayerNode().FindChildByUiTypePriority(GUIType.Background, GUIType.Image, GUIType.RawImage);
			text = GetLayerNode().FindChildByUiTypePriority(GUIType.Button_Text, GUIType.TMPText, GUIType.Text);
			highlight = GetLayerNode().FindChildByUiType(GUIType.Button_Highlight);
			press = GetLayerNode().FindChildByUiType(GUIType.Button_Press);
			select = GetLayerNode().FindChildByUiType(GUIType.Button_Select);
			disable = GetLayerNode().FindChildByUiType(GUIType.Button_Disable);
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
		Image component2 = component.GetComponent<Image>();
		UGUIParser.Instance.ApplyLayerSpriteToImage(background, component2);
		UGUIParser.ApplyLayerRectToUiElement(GetLayerNode(), component);
		TextMeshProUGUI componentInChildren = uiRoot.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
		if (!(text == null))
		{
			componentInChildren = componentInChildren ?? InstantiateTemplateTmpText(uiRoot);
			UGUIParser.ApplyPsdTextToTmpText(text, componentInChildren);
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

	private TextMeshProUGUI InstantiateTemplateTmpText(GameObject P_0)
	{
		GameObject gameObject = UGUIParser.Instance?.GetRuleForUiType(GetLayerNode().UIType)?.UIPrefab;
		TextMeshProUGUI textMeshProUGUI = ((gameObject != null) ? gameObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true) : null);
		if (!(textMeshProUGUI == null))
		{
			GameObject obj = Object.Instantiate(textMeshProUGUI.gameObject, P_0.transform, worldPositionStays: false);
			obj.name = textMeshProUGUI.gameObject.name;
			obj.transform.SetAsLastSibling();
			return obj.GetComponent<TextMeshProUGUI>();
		}
		return null;
	}

	public TMPButtonHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private TMPButtonHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
