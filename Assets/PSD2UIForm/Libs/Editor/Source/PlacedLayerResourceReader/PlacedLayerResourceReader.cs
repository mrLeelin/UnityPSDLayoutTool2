using PsdProtectionGuards;
using PsdResources;
using PsdDescriptors;
using PsdReaderMetadata;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("PlLd")]
internal class PlacedLayerResourceReader : PsdResourceReader
{
	public PlacedLayerResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private PlacedLayerResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		P_0.ExpectNamedFourCharacterCode("plcL", "LayerResource PlLd");
		wBX0tc6bIp34eGtoXLH["Version"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["UniqueID"] = P_0.ReadPaddedPascalString(1);
		wBX0tc6bIp34eGtoXLH["PageNumbers"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["Pages"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["AntiAlias"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["LayerType"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["Transformation"] = P_0.ReadDoubleArray(8);
		wBX0tc6bIp34eGtoXLH["WarpVersion"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["Warp"] = new ActionDescriptor(P_0);
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
