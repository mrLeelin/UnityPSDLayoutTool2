using System;
using System.Collections.Generic;
using System.Linq;
using PsdLayerUtilities;
using PsdProtectionGuards;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class LayerInfoReader : PsdSectionReader<PsdLayer[]>
{
	public LayerInfoReader(PsdBigEndianReader P_0, PsdDocument P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private LayerInfoReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, PsdDocument P_1)
		: base(P_0, true, (object)P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out PsdLayer[] P_2)
	{
		P_2 = ReadLayersAndChannels(P_0, P_1 as PsdDocument);
	}

	public static PsdLayer[] BuildLayerHierarchy(object P_0, object P_1)
	{
		Stack<PsdLayer> stack = new Stack<PsdLayer>();
		List<PsdLayer> list = new List<PsdLayer>();
		Dictionary<PsdLayer, List<PsdLayer>> dictionary = new Dictionary<PsdLayer, List<PsdLayer>>();
		foreach (PsdLayer item in ((IEnumerable<PsdLayer>)P_1).Reverse())
		{
			if (item.SectionType == SectionType.Divider)
			{
				P_0 = ((stack.Count <= 0) ? null : stack.Pop());
				continue;
			}
			if (P_0 == null)
			{
				list.Insert(0, item);
			}
			else
			{
				if (!dictionary.ContainsKey((PsdLayer)P_0))
				{
					dictionary.Add((PsdLayer)P_0, new List<PsdLayer>());
				}
				dictionary[(PsdLayer)P_0].Insert(0, item);
				item.Parent = (PsdLayer)P_0;
			}
			if (item.SectionType == SectionType.Opend || item.SectionType == SectionType.Closed)
			{
				stack.Push((PsdLayer)P_0);
				P_0 = item;
			}
		}
		foreach (KeyValuePair<PsdLayer, List<PsdLayer>> item2 in dictionary)
		{
			item2.Key.Childs = item2.Value.ToArray();
		}
		return list.ToArray();
	}

	internal static PsdLayer[] ReadLayersAndChannels(object P_0, object P_1)
	{
		int num = Math.Abs((int)((PsdBigEndianReader)P_0).ReadInt16());
		PsdLayer[] array = new PsdLayer[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = new PsdLayer((PsdBigEndianReader)P_0, (PsdDocument)P_1);
		}
		PsdLayer[] array2 = array;
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j].ReadChannels((PsdBigEndianReader)P_0);
		}
		array = BuildLayerHierarchy(null, array);
		foreach (PsdLayer item in array.SelectMany((PsdLayer item) => item.EnumerateSelfAndDescendants()).Reverse())
		{
			item.ComputeBounds();
		}
		return array;
	}
}
}
