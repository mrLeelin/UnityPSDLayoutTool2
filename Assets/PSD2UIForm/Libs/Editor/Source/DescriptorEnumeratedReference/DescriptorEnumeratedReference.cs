using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorEnumeratedReference : PsdPropertyBag
{
	public DescriptorEnumeratedReference()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private DescriptorEnumeratedReference(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base(4)
	{
	}

	public DescriptorEnumeratedReference(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorEnumeratedReference(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		Add("Name", P_0.ReadUnicodeString());
		Add("ClassID", P_0.ReadDescriptorKey());
		Add("TypeID", P_0.ReadDescriptorKey());
		Add("EnumID", P_0.ReadDescriptorKey());
	}
}
}
