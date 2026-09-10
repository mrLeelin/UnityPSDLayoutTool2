using PsdBinaryReaderNamespace;
using PropertyCollectionNamespace;
using PsdDescriptorValueReaderNamespace;
using PsdEngineDataParserNamespace;

namespace PsdDescriptorReaderNamespace
{
    internal class PsdDescriptorReader : PropertyCollection
    {
        private readonly int _version;

        private static PsdDescriptorReader s_ObfuscationSentinel;

        public PsdDescriptorReader(PsdBinaryReader psdBinaryReader)
            : this(psdBinaryReader, true)
        {
        }

        public PsdDescriptorReader(PsdBinaryReader psdBinaryReader, bool enabled)
        {
            if (enabled)
            {
                _version = psdBinaryReader.ReadInt32();
            }
            Add("Name", psdBinaryReader.ReadUnicodeString());
            Add("ClassID", psdBinaryReader.ReadDescriptorId());
            int num = psdBinaryReader.ReadInt32();
            for (int i = 0; i < num; i++)
            {
                string text = psdBinaryReader.ReadDescriptorId();
                string text2 = psdBinaryReader.ReadSignature();
                if (!(text == "EngineData"))
                {
                    object value = PsdDescriptorValueReader.ReadValue(text2, psdBinaryReader);
                    Add(text.Trim(), value);
                }
                else
                {
                    Add(text.Trim(), new PsdEngineDataParser(psdBinaryReader));
                }
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdDescriptorReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
