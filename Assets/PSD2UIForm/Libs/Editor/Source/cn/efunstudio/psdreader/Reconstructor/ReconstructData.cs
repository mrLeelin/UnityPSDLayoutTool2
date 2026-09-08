using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.Reconstructor
{

internal struct ReconstructData
{
	public Dictionary<int[], Sprite> spriteIndex;

	public Dictionary<int[], Vector2> spriteAnchors;

	public Dictionary<int[], Rect> layerBoundsIndex;

	public Vector2 documentSize;

	public Vector2 documentPivot;

	public float documentPPU;

	internal static object bHKB36Z43GxyGu8Z8DvT;

	public ReconstructData(Vector2 docSize, Vector2 docPivot, float PPU)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		spriteIndex = new Dictionary<int[], Sprite>();
		spriteAnchors = new Dictionary<int[], Vector2>();
		layerBoundsIndex = new Dictionary<int[], Rect>();
		documentPivot = docPivot;
		documentSize = docSize;
		documentPPU = PPU;
	}

	public void AddSprite(int[] layerIdx, Sprite sprite, Vector2 anchor)
	{
		spriteIndex.Add(layerIdx, sprite);
		spriteAnchors.Add(layerIdx, anchor);
	}

	internal static bool WuyPeAZ4sk1FkTdiUonJ()
	{
		return bHKB36Z43GxyGu8Z8DvT == null;
	}

	internal static object xbaATsZ4rnZF5sXmCNb3()
	{
		return bHKB36Z43GxyGu8Z8DvT;
	}
}
}
