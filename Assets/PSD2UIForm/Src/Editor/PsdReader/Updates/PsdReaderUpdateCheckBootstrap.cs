using System;
using UnityEditor;

namespace PsdReaderUpdateCheckBootstrapNamespace
{
    [InitializeOnLoad]
    internal sealed class PsdReaderUpdateCheckBootstrap
    {
        internal static PsdReaderUpdateCheckBootstrap s_ObfuscationSentinel;

        static PsdReaderUpdateCheckBootstrap()
        {
            // 已删除 License/更新检查：启动时不再联网。
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderUpdateCheckBootstrap GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}