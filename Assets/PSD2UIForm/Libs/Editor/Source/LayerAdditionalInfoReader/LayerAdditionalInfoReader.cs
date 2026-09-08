using PsdProtectionGuards;
using PsdResources;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdSections;
using PsdReaderMetadata;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class LayerAdditionalInfoReader : PropertySectionReader
{
	public LayerAdditionalInfoReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private LayerAdditionalInfoReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1, null)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		while (P_0.Position < GetSectionEndPosition())
		{
			P_0.ExpectPsdBlockSignature();
			string text = P_0.ReadFourCharacterCode();
			long num = P_0.ReadInt32();
			num += num % 2L;
			PsdResourceReader kbmtLHLHSfTRwWr0ZQR = PsdTaggedBlockFactory.CreateTaggedBlockReader(text, P_0, num);
			string text2 = PsdTaggedBlockFactory.GetSignatureDisplayName(text);
			wBX0tc6bIp34eGtoXLH[text2] = kbmtLHLHSfTRwWr0ZQR;
		}
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
