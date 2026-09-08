using PsdProtectionGuards;
using TMPro;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class TMPTextHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode text;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(text);
	}

	internal override void ParseAndAttachUIElements()
	{
		if (GetLayerNode().TryGetPsdTextLayerInfo(out var _))
		{
			text = GetLayerNode();
		}
		else
		{
			GetLayerNode().SetUiType(UGUIParser.Instance.GetDefaultImageUiType());
		}
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		TextMeshProUGUI componentInChildren = uiRoot.GetComponentInChildren<TextMeshProUGUI>();
		UGUIParser.ApplyPsdTextToTmpText(text, componentInChildren);
		UGUIParser.ApplyLayerRectToUiElement(text, componentInChildren);
	}

	public TMPTextHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private TMPTextHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
