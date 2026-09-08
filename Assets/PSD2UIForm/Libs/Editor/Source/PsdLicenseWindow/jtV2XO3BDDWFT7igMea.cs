using PsdLicensing;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;
using pcFLST3L4fps8LGdKbk;

namespace aFoH0G3UVxsdBFQJYkK
{

internal sealed class jtV2XO3BDDWFT7igMea : EditorWindow
{
	[SerializeField]
	private Vector2 m_ScrollPosition;

	private string _orderNumber = string.Empty;

	private LicenseStatusSnapshot _licenseStatus;

	internal static void OpenLicenseWindowMenu()
	{
		ShowWindow();
	}

	internal static void ClearLicenseCacheMenu()
	{
		if (EditorUtility.DisplayDialog("Psd2UGUI License", "确定清除本地授权缓存和插件目录授权文件吗？", "确定", "取消"))
		{
			string message = PsdLicenseService.ClearLocalLicense();
			EditorUtility.DisplayDialog("Psd2UGUI License", message, "确定");
		}
	}

	internal static void CheckUpdatesMenu()
	{
		PsdReaderProductAccess.CheckForUpdatesAndPrompt();
	}

	internal static void ShowWindow()
	{
		jtV2XO3BDDWFT7igMea window = EditorWindow.GetWindow<jtV2XO3BDDWFT7igMea>(utility: false, "Psd2UGUI License");
		window.minSize = new Vector2(520f, 360f);
		window.RefreshStatusSnapshot();
		window.Show();
	}

	private void OnEnable()
	{
		base.minSize = new Vector2(520f, 360f);
		RefreshStatusSnapshot();
	}

	private void OnFocus()
	{
		RefreshStatusSnapshot();
	}

	private void OnGUI()
	{
		m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
		EditorGUILayout.LabelField("Psd2UGUI 授权", EditorStyles.boldLabel);
		EditorGUILayout.Space(6f);
		DrawActivationHint();
		EditorGUILayout.Space(8f);
		DrawActivationControls();
		EditorGUILayout.Space(8f);
		DrawCurrentStatus();
		EditorGUILayout.EndScrollView();
	}

	private void DrawActivationHint()
	{
		EnsureStatusSnapshot();
		EditorGUILayout.HelpBox(LicenseStatusPresentation.GetActivationHint(_licenseStatus), GetStatusMessageType(_licenseStatus));
	}

	private void DrawActivationControls()
	{
		EditorGUILayout.LabelField("激活", EditorStyles.boldLabel);
		EnsureStatusSnapshot();
		EditorGUI.BeginChangeCheck();
		string text = EditorGUILayout.PasswordField("订单号", _orderNumber);
		if (EditorGUI.EndChangeCheck())
		{
			_orderNumber = text ?? string.Empty;
		}
		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("激活授权", GUILayout.Height(28f)))
		{
			ActivateOrder();
		}
		if (GUILayout.Button("刷新授权", GUILayout.Height(28f)))
		{
			RefreshLicense();
		}
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space(4f);
		EditorGUILayout.BeginHorizontal();
		using (new EditorGUI.DisabledScope(!PsdLicenseService.HasProjectLicense()))
		{
			if (GUILayout.Button("使用授权文件激活", GUILayout.Height(24f)))
			{
				ActivateProjectLicense();
			}
		}
		if (PsdLicenseService.CanSaveProjectLicense() && GUILayout.Button("保存授权文件到插件目录", GUILayout.Height(24f)))
		{
			ShowSaveLicenseWindow();
		}
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.HelpBox(PsdLicenseService.GetProjectLicenseHint(), MessageType.None);
	}

	private void DrawCurrentStatus()
	{
		EnsureStatusSnapshot();
		EditorGUILayout.LabelField("当前状态", EditorStyles.boldLabel);
		if (_licenseStatus == null)
		{
			EditorGUILayout.HelpBox("授权状态不可用。", MessageType.Info);
			return;
		}
		using (new EditorGUI.DisabledScope(disabled: true))
		{
			EditorGUILayout.TextField("授权状态", LicenseStatusPresentation.GetStatusLabel(_licenseStatus));
		}
		EditorGUILayout.Space(4f);
		EditorGUILayout.HelpBox(LicenseStatusPresentation.GetDetailedStatusMessage(_licenseStatus), GetStatusMessageType(_licenseStatus));
	}

	private void ActivateOrder()
	{
		string text;
		bool num = PsdLicenseService.ActivateOrder(_orderNumber, out text);
		RefreshStatusSnapshot();
		if (!num)
		{
			switch (EditorUtility.DisplayDialogComplex("Psd2UGUI License", "订单号验证失败，请前往购买获取。", "前往获取订单号", "前往官网", string.Empty))
			{
			case 1:
				Application.OpenURL("https://efunstudio.cn");
				break;
			case 0:
				Application.OpenURL("https://shop106471535.taobao.com");
				break;
			}
		}
		else
		{
			EditorUtility.DisplayDialog("Psd2UGUI License", LicenseStatusPresentation.GetDetailedStatusMessage(_licenseStatus), "确定");
		}
	}

	private void RefreshLicense()
	{
		PsdLicenseService.RefreshLicense(out var _);
		RefreshStatusSnapshot();
		EditorUtility.DisplayDialog("Psd2UGUI License", LicenseStatusPresentation.GetDetailedStatusMessage(_licenseStatus), "确定");
	}

	private void ActivateProjectLicense()
	{
		PsdLicenseService.ActivateProjectLicense(out var _);
		RefreshStatusSnapshot();
		EditorUtility.DisplayDialog("Psd2UGUI License", LicenseStatusPresentation.GetDetailedStatusMessage(_licenseStatus), "确定");
	}

	private void ShowSaveLicenseWindow()
	{
		wR7S733SuTDbtmfJNkP.ShowWindow();
	}

	private void RefreshStatusSnapshot()
	{
		_licenseStatus = PsdLicenseService.GetStatusSnapshot();
		Repaint();
	}

	private void EnsureStatusSnapshot()
	{
		if (_licenseStatus == null)
		{
			_licenseStatus = PsdLicenseService.GetStatusSnapshot();
		}
	}

	private static MessageType GetStatusMessageType(object P_0)
	{
		return LicenseStatusPresentation.GetAvailability(P_0) switch
		{
			(LicenseAvailability)1 => MessageType.Warning, 
			(LicenseAvailability)0 => MessageType.Info, 
			_ => MessageType.Error, 
		};
	}

	public jtV2XO3BDDWFT7igMea()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private jtV2XO3BDDWFT7igMea(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
