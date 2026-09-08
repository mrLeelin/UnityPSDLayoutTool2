using PsdProtectionGuards;
using PsdSections;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class GlobalLayerMaskSectionReader : PsdSectionReader<object>
{
	public GlobalLayerMaskSectionReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private GlobalLayerMaskSectionReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, true, (object)null)
	{
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return P_0.ReadInt32();
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out object P_2)
	{
		P_2 = new object();
	}
}
}
