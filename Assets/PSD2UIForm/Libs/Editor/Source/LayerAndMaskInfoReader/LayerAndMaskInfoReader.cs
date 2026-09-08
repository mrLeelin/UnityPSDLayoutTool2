using PsdProtectionGuards;
using PsdSections;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdLayerData;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class LayerAndMaskInfoReader : LengthPrefixedSectionReader<PsdLayerAndMaskData>
{
	public LayerAndMaskInfoReader(PsdBigEndianReader P_0, PsdDocument P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private LayerAndMaskInfoReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, PsdDocument P_1)
		: base(P_0, (object)P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out PsdLayerAndMaskData P_2)
	{
		PsdDocument psdDocument = P_1 as PsdDocument;
		LayerInfoReader LayerInfoReader = new LayerInfoReader(P_0, psdDocument);
		if (P_0.Position + 4L >= GetSectionEndPosition())
		{
			P_2 = new PsdLayerAndMaskData(LayerInfoReader, null, new PsdPropertyBag());
			return;
		}
		GlobalLayerMaskSectionReader GlobalLayerMaskSectionReader = new GlobalLayerMaskSectionReader(P_0);
		GlobalAdditionalLayerInfoReader iuyjpx0Z5mJpKFZTwJw = new GlobalAdditionalLayerInfoReader(P_0, GetSectionEndPosition() - P_0.Position, psdDocument);
		P_2 = new PsdLayerAndMaskData(LayerInfoReader, GlobalLayerMaskSectionReader, iuyjpx0Z5mJpKFZTwJw);
	}
}
}
