using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI
{
    [Serializable]
    internal sealed class AiPatchDocument
    {
        public string version;

        public string providerId;

        public string jobId;

        public string treeHash;

        public List<AiAuditEntry> analysis = new List<AiAuditEntry>();

        public List<AiPatchOperation> operations = new List<AiPatchOperation>();

        private static AiPatchDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
