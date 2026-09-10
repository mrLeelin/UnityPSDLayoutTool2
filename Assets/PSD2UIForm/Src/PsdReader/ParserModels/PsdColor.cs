namespace cn.efunstudio.psdreader.PsdParser
{
    public struct PsdColor
    {
        private readonly byte r;

        private readonly byte g;

        private readonly byte b;

        private readonly byte a;

        private static object s_ObfuscationSentinel;

        public byte R => r;

        public byte G => g;

        public byte B => b;

        public byte A => a;

        public PsdColor(byte r, byte g, byte b, byte a = byte.MaxValue)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public PsdColor WithAlpha(byte alpha)
        {
            return new PsdColor(R, G, B, alpha);
        }

        public override string ToString()
        {
            return $"RGBA({R}, {G}, {B}, {A})";
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static object GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
