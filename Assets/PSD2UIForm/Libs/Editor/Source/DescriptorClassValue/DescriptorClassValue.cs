using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorClassValue : PsdPropertyBag
{
	public DescriptorClassValue()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private DescriptorClassValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base(2)
	{
	}

	public DescriptorClassValue(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorClassValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		Add("Name", P_0.ReadUnicodeString());
		Add("ClassID", P_0.ReadDescriptorKey());
	}
}
}
