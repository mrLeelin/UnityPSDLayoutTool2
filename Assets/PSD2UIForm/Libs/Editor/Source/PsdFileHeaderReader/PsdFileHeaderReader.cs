using System;
using PsdProtectionGuards;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal class PsdFileHeaderReader : PsdSectionReader<FileHeaderSection>
{
	public PsdFileHeaderReader(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private PsdFileHeaderReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base(P_0, false, (object)null)
	{
	}

	public static FileHeaderSection ReadFileHeader(object reader)
	{
		return new PsdFileHeaderReader((PsdBigEndianReader)reader).Value;
	}

	protected override void ReadSectionValue(PsdBigEndianReader reader, object P_1, out FileHeaderSection fileHeader)
	{
		fileHeader = default(FileHeaderSection);
		fileHeader.NumberOfChannels = reader.ReadInt16();
		fileHeader.Height = reader.ReadInt32();
		fileHeader.Width = reader.ReadInt32();
		fileHeader.Depth = reader.ReadInt16();
		fileHeader.ColorMode = reader.ReadColorMode();
		if (fileHeader.Depth != 8 && fileHeader.Depth != 16)
		{
			throw new NotSupportedException("暂不支持32-bit PSD文件，请先将PSD文档转换为8-bit或16-bit后再使用。");
		}
	}
}
}
