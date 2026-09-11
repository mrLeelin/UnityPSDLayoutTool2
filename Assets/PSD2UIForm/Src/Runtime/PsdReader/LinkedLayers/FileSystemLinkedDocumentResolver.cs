using System;
using System.Collections.Generic;
using System.IO;
using cn.efunstudio.psdreader.PsdParser;
using LinkedDocumentResolverNamespace;

namespace FileSystemLinkedDocumentResolverNamespace
{
    internal class FileSystemLinkedDocumentResolver : LinkedDocumentResolver
    {
        private readonly Dictionary<Uri, PsdDocument> _documentsByUri = new Dictionary<Uri, PsdDocument>();

        private static FileSystemLinkedDocumentResolver s_ObfuscationSentinel;

        internal override PsdDocument ResolveDocument(Uri uri)
        {
            string localPath = uri.LocalPath;
            if (!File.Exists(localPath))
            {
                throw new FileNotFoundException($"{localPath} 파일을 찾을 수 없습니다.", localPath);
            }
            if (!_documentsByUri.ContainsKey(uri))
            {
                PsdDocument value = PsdDocument.Create(localPath);
                _documentsByUri.Add(uri, value);
            }
            return _documentsByUri[uri];
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static FileSystemLinkedDocumentResolver GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
