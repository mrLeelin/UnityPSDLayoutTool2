using System;
using cn.efunstudio.psdreader.PsdParser;

namespace EmbeddedPsdDocumentNamespace
{
    internal class EmbeddedPsdDocument : PsdDocument
    {
        private static EmbeddedPsdDocument s_ObfuscationSentinel;

        protected override void OnDisposed(EventArgs eventArgs)
        {
            throw new Exception();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static EmbeddedPsdDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
