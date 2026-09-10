using System;
using cn.efunstudio.psdreader.PsdParser;
using EmbeddedPsdDocumentReaderNamespace;
using EmbeddedPsdHeaderReaderNamespace;

namespace EmbeddedLinkedLayerNamespace
{
    internal class EmbeddedLinkedLayer : ILinkedLayer
    {
        private readonly string _name;

        private readonly Guid _id;

        private readonly EmbeddedPsdDocumentReader _documentReader;

        private readonly EmbeddedPsdHeaderReader _headerReader;

        internal static EmbeddedLinkedLayer s_ObfuscationSentinel;

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

        public EmbeddedLinkedLayer(string text, Guid guid, EmbeddedPsdDocumentReader embeddedPsdDocumentReader, EmbeddedPsdHeaderReader embeddedPsdHeaderReader)
        {
            _name = text;
            _id = guid;
            _documentReader = embeddedPsdDocumentReader;
            _headerReader = embeddedPsdHeaderReader;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EmbeddedLinkedLayer GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
