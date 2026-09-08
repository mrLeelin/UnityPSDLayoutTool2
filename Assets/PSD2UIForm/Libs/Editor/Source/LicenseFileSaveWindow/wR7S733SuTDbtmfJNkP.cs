using PsdLicensing;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;

namespace pcFLST3L4fps8LGdKbk
{

internal sealed class wR7S733SuTDbtmfJNkP : EditorWindow
{
	[SerializeField]
	private int m_ValidDays;

	internal static void ShowWindow()
	{
		wR7S733SuTDbtmfJNkP window = EditorWindow.GetWindow<wR7S733SuTDbtmfJNkP>(utility: true, "保存授权文件", focus: true);
		window.minSize = new Vector2(520f, 150f);
		window.maxSize = new Vector2(520f, 150f);
		if (window.m_ValidDays <= 0)
		{
			window.m_ValidDays = PsdLicenseService.GetDefaultExportValidDays();
		}
		window.ShowUtility();
	}

	private void OnEnable()
	{
		if (m_ValidDays <= 0)
		{
			m_ValidDays = PsdLicenseService.GetDefaultExportValidDays();
		}
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("授权文件有效期", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("请输入有效期天数。授权文件过期后会自动失效并删除。", MessageType.None);
		if (PsdLicenseService.CheckProjectLicenseExportEligibility(out var message))
		{
			m_ValidDays = Mathf.Clamp(EditorGUILayout.IntField("时长(天)", Mathf.Clamp(Mathf.Max(1, m_ValidDays), 1, 36500)), 1, 36500);
			EditorGUILayout.Space(6f);
			EditorGUILayout.BeginHorizontal();
			if (DrawDurationPresetButton("3个月", 90) || DrawDurationPresetButton("6个月", 180) || DrawDurationPresetButton("1年", 365) || DrawDurationPresetButton("2年", 730) || DrawDurationPresetButton("3年", 1095) || DrawDurationPresetButton("永久", 36500))
			{
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space(8f);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("保存", GUILayout.Height(24f)) && SaveProjectLicense(m_ValidDays))
			{
				GUIUtility.ExitGUI();
			}
			if (GUILayout.Button("取消", GUILayout.Height(24f)))
			{
				Close();
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.EndHorizontal();
		}
		else
		{
			EditorGUILayout.HelpBox(message, MessageType.Warning);
			if (GUILayout.Button("关闭", GUILayout.Height(24f)))
			{
				Close();
				GUIUtility.ExitGUI();
			}
		}
	}

	private bool DrawDurationPresetButton(string P_0, int P_1)
	{
		if (!GUILayout.Button(P_0, GUILayout.Height(24f)))
		{
			return false;
		}
		return SaveProjectLicense(P_1);
	}

	private bool SaveProjectLicense(int P_0)
	{
		if (PsdLicenseService.SaveProjectLicense(Mathf.Max(1, P_0), out var message))
		{
			Debug.Log(message);
			Close();
			return true;
		}
		EditorUtility.DisplayDialog("保存授权文件", message, "确定");
		return false;
	}

	public wR7S733SuTDbtmfJNkP()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private wR7S733SuTDbtmfJNkP(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
