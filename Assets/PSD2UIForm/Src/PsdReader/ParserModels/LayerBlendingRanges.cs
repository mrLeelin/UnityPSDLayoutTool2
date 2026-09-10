namespace cn.efunstudio.psdreader.PsdParser
{
    internal class LayerBlendingRanges
    {
        private static LayerBlendingRanges s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerBlendingRanges GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
