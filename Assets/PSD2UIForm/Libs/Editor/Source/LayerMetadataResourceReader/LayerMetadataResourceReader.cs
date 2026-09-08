using System.Collections.Generic;
using PsdProtectionGuards;
using PsdResources;
using PsdDescriptors;
using PsdReaderMetadata;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("shmd")]
internal class LayerMetadataResourceReader : PsdResourceReader
{
	public LayerMetadataResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private LayerMetadataResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		int num = P_0.ReadInt32();
		List<ActionDescriptor> list = new List<ActionDescriptor>();
		for (int i = 0; i < num; i++)
		{
			P_0.ReadAsciiString(4);
			P_0.ReadAsciiString(4);
			P_0.ReadByte();
			P_0.ReadBytes(3);
			int num2 = P_0.ReadInt32();
			long num3 = P_0.Position;
			ActionDescriptor item = new ActionDescriptor(P_0);
			list.Add(item);
			P_0.Position = num3 + num2;
		}
		wBX0tc6bIp34eGtoXLH["Items"] = list;
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
