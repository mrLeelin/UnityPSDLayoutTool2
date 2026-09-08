using System.Collections.Generic;
using PsdProtectionGuards;
using PsdResources;
using PsdReaderMetadata;
using PsdDescriptors;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdLinkedLayers;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("lnkD")]
internal class EmbeddedLinkedLayersResourceReader : PsdResourceReader
{
	public EmbeddedLinkedLayersResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private EmbeddedLinkedLayersResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		List<EmbeddedLinkedPsdLayer> list = new List<EmbeddedLinkedPsdLayer>();
		while (P_0.Position < GetSectionEndPosition())
		{
			EmbeddedLinkedLayerRecordReader dmLNVt0ImlRlt2XsnHd = new EmbeddedLinkedLayerRecordReader(P_0);
			list.Add(dmLNVt0ImlRlt2XsnHd.Value);
		}
		wBX0tc6bIp34eGtoXLH["Items"] = list.ToArray();
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
