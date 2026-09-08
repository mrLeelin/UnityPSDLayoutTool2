using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class FillColorHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode fillColor;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(fillColor);
	}

	internal override void ParseAndAttachUIElements()
	{
		if (GetLayerNode().UIType != GUIType.FillColor && GetLayerNode().LayerType != PsdLayerType.FillLayer)
		{
			GetLayerNode().SetUiType(UGUIParser.Instance.GetDefaultImageUiType());
		}
		else
		{
			fillColor = GetLayerNode();
		}
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		RawImage componentInChildren = uiRoot.GetComponentInChildren<RawImage>();
		UGUIParser.ApplyLayerRectToUiElement(fillColor, componentInChildren);
		componentInChildren.color = UGUIParser.GetLayerColorOrDefault(fillColor, componentInChildren.color);
	}

	public FillColorHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private FillColorHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
