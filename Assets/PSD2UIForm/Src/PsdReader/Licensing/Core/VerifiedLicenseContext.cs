using System.Runtime.CompilerServices;
using cn.efunstudio.psdreader;

namespace VerifiedLicenseContextNamespace
{
    internal sealed class VerifiedLicenseContext
    {
        [CompilerGenerated]
        private PsdReaderLicensePayloadDocument _licensePayload = new PsdReaderLicensePayloadDocument();

        [CompilerGenerated]
        private PsdReaderLicenseCacheDocument _cacheDocument = new PsdReaderLicenseCacheDocument();

        [CompilerGenerated]
        private PsdReaderProductMetaPayloadDocument _productMetadata = new PsdReaderProductMetaPayloadDocument();

        [CompilerGenerated]
        private string _protectionKeyHex = string.Empty;

        [CompilerGenerated]
        private int _integrityToken;

        private static VerifiedLicenseContext s_ObfuscationSentinel;

        [SpecialName]
        [CompilerGenerated]
        internal PsdReaderLicensePayloadDocument GetLicensePayload()
        {
            return _licensePayload;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLicensePayload(PsdReaderLicensePayloadDocument psdReaderLicensePayloadDocument)
        {
            _licensePayload = psdReaderLicensePayloadDocument;
        }

        [SpecialName]
        [CompilerGenerated]
        internal PsdReaderLicenseCacheDocument GetCacheDocument()
        {
            return _cacheDocument;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetCacheDocument(PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument)
        {
            _cacheDocument = psdReaderLicenseCacheDocument;
        }

        [SpecialName]
        [CompilerGenerated]
        internal PsdReaderProductMetaPayloadDocument GetProductMetadata()
        {
            return _productMetadata;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetProductMetadata(PsdReaderProductMetaPayloadDocument psdReaderProductMetaPayloadDocument)
        {
            _productMetadata = psdReaderProductMetaPayloadDocument;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetProtectionKeyHex()
        {
            return _protectionKeyHex;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetProtectionKeyHex(string key)
        {
            _protectionKeyHex = key;
        }

        [SpecialName]
        [CompilerGenerated]
        internal int GetIntegrityToken()
        {
            return _integrityToken;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetIntegrityToken(int value)
        {
            _integrityToken = value;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static VerifiedLicenseContext GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
