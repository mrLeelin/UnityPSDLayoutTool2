using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class InputFieldHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode background;

	[SerializeField]
	private PsdLayerNode placeholder;

	[SerializeField]
	private PsdLayerNode text;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(background, placeholder, text);
	}

	internal override void ParseAndAttachUIElements()
	{
		background = GetLayerNode().FindChildByUiTypePriority(GUIType.Background, GUIType.Image, GUIType.RawImage);
		placeholder = GetLayerNode().FindChildByUiType(GUIType.InputField_Placeholder);
		text = GetLayerNode().FindChildByUiTypePriority(GUIType.InputField_Text, GUIType.Text);
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		InputField component = uiRoot.GetComponent<InputField>();
		UGUIParser.ApplyLayerRectToUiElement(background, component);
		Image image = component.targetGraphic as Image;
		UGUIParser.Instance.ApplyLayerSpriteToImage(background, image);
		UGUIParser.ApplyLayerRectToUiElement(placeholder, component.placeholder);
		UGUIParser.ApplyLayerRectToUiElement(text, component.textComponent);
		UGUIParser.ApplyPsdTextToUguiText(placeholder, component.placeholder as Text);
		component.text = UGUIParser.ApplyPsdTextToUguiText(text, component.textComponent).TextContent;
	}

	public InputFieldHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private InputFieldHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
