using System;
using System.IO;
using PsdSections;
using PsdDocumentResolution;
using PsdBinaryUtilities;

namespace cn.efunstudio.psdreader.PsdParser
{

internal struct FileHeaderSection
{
	internal static object kuQcXsZfZYnUyQiKyoBh;

	public int Depth { get; set; }

	public int NumberOfChannels { get; set; }

	public ColorMode ColorMode { get; set; }

	public int Height { get; set; }

	public int Width { get; set; }

	public static FileHeaderSection FromFile(string filename)
	{
		using FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
		using PsdBigEndianReader PsdBigEndianReader = new PsdBigEndianReader(fileStream, new CachedFilePsdDocumentResolver(), new Uri(Path.GetDirectoryName(filename)));
		PsdBigEndianReader.ReadSignatureAndVersion();
		return PsdFileHeaderReader.ReadFileHeader(PsdBigEndianReader);
	}

	internal static bool FbfisrZfOaZhEFWEhZn6()
	{
		return kuQcXsZfZYnUyQiKyoBh == null;
	}

	internal static object aWaq4kZfhXDLfmqhWjeE()
	{
		return kuQcXsZfZYnUyQiKyoBh;
	}
}
}
