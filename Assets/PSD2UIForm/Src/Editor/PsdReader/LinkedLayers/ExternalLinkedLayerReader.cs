using System;
using System.IO;
using PsdSectionReaderNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using PsdDescriptorReaderNamespace;
using ExternalLinkedLayerNamespace;

namespace ExternalLinkedLayerReaderNamespace
{
    internal class ExternalLinkedLayerReader : PsdSectionReader<ExternalLinkedLayer>
    {
        private static ExternalLinkedLayerReader s_ObfuscationSentinel;

        public ExternalLinkedLayerReader(PsdBinaryReader psdBinaryReader)
            : base(psdBinaryReader, true, (object)null)
        {
        }

        protected override long ReadSectionLength(PsdBinaryReader reader)
        {
            return (reader.ReadInt64() + 3L) & -4L;
        }

        private Uri ResolveLinkedDocumentUri(PsdBinaryReader value)
        {
            IProperties properties = new PsdDescriptorReader(value);
            if (properties.Contains("fullPath"))
            {
                Uri uri = new Uri(properties["fullPath"] as string);
                if (File.Exists(uri.LocalPath))
                {
                    return uri;
                }
            }
            if (properties.Contains("relPath"))
            {
                string text = properties["relPath"] as string;
                Uri uri2 = value.GetLinkedDocumentResolver().ResolveUri(value.GetBaseUri(), text);
                if (File.Exists(uri2.LocalPath))
                {
                    return uri2;
                }
            }
            if (properties.Contains("Nm"))
            {
                string text2 = properties["Nm"] as string;
                Uri uri3 = value.GetLinkedDocumentResolver().ResolveUri(value.GetBaseUri(), text2);
                if (File.Exists(uri3.LocalPath))
                {
                    return uri3;
                }
            }
            if (!properties.Contains("fullPath"))
            {
                return null;
            }
            return new Uri(properties["fullPath"] as string);
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out ExternalLinkedLayer result)
        {
            reader.ValidateSignature("liFE");
            reader.ReadInt32();
            Guid guid = new Guid(reader.ReadPascalString(1));
            reader.ReadUnicodeString();
            reader.ReadSignature();
            reader.ReadSignature();
            reader.ReadInt64();
            if (reader.ReadBoolean())
            {
                new PsdDescriptorReader(reader);
            }
            Uri uri = ResolveLinkedDocumentUri(reader);
            result = new ExternalLinkedLayer(guid, reader.GetLinkedDocumentResolver(), uri);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ExternalLinkedLayerReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
