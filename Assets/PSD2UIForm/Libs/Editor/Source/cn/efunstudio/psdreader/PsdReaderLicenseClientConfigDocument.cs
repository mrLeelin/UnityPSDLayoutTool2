using System;
using PsdProtectionGuards;
using PsdLicensing;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderLicenseClientConfigDocument
{
	public string VendorCode;

	public string ProductCode;

	public string[] RepositoryBaseUrls;

	public string MatomoUrl;

	public string MatomoSiteId;

	public int RequestTimeoutSeconds;

	public int CurrentMajorVersion;

	public PsdReaderLicenseClientConfigDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		VendorCode = string.Empty;
		ProductCode = string.Empty;
		RepositoryBaseUrls = Array.Empty<string>();
		MatomoUrl = string.Empty;
		MatomoSiteId = string.Empty;
		RequestTimeoutSeconds = 60;
		CurrentMajorVersion = PsdProductVersionAccessor.GetCurrentMajorVersion();
	}
}
}
