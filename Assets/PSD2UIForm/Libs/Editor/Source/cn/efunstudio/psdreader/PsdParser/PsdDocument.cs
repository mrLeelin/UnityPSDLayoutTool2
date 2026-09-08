using System;
using System.Collections.Generic;
using System.IO;
using PsdPropertyUtilities;
using PsdDocumentResolution;
using PsdProtectionGuards;
using PsdSections;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace cn.efunstudio.psdreader.PsdParser
{

public class PsdDocument : IDisposable
{
	private PsdFileHeaderReader fileHeaderSection;

	private ColorModeDataReader colorModeDataSection;

	private ImageResourcesReader imageResourcesSection;

	private LayerAndMaskInfoReader layerAndMaskSection;

	private CompositeImageDataReader imageDataSection;

	private PsdBigEndianReader reader;

	internal FileHeaderSection FileHeaderSection => fileHeaderSection.Value;

	internal byte[] ColorModeData => colorModeDataSection.Value;

	public int Width => fileHeaderSection.Value.Width;

	public int Height => fileHeaderSection.Value.Height;

	public int Depth => fileHeaderSection.Value.Depth;

	public PsdLayer[] Childs => layerAndMaskSection.Value.Layers;

	internal IEnumerable<ILinkedLayer> LinkedLayers => layerAndMaskSection.Value.GetLinkedLayers();

	internal IProperties Resources => layerAndMaskSection.Value.GetAdditionalLayerProperties();

	internal IProperties ImageResources => imageResourcesSection;

	internal bool HasImage
	{
		get
		{
			if (imageResourcesSection.Contains("Version"))
			{
				return imageResourcesSection.GetBooleanValue("Version", "HasCompatibilityImage");
			}
			return false;
		}
	}

	internal event EventHandler Disposed;

	internal PsdDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}

	public static PsdDocument Create(string filename)
	{
		return Create(filename, new CachedFilePsdDocumentResolver());
	}

	internal static PsdDocument Create(string filename, PsdDocumentResolver resolver)
	{
		PsdDocument psdDocument = new PsdDocument();
		FileInfo fileInfo = new FileInfo(filename);
		FileStream fileStream = null;
		try
		{
			fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
			psdDocument.Read(fileStream, resolver, new Uri(fileInfo.DirectoryName));
			return psdDocument;
		}
		catch
		{
			psdDocument.Dispose();
			fileStream?.Dispose();
			throw;
		}
	}

	public static PsdDocument Create(Stream stream)
	{
		return Create(stream, null);
	}

	internal static PsdDocument Create(Stream stream, PsdDocumentResolver resolver)
	{
		PsdDocument psdDocument = new PsdDocument();
		try
		{
			psdDocument.Read(stream, resolver, new Uri(Directory.GetCurrentDirectory()));
			return psdDocument;
		}
		catch
		{
			psdDocument.Dispose();
			throw;
		}
	}

	public void Dispose()
	{
		if (reader != null)
		{
			reader.Dispose();
			reader = null;
			OnDisposed(EventArgs.Empty);
		}
	}

	protected virtual void OnDisposed(EventArgs e)
	{
		if (this.Disposed != null)
		{
			this.Disposed(this, e);
		}
	}

	internal void Read(Stream stream, PsdDocumentResolver resolver, Uri uri)
	{
		reader = new PsdBigEndianReader(stream, resolver, uri);
		reader.ReadSignatureAndVersion();
		fileHeaderSection = new PsdFileHeaderReader(reader);
		colorModeDataSection = new ColorModeDataReader(reader);
		imageResourcesSection = new ImageResourcesReader(reader);
		layerAndMaskSection = new LayerAndMaskInfoReader(reader, this);
		imageDataSection = new CompositeImageDataReader(reader, this);
	}
}
}
