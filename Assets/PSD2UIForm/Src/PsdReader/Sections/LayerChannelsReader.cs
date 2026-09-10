using System.IO;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LengthPrefixedPsdSectionNamespace;

namespace LayerChannelsReaderNamespace
{
    internal class LayerChannelsReader : LengthPrefixedPsdSection<Channel[]>
    {
        private static LayerChannelsReader s_ObfuscationSentinel;

        public LayerChannelsReader(PsdBinaryReader psdBinaryReader, long value, PsdLayer psdLayer)
            : base(psdBinaryReader, value, (object)psdLayer)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out Channel[] result)
        {
            PsdLayer psdLayer = context as PsdLayer;
            LayerRecords records = psdLayer.Records;
            using (MemoryStream memoryStream = new MemoryStream(reader.ReadBytes((int)GetSectionLength())))
            {
                using PsdBinaryReader value = new PsdBinaryReader(memoryStream, reader.GetLinkedDocumentResolver(), reader.GetBaseUri());
                value.Version = reader.Version;
                ReadChannelData(value, psdLayer.Depth, records.Channels);
            }
            result = records.Channels;
        }

        private void ReadChannelData(PsdBinaryReader value, int value2, Channel[] values)
        {
            foreach (Channel channel in values)
            {
                CompressionType compressionType = value.ReadCompressionType();
                channel.ReadHeader(value, compressionType);
                channel.Read(value, value2, compressionType, checked((int)channel.Size) - 2);
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerChannelsReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
