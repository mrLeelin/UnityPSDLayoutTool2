using PsdProtectionGuards;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class LayerMaskReader : PsdSectionReader<LayerMask>
{
	private LayerMaskReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private LayerMaskReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, true, (object)null)
	{
	}

	public static LayerMask ReadLayerMask(object P_0)
	{
		return new LayerMaskReader((PsdBigEndianReader)P_0).Value;
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return P_0.ReadInt32();
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out LayerMask P_2)
	{
		LayerMask layerMask = new LayerMask();
		layerMask.Top = P_0.ReadInt32();
		layerMask.Left = P_0.ReadInt32();
		layerMask.Bottom = P_0.ReadInt32();
		layerMask.Right = P_0.ReadInt32();
		layerMask.Color = P_0.ReadByte();
		layerMask.Flag = P_0.ReadByte();
		P_2 = layerMask;
	}
}
}
