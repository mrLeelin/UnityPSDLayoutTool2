using PsdProtectionGuards;
using PsdResources;
using PsdDescriptors;
using PsdReaderMetadata;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("SoCo")]
internal class SolidColorFillResourceReader : PsdResourceReader
{
	public SolidColorFillResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private SolidColorFillResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		int num = P_0.ReadInt32();
		ActionDescriptor ActionDescriptor = new ActionDescriptor(P_0, false);
		ActionDescriptor["Version"] = num;
		P_2 = ActionDescriptor;
	}
}
}
