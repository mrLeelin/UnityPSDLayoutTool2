using System;
using System.IO;
using cn.efunstudio.psdreader.PsdParser;
using LinkedDocumentResolverNamespace;

namespace ExternalLinkedLayerNamespace
{
    internal class ExternalLinkedLayer : ILinkedLayer
    {
        private readonly Guid _id;

        private readonly LinkedDocumentResolver _resolver;

        private readonly Uri _absoluteUri;

        private PsdDocument _document;

        private readonly int _width;

        private readonly int _height;

        internal static ExternalLinkedLayer s_ObfuscationSentinel;

        public PsdDocument Document
        {
            get
            {
                if (_document == null)
                {
                    _document = _resolver.ResolveDocument(_absoluteUri);
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

        public ExternalLinkedLayer(Guid guid, LinkedDocumentResolver linkedDocumentResolver, Uri uri)
        {
            _id = guid;
            _resolver = linkedDocumentResolver;
            _absoluteUri = uri;
            if (File.Exists(_absoluteUri.LocalPath))
            {
                FileHeaderSection fileHeaderSection = FileHeaderSection.FromFile(_absoluteUri.LocalPath);
                _width = fileHeaderSection.Width;
                _height = fileHeaderSection.Height;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ExternalLinkedLayer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
