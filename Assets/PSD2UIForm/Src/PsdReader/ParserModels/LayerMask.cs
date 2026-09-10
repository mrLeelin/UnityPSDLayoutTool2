namespace cn.efunstudio.psdreader.PsdParser
{
    internal class LayerMask
    {
        private static LayerMask s_ObfuscationSentinel;

        public int Left { get; set; }

        public int Top { get; set; }

        public int Right { get; set; }

        public int Bottom { get; set; }

        public byte Color { get; set; }

        public byte Flag { get; set; }

        public int Width => Right - Left;

        public int Height => Bottom - Top;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerMask GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
