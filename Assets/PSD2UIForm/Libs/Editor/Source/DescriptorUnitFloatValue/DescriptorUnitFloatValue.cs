using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorUnitFloatValue : PsdPropertyBag
{
	public DescriptorUnitFloatValue()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private DescriptorUnitFloatValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base(2)
	{
	}

	public DescriptorUnitFloatValue(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorUnitFloatValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		string text = P_0.ReadFourCharacterCode();
		Add("Type", PsdBinaryDataUtilities.ParseUnitTypeCode(text));
		Add("Value", P_0.ReadDouble());
	}
}
}
