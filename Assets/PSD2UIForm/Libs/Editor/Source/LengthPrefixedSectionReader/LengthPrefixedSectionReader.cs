using PsdProtectionGuards;
using PsdSections;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal abstract class LengthPrefixedSectionReader<TSectionValue> : PsdSectionReader<TSectionValue>
{
	private static object _lengthPrefixedSectionSentinel;

	protected LengthPrefixedSectionReader(PsdBigEndianReader P_0, object P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private LengthPrefixedSectionReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, object P_1)
		: base(P_0, true, P_1)
	{
	}

	protected LengthPrefixedSectionReader(PsdBigEndianReader P_0, long P_1, object P_2)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1, P_2)
	{
	}

	private LengthPrefixedSectionReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1, object P_2)
		: base(P_0, P_1, P_2)
	{
	}

	internal static bool IsLengthPrefixedSectionSentinelNull()
	{
		return _lengthPrefixedSectionSentinel == null;
	}

	internal static object GetLengthPrefixedSectionSentinel()
	{
		return _lengthPrefixedSectionSentinel;
	}
}
}
