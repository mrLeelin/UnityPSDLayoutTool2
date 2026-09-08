using System.Linq;
using PsdProtectionGuards;
using PsdResources;
using PsdSections;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdReaderMetadata;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class GlobalAdditionalLayerInfoReader : PropertySectionReader
{
	private static string[] _psbLongLengthTags;

	public GlobalAdditionalLayerInfoReader(PsdBigEndianReader P_0, long P_1, PsdDocument P_2)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1, P_2)
	{
	}

	private GlobalAdditionalLayerInfoReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1, PsdDocument P_2)
		: base(P_0, P_1, P_2)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdDocument psdDocument = P_1 as PsdDocument;
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		while (P_0.Position < GetSectionEndPosition())
		{
			P_0.ExpectPsdOrPsbBlockSignature(true);
			string text = P_0.ReadFourCharacterCode();
			long num = ReadAlignedAdditionalInfoLength(P_0, text);
			long num2 = P_0.Position + num;
			string text2 = PsdTaggedBlockFactory.GetSignatureDisplayName(text);
			if ((text == "Lr16" || text == "Lr32") && psdDocument != null)
			{
				PsdPropertyBag wBX0tc6bIp34eGtoXLH2 = new PsdPropertyBag(1);
				wBX0tc6bIp34eGtoXLH2["Layers"] = LayerInfoReader.ReadLayersAndChannels(P_0, psdDocument);
				wBX0tc6bIp34eGtoXLH[text2] = wBX0tc6bIp34eGtoXLH2;
				P_0.Position = num2;
			}
			else
			{
				PsdResourceReader kbmtLHLHSfTRwWr0ZQR = PsdTaggedBlockFactory.CreateTaggedBlockReader(text, P_0, num);
				wBX0tc6bIp34eGtoXLH[text2] = kbmtLHLHSfTRwWr0ZQR;
			}
		}
		P_2 = wBX0tc6bIp34eGtoXLH;
	}

	private long ReadAlignedAdditionalInfoLength(PsdBigEndianReader P_0, string P_1)
	{
		long num = 0L;
		num = ((!_psbLongLengthTags.Contains(P_1) || P_0.Version != 2) ? P_0.ReadInt32() : P_0.ReadInt64());
		return (num + 3L) & -4L;
	}

	static GlobalAdditionalLayerInfoReader()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_psbLongLengthTags = new string[17]
		{
			"LMsk", "Lr16", "Lr32", "Layr", "Mt16", "Mt32", "Mtrn", "Alph", "FMsk", "lnk2",
			"lnk3", "lnkD", "FEid", "FXid", "PxSD", "lnkE", "extd"
		};
	}
}
}
