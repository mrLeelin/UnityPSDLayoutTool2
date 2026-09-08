using System.Collections.Generic;
using System.Linq;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdLayerUtilities
{

internal static class PsdLayerGeometryExtensions
{
	internal static string GetLayerName(this object P_0)
	{
		if (P_0 != null)
		{
			return ((PsdLayer)P_0).Name ?? string.Empty;
		}
		return string.Empty;
	}

	internal static PsdLayerType GetUiLayerType(this object P_0)
	{
		if (P_0 == null)
		{
			return PsdLayerType.Unknown;
		}
		if (!((PsdLayer)P_0).IsTextLayer())
		{
			if (!((PsdLayer)P_0).IsSolidFillLayer())
			{
				if (!((PsdLayer)P_0).IsGroup)
				{
					return PsdLayerType.Layer;
				}
				return PsdLayerType.LayerGroup;
			}
			return PsdLayerType.FillLayer;
		}
		return PsdLayerType.TextLayer;
	}

	internal static Rect GetCenteredLayerRect(this object P_0)
	{
		if (P_0 != null && ((PsdLayer)P_0).Document != null)
		{
			Vector2Int documentSize = new Vector2Int(((PsdLayer)P_0).Document.Width, ((PsdLayer)P_0).Document.Height);
			return ConvertPsdBoundsToCenteredRect(((PsdLayer)P_0).Left, ((PsdLayer)P_0).Top, ((PsdLayer)P_0).Right, ((PsdLayer)P_0).Bottom, documentSize);
		}
		return default(Rect);
	}

	internal static Rect ConvertPsdBoundsToCenteredRect(int P_0, int P_1, int P_2, int P_3, Vector2Int P_4)
	{
		float num = (float)Mathf.Abs(P_2 - P_0) * 0.5f;
		float num2 = (float)Mathf.Abs(P_3 - P_1) * 0.5f;
		return new Rect((float)P_0 + num - (float)P_4.x * 0.5f, (float)P_4.y - ((float)P_1 + num2) - (float)P_4.y * 0.5f, P_2 - P_0, P_3 - P_1);
	}

	internal static PsdLayer FindLayerByRecordIndex(this object P_0, int P_1)
	{
		if (P_0 != null && P_1 >= 0)
		{
			int num = 0;
			return FindLayerRecordInChildren(((PsdDocument)P_0).Childs.OfType<PsdLayer>(), P_1, ref num);
		}
		return null;
	}

	private static PsdLayer FindLayerRecordInChildren(IEnumerable<PsdLayer> P_0, int P_1, ref int P_2)
	{
		if (P_0 == null)
		{
			return null;
		}
		foreach (PsdLayer item in P_0)
		{
			PsdLayer psdLayer = FindLayerRecordInSubtree(item, P_1, ref P_2);
			if (psdLayer != null)
			{
				return psdLayer;
			}
		}
		return null;
	}

	private static PsdLayer FindLayerRecordInSubtree(object P_0, int P_1, ref int P_2)
	{
		if (P_0 == null)
		{
			return null;
		}
		if (((PsdLayer)P_0).IsGroup)
		{
			P_2++;
			PsdLayer psdLayer = FindLayerRecordInChildren(((PsdLayer)P_0).Childs, P_1, ref P_2);
			if (psdLayer != null)
			{
				return psdLayer;
			}
		}
		if (P_2 == P_1)
		{
			P_2++;
			return (PsdLayer)P_0;
		}
		P_2++;
		return null;
	}

	internal static int CountDocumentLayers(this object P_0)
	{
		if (P_0 != null && ((PsdDocument)P_0).Childs != null)
		{
			return CountDescendantLayers(((PsdDocument)P_0).Childs.OfType<PsdLayer>());
		}
		return 0;
	}

	internal static int CountLayerRecords(this object P_0)
	{
		if (P_0 == null)
		{
			return 0;
		}
		if (((PsdLayer)P_0).IsGroup)
		{
			return CountChildLayerRecords(((PsdLayer)P_0).Childs) + 2;
		}
		return 1;
	}

	internal static int CountDocumentLayerRecords(this object P_0)
	{
		if (P_0 != null && ((PsdDocument)P_0).Childs != null)
		{
			return CountChildLayerRecords(((PsdDocument)P_0).Childs.OfType<PsdLayer>());
		}
		return 0;
	}

	private static int CountDescendantLayers(IEnumerable<PsdLayer> P_0)
	{
		int num = 0;
		if (P_0 == null)
		{
			return num;
		}
		foreach (PsdLayer item in P_0)
		{
			num++;
			if (item != null && item.Childs != null && item.Childs.Length != 0)
			{
				num += CountDescendantLayers(item.Childs);
			}
		}
		return num;
	}

	private static int CountChildLayerRecords(IEnumerable<PsdLayer> P_0)
	{
		int num = 0;
		if (P_0 == null)
		{
			return num;
		}
		foreach (PsdLayer item in P_0)
		{
			num += item.CountLayerRecords();
		}
		return num;
	}
}
}
