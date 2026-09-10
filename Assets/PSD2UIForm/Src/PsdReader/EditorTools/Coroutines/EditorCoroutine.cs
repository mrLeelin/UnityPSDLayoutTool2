using UnityEngine;

namespace cn.efunstudio.psdreader
{
    internal class EditorCoroutine : YieldInstruction
    {
        public bool HasFinished;

        private static EditorCoroutine s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EditorCoroutine GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
