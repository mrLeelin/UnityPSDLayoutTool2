using System;

namespace cn.efunstudio.psdreader
{
    [Serializable]
    internal sealed class PsdReaderSignedEnvelopeDocument
    {
        public const int CurrentSchema = 1;

        public const string CurrentKdf = "EFUN-HMACSHA256-V1";

        public int Schema = 1;

        public string Kind = "SecureEnvelope";

        public string Kid = string.Empty;

        public string Alg = "RS256";

        public string Enc = "AES256-CBC";

        public string Kdf = "EFUN-HMACSHA256-V1";

        public string Scope = string.Empty;

        public string Iv = string.Empty;

        public string Payload = string.Empty;

        public string Sig = string.Empty;

        internal static PsdReaderSignedEnvelopeDocument s_ObfuscationSentinel;

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderSignedEnvelopeDocument GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
