using System.Runtime.CompilerServices;
using PsdLicensing;
using aFoH0G3UVxsdBFQJYkK;
using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdLicensing
{

internal static class PsdProductAccessFacade
{
	[SpecialName]
	public static bool qYV9KxLfKy()
	{
		return true;
	}

	[SpecialName]
	public static bool IsLicenseAvailable()
	{
		return LicenseStatusPresentation.GetAvailability(PsdLicenseService.GetStatusSnapshot()) == (LicenseAvailability)0;
	}

	public static string GetStatusLabel()
	{
		return LicenseStatusPresentation.GetStatusLabel(PsdLicenseService.GetStatusSnapshot());
	}

	public static string GetActivationHint()
	{
		return LicenseStatusPresentation.GetActivationHint(PsdLicenseService.GetStatusSnapshot());
	}

	public static string GetDetailedStatusMessage()
	{
		return LicenseStatusPresentation.GetDetailedStatusMessage(PsdLicenseService.GetStatusSnapshot());
	}

	public static bool HasMainFeature()
	{
		return PsdLicenseService.HasMainFeature();
	}

	public static string GetPreviewProtectionFingerprint(object P_0)
	{
		if (P_0 == null)
		{
			return string.Empty;
		}
		return ((PsdLayer)P_0).GetPreviewProtectionFingerprint();
	}

	public static bool ActivateOrder(object P_0, out string P_1)
	{
		return PsdLicenseService.ActivateOrder(P_0, out P_1);
	}

	public static bool RefreshLicense(out string P_0)
	{
		return PsdLicenseService.RefreshLicense(out P_0);
	}

	public static bool HasProjectLicense()
	{
		return PsdLicenseService.HasProjectLicense();
	}

	public static string GetProjectLicenseHint()
	{
		return PsdLicenseService.GetProjectLicenseHint();
	}

	public static bool ActivateProjectLicense(out string P_0)
	{
		return PsdLicenseService.ActivateProjectLicense(out P_0);
	}

	public static bool CanSaveProjectLicense()
	{
		return PsdLicenseService.CanSaveProjectLicense();
	}

	public static bool SaveProjectLicense(int P_0, out string P_1)
	{
		return PsdLicenseService.SaveProjectLicense(P_0, out P_1);
	}

	public static void ClearLocalLicense()
	{
		PsdLicenseService.ClearLocalLicense();
	}

	public static void ShowLicenseWindow()
	{
		jtV2XO3BDDWFT7igMea.ShowWindow();
	}

	public static PsdReaderProductUpdateInfo CheckForUpdates()
	{
		return PsdProductUpdateService.CheckForUpdates();
	}

	public static void CheckForUpdatesAndPrompt()
	{
		PsdProductUpdateService.CheckForUpdatesAndPrompt();
	}

	public static void CheckForUpdatesAtStartup()
	{
		PsdProductUpdateService.CheckForUpdatesAtStartup();
	}

	public static bool HasCachedUpdate()
	{
		return PsdProductUpdateService.HasCachedUpdate();
	}

	public static string GetCachedUpdateLabel()
	{
		return PsdProductUpdateService.GetCachedUpdateLabel();
	}

	public static bool OpenCachedUpdateDownload()
	{
		return PsdProductUpdateService.OpenCachedUpdateDownload();
	}
}
}
