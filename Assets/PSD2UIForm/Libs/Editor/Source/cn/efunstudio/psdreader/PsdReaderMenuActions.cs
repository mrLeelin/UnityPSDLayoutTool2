using aFoH0G3UVxsdBFQJYkK;

namespace cn.efunstudio.psdreader
{

public static class PsdReaderMenuActions
{
	public const string ForceResetMenuPath = "Window/PSDReader/Force Reset";

	public const string LicenseWindowMenuPath = "Tools/Psd2UIForm/Other/LicenseWindow";

	public const string ClearLicenseMenuPath = "Tools/Psd2UIForm/Other/Clear License";

	public const string CheckUpdateMenuPath = "Tools/Psd2UIForm/Check Update";

	public const int LicenseWindowPriority = 9999;

	public const int ClearLicensePriority = 10000;

	public const int CheckUpdatePriority = 10001;

	public static void ForceReset()
	{
		EditorCoroutineRunner.KillAllCoroutines();
	}

	public static void OpenLicenseWindow()
	{
		jtV2XO3BDDWFT7igMea.OpenLicenseWindowMenu();
	}

	public static void ClearLicense()
	{
		jtV2XO3BDDWFT7igMea.ClearLicenseCacheMenu();
	}

	public static void CheckUpdate()
	{
		jtV2XO3BDDWFT7igMea.CheckUpdatesMenu();
	}
}
}
