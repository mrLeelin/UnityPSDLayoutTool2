using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdBinaryUtilities;
using PsdSections;

namespace PsdSections
{

internal class ColorModeDataReader : LengthPrefixedSectionReader<byte[]>
{
	public ColorModeDataReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private ColorModeDataReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, (object)null)
	{
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return P_0.ReadInt32();
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out byte[] P_2)
	{
		if (GetSectionLength() > 0L)
		{
			P_2 = P_0.ReadBytes((int)GetSectionLength());
		}
		else
		{
			P_2 = new byte[0];
		}
	}
}
}
