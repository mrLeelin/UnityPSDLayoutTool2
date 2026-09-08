using PsdProtectionGuards;
using PsdResources;
using PsdReaderMetadata;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("lrFX")]
internal class LegacyLayerEffectsResourceReader : PsdResourceReader
{
	public LegacyLayerEffectsResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private LegacyLayerEffectsResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		P_2 = new PsdPropertyBag();
		P_0.ReadInt16();
		int num = P_0.ReadInt16();
		for (int i = 0; i < num; i++)
		{
			P_0.ReadAsciiString(4);
			string text = P_0.ReadAsciiString(4);
			int num2 = P_0.ReadInt32();
			long num3 = P_0.Position;
			if (!(text == "dsdw"))
			{
				_ = text == "sofi";
			}
			P_0.Position = num3 + num2;
		}
	}
}
}
