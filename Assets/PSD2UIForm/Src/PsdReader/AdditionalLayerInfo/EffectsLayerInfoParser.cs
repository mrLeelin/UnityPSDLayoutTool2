using AdditionalLayerInfoParserAttributeNamespace;
using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace EffectsLayerInfoParserNamespace
{
    [AdditionalLayerInfoParserAttribute("lrFX")]
    internal class EffectsLayerInfoParser : AdditionalLayerInfoSection
    {
        internal static EffectsLayerInfoParser s_ObfuscationSentinel;

        public EffectsLayerInfoParser(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            result = new PropertyCollection();
            reader.ReadInt16();
            int num = reader.ReadInt16();
            for (int i = 0; i < num; i++)
            {
                reader.ReadAsciiString(4);
                string text = reader.ReadAsciiString(4);
                int num2 = reader.ReadInt32();
                long num3 = reader.Position;
                if (!(text == "dsdw"))
                {
                    _ = text == "sofi";
                }
                reader.Position = num3 + num2;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EffectsLayerInfoParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
