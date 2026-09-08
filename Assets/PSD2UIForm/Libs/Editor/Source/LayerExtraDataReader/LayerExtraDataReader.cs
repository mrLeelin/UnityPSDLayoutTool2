using PsdSections;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class LayerExtraDataReader : PsdSectionReader<LayerRecords>
{
	private LayerExtraDataReader(PsdBigEndianReader P_0, LayerRecords P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private LayerExtraDataReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, LayerRecords P_1)
		: base(P_0, true, (object)P_1)
	{
	}

	public static LayerRecords ReadLayerExtraData(object P_0, object P_1)
	{
		return new LayerExtraDataReader((PsdBigEndianReader)P_0, (LayerRecords)P_1).Value;
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return P_0.ReadUInt32();
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out LayerRecords P_2)
	{
		LayerRecords layerRecords = P_1 as LayerRecords;
		LayerMask layerMask = LayerMaskReader.ReadLayerMask(P_0);
		LayerBlendingRanges blendingRanges = LayerBlendingRangesReader.ReadLayerBlendingRanges(P_0);
		string name = P_0.ReadPaddedPascalString(4);
		IProperties resources = new LayerAdditionalInfoReader(P_0, GetSectionEndPosition() - P_0.Position);
		layerRecords.SetExtraRecords(layerMask, blendingRanges, resources, name);
		P_2 = layerRecords;
	}
}
}
