using System;
using System.IO;
using PsdDocumentResolution;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdLinkedLayers
{

internal class ExternalLinkedPsdLayer : ILinkedLayer
{
	private readonly Guid _id;

	private readonly PsdDocumentResolver _documentResolver;

	private readonly Uri _absoluteUri;

	private PsdDocument _document;

	private readonly int _width;

	private readonly int _height;

	public PsdDocument Document
	{
		get
		{
			if (_document == null)
			{
				_document = _documentResolver.ResolveDocument(_absoluteUri);
			}
			return _document;
		}
	}

	public Uri AbsoluteUri => _absoluteUri;

	public bool HasDocument => File.Exists(_absoluteUri.LocalPath);

	public Guid ID => _id;

	public string Name => _absoluteUri.LocalPath;

	public int Width => _width;

	public int Height => _height;

	public ExternalLinkedPsdLayer(Guid P_0, PsdDocumentResolver P_1, Uri P_2)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_id = P_0;
		_documentResolver = P_1;
		_absoluteUri = P_2;
		if (File.Exists(_absoluteUri.LocalPath))
		{
			FileHeaderSection fileHeaderSection = FileHeaderSection.FromFile(_absoluteUri.LocalPath);
			_width = fileHeaderSection.Width;
			_height = fileHeaderSection.Height;
		}
	}
}
}
