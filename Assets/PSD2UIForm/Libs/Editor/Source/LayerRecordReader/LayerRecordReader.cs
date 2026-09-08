using PsdProtectionGuards;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class LayerRecordReader : PsdSectionReader<LayerRecords>
{
	private LayerRecordReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private LayerRecordReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, false, (object)null)
	{
	}

	public static LayerRecords ReadLayerRecord(object P_0)
	{
		return new LayerRecordReader((PsdBigEndianReader)P_0).Value;
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out LayerRecords P_2)
	{
		LayerRecords layerRecords = new LayerRecords();
		layerRecords.Top = P_0.ReadInt32();
		layerRecords.Left = P_0.ReadInt32();
		layerRecords.Bottom = P_0.ReadInt32();
		layerRecords.Right = P_0.ReadInt32();
		layerRecords.ValidateSize();
		int num = (layerRecords.ChannelCount = P_0.ReadUInt16());
		for (int i = 0; i < num; i++)
		{
			layerRecords.Channels[i].Type = P_0.ReadChannelType();
			layerRecords.Channels[i].Size = P_0.ReadVersionedLength();
			layerRecords.Channels[i].Width = layerRecords.Width;
			layerRecords.Channels[i].Height = layerRecords.Height;
		}
		P_0.ExpectPsdBlockSignature();
		layerRecords.BlendMode = P_0.ReadBlendMode();
		layerRecords.Opacity = P_0.ReadByte();
		layerRecords.Clipping = P_0.ReadBoolean();
		layerRecords.Flags = P_0.ReadLayerFlags();
		layerRecords.Filter = P_0.ReadByte();
		P_2 = layerRecords;
	}
}
}
