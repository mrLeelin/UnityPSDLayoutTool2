using PsdProtectionGuards;
using PsdResources;
using PsdReaderMetadata;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("1057", DisplayName = "Version")]
internal class VersionInfoResourceReader : PsdResourceReader
{
	public VersionInfoResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private VersionInfoResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag(5);
		wBX0tc6bIp34eGtoXLH["Version"] = P_0.ReadInt32();
		wBX0tc6bIp34eGtoXLH["HasCompatibilityImage"] = P_0.ReadBoolean();
		wBX0tc6bIp34eGtoXLH["WriterName"] = P_0.ReadUnicodeString();
		wBX0tc6bIp34eGtoXLH["ReaderName"] = P_0.ReadUnicodeString();
		wBX0tc6bIp34eGtoXLH["FileVersion"] = P_0.ReadInt32();
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
