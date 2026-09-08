using System;
using System.Collections.Generic;
using System.IO;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;
using PsdPathUtilities;

namespace UGF.EditorTools.Psd2UGUI
{

[CustomEditor(typeof(UGUIParser))]
internal sealed class UGUIParserEditor : Editor
{
	[Serializable]
	private sealed class UGUIParserSnapshot
	{
		public GUIType defaultTextType;

		public GUIType defaultImageType;

		public bool forceUseTMP;

		public string uiFormTemplateGuid;

		public UGUIParseRuleSnapshot[] rules;

		public string readmeDoc;

		public bool convertZh2En;

		public int nineSliceBorderTolerance;

		public string sharedAssetsOutput;

		public string sharedPrefabOutput;

		public UGUIParserSnapshot()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	[Serializable]
	private sealed class UGUIParseRuleSnapshot
	{
		public GUIType UIType;

		public string UITypeDesc;

		public string[] TypeMatches;

		public string UIPrefabGuid;

		public string UIHelper;

		public string Comment;

		public UGUIParseRuleSnapshot()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private SerializedProperty readmeProperty;

	private SerializedProperty defaultTextType;

	private SerializedProperty defaultImageType;

	private SerializedProperty forceUseTMP;

	private SerializedProperty convertZh2En;

	private SerializedProperty nineSliceBorderTolerance;

	private SerializedProperty sharedAssetsOutput;

	private SerializedProperty sharedPrefabOutput;

	private string[] textTypesDisplay;

	private int[] textTypes;

	private string[] imageTypesDisplay;

	private int[] imageTypes;

	private void OnEnable()
	{
		readmeProperty = base.serializedObject.FindProperty("readmeDoc");
		defaultTextType = base.serializedObject.FindProperty("defaultTextType");
		defaultImageType = base.serializedObject.FindProperty("defaultImageType");
		forceUseTMP = base.serializedObject.FindProperty("forceUseTMP");
		convertZh2En = base.serializedObject.FindProperty("convertZh2En");
		nineSliceBorderTolerance = base.serializedObject.FindProperty("nineSliceBorderTolerance");
		sharedAssetsOutput = base.serializedObject.FindProperty("sharedAssetsOutput");
		sharedPrefabOutput = base.serializedObject.FindProperty("sharedPrefabOutput");
		GUIType[] array = new GUIType[2]
		{
			GUIType.Text,
			GUIType.TMPText
		};
		textTypes = new int[array.Length];
		textTypesDisplay = new string[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			GUIType gUIType = array[i];
			textTypes[i] = (int)gUIType;
			textTypesDisplay[i] = gUIType.ToString();
		}
		GUIType[] array2 = new GUIType[2]
		{
			GUIType.Image,
			GUIType.RawImage
		};
		imageTypes = new int[array2.Length];
		imageTypesDisplay = new string[array2.Length];
		for (int j = 0; j < array2.Length; j++)
		{
			GUIType gUIType2 = array2[j];
			imageTypes[j] = (int)gUIType2;
			imageTypesDisplay[j] = gUIType2.ToString();
		}
	}

