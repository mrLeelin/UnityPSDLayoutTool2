using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderProjectLicenseBundleDocument
{
	public const int CurrentSchema = 2;

	public int Schema;

	public string VendorCode;

	public string ProductCode;

	public string LookupId;

	public string LicenseId;

	public string MetaEnvelopeBase64;

	public string LicenseEnvelopeBase64;

	public string OfflineLeaseEnvelopeBase64;

	public string WrappedLicenseAccessKey;

	public string WrappedOrderIdCipher;

	public long ExportIssuedUtcTicks;

	public string ExportIssuedUtc;

	public long ExportExpiresUtcTicks;

	public string ExportExpiresUtc;

	public string BundleSeal;

	public PsdReaderProjectLicenseBundleDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 2;
		VendorCode = string.Empty;
		ProductCode = string.Empty;
		LookupId = string.Empty;
		LicenseId = string.Empty;
		MetaEnvelopeBase64 = string.Empty;
		LicenseEnvelopeBase64 = string.Empty;
		OfflineLeaseEnvelopeBase64 = string.Empty;
		WrappedLicenseAccessKey = string.Empty;
		WrappedOrderIdCipher = string.Empty;
		ExportIssuedUtc = string.Empty;
		ExportExpiresUtc = string.Empty;
		BundleSeal = string.Empty;
	}
}
}
