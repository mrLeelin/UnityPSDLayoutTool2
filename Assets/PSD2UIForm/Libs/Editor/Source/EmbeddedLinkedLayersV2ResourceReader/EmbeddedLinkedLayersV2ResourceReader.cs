using PsdProtectionGuards;
using PsdReaderMetadata;
using PsdResources;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("lnk2")]
internal class EmbeddedLinkedLayersV2ResourceReader : EmbeddedLinkedLayersResourceReader
{
	public EmbeddedLinkedLayersV2ResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private EmbeddedLinkedLayersV2ResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}
}
}
