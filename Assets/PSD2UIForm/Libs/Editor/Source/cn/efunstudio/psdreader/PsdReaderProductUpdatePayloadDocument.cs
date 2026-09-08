using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderProductUpdatePayloadDocument
{
	public int Schema;

	public string VendorCode;

	public string ProductCode;

	public int Revision;

	public string Version;

	public string DownloadUrl;

	public string ReleaseNotes;

	public long PublishedUtcTicks;

	public string PublishedAtUtc;

	public PsdReaderProductUpdatePayloadDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 1;
		VendorCode = string.Empty;
		ProductCode = string.Empty;
		Version = string.Empty;
		DownloadUrl = string.Empty;
		ReleaseNotes = string.Empty;
		PublishedAtUtc = string.Empty;
	}
}
}
