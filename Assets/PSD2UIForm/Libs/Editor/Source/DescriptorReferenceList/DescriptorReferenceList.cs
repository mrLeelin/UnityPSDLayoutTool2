using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdDescriptors;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorReferenceList : DescriptorTypedValueCollection
{
	public DescriptorReferenceList(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorReferenceList(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0)
	{
	}
}
}
