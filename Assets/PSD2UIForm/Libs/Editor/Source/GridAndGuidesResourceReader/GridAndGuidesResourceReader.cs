using System.Collections.Generic;
using PsdProtectionGuards;
using PsdResources;
using PsdReaderMetadata;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("1032", DisplayName = "GridAndGuides")]
internal class GridAndGuidesResourceReader : PsdResourceReader
{
	public GridAndGuidesResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private GridAndGuidesResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		if (P_0.ReadInt32() != 1)
		{
			throw new PsdInvalidDataException();
		}
		wBX0tc6bIp34eGtoXLH["HorizontalGrid"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["VerticalGrid"] = P_0.ReadInt32();
		int num = P_0.ReadInt32();
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		for (int i = 0; i < num; i++)
		{
			int item = P_0.ReadInt32();
			if (P_0.ReadByte() == 0)
			{
				list2.Add(item);
			}
			else
			{
				list.Add(item);
			}
		}
		wBX0tc6bIp34eGtoXLH["HorizontalGuides"] = list.ToArray();
		wBX0tc6bIp34eGtoXLH["VerticalGuides"] = list2.ToArray();
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
