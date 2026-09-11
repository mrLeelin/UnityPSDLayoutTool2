using PsdBinaryReaderNamespace;
using cn.efunstudio.psdreader.PsdParser;
using AdditionalLayerInfoSectionNamespace;
using PropertyCollectionNamespace;

namespace UnknownAdditionalLayerInfoSectionNamespace
{
    internal class UnknownAdditionalLayerInfoSection : AdditionalLayerInfoSection
    {
        private static UnknownAdditionalLayerInfoSection s_ObfuscationSentinel;

        public UnknownAdditionalLayerInfoSection(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value)
        {
        }

        protected override void ReadValue(PsdBinaryReader reader, object context, out IProperties result)
        {
            result = new PropertyCollection();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static UnknownAdditionalLayerInfoSection GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
