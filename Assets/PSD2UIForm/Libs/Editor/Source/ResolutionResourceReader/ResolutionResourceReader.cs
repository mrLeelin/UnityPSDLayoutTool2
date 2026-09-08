using PsdProtectionGuards;
using PsdResources;
using PsdReaderMetadata;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("1005", DisplayName = "Resolution")]
internal class ResolutionResourceReader : PsdResourceReader
{
	public ResolutionResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private ResolutionResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag(6);
		wBX0tc6bIp34eGtoXLH["HorizontalRes"] = P_0.ReadInt16();
		wBX0tc6bIp34eGtoXLH["HorizontalResUnit"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["WidthUnit"] = P_0.ReadInt16();
		wBX0tc6bIp34eGtoXLH["VerticalRes"] = P_0.ReadInt16();
		wBX0tc6bIp34eGtoXLH["VerticalResUnit"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["HeightUnit"] = P_0.ReadInt16();
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
