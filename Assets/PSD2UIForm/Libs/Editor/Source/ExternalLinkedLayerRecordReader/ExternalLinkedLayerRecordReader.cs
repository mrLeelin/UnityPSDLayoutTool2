using System;
using System.IO;
using PsdProtectionGuards;
using PsdLinkedLayers;
using PsdDescriptors;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class ExternalLinkedLayerRecordReader : PsdSectionReader<ExternalLinkedPsdLayer>
{
	public ExternalLinkedLayerRecordReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private ExternalLinkedLayerRecordReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, true, (object)null)
	{
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return (P_0.ReadInt64() + 3L) & -4L;
	}

	private Uri ReadLinkedFileUri(PsdBigEndianReader P_0)
	{
		IProperties properties = new ActionDescriptor(P_0);
		if (properties.Contains("fullPath"))
		{
			Uri uri = new Uri(properties["fullPath"] as string);
			if (File.Exists(uri.LocalPath))
			{
				return uri;
			}
		}
		if (properties.Contains("relPath"))
		{
			string text = properties["relPath"] as string;
			Uri uri2 = P_0.GetDocumentResolver().ResolveUri(P_0.GetDocumentUri(), text);
			if (File.Exists(uri2.LocalPath))
			{
				return uri2;
			}
		}
		if (properties.Contains("Nm"))
		{
			string text2 = properties["Nm"] as string;
			Uri uri3 = P_0.GetDocumentResolver().ResolveUri(P_0.GetDocumentUri(), text2);
			if (File.Exists(uri3.LocalPath))
			{
				return uri3;
			}
		}
		if (properties.Contains("fullPath"))
		{
			return new Uri(properties["fullPath"] as string);
		}
		return null;
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out ExternalLinkedPsdLayer P_2)
	{
		P_0.ExpectFourCharacterCode("liFE");
		P_0.ReadInt32();
		Guid guid = new Guid(P_0.ReadPaddedPascalString(1));
		P_0.ReadUnicodeString();
		P_0.ReadFourCharacterCode();
		P_0.ReadFourCharacterCode();
		P_0.ReadInt64();
		if (P_0.ReadBoolean())
		{
			new ActionDescriptor(P_0);
		}
		Uri uri = ReadLinkedFileUri(P_0);
		P_2 = new ExternalLinkedPsdLayer(guid, P_0.GetDocumentResolver(), uri);
	}
}
}
