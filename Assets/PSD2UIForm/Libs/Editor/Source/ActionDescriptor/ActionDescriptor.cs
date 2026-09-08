using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdPropertyUtilities;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class ActionDescriptor : PsdPropertyBag
{
	private readonly int _descriptorVersion;

	public ActionDescriptor(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private ActionDescriptor(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: this(P_0, true)
	{
	}

	public ActionDescriptor(PsdBigEndianReader P_0, bool P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private ActionDescriptor(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, bool P_1)
		: base()
	{
		if (P_1)
		{
			_descriptorVersion = P_0.ReadInt32();
		}
		Add("Name", P_0.ReadUnicodeString());
		Add("ClassID", P_0.ReadDescriptorKey());
		int num = P_0.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			string text = P_0.ReadDescriptorKey();
			string text2 = P_0.ReadFourCharacterCode();
			if (text == "EngineData")
			{
				Add(text.Trim(), new TextEngineData(P_0));
				continue;
			}
			object value = PsdDescriptorValueFactory.ReadDescriptorValue(text2, P_0);
			Add(text.Trim(), value);
		}
	}
}
}
