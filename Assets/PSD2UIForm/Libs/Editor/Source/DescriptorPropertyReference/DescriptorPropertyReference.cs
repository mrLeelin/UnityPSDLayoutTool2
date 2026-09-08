using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorPropertyReference : PsdPropertyBag
{
	public DescriptorPropertyReference()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private DescriptorPropertyReference(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base(3)
	{
	}

	public DescriptorPropertyReference(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorPropertyReference(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		Add("Name", P_0.ReadUnicodeString());
		Add("ClassID", P_0.ReadDescriptorKey());
		Add("KeyID", P_0.ReadDescriptorKey());
	}
}
}
