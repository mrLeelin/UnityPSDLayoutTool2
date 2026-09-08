using PsdProtectionGuards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class TMPInputFieldHelper : UIHelperBase
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
		text = GetLayerNode().FindChildByUiTypePriority(GUIType.InputField_Text, GUIType.TMPText);
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		TMP_InputField component = uiRoot.GetComponent<TMP_InputField>();
		if (component == null)
		{
			Debug.LogWarning("TMPInputField缺少TMP_InputField组件, 已跳过初始化: " + uiRoot.name);
			return;
		}
		UGUIParser.ApplyLayerRectToUiElement(background, component);
		Image image = (component.targetGraphic as Image) ?? uiRoot.GetComponent<Image>();
		if (image != null)
		{
			component.targetGraphic = image;
			UGUIParser.Instance.ApplyLayerSpriteToImage(background, image);
		}
		TextMeshProUGUI textMeshProUGUI = component.placeholder as TextMeshProUGUI;
		if (textMeshProUGUI == null)
		{
			textMeshProUGUI = uiRoot.transform.Find("Text Area/Placeholder")?.GetComponent<TextMeshProUGUI>();
			if (textMeshProUGUI != null)
			{
				component.placeholder = textMeshProUGUI;
			}
		}
		TextMeshProUGUI textMeshProUGUI2 = component.textComponent as TextMeshProUGUI;
		if (textMeshProUGUI2 == null)
		{
			textMeshProUGUI2 = uiRoot.transform.Find("Text Area/Text")?.GetComponent<TextMeshProUGUI>();
			if (textMeshProUGUI2 != null)
			{
				component.textComponent = textMeshProUGUI2;
			}
		}
		UGUIParser.ApplyLayerRectToUiElement(placeholder, textMeshProUGUI);
		UGUIParser.ApplyLayerRectToUiElement(text, textMeshProUGUI2);
		UGUIParser.ApplyPsdTextToTmpText(placeholder, textMeshProUGUI);
		UGUIParser.ApplyPsdTextToTmpText(text, textMeshProUGUI2);
		if (text != null && text.TryBuildTextStyleData(out var PsdTextStyleInfo))
		{
			component.text = PsdTextStyleInfo.TextContent ?? string.Empty;
		}
	}

	public TMPInputFieldHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private TMPInputFieldHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
