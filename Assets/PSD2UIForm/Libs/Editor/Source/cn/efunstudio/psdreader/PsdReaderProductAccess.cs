using PsdLicensing;

namespace cn.efunstudio.psdreader
{

public static class PsdReaderProductAccess
{
	public static bool IsAvailable => PsdProductAccessFacade.IsLicenseAvailable();

	public static bool NeedsAttention
	{
		get
		{
			if (!PsdProductAccessFacade.qYV9KxLfKy())
			{
				return false;
			}
			return !PsdProductAccessFacade.IsLicenseAvailable();
		}
	}

	public static string GetStatusLabel()
	{
		return PsdProductAccessFacade.GetStatusLabel();
	}

	public static string GetOverviewMessage()
	{
		return PsdProductAccessFacade.GetActivationHint();
	}

	public static string GetUserFacingMessage()
	{
		return PsdProductAccessFacade.GetDetailedStatusMessage();
	}

	public static bool PrimeBuildProtection()
	{
		return PsdProductAccessFacade.HasMainFeature();
	}

	public static void OpenManagementWindow()
	{
		PsdProductAccessFacade.ShowLicenseWindow();
	}

	public static PsdReaderProductUpdateInfo CheckForUpdates()
	{
		return PsdProductAccessFacade.CheckForUpdates();
	}

	public static void CheckForUpdatesAndPrompt()
	{
		PsdProductAccessFacade.CheckForUpdatesAndPrompt();
	}

	public static void EnsureDailyUpdateCheck()
	{
		PsdProductAccessFacade.CheckForUpdatesAtStartup();
	}

	public static bool HasPendingUpdateTip()
	{
		return PsdProductAccessFacade.HasCachedUpdate();
	}

	public static string GetPendingUpdateTipMessage()
	{
		return PsdProductAccessFacade.GetCachedUpdateLabel();
	}

	public static bool TryOpenPendingUpdateDownloadUrl()
	{
		return PsdProductAccessFacade.OpenCachedUpdateDownload();
	}
}
}
