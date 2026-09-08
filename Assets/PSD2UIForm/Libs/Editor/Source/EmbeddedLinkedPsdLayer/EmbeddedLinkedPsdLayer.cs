using System;
using PsdProtectionGuards;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdLinkedLayers
{

internal class EmbeddedLinkedPsdLayer : ILinkedLayer
{
	private readonly string _name;

	private readonly Guid _id;

	private readonly EmbeddedPsdDocumentReader _documentReader;

	private readonly EmbeddedPsdHeaderReader _headerReader;

	public PsdDocument Document
	{
		get
		{
			if (_documentReader != null)
			{
				return _documentReader.Value;
			}
			return null;
		}
	}

	public Uri AbsoluteUri => null;

	public bool HasDocument => _documentReader != null;

	public Guid ID => _id;

	public string Name => _name;

	public int Width => _headerReader.Value.Width;

	public int Height => _headerReader.Value.Height;

	public EmbeddedLinkedPsdLayer(string P_0, Guid P_1, EmbeddedPsdDocumentReader P_2, EmbeddedPsdHeaderReader P_3)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_name = P_0;
		_id = P_1;
		_documentReader = P_2;
		_headerReader = P_3;
	}
}
}
