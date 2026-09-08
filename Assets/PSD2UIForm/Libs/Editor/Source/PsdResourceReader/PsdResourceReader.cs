using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdSections;
using PsdBinaryUtilities;

namespace PsdResources
{

internal abstract class PsdResourceReader : PropertySectionReader
{
	public PsdResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private PsdResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1, null)
	{
	}
}
}
