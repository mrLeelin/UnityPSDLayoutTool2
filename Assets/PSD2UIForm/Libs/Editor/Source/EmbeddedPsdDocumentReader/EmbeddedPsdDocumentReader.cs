using System.IO;
using PsdProtectionGuards;
using PsdDocumentResolution;
using PsdBinaryUtilities;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdSections;

namespace PsdSections
{

internal class EmbeddedPsdDocumentReader : LengthPrefixedSectionReader<PsdDocument>
{
	public EmbeddedPsdDocumentReader(PsdBigEndianReader P_0, long P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private EmbeddedPsdDocumentReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1)
		: base(P_0, P_1, (object)null)
	{
	}

	protected override void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out PsdDocument P_2)
	{
		if (HasPsdSignature(P_0))
		{
			using (Stream stream = new ReadOnlySubstream(P_0.GetBaseStream(), P_0.Position, GetSectionLength()))
			{
				PsdDocument psdDocument = new EmbeddedPsdDocument();
				psdDocument.Read(stream, P_0.GetDocumentResolver(), P_0.GetDocumentUri());
				P_2 = psdDocument;
				return;
			}
		}
		P_2 = null;
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
