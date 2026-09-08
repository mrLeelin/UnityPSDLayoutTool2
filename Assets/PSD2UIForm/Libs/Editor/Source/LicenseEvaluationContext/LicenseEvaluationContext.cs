using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal sealed class LicenseEvaluationContext
{
	[CompilerGenerated]
	private PsdReaderLicensePayloadDocument _licensePayload;

	[CompilerGenerated]
	private PsdReaderLicenseCacheDocument _licenseCache;

	[CompilerGenerated]
	private PsdReaderProductMetaPayloadDocument _productMetadata;

	[CompilerGenerated]
	private string GW6ToLt5d3;

	[CompilerGenerated]
	private int DJhTJBUb9H;

	[SpecialName]
	[CompilerGenerated]
	internal PsdReaderLicensePayloadDocument GetLicensePayload()
	{
		return _licensePayload;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetLicensePayload(PsdReaderLicensePayloadDocument P_0)
	{
		_licensePayload = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal PsdReaderLicenseCacheDocument GetLicenseCache()
	{
		return _licenseCache;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetLicenseCache(PsdReaderLicenseCacheDocument P_0)
	{
		_licenseCache = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal PsdReaderProductMetaPayloadDocument GetProductMetadata()
	{
		return _productMetadata;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetProductMetadata(PsdReaderProductMetaPayloadDocument P_0)
	{
		_productMetadata = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal string NVQTnvlJrJ()
	{
		return GW6ToLt5d3;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void y9dTpPFQwG(string P_0)
	{
		GW6ToLt5d3 = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal int RQlT2PAwIt()
	{
		return DJhTJBUb9H;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void dmnT5ljly4(int P_0)
	{
		DJhTJBUb9H = P_0;
	}

	public LicenseEvaluationContext()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_licensePayload = new PsdReaderLicensePayloadDocument();
		_licenseCache = new PsdReaderLicenseCacheDocument();
		_productMetadata = new PsdReaderProductMetaPayloadDocument();
		GW6ToLt5d3 = string.Empty;
	}
}
}
