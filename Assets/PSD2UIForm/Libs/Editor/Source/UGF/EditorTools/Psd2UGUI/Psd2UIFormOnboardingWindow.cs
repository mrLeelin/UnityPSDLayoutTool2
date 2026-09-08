using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

internal sealed class Psd2UIFormOnboardingWindow : EditorWindow
{
	private bool dontShowAgain = true;

	internal static void ShowWindow()
	{
		Psd2UIFormOnboardingWindow window = EditorWindow.GetWindow<Psd2UIFormOnboardingWindow>(utility: true, "Psd2UIForm 新手引导", focus: true);
		window.minSize = new Vector2(420f, 260f);
		window.Show();
	}

	private void OnGUI()
	{
		GUILayout.Space(8f);
		EditorGUILayout.LabelField("Psd2UIForm 新手引导", EditorStyles.boldLabel);
		GUILayout.Space(6f);
		EditorGUILayout.LabelField("第一步：准备 PSD 文件", EditorStyles.boldLabel);
		EditorGUILayout.LabelField("- 把你的 PSD 文件放入工程", EditorStyles.wordWrappedLabel);
		EditorGUILayout.LabelField("或直接使用示例 PSD 文件测试", EditorStyles.wordWrappedLabel);
		if (GUILayout.Button("点击选中示例PSD文件：Psd2UguiForm_UGUI.psd", GUILayout.Height(24f)))
		{
			LocateSamplePsd();
		}
		GUILayout.Space(6f);
		EditorGUILayout.LabelField("第二步：解析 PSD", EditorStyles.boldLabel);
		EditorGUILayout.LabelField("选中PSD文件, 鼠标右键单击 PSD 文件", EditorStyles.wordWrappedLabel);
		EditorGUILayout.LabelField("选择菜单：Psd2UIForm Editor", EditorStyles.wordWrappedLabel);
		GUILayout.Space(6f);
		EditorGUILayout.LabelField("第三步：生成 UIForm", EditorStyles.boldLabel);
		EditorGUILayout.LabelField("在编辑界面点击“生成UIForm”按钮，自动生成 UI 预制体", EditorStyles.wordWrappedLabel);
		GUILayout.FlexibleSpace();
		dontShowAgain = EditorGUILayout.ToggleLeft("下次不再提示", dontShowAgain);
		GUILayout.Space(6f);
		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("视频教程", GUILayout.Height(28f)))
		{
			Application.OpenURL("https://www.bilibili.com/video/BV1SVUkBWE2v");
		}
		if (GUILayout.Button("知道了", GUILayout.Height(28f)))
		{
			if (dontShowAgain)
			{
				Psd2UIFormOnboarding.MarkShown();
			}
			Close();
		}
		EditorGUILayout.EndHorizontal();
		GUILayout.Space(8f);
	}

	private static void LocateSamplePsd()
	{
		string[] array = AssetDatabase.FindAssets("Psd2UguiForm_UGUI t:DefaultAsset t:Texture2D");
		if (array != null && array.Length != 0)
		{
			Object obj = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(array[0]));
			if (obj != null)
			{
				EditorGUIUtility.PingObject(obj);
				Selection.activeObject = obj;
				return;
			}
		}
		EditorUtility.DisplayDialog("未找到示例 PSD", "请确认示例文件已导入工程：Psd2UguiForm_UGUI.psd", "确定");
	}

	public Psd2UIFormOnboardingWindow()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private Psd2UIFormOnboardingWindow(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
