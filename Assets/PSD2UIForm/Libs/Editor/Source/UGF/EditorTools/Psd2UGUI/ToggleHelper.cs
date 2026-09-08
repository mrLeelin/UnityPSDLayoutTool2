using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class ToggleHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode background;

	[SerializeField]
	private PsdLayerNode checkmark;

	[SerializeField]
	private PsdLayerNode label;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(background, checkmark, label);
	}

	internal override void ParseAndAttachUIElements()
	{
		background = GetLayerNode().FindChildByUiTypePriority(GUIType.Background, GUIType.Image, GUIType.RawImage);
		checkmark = GetLayerNode().FindChildByUiType(GUIType.Toggle_Checkmark);
		label = GetLayerNode().FindChildByUiTypePriority(GUIType.Toggle_Label, GUIType.Text, GUIType.TMPText);
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		Toggle component = uiRoot.GetComponent<Toggle>();
		UGUIParser.ApplyLayerRectToUiElement(GetLayerNode(), component);
		Image image = component.targetGraphic as Image;
		if (image != null)
		{
			UGUIParser.ApplyLayerRectToUiElement(background, image);
			UGUIParser.Instance.ApplyLayerSpriteToImage(background, image);
		}
		Image image2 = component.graphic as Image;
		if (image2 != null)
		{
			UGUIParser.ApplyLayerRectToUiElement(checkmark, image2);
			UGUIParser.Instance.ApplyLayerSpriteToImage(checkmark, image2);
		}
		Text text = component.transform.Find("Label")?.GetComponent<Text>();
		if (text != null)
		{
			text.gameObject.SetActive(label != null);
		}
		UGUIParser.ApplyPsdTextToUguiText(label, text);
		UGUIParser.ApplyLayerRectToUiElement(label, text);
	}

	public ToggleHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private ToggleHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
