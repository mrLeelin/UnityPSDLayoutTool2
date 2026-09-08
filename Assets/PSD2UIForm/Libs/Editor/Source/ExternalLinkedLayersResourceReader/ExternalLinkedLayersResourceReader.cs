using System.Collections.Generic;
using PsdProtectionGuards;
using PsdLinkedLayers;
using PsdResources;
using PsdSections;
using PsdReaderMetadata;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdResources
{

[PsdBlockSignatureAttribute("lnkE")]
internal class ExternalLinkedLayersResourceReader : PsdResourceReader
{
	public ExternalLinkedLayersResourceReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private ExternalLinkedLayersResourceReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out IProperties P_2)
	{
		PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
		List<ExternalLinkedPsdLayer> list = new List<ExternalLinkedPsdLayer>();
		while (P_0.Position < GetSectionEndPosition())
		{
			ExternalLinkedLayerRecordReader ExternalLinkedLayerRecordReader = new ExternalLinkedLayerRecordReader(P_0);
			list.Add(ExternalLinkedLayerRecordReader.Value);
		}
		wBX0tc6bIp34eGtoXLH["Items"] = list.ToArray();
		P_2 = wBX0tc6bIp34eGtoXLH;
	}
}
}
