using System.Collections.Generic;
using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorObjectArrayValue : PsdPropertyBag
{
	public DescriptorObjectArrayValue(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorObjectArrayValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		P_0.ReadInt32();
		Add("Name", P_0.ReadUnicodeString());
		Add("ClassID", P_0.ReadDescriptorKey());
		int num = P_0.ReadInt32();
		List<PsdPropertyBag> list = new List<PsdPropertyBag>();
		for (int i = 0; i < num; i++)
		{
			PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag
			{
				{
					"Type1",
					P_0.ReadDescriptorKey()
				},
				{
					"EnumName",
					P_0.ReadFourCharacterCode()
				},
				{
					"Type2",
					PsdBinaryDataUtilities.ParseUnitTypeCode(P_0.ReadFourCharacterCode())
				}
			};
			int num2 = P_0.ReadInt32();
			wBX0tc6bIp34eGtoXLH.Add("Values", P_0.ReadDoubleArray(num2));
			list.Add(wBX0tc6bIp34eGtoXLH);
		}
		Add("items", list.ToArray());
	}
}
}
