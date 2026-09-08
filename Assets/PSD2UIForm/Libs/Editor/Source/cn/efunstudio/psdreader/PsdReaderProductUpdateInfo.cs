using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
public sealed class PsdReaderProductUpdateInfo
{
	public PsdReaderProductUpdateStatus Status { get; internal set; }

	public bool HasUpdate { get; internal set; }

	public string CurrentVersion { get; internal set; }

	public string LatestVersion { get; internal set; }

	public string DownloadUrl { get; internal set; }

	public string ReleaseNotes { get; internal set; }

	public string PublishedAtUtc { get; internal set; }

	public string Message { get; internal set; }

	public PsdReaderProductUpdateInfo()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Status = PsdReaderProductUpdateStatus.UnknownError;
		CurrentVersion = string.Empty;
		LatestVersion = string.Empty;
		DownloadUrl = string.Empty;
		ReleaseNotes = string.Empty;
		PublishedAtUtc = string.Empty;
		Message = string.Empty;
	}
}
}
