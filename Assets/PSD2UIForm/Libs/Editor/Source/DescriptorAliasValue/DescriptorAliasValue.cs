using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorAliasValue : PsdPropertyBag
{
	public DescriptorAliasValue(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorAliasValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		int num = P_0.ReadInt32();
		Add("Alias", P_0.ReadAsciiString(num));
	}
}
}
