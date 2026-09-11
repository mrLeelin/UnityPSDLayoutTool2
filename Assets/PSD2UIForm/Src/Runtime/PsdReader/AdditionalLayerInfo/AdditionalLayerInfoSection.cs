using PsdPropertiesSectionNamespace;
using PsdBinaryReaderNamespace;

namespace AdditionalLayerInfoSectionNamespace
{
    internal abstract class AdditionalLayerInfoSection : PsdPropertiesSection
    {
        private static AdditionalLayerInfoSection s_ObfuscationSentinel;

        public AdditionalLayerInfoSection(PsdBinaryReader psdBinaryReader, long value)
            : base(psdBinaryReader, value, null)
        {
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AdditionalLayerInfoSection GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
