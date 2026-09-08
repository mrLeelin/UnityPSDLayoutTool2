using PsdProtectionGuards;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using PsdProtectionRuntime;

namespace ga0lXEJuJwIsOHyWxS
{

internal sealed class rmJEJCoirKTmfOKTDP : AssetPostprocessor
{
	private static void HandleImportedAssetChanges(object P_0, object P_1, object P_2, object P_3)
	{
		if (Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])P_0) || Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])P_1) || Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])P_2) || Psd2UIFormConfigRepair.ContainsRelevantAsset((string[])P_3))
		{
			Psd2UIFormConfigRepair.ScheduleEnsureConfigReady();
		}
	}

	public rmJEJCoirKTmfOKTDP()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private rmJEJCoirKTmfOKTDP(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
