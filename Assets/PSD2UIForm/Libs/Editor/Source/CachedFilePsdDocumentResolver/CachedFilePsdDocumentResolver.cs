using System;
using System.Collections.Generic;
using System.IO;
using PsdDocumentResolution;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdDocumentResolution
{

internal class CachedFilePsdDocumentResolver : PsdDocumentResolver
{
	private readonly Dictionary<Uri, PsdDocument> _documentsByUri = new Dictionary<Uri, PsdDocument>();

	internal override PsdDocument ResolveDocument(Uri P_0)
	{
		string localPath = P_0.LocalPath;
		if (File.Exists(localPath))
		{
			if (!_documentsByUri.ContainsKey(P_0))
			{
				PsdDocument value = PsdDocument.Create(localPath);
				_documentsByUri.Add(P_0, value);
			}
			return _documentsByUri[P_0];
		}
		throw new FileNotFoundException($"{localPath} 파일을 찾을 수 없습니다.", localPath);
	}

	public CachedFilePsdDocumentResolver()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private CachedFilePsdDocumentResolver(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
