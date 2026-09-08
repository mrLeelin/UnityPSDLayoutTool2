using System;
using System.Linq;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;
using PsdSections;

namespace PsdSections
{

internal class CompositeImageDataReader : LengthPrefixedSectionReader<Channel[]>
{
	public CompositeImageDataReader(PsdBigEndianReader P_0, PsdDocument P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private CompositeImageDataReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, PsdDocument P_1)
		: base(P_0, (object)P_1)
	{
	}

	protected override long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return P_0.GetLength() - P_0.Position;
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out Channel[] P_2)
	{
		P_2 = ReadCompositeChannels(P_0, P_1 as PsdDocument);
	}

	private static Channel[] ReadCompositeChannels(object P_0, object P_1)
	{
		int numberOfChannels = ((PsdDocument)P_1).FileHeaderSection.NumberOfChannels;
		int width = ((PsdDocument)P_1).Width;
		int height = ((PsdDocument)P_1).Height;
		int depth = ((PsdDocument)P_1).FileHeaderSection.Depth;
		CompressionType compressionType = (CompressionType)((PsdBigEndianReader)P_0).ReadInt16();
		ChannelType[] array = new ChannelType[4]
		{
			ChannelType.Red,
			ChannelType.Green,
			ChannelType.Blue,
			ChannelType.Alpha
		};
		Channel[] array2 = new Channel[numberOfChannels];
		for (int i = 0; i < array2.Length; i++)
		{
			ChannelType type = ((i < array.Length) ? array[i] : ChannelType.Mask);
			array2[i] = new Channel(type, width, height, 0L);
		}
		switch (compressionType)
		{
		default:
			throw new NotSupportedException();
		case CompressionType.Zip:
		case CompressionType.ZipPrediction:
		{
			int num = PsdBinaryDataUtilities.GetChannelRowByteCount(depth, width);
			int num2 = num * height;
			int num3 = num2 * numberOfChannels;
			byte[] src = PsdBinaryDataUtilities.DecompressZipData(((PsdBigEndianReader)P_0).ReadBytes(checked((int)(((PsdBigEndianReader)P_0).GetLength() - ((PsdBigEndianReader)P_0).Position))), num3);
			for (int l = 0; l < array2.Length; l++)
			{
				byte[] array3 = new byte[num2];
				Buffer.BlockCopy(src, l * num2, array3, 0, num2);
				if (compressionType == CompressionType.ZipPrediction)
				{
					PsdBinaryDataUtilities.UndoHorizontalPrediction(array3, num, height, depth);
				}
				array2[l].SetData(array3, depth);
			}
			break;
		}
		case CompressionType.Raw:
		case CompressionType.RLE:
		{
			for (int j = 0; j < array2.Length; j++)
			{
				array2[j].ReadHeader((PsdBigEndianReader)P_0, compressionType);
			}
			for (int k = 0; k < array2.Length; k++)
			{
				array2[k].Read((PsdBigEndianReader)P_0, depth, compressionType);
			}
			break;
		}
		}
		if (array2.Length == 4)
		{
			for (int m = 0; m < array2[3].Data.Length; m++)
			{
				float num4 = (float)(int)array2[3].Data[m] / 255f;
				for (int n = 0; n < 3; n++)
				{
					float num5 = (float)(int)array2[n].Data[m] / 255f;
					float num6 = (num4 + num5 - 1f) * 1f / num4;
					array2[n].Data[m] = (byte)(num6 * 255f);
				}
			}
		}
		return array2.OrderBy((Channel item) => item.Type).ToArray();
	}
}
}
