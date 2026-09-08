using System.Collections.Generic;
using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdPropertyUtilities;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class DescriptorTypedValueCollection : PsdPropertyBag
{
	public DescriptorTypedValueCollection(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private DescriptorTypedValueCollection(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		List<object> list = new List<object>();
		int num = P_0.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			object item = PsdDescriptorValueFactory.ReadDescriptorValue(P_0.ReadFourCharacterCode(), P_0);
			list.Add(item);
		}
		Add("Items", list.ToArray());
	}
}
}
