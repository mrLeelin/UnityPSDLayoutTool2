using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorOffsetReference : PsdPropertyBag
{
	public DescriptorOffsetReference()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private DescriptorOffsetReference(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base(4)
	{
	}

	public DescriptorOffsetReference(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorOffsetReference(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		Add("Name", P_0.ReadUnicodeString());
		Add("ClassID", P_0.ReadDescriptorKey());
		Add("Offset", P_0.ReadInt32());
	}
}
}
