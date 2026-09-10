using System.Collections.Generic;

namespace cn.efunstudio.psdreader
{
    internal class DisplayLayerData
    {
        public int[] indexId;

        public bool isVisible;

        public bool isGroup;

        public bool isOpen;

        public bool isLinked;

        public int[] linkId;

        public List<DisplayLayerData> Childs = new List<DisplayLayerData>();

        internal static DisplayLayerData s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static DisplayLayerData GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
