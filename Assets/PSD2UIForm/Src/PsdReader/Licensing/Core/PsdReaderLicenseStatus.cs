using System;
using System.Runtime.CompilerServices;
using LicenseResultCodeNamespace;

namespace PsdReaderLicenseStatusNamespace
{
    internal sealed class PsdReaderLicenseStatus
    {
        [CompilerGenerated]
        private LicenseResultCode _resultCode;

        [CompilerGenerated]
        private string _message = string.Empty;

        [CompilerGenerated]
        private string _licenseId = string.Empty;

        [CompilerGenerated]
        private string _lookupId = string.Empty;

        [CompilerGenerated]
        private string _statusText = string.Empty;

        [CompilerGenerated]
        private string[] _features = Array.Empty<string>();

        [CompilerGenerated]
        private DateTime _lastVerifiedUtc;

        [CompilerGenerated]
        private DateTime _supportUntilUtc;

        [CompilerGenerated]
        private int _revision;

        internal static PsdReaderLicenseStatus s_ObfuscationSentinel;

        public string Message
        {
            [CompilerGenerated]
            get
            {
                return _message;
            }
            [CompilerGenerated]
            internal set
            {
                _message = value;
            }
        }

        [SpecialName]
        [CompilerGenerated]
        public LicenseResultCode GetResultCode()
        {
            return _resultCode;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetResultCode(LicenseResultCode licenseResultCode)
        {
            _resultCode = licenseResultCode;
        }

        [SpecialName]
        [CompilerGenerated]
        public string GetLicenseId()
        {
            return _licenseId;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLicenseId(string licenseId)
        {
            _licenseId = licenseId;
        }

        [SpecialName]
        [CompilerGenerated]
        public string GetLookupId()
        {
            return _lookupId;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLookupId(string lookupId)
        {
            _lookupId = lookupId;
        }

        [SpecialName]
        [CompilerGenerated]
        public string GetStatusText()
        {
            return _statusText;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetStatusText(string statusText)
        {
            _statusText = statusText;
        }

        [SpecialName]
        [CompilerGenerated]
        public string[] GetFeatures()
        {
            return _features;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetFeatures(string[] features)
        {
            _features = features;
        }

        [SpecialName]
        [CompilerGenerated]
        public DateTime GetLastVerifiedUtc()
        {
            return _lastVerifiedUtc;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLastVerifiedUtc(DateTime lastVerifiedUtc)
        {
            _lastVerifiedUtc = lastVerifiedUtc;
        }

        [SpecialName]
        [CompilerGenerated]
        public DateTime GetSupportUntilUtc()
        {
            return _supportUntilUtc;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetSupportUntilUtc(DateTime supportUntilUtc)
        {
            _supportUntilUtc = supportUntilUtc;
        }

        [SpecialName]
        [CompilerGenerated]
        public int GetRevision()
        {
            return _revision;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetRevision(int revision)
        {
            _revision = revision;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdReaderLicenseStatus GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
