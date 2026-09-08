using System;
using PsdProtectionGuards;
using PsdLicensing;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderProductMetaPayloadDocument
{
	public const int CurrentSchema = 1;

	public int Schema;

	public string VendorCode;

	public string ProductCode;

	public string DisplayName;

	public int CurrentMajorVersion;

	public uint FeatureCatalogMask;

	public string[] Features;

	public int LocalCacheTimeoutDays;

	public int SoftMaxMachines;

	public int Revision;

	public string ActiveKid;

	public string ActiveLeafPublicKeyPem;

	public long PublishedUtcTicks;

	public string PublishedAtUtc;

	public string[] RepositoryBaseUrls;

	public string TelemetryUrl;

	public string TelemetrySiteId;

	public byte[] StateSalt;

	public PsdReaderProductMetaPayloadDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 1;
		VendorCode = string.Empty;
		ProductCode = string.Empty;
		DisplayName = string.Empty;
		CurrentMajorVersion = PsdProductVersionAccessor.GetCurrentMajorVersion();
		Features = Array.Empty<string>();
		LocalCacheTimeoutDays = 2;
		SoftMaxMachines = 10;
		ActiveKid = string.Empty;
		ActiveLeafPublicKeyPem = string.Empty;
		PublishedAtUtc = string.Empty;
		RepositoryBaseUrls = Array.Empty<string>();
		TelemetryUrl = string.Empty;
		TelemetrySiteId = string.Empty;
		StateSalt = Array.Empty<byte>();
	}
}
}
