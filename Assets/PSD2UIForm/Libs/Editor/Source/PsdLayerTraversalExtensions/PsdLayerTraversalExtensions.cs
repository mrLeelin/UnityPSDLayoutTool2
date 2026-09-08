using System.Collections.Generic;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdLayerUtilities
{

internal static class PsdLayerTraversalExtensions
{
	internal static IEnumerable<PsdLayer> EnumerateSelfAndDescendants(this object P_0)
	{
		yield return (PsdLayer)P_0;
		PsdLayer[] childs = ((PsdLayer)P_0).Childs;
		foreach (PsdLayer psdLayer in childs)
		{
			foreach (PsdLayer item in psdLayer.EnumerateSelfAndDescendants())
			{
				yield return item;
			}
		}
	}
}
}
