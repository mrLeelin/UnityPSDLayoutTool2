using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorEnumValue : PsdPropertyBag
{
	public DescriptorEnumValue()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private DescriptorEnumValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base(2)
	{
	}

	public DescriptorEnumValue(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorEnumValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		Add("Type", P_0.ReadDescriptorKey());
		Add("Value", P_0.ReadDescriptorKey());
	}
}
}
