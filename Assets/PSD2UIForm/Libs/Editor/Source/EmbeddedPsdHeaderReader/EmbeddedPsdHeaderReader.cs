using System.IO;
using PsdProtectionGuards;
using PsdSections;
using PsdBinaryUtilities;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdSections
{

internal class EmbeddedPsdHeaderReader : LengthPrefixedSectionReader<FileHeaderSection>
{
	public EmbeddedPsdHeaderReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private EmbeddedPsdHeaderReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1, (object)null)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader reader, object P_1, out FileHeaderSection fileHeader)
	{
		if (HasPsdSignature(reader))
		{
			using (Stream embeddedPsdStream = new ReadOnlySubstream(reader.GetBaseStream(), reader.Position, GetSectionLength()))
			{
				using PsdBigEndianReader embeddedReader = new PsdBigEndianReader(embeddedPsdStream, reader.GetDocumentResolver(), reader.GetDocumentUri());
				embeddedReader.ReadSignatureAndVersion();
				fileHeader = PsdFileHeaderReader.ReadFileHeader(embeddedReader);
				return;
			}
		}
		fileHeader = default(FileHeaderSection);
	}

	private bool HasPsdSignature(PsdBigEndianReader reader)
	{
		long savedPosition = reader.Position;
		try
		{
			return reader.ReadFourCharacterCode() == "8BPS";
		}
		finally
		{
			reader.Position = savedPosition;
		}
	}
}
}
