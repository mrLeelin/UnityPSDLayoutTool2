using PsdProtectionGuards;
using PsdResources;
using PsdDescriptors;
using PsdReaderMetadata;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("TySh")]
internal class TextLayerResourceReader : PsdResourceReader
{
	public TextLayerResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private TextLayerResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag(7);
		wBX0tc6bIp34eGtoXLH["Version"] = P_0.ReadInt16();
		wBX0tc6bIp34eGtoXLH["Transforms"] = P_0.ReadDoubleArray(6);
		wBX0tc6bIp34eGtoXLH["TextVersion"] = P_0.ReadInt16();
		wBX0tc6bIp34eGtoXLH["Text"] = new ActionDescriptor(P_0);
		wBX0tc6bIp34eGtoXLH["WarpVersion"] = P_0.ReadInt16();
		wBX0tc6bIp34eGtoXLH["Warp"] = new ActionDescriptor(P_0);
		wBX0tc6bIp34eGtoXLH["Bounds"] = P_0.ReadDoubleArray(2);
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
