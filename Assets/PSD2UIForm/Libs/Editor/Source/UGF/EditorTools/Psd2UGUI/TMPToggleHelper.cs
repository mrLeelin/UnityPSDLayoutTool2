using PsdProtectionGuards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class TMPToggleHelper : UIHelperBase
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
		label = GetLayerNode().FindChildByUiTypePriority(GUIType.Toggle_Label, GUIType.TMPText, GUIType.Text);
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
		TextMeshProUGUI textMeshProUGUI = component.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
		if (textMeshProUGUI != null)
		{
			textMeshProUGUI.gameObject.SetActive(label != null);
		}
		UGUIParser.ApplyPsdTextToTmpText(label, textMeshProUGUI);
		UGUIParser.ApplyLayerRectToUiElement(label, textMeshProUGUI);
	}

	public TMPToggleHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private TMPToggleHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
