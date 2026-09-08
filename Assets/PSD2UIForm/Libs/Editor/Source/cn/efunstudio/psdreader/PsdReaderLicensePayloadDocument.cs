using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderLicensePayloadDocument
{
	public const int CurrentSchema = 1;

	public int Schema;

	public int Revision;

	public string LookupId;

	public string LicenseId;

	public string VendorCode;

	public string ProductCode;

	public byte StatusCode;

	public string Status;

	public long IssuedUtcTicks;

	public string IssuedAtUtc;

	public long SupportUntilUtcTicks;

	public string SupportUntilUtc;

	public int MaxMajorVersion;

	public uint FeatureMask;

	public string[] Features;

	public byte[] GrantSeed;

	public PsdReaderLicensePolicyDocument Policy;

	public PsdReaderLicensePayloadDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 1;
		LookupId = string.Empty;
		LicenseId = string.Empty;
		VendorCode = string.Empty;
		ProductCode = string.Empty;
		Status = string.Empty;
		IssuedAtUtc = string.Empty;
		SupportUntilUtc = string.Empty;
		Features = Array.Empty<string>();
		GrantSeed = Array.Empty<byte>();
		Policy = new PsdReaderLicensePolicyDocument();
	}
}
}
