using System;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdDocumentResolution
{

internal class EmbeddedPsdDocument : PsdDocument
{
	public EmbeddedPsdDocument()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private EmbeddedPsdDocument(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}

	protected override void OnDisposed(EventArgs P_0)
	{
		throw new Exception();
	}
}
}
