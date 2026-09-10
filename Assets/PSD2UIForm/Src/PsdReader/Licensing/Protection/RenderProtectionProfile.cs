using System;
using System.Runtime.CompilerServices;

namespace RenderProtectionProfileNamespace
{
    internal sealed class RenderProtectionProfile
    {
        [CompilerGenerated]
        private string _profileName = string.Empty;

        [CompilerGenerated]
        private bool _isAuthorized;

        [CompilerGenerated]
        private string _protectionKeyHex = string.Empty;

        [CompilerGenerated]
        private string _lookupId = string.Empty;

        [CompilerGenerated]
        private string _licenseId = string.Empty;

        [CompilerGenerated]
        private int _revision;

        [CompilerGenerated]
        private string _validationTranscriptHead = string.Empty;

        [CompilerGenerated]
        private string _ledgerHash = string.Empty;

        [CompilerGenerated]
        private string _projectIdentity = string.Empty;

        [CompilerGenerated]
        private string _deviceIdentity = string.Empty;

        [CompilerGenerated]
        private DateTime _clockHighWaterUtc;

        [CompilerGenerated]
        private long _generatedAtUtcTicks;

        [CompilerGenerated]
        private int _statusCode;

        [CompilerGenerated]
        private int _schemaVersion;

        [CompilerGenerated]
        private string _protectionStateHash = string.Empty;

        [CompilerGenerated]
        private string _statusMessage = string.Empty;

        private static RenderProtectionProfile s_ObfuscationSentinel;

        [SpecialName]
        [CompilerGenerated]
        internal string GetProfileName()
        {
            return _profileName;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetProfileName(string name)
        {
            _profileName = name;
        }

        [SpecialName]
        [CompilerGenerated]
        internal bool GetIsAuthorized()
        {
            return _isAuthorized;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetIsAuthorized(bool enabled)
        {
            _isAuthorized = enabled;
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
        internal string GetLookupId()
        {
            return _lookupId;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLookupId(string id)
        {
            _lookupId = id;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetLicenseId()
        {
            return _licenseId;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLicenseId(string id)
        {
            _licenseId = id;
        }

        [SpecialName]
        [CompilerGenerated]
        internal int GetRevision()
        {
            return _revision;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetRevision(int value)
        {
            _revision = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetValidationTranscriptHead()
        {
            return _validationTranscriptHead;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetValidationTranscriptHead(string id)
        {
            _validationTranscriptHead = id;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetLedgerHash()
        {
            return _ledgerHash;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetLedgerHash(string text)
        {
            _ledgerHash = text;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetProjectIdentity()
        {
            return _projectIdentity;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetProjectIdentity(string id)
        {
            _projectIdentity = id;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetDeviceIdentity()
        {
            return _deviceIdentity;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetDeviceIdentity(string id)
        {
            _deviceIdentity = id;
        }

        [SpecialName]
        [CompilerGenerated]
        internal DateTime GetClockHighWaterUtc()
        {
            return _clockHighWaterUtc;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetClockHighWaterUtc(DateTime dateTime)
        {
            _clockHighWaterUtc = dateTime;
        }

        [SpecialName]
        [CompilerGenerated]
        internal long GetGeneratedAtUtcTicks()
        {
            return _generatedAtUtcTicks;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetGeneratedAtUtcTicks(long value)
        {
            _generatedAtUtcTicks = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal int GetStatusCode()
        {
            return _statusCode;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetStatusCode(int value)
        {
            _statusCode = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal int GetSchemaVersion()
        {
            return _schemaVersion;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetSchemaVersion(int value)
        {
            _schemaVersion = value;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetProtectionStateHash()
        {
            return _protectionStateHash;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetProtectionStateHash(string text)
        {
            _protectionStateHash = text;
        }

        [SpecialName]
        [CompilerGenerated]
        internal string GetStatusMessage()
        {
            return _statusMessage;
        }

        [SpecialName]
        [CompilerGenerated]
        internal void SetStatusMessage(string message)
        {
            _statusMessage = message;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static RenderProtectionProfile GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
