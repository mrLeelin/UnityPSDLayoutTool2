using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderOfflineLeasePayloadDocument
{
	public const int CurrentSchema = 1;

	public int Schema;

	public string Kind;

	public int Revision;

	public string LookupId;

	public string LicenseId;

	public string VendorCode;

	public string ProductCode;

	public byte StatusCode;

	public string Status;

	public uint FeatureMask;

	public string[] Features;

	public long SupportUntilUtcTicks;

	public string SupportUntilUtc;

	public int OfflineCacheTimeoutDays;

	public long IssuedUtcTicks;

	public string IssuedAtUtc;

	public PsdReaderOfflineLeasePayloadDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 1;
		Kind = "OfflineLease";
		LookupId = string.Empty;
		LicenseId = string.Empty;
		VendorCode = string.Empty;
		ProductCode = string.Empty;
		Status = string.Empty;
		Features = Array.Empty<string>();
		SupportUntilUtc = string.Empty;
		OfflineCacheTimeoutDays = 2;
		IssuedAtUtc = string.Empty;
	}
}
}
