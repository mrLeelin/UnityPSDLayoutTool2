using System.IO;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;
using PsdSections;

namespace PsdSections
{

internal class LayerChannelDataReader : LengthPrefixedSectionReader<Channel[]>
{
	public LayerChannelDataReader(PsdBigEndianReader P_0, long P_1, PsdLayer P_2)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1, P_2)
	{
	}

	private LayerChannelDataReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1, PsdLayer P_2)
		: base(P_0, P_1, (object)P_2)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out Channel[] P_2)
	{
		PsdLayer psdLayer = P_1 as PsdLayer;
		LayerRecords records = psdLayer.Records;
		using (MemoryStream memoryStream = new MemoryStream(P_0.ReadBytes((int)GetSectionLength())))
		{
			using PsdBigEndianReader PsdBigEndianReader = new PsdBigEndianReader(memoryStream, P_0.GetDocumentResolver(), P_0.GetDocumentUri());
			PsdBigEndianReader.Version = P_0.Version;
			ReadLayerChannels(PsdBigEndianReader, psdLayer.Depth, records.Channels);
		}
		P_2 = records.Channels;
	}

	private void ReadLayerChannels(PsdBigEndianReader P_0, int P_1, Channel[] P_2)
	{
		foreach (Channel channel in P_2)
		{
			CompressionType compressionType = P_0.ReadCompressionType();
			channel.ReadHeader(P_0, compressionType);
			channel.Read(P_0, P_1, compressionType, checked((int)channel.Size) - 2);
		}
	}
}
}
