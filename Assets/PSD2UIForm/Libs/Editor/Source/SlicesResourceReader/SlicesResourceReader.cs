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

[PsdBlockSignatureAttribute("1050", DisplayName = "Slices")]
internal class SlicesResourceReader : PsdResourceReader
{
	public SlicesResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private SlicesResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		if (P_0.ReadInt32() == 6)
		{
			P_0.ReadInt32();
			P_0.ReadInt32();
			P_0.ReadInt32();
			P_0.ReadInt32();
			P_0.ReadUnicodeString();
			int num = P_0.ReadInt32();
			List<IProperties> list = new List<IProperties>(num);
			for (int i = 0; i < num; i++)
			{
				list.Add(ReadVersion6Slice(P_0));
			}
		}
		object[] obj = ((IProperties)new ActionDescriptor(P_0))["slices.Items[0]"] as object[];
		List<IProperties> list2 = new List<IProperties>(obj.Length);
		object[] array = obj;
		foreach (object obj2 in array)
		{
			list2.Add(ConvertSliceDescriptor(obj2 as IProperties));
		}
		wBX0tc6bIp34eGtoXLH["Items"] = list2.ToArray();
		P_2 = wBX0tc6bIp34eGtoXLH;
	}

	private static PsdPropertyBag ReadVersion6Slice(object P_0)
	{
		PsdPropertyBag obj = new PsdPropertyBag
		{
			["ID"] = ((PsdBigEndianReader)P_0).ReadInt32(),
			["GroupID"] = ((PsdBigEndianReader)P_0).ReadInt32()
		};
		if (((PsdBigEndianReader)P_0).ReadInt32() == 1)
		{
			((PsdBigEndianReader)P_0).ReadInt32();
		}
		obj["Name"] = ((PsdBigEndianReader)P_0).ReadUnicodeString();
		((PsdBigEndianReader)P_0).ReadInt32();
		obj["Left"] = ((PsdBigEndianReader)P_0).ReadInt32();
		obj["Top"] = ((PsdBigEndianReader)P_0).ReadInt32();
		obj["Right"] = ((PsdBigEndianReader)P_0).ReadInt32();
		obj["Bottom"] = ((PsdBigEndianReader)P_0).ReadInt32();
		obj["Url"] = ((PsdBigEndianReader)P_0).ReadUnicodeString();
		obj["Target"] = ((PsdBigEndianReader)P_0).ReadUnicodeString();
		obj["Message"] = ((PsdBigEndianReader)P_0).ReadUnicodeString();
		obj["AltTag"] = ((PsdBigEndianReader)P_0).ReadUnicodeString();
		((PsdBigEndianReader)P_0).ReadBoolean();
		((PsdBigEndianReader)P_0).ReadUnicodeString();
		obj["HorzAlign"] = ((PsdBigEndianReader)P_0).ReadInt32();
		obj["VertAlign"] = ((PsdBigEndianReader)P_0).ReadInt32();
		obj["Alpha"] = ((PsdBigEndianReader)P_0).ReadByte();
		obj["Red"] = ((PsdBigEndianReader)P_0).ReadByte();
		obj["Green"] = ((PsdBigEndianReader)P_0).ReadByte();
		obj["Blue"] = ((PsdBigEndianReader)P_0).ReadByte();
		return obj;
	}

	private static PsdPropertyBag ConvertSliceDescriptor(object P_0)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		wBX0tc6bIp34eGtoXLH["ID"] = (int)((IProperties)P_0)["sliceID"];
		wBX0tc6bIp34eGtoXLH["GroupID"] = (int)((IProperties)P_0)["groupID"];
		if (((IProperties)P_0).Contains("Nm"))
		{
			wBX0tc6bIp34eGtoXLH["Name"] = ((IProperties)P_0)["Nm"] as string;
		}
		wBX0tc6bIp34eGtoXLH["Left"] = (int)((IProperties)P_0)["bounds.Left"];
		wBX0tc6bIp34eGtoXLH["Top"] = (int)((IProperties)P_0)["bounds.Top"];
		wBX0tc6bIp34eGtoXLH["Right"] = (int)((IProperties)P_0)["bounds.Rght"];
		wBX0tc6bIp34eGtoXLH["Bottom"] = (int)((IProperties)P_0)["bounds.Btom"];
		wBX0tc6bIp34eGtoXLH["Url"] = ((IProperties)P_0)["url"] as string;
		wBX0tc6bIp34eGtoXLH["Target"] = ((IProperties)P_0)["null"] as string;
		wBX0tc6bIp34eGtoXLH["Message"] = ((IProperties)P_0)["Msge"] as string;
		wBX0tc6bIp34eGtoXLH["AltTag"] = ((IProperties)P_0)["altTag"] as string;
		if (((IProperties)P_0).Contains("bgColor"))
		{
			wBX0tc6bIp34eGtoXLH["Alpha"] = (byte)(int)((IProperties)P_0)["bgColor.alpha"];
			wBX0tc6bIp34eGtoXLH["Red"] = (byte)(int)((IProperties)P_0)["bgColor.Rd"];
			wBX0tc6bIp34eGtoXLH["Green"] = (byte)(int)((IProperties)P_0)["bgColor.Grn"];
			wBX0tc6bIp34eGtoXLH["Blue"] = (byte)(int)((IProperties)P_0)["bgColor.Bl"];
		}
		return wBX0tc6bIp34eGtoXLH;
	}
}
}
