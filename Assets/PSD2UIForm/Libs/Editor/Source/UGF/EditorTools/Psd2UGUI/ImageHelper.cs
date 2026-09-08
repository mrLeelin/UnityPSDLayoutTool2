using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class ImageHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode image;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(image);
	}

	internal override void ParseAndAttachUIElements()
	{
		image = GetLayerNode();
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		Image componentInChildren = uiRoot.GetComponentInChildren<Image>();
		UGUIParser.ApplyLayerRectToUiElement(image, componentInChildren);
		UGUIParser.Instance.ApplyLayerSpriteToImage(image, componentInChildren);
	}

	public ImageHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private ImageHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