	public override void OnInspectorGUI()
	{
		base.serializedObject.Update();
		PsdReaderProductAccess.EnsureDailyUpdateCheck();
		if (GUILayout.Button("使用教程"))
		{
			Application.OpenURL("https://efunstudio.cn");
		}
		if (GUILayout.Button("导出使用文档"))
		{
			(base.target as UGUIParser).ExportDesignerDocumentation();
		}
		if (GUILayout.Button("导出PS脚本工具"))
		{
			(base.target as UGUIParser).ExportAndDeployPhotoshopScriptConfig();
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("导出配置json"))
			{
				ExportConfigJson(base.serializedObject);
			}
			if (GUILayout.Button("从json导入配置"))
			{
				ImportConfigFromJson(base.serializedObject);
				GUIUtility.ExitGUI();
			}
		}
		if (GUILayout.Button("新手引导"))
		{
			Psd2UIFormOnboardingWindow.ShowWindow();
		}
		EditorGUILayout.LabelField("使用说明:");
		readmeProperty.stringValue = EditorGUILayout.TextArea(readmeProperty.stringValue, GUILayout.Height(100f));
		EditorGUILayout.Space(4f);
		EditorGUILayout.LabelField("插件授权:");
		using (new EditorGUILayout.VerticalScope("box"))
		{
			EditorGUILayout.LabelField("当前状态:", PsdReaderProductAccess.GetStatusLabel());
			MessageType type = ((!PsdReaderProductAccess.NeedsAttention) ? MessageType.Info : MessageType.Warning);
			EditorGUILayout.HelpBox(PsdReaderProductAccess.GetOverviewMessage(), type);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("授权管理"))
				{
					PsdReaderProductAccess.OpenManagementWindow();
				}
				if (GUILayout.Button("获取订单号"))
				{
					Application.OpenURL("https://shop106471535.taobao.com");
				}
			}
			if (PsdReaderProductAccess.HasPendingUpdateTip() && Psd2UIFormEditorNoticeUtility.DrawVersionUpdateNotice(PsdReaderProductAccess.GetPendingUpdateTipMessage(), "下载") && PsdReaderProductAccess.TryOpenPendingUpdateDownloadUrl())
			{
				GUIUtility.ExitGUI();
			}
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			nineSliceBorderTolerance.intValue = EditorGUILayout.IntSlider("九宫识别容错(默认:5)", nineSliceBorderTolerance.intValue, 1, 10);
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice = EditorGUILayout.ToggleLeft("导出时自动裁剪九宫格", ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice);
		}
		EditorGUILayout.HelpBox("九宫格边框由程序自动识别，结果可能与预期不符。建议保持关闭，导出后手动检查边框，再通过右键菜单 Psd2UIForm > Crop Minimal 9-Slice 批量裁剪，更安全可控。", MessageType.Info);
		using (new EditorGUILayout.HorizontalScope())
		{
			defaultTextType.enumValueIndex = EditorGUILayout.IntPopup("默认文本类型:", defaultTextType.enumValueIndex, textTypesDisplay, textTypes);
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			defaultImageType.enumValueIndex = EditorGUILayout.IntPopup("默认图片类型:", defaultImageType.enumValueIndex, imageTypesDisplay, imageTypes);
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			forceUseTMP.boolValue = EditorGUILayout.ToggleLeft("强制优先使用TMP", forceUseTMP.boolValue);
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			convertZh2En.boolValue = EditorGUILayout.ToggleLeft("中文转拼音", convertZh2En.boolValue);
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			sharedAssetsOutput.stringValue = EditorGUILayout.TextField("复用图片导出路径", sharedAssetsOutput.stringValue);
			if (GUILayout.Button("Select", GUILayout.Width(60f)))
			{
				string text = EditorUtility.OpenFolderPanel("选择导出路径", Application.dataPath, "");
				if (!string.IsNullOrEmpty(text))
				{
					sharedAssetsOutput.stringValue = RelativePathUtilities.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text);
				}
			}
		}
		using (new EditorGUILayout.HorizontalScope())
		{
			sharedPrefabOutput.stringValue = EditorGUILayout.TextField("复用prefab导出路径", sharedPrefabOutput.stringValue);
			if (GUILayout.Button("Select", GUILayout.Width(60f)))
			{
				string text2 = EditorUtility.OpenFolderPanel("选择导出路径", Application.dataPath, "");
				if (!string.IsNullOrEmpty(text2))
				{
					sharedPrefabOutput.stringValue = RelativePathUtilities.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text2);
				}
			}
		}
		base.serializedObject.ApplyModifiedProperties();
		base.OnInspectorGUI();
	}

	private void ImportConfigFromJson(SerializedObject serializedObject)
	{
		string text = EditorUtility.OpenFilePanel("Select Config Json", Application.dataPath, "json");
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			string text2 = File.ReadAllText(text);
			UGUIParserSnapshot uGUIParserSnapshot = JsonUtility.FromJson<UGUIParserSnapshot>(text2);
			if (uGUIParserSnapshot == null)
			{
				Debug.LogError("Failed to parse config json");
				return;
			}
			serializedObject.FindProperty("defaultTextType").enumValueIndex = (int)uGUIParserSnapshot.defaultTextType;
			serializedObject.FindProperty("defaultImageType").enumValueIndex = (int)uGUIParserSnapshot.defaultImageType;
			serializedObject.FindProperty("forceUseTMP").boolValue = uGUIParserSnapshot.forceUseTMP;
			serializedObject.FindProperty("readmeDoc").stringValue = uGUIParserSnapshot.readmeDoc;
			serializedObject.FindProperty("convertZh2En").boolValue = uGUIParserSnapshot.convertZh2En;
			if (text2.IndexOf("\"nineSliceBorderTolerance\"", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				serializedObject.FindProperty("nineSliceBorderTolerance").intValue = Mathf.Clamp(uGUIParserSnapshot.nineSliceBorderTolerance, 0, 255);
			}
			serializedObject.FindProperty("sharedAssetsOutput").stringValue = uGUIParserSnapshot.sharedAssetsOutput;
			serializedObject.FindProperty("sharedPrefabOutput").stringValue = uGUIParserSnapshot.sharedPrefabOutput;
			SerializedProperty serializedProperty = serializedObject.FindProperty("uiFormTemplate");
			serializedProperty.objectReferenceValue = null;
			if (!string.IsNullOrEmpty(uGUIParserSnapshot.uiFormTemplateGuid))
			{
				string text3 = AssetDatabase.GUIDToAssetPath(uGUIParserSnapshot.uiFormTemplateGuid);
				if (!string.IsNullOrEmpty(text3))
				{
					GameObject objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(text3);
					serializedProperty.objectReferenceValue = objectReferenceValue;
				}
			}
			if (uGUIParserSnapshot.rules != null)
			{
				SerializedProperty serializedProperty2 = serializedObject.FindProperty("rules");
				serializedProperty2.ClearArray();
				for (int i = 0; i < uGUIParserSnapshot.rules.Length; i++)
				{
					UGUIParseRuleSnapshot uGUIParseRuleSnapshot = uGUIParserSnapshot.rules[i];
					serializedProperty2.InsertArrayElementAtIndex(i);
					SerializedProperty arrayElementAtIndex = serializedProperty2.GetArrayElementAtIndex(i);
					arrayElementAtIndex.FindPropertyRelative("UIType").enumValueIndex = (int)uGUIParseRuleSnapshot.UIType;
					arrayElementAtIndex.FindPropertyRelative("UITypeDesc").stringValue = uGUIParseRuleSnapshot.UITypeDesc;
					arrayElementAtIndex.FindPropertyRelative("UIHelper").stringValue = uGUIParseRuleSnapshot.UIHelper;
					arrayElementAtIndex.FindPropertyRelative("Comment").stringValue = uGUIParseRuleSnapshot.Comment;
					SerializedProperty serializedProperty3 = arrayElementAtIndex.FindPropertyRelative("TypeMatches");
					serializedProperty3.ClearArray();
					if (uGUIParseRuleSnapshot.TypeMatches != null)
					{
						for (int j = 0; j < uGUIParseRuleSnapshot.TypeMatches.Length; j++)
						{
							serializedProperty3.InsertArrayElementAtIndex(j);
							serializedProperty3.GetArrayElementAtIndex(j).stringValue = uGUIParseRuleSnapshot.TypeMatches[j];
						}
					}
					SerializedProperty serializedProperty4 = arrayElementAtIndex.FindPropertyRelative("UIPrefab");
					serializedProperty4.objectReferenceValue = null;
					if (!string.IsNullOrEmpty(uGUIParseRuleSnapshot.UIPrefabGuid))
					{
						string text4 = AssetDatabase.GUIDToAssetPath(uGUIParseRuleSnapshot.UIPrefabGuid);
						if (!string.IsNullOrEmpty(text4))
						{
							GameObject objectReferenceValue2 = AssetDatabase.LoadAssetAtPath<GameObject>(text4);
							serializedProperty4.objectReferenceValue = objectReferenceValue2;
						}
					}
				}
			}
			serializedObject.ApplyModifiedProperties();
			Debug.Log("Config imported from " + text);
		}
		catch (Exception ex)
		{
			Debug.LogError("Import config failed: " + ex.Message);
		}
	}

	private void ExportConfigJson(SerializedObject serializedObject)
	{
		string text = EditorUtility.SaveFilePanel("Save Config Json", Application.dataPath, "Psd2UIFormConfig", "json");
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		UGUIParserSnapshot uGUIParserSnapshot = new UGUIParserSnapshot();
		if (serializedObject.targetObject as UGUIParser == null)
		{
			return;
		}
		uGUIParserSnapshot.defaultTextType = (GUIType)serializedObject.FindProperty("defaultTextType").enumValueIndex;
		uGUIParserSnapshot.defaultImageType = (GUIType)serializedObject.FindProperty("defaultImageType").enumValueIndex;
		uGUIParserSnapshot.forceUseTMP = serializedObject.FindProperty("forceUseTMP").boolValue;
		uGUIParserSnapshot.readmeDoc = serializedObject.FindProperty("readmeDoc").stringValue;
		uGUIParserSnapshot.convertZh2En = serializedObject.FindProperty("convertZh2En").boolValue;
		uGUIParserSnapshot.nineSliceBorderTolerance = Mathf.Clamp(serializedObject.FindProperty("nineSliceBorderTolerance").intValue, 0, 255);
		uGUIParserSnapshot.sharedAssetsOutput = serializedObject.FindProperty("sharedAssetsOutput").stringValue;
		uGUIParserSnapshot.sharedPrefabOutput = serializedObject.FindProperty("sharedPrefabOutput").stringValue;
		UnityEngine.Object objectReferenceValue = serializedObject.FindProperty("uiFormTemplate").objectReferenceValue;
		if (objectReferenceValue != null)
		{
			string assetPath = AssetDatabase.GetAssetPath(objectReferenceValue);
			uGUIParserSnapshot.uiFormTemplateGuid = AssetDatabase.AssetPathToGUID(assetPath);
		}
		SerializedProperty serializedProperty = serializedObject.FindProperty("rules");
		if (serializedProperty != null && serializedProperty.isArray)
		{
			List<UGUIParseRuleSnapshot> list = new List<UGUIParseRuleSnapshot>();
			for (int i = 0; i < serializedProperty.arraySize; i++)
			{
				SerializedProperty arrayElementAtIndex = serializedProperty.GetArrayElementAtIndex(i);
				UGUIParseRuleSnapshot uGUIParseRuleSnapshot = new UGUIParseRuleSnapshot();
				uGUIParseRuleSnapshot.UIType = (GUIType)arrayElementAtIndex.FindPropertyRelative("UIType").enumValueIndex;
				uGUIParseRuleSnapshot.UITypeDesc = arrayElementAtIndex.FindPropertyRelative("UITypeDesc").stringValue;
				uGUIParseRuleSnapshot.UIHelper = arrayElementAtIndex.FindPropertyRelative("UIHelper").stringValue;
				uGUIParseRuleSnapshot.Comment = arrayElementAtIndex.FindPropertyRelative("Comment").stringValue;
				SerializedProperty serializedProperty2 = arrayElementAtIndex.FindPropertyRelative("TypeMatches");
				if (serializedProperty2 != null && serializedProperty2.isArray)
				{
					List<string> list2 = new List<string>();
					for (int j = 0; j < serializedProperty2.arraySize; j++)
					{
						list2.Add(serializedProperty2.GetArrayElementAtIndex(j).stringValue);
					}
					uGUIParseRuleSnapshot.TypeMatches = list2.ToArray();
				}
				UnityEngine.Object objectReferenceValue2 = arrayElementAtIndex.FindPropertyRelative("UIPrefab").objectReferenceValue;
				if (objectReferenceValue2 != null)
				{
					string assetPath2 = AssetDatabase.GetAssetPath(objectReferenceValue2);
					uGUIParseRuleSnapshot.UIPrefabGuid = AssetDatabase.AssetPathToGUID(assetPath2);
				}
				list.Add(uGUIParseRuleSnapshot);
			}
			uGUIParserSnapshot.rules = list.ToArray();
		}
		try
		{
			string contents = JsonUtility.ToJson(uGUIParserSnapshot, prettyPrint: true);
			File.WriteAllText(text, contents);
			Debug.Log("Config exported to " + text);
			EditorUtility.RevealInFinder(text);
		}
		catch (Exception ex)
		{
			Debug.LogError("Export config failed: " + ex.Message);
		}
	}

	public UGUIParserEditor()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private UGUIParserEditor(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
