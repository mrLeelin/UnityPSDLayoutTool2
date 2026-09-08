using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class TextHelper : UIHelperBase
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
		Text componentInChildren = uiRoot.GetComponentInChildren<Text>();
		UGUIParser.ApplyPsdTextToUguiText(text, componentInChildren);
		UGUIParser.ApplyLayerRectToUiElement(text, componentInChildren);
	}

	public TextHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private TextHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
