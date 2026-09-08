using PsdProtectionGuards;
using PsdResources;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

internal class UnknownPsdResourceReader : PsdResourceReader
{
	public UnknownPsdResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private UnknownPsdResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		P_2 = new PsdPropertyBag();
	}
}
}
