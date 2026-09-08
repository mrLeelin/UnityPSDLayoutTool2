using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdDescriptors;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorValueList : DescriptorTypedValueCollection
{
	public DescriptorValueList(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorValueList(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0)
	{
	}
}
}
