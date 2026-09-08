using PsdProtectionGuards;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class LayerBlendingRangesReader : PsdSectionReader<LayerBlendingRanges>
{
	private LayerBlendingRangesReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private LayerBlendingRangesReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, true, (object)null)
	{
	}

	public static LayerBlendingRanges ReadLayerBlendingRanges(object P_0)
	{
		return new LayerBlendingRangesReader((PsdBigEndianReader)P_0).Value;
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return P_0.ReadInt32();
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out LayerBlendingRanges P_2)
	{
		P_2 = new LayerBlendingRanges();
	}
}
}
