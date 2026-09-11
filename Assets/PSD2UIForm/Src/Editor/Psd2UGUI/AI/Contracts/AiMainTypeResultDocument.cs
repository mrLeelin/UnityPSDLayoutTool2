using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiMainTypeResultDocument
    {
        public string version;

        public string treeHash;

        public List<AiMainTypeEntry> nodes = new List<AiMainTypeEntry>();

        private static AiMainTypeResultDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiMainTypeResultDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
