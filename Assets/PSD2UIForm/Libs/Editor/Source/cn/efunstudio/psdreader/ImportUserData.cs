using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

internal class ImportUserData
{
	public NamingConvention fileNaming;

	public GroupMode groupMode;

	public string PackingTag;

	public string TargetDirectory;

	public bool AutoImport;

	public SpriteAlignment DefaultAlignment;

	public Vector2 DefaultPivot;

	public ScaleFactor ScaleFactor;

	public SpriteAlignment DocAlignment;

	public Vector2 DocPivot;

	public ImportLayerData DocRoot;

	public ImportLayerData GetLayerData(int[] layerIdx)
	{
		if (DocRoot == null)
		{
			return null;
		}
		ImportLayerData importLayerData = DocRoot;
		foreach (int num in layerIdx)
		{
			if (num >= 0 && num < importLayerData.Childs.Count)
			{
				importLayerData = importLayerData.Childs[num];
				continue;
			}
			return null;
		}
		return importLayerData;
	}

	public ImportUserData()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		DefaultPivot = new Vector2(0.5f, 0.5f);
		DocPivot = new Vector2(0.5f, 0.5f);
	}
}
}
