using UnityEngine;

namespace cn.efunstudio.psdreader.Reconstructor
{

internal interface IReconstructor
{
	string DisplayName { get; }

	string HelpMessage { get; }

	bool CanReconstruct(GameObject selection);

	GameObject Reconstruct(ImportLayerData root, ReconstructData data, GameObject selection);
}
}
