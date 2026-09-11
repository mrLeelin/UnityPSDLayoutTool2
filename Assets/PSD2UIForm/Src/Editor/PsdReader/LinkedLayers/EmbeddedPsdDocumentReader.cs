using System.IO;
using PsdBinaryReaderNamespace;
using EmbeddedPsdDocumentNamespace;
using cn.efunstudio.psdreader.PsdParser;
using ReadOnlySubStreamNamespace;
using LengthPrefixedPsdSectionNamespace;

namespace EmbeddedPsdDocumentReaderNamespace
{
    internal class EmbeddedPsdDocumentReader : LengthPrefixedPsdSection<PsdDocument>
    {
        internal static EmbeddedPsdDocumentReader s_ObfuscationSentinel;

        public EmbeddedPsdDocumentReader(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value, (object)null)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out PsdDocument result)
        {
            if (HasPsdSignature(reader))
            {
                using (Stream stream = new ReadOnlySubStream(reader.GetBaseStream(), reader.Position, GetSectionLength()))
                {
                    PsdDocument psdDocument = new EmbeddedPsdDocument();
                    psdDocument.Read(stream, reader.GetLinkedDocumentResolver(), reader.GetBaseUri());
                    result = psdDocument;
                    return;
                }
            }
            result = null;
        }

        private bool HasPsdSignature(PsdBinaryReader value)
        {
            long num = value.Position;
            try
            {
                return value.ReadSignature() == "8BPS";
            }
            finally
            {
                value.Position = num;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EmbeddedPsdDocumentReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
