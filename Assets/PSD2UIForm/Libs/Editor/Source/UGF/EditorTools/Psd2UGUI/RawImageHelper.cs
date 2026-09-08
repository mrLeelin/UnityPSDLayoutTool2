using PsdProtectionGuards;
using UnityEngine;
using UnityEngine.UI;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[DisallowMultipleComponent]
public sealed class RawImageHelper : UIHelperBase
{
	[SerializeField]
	private PsdLayerNode rawImage;

	internal override PsdLayerNode[] GetDependencies()
	{
		return CalculateDependencies(rawImage);
	}

	internal override void ParseAndAttachUIElements()
	{
		rawImage = GetLayerNode();
	}

	protected override void InitUIElements(GameObject uiRoot)
	{
		RawImage componentInChildren = uiRoot.GetComponentInChildren<RawImage>();
		UGUIParser.ApplyLayerRectToUiElement(rawImage, componentInChildren);
		componentInChildren.texture = UGUIParser.ExportAndLoadLayerTexture(rawImage);
	}

	public RawImageHelper()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private RawImageHelper(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
