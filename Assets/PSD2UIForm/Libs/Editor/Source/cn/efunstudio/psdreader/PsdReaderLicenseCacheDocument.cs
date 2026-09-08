using System;
using PsdProtectionGuards;
using PsdLicensing;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderLicenseCacheDocument
{
	public const int CurrentSchema = 1;

	public int Schema;

	public string LookupId;

	public string LicenseId;

	public byte StatusCode;

	public string StatusText;

	public string Message;

	public string[] Features;

	public uint FeatureMask;

	public uint CatalogMask;

	public string LastVerifiedUtc;

	public string SupportUntilUtc;

	public string ClockHighWaterUtc;

	public string ClockWatermarkCipher;

	public string ValidationTranscriptHead;

	public int Revision;

	public int LocalCacheTimeoutDays;

	public int CurrentMajorVersion;

	public int MaxMajorVersion;

	public string DeviceFingerprint;

	public int MetaRevision;

	public string ActiveKid;

	public LicenseValidationStatus LastResultCode;

	public string LicenseAccessKeyCipher;

	public string OrderIdCipher;

	public string GrantSeedCipher;

	public string StateSaltHex;

	public string TelemetryUrl;

	public string TelemetrySiteId;

	public string ActivationSource;

	public string LastRepositoryRequestUtc;

	public string MetaEnvelopeBase64;

	public string LicenseEnvelopeBase64;

	public string OfflineLeaseEnvelopeBase64;

	public string DeviceShardEnvelopeBase64;

	public PsdReaderLicenseCacheDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 1;
		LookupId = string.Empty;
		LicenseId = string.Empty;
		StatusText = string.Empty;
		Message = string.Empty;
		Features = Array.Empty<string>();
		LastVerifiedUtc = string.Empty;
		SupportUntilUtc = string.Empty;
		ClockHighWaterUtc = string.Empty;
		ClockWatermarkCipher = string.Empty;
		ValidationTranscriptHead = string.Empty;
		LocalCacheTimeoutDays = 2;
		CurrentMajorVersion = PsdProductVersionAccessor.GetCurrentMajorVersion();
		MaxMajorVersion = PsdProductVersionAccessor.GetCurrentMajorVersion();
		DeviceFingerprint = string.Empty;
		ActiveKid = string.Empty;
		LicenseAccessKeyCipher = string.Empty;
		OrderIdCipher = string.Empty;
		GrantSeedCipher = string.Empty;
		StateSaltHex = string.Empty;
		TelemetryUrl = string.Empty;
		TelemetrySiteId = string.Empty;
		ActivationSource = string.Empty;
		LastRepositoryRequestUtc = string.Empty;
		MetaEnvelopeBase64 = string.Empty;
		LicenseEnvelopeBase64 = string.Empty;
		OfflineLeaseEnvelopeBase64 = string.Empty;
		DeviceShardEnvelopeBase64 = string.Empty;
	}
}
}
