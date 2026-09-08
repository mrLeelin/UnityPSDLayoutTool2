using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;

namespace PsdDescriptors
{

internal class DescriptorStringValue : PsdPropertyBag
{
	public DescriptorStringValue(string P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorStringValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0)
		: base()
	{
		Add("Value", P_0);
	}
}
}
