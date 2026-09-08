using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class MaskHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode mask;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(mask);
	}

	internal override void ParseAndAttachUIElements()
	{
		mask = GetLayerNode();
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		Image componentInChildren = uiRoot.GetComponentInChildren<Image>();
		UGUIParser.ApplyLayerRectToUiElement(mask, componentInChildren);
		UGUIParser.Instance.ApplyLayerSpriteToImage(mask, componentInChildren);
	}

	public MaskHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private MaskHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
