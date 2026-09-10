using System;
using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using EmbeddedLinkedLayerNamespace;
using PsdDescriptorReaderNamespace;
using EmbeddedPsdDocumentReaderNamespace;
using EmbeddedPsdHeaderReaderNamespace;

namespace EmbeddedLinkedLayerReaderNamespace
{
    internal class EmbeddedLinkedLayerReader : PsdSectionReader<EmbeddedLinkedLayer>
    {
        private static EmbeddedLinkedLayerReader s_ObfuscationSentinel;

        public EmbeddedLinkedLayerReader(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader, true, (object)null)
        {
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return (reader.ReadInt64() + 3L) & -4L;
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out EmbeddedLinkedLayer result)
        {
            reader.ValidateSignature("liFD");
            reader.ReadInt32();
            Guid guid = new Guid(reader.ReadPascalString(1));
            string text = reader.ReadUnicodeString();
            reader.ReadSignature();
            reader.ReadSignature();
            long num = reader.ReadInt64();
            if (reader.ReadBoolean())
            {
                new PsdDescriptorReader(reader);
            }
            bool flag = HasPsdSignature(reader);
            EmbeddedPsdDocumentReader value = null;
            EmbeddedPsdHeaderReader embeddedPsdHeaderReader = null;
            if (num > 0L && flag)
            {
                long num2 = reader.Position;
                value = new EmbeddedPsdDocumentReader(reader, num);
                reader.Position = num2;
                embeddedPsdHeaderReader = new EmbeddedPsdHeaderReader(reader, num);
            }
            result = new EmbeddedLinkedLayer(text, guid, value, embeddedPsdHeaderReader);
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

        internal static EmbeddedLinkedLayerReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
