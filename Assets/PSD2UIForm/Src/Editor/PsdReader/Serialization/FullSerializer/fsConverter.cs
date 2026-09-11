using System;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public abstract class fsConverter : fsBaseConverter
    {
        internal static fsConverter s_ObfuscationSentinel;

        public abstract bool CanProcess(Type type);

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
