using System;
using System.IO;
using cn.efunstudio.psdreader.PsdParser;

namespace LinkedDocumentResolverNamespace
{
    internal abstract class LinkedDocumentResolver
    {
        private static LinkedDocumentResolver s_ObfuscationSentinel;

        internal abstract PsdDocument ResolveDocument(Uri uri);

        internal virtual Uri ResolveUri(Uri baseUri, string relativeUri)
        {
            if (!(baseUri == null) && (baseUri.IsAbsoluteUri || baseUri.OriginalString.Length != 0))
            {
                if (relativeUri != null && relativeUri.Length != 0)
                {
                    if (baseUri.IsAbsoluteUri)
                    {
                        return new Uri(baseUri, relativeUri);
                    }
                    throw new NotSupportedException("PSD_RelativeUriNotSupported");
                }
                return baseUri;
            }
            Uri uri = new Uri(relativeUri, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri && uri.OriginalString.Length > 0)
            {
                uri = new Uri(Path.GetFullPath(relativeUri));
            }
            return uri;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LinkedDocumentResolver GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
