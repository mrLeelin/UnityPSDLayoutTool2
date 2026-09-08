using System;
using PsdDescriptors;
using PsdBinaryUtilities;

namespace PsdPropertyUtilities
{

internal static class PsdDescriptorValueFactory
{
	public static object ReadDescriptorValue(object P_0, object P_1)
	{
		return P_0 switch
		{
			"obj " => new DescriptorReferenceList((PsdBigEndianReader)P_1), 
			"tdta" => ReadRawDataValue(P_1), 
			"UntF" => new DescriptorUnitFloatValue((PsdBigEndianReader)P_1), 
			"doub" => ((PsdBigEndianReader)P_1).ReadDouble(), 
			"indx" => ReadIndexValue(P_1), 
			"type" => new DescriptorClassValue((PsdBigEndianReader)P_1), 
			"Objc" => new ActionDescriptor((PsdBigEndianReader)P_1, false), 
			"ObAr" => new DescriptorObjectArrayValue((PsdBigEndianReader)P_1), 
			"TEXT" => ((PsdBigEndianReader)P_1).ReadUnicodeString(), 
			"VlLs" => new DescriptorValueList((PsdBigEndianReader)P_1), 
			"Idnt" => ReadIdentifierValue(P_1), 
			"long" => ((PsdBigEndianReader)P_1).ReadInt32(), 
			"name" => ReadNameValue(P_1), 
			"enum" => new DescriptorEnumValue((PsdBigEndianReader)P_1), 
			"prop" => new DescriptorPropertyReference((PsdBigEndianReader)P_1), 
			"Clss" => new DescriptorClassValue((PsdBigEndianReader)P_1), 
			"bool" => ((PsdBigEndianReader)P_1).ReadBoolean(), 
			"alis" => new DescriptorAliasValue((PsdBigEndianReader)P_1), 
			"GlbO" => new ActionDescriptor((PsdBigEndianReader)P_1, false), 
			"GlbC" => new DescriptorClassValue((PsdBigEndianReader)P_1), 
			"Enmr" => new DescriptorEnumValue((PsdBigEndianReader)P_1), 
			"comp" => ((PsdBigEndianReader)P_1).ReadInt64(), 
			"rele" => new DescriptorOffsetReference((PsdBigEndianReader)P_1), 
			_ => throw new NotSupportedException((string)P_0), 
		};
	}

	private static PsdPropertyBag ReadRawDataValue(object P_0)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag(1);
		int num = ((PsdBigEndianReader)P_0).ReadInt32();
		wBX0tc6bIp34eGtoXLH.Add("Data", ((PsdBigEndianReader)P_0).ReadBytes(num));
		return wBX0tc6bIp34eGtoXLH;
	}

	private static PsdPropertyBag ReadIdentifierValue(object P_0)
	{
		return new PsdPropertyBag(3)
		{
			{
				"Name",
				((PsdBigEndianReader)P_0).ReadUnicodeString()
			},
			{
				"ClassID",
				((PsdBigEndianReader)P_0).ReadDescriptorKey()
			},
			{
				"Identifier",
				((PsdBigEndianReader)P_0).ReadInt32()
			}
		};
	}

	private static PsdPropertyBag ReadIndexValue(object P_0)
	{
		return new PsdPropertyBag(3)
		{
			{
				"Name",
				((PsdBigEndianReader)P_0).ReadUnicodeString()
			},
			{
				"ClassID",
				((PsdBigEndianReader)P_0).ReadDescriptorKey()
			},
			{
				"Index",
				((PsdBigEndianReader)P_0).ReadInt32()
			}
		};
	}

	private static PsdPropertyBag ReadNameValue(object P_0)
	{
		return new PsdPropertyBag(3)
		{
			{
				"Name",
				((PsdBigEndianReader)P_0).ReadUnicodeString()
			},
			{
				"ClassID",
				((PsdBigEndianReader)P_0).ReadDescriptorKey()
			},
			{
				"Value",
				((PsdBigEndianReader)P_0).ReadUnicodeString()
			}
		};
	}
}
}
