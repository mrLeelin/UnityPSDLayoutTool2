using PsdProtectionGuards;
using PsdReaderMetadata;
using PsdResources;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("lnk3")]
internal class EmbeddedLinkedLayersV3ResourceReader : EmbeddedLinkedLayersResourceReader
{
	public EmbeddedLinkedLayersV3ResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private EmbeddedLinkedLayersV3ResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}
}
}
