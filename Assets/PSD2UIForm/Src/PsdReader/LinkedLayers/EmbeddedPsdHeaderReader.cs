using System.IO;
using FileHeaderSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using ReadOnlySubStreamNamespace;
using LengthPrefixedPsdSectionNamespace;

namespace EmbeddedPsdHeaderReaderNamespace
{
    internal class EmbeddedPsdHeaderReader : LengthPrefixedPsdSection<FileHeaderSection>
    {
        internal static EmbeddedPsdHeaderReader s_ObfuscationSentinel;

        public EmbeddedPsdHeaderReader(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value, (object)null)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out FileHeaderSection result)
        {
            if (!HasPsdSignature(reader))
            {
                result = default(FileHeaderSection);
                return;
            }
            using Stream stream = new ReadOnlySubStream(reader.GetBaseStream(), reader.Position, GetSectionLength());
            using PsdBinaryReader value = new PsdBinaryReader(stream, reader.GetLinkedDocumentResolver(), reader.GetBaseUri());
            value.ReadFileHeaderPreamble();
            result = FileHeaderSectionReader.ReadFileHeader(value);
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

        internal static EmbeddedPsdHeaderReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
