using UnityEditor;
using cn.efunstudio.psdreader;

namespace UGF.EditorTools.Psd2UGUI
{

internal static class PsdReaderMenuItems
{
	[MenuItem("Window/PSDReader/Force Reset")]
	private static void ForceReset()
	{
		PsdReaderMenuActions.ForceReset();
	}

	[MenuItem("Tools/Psd2UIForm/Other/LicenseWindow", priority = 9999)]
	private static void OpenLicenseWindow()
	{
		PsdReaderMenuActions.OpenLicenseWindow();
	}

	[MenuItem("Tools/Psd2UIForm/Other/Clear License", priority = 10000)]
	private static void ClearLicense()
	{
		PsdReaderMenuActions.ClearLicense();
	}

	[MenuItem("Tools/Psd2UIForm/Check Update", priority = 10001)]
	private static void CheckUpdate()
	{
		PsdReaderMenuActions.CheckUpdate();
	}
}
}
