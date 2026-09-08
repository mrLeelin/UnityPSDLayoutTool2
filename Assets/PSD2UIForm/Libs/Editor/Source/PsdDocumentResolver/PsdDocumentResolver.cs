using System;
using System.IO;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdDocumentResolution
{

internal abstract class PsdDocumentResolver
{
	internal abstract PsdDocument ResolveDocument(Uri P_0);

	internal virtual Uri ResolveUri(Uri P_0, string P_1)
	{
		if (!(P_0 == null) && (P_0.IsAbsoluteUri || P_0.OriginalString.Length != 0))
		{
			if (P_1 != null && P_1.Length != 0)
			{
				if (!P_0.IsAbsoluteUri)
				{
					throw new NotSupportedException("PSD_RelativeUriNotSupported");
				}
				return new Uri(P_0, P_1);
			}
			return P_0;
		}
		Uri uri = new Uri(P_1, UriKind.RelativeOrAbsolute);
		if (!uri.IsAbsoluteUri && uri.OriginalString.Length > 0)
		{
			uri = new Uri(Path.GetFullPath(P_1));
		}
		return uri;
	}

	protected PsdDocumentResolver()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
