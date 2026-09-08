using System;
using PsdProtectionGuards;
using PsdDescriptors;
using PsdSections;
using PsdProtectionRuntime;
using PsdLinkedLayers;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class EmbeddedLinkedLayerRecordReader : PsdSectionReader<EmbeddedLinkedPsdLayer>
{
	public EmbeddedLinkedLayerRecordReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private EmbeddedLinkedLayerRecordReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, true, (object)null)
	{
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return (P_0.ReadInt64() + 3L) & -4L;
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out EmbeddedLinkedPsdLayer P_2)
	{
		P_0.ExpectFourCharacterCode("liFD");
		P_0.ReadInt32();
		Guid guid = new Guid(P_0.ReadPaddedPascalString(1));
		string text = P_0.ReadUnicodeString();
		P_0.ReadFourCharacterCode();
		P_0.ReadFourCharacterCode();
		long num = P_0.ReadInt64();
		if (P_0.ReadBoolean())
		{
			new ActionDescriptor(P_0);
		}
		bool flag = HasPsdSignature(P_0);
		EmbeddedPsdDocumentReader o43Uwi0LTvZSERxWFxp = null;
		EmbeddedPsdHeaderReader jqJuns0CSFZNvcPdXKJ = null;
		if (num > 0L && flag)
		{
			long num2 = P_0.Position;
			o43Uwi0LTvZSERxWFxp = new EmbeddedPsdDocumentReader(P_0, num);
			P_0.Position = num2;
			jqJuns0CSFZNvcPdXKJ = new EmbeddedPsdHeaderReader(P_0, num);
		}
		P_2 = new EmbeddedLinkedPsdLayer(text, guid, o43Uwi0LTvZSERxWFxp, jqJuns0CSFZNvcPdXKJ);
	}

	private bool HasPsdSignature(PsdBigEndianReader P_0)
	{
		long num = P_0.Position;
		try
		{
			return P_0.ReadFourCharacterCode() == "8BPS";
		}
		finally
		{
			P_0.Position = num;
		}
	}
}
}
