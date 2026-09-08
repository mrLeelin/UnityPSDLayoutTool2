using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[InitializeOnLoad]
internal static class Psd2UIFormConfigRepair
{
	[Serializable]
	private sealed class ConfigSnapshot
	{
		public GUIType defaultTextType;

		public GUIType defaultImageType;

		public bool forceUseTMP;

		public string uiFormTemplateGuid;

		public RuleSnapshot[] rules;

		public string readmeDoc;

		public bool convertZh2En;

		public int nineSliceBorderTolerance;

		public string sharedAssetsOutput;

		public string sharedPrefabOutput;

		public ConfigSnapshot()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	[Serializable]
	private sealed class RuleSnapshot
	{
		public GUIType UIType;

		public string UITypeDesc;

		public string[] TypeMatches;

		public string UIPrefabGuid;

		public string UIHelper;

		public string Comment;

		public RuleSnapshot()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	internal const string ConfigAssetPath = "Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset";

	internal const string ConfigJsonPath = "Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.json";

	private static readonly Regex ScriptReferenceRegex;

	private static bool s_IsRepairing;

	private static bool s_IsScheduled;

	static Psd2UIFormConfigRepair()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		ScriptReferenceRegex = new Regex("^(\\s*m_Script:\\s*)\\{fileID:\\s*-?\\d+,\\s*guid:\\s*[0-9a-fA-F]+,\\s*type:\\s*\\d+\\}\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
		ScheduleEnsureConfigReady();
	}

	internal static void TryEnsureConfigReady()
	{
		if (s_IsRepairing)
		{
			return;
		}
		s_IsRepairing = true;
		try
		{
			bool flag = false;
			if (!File.Exists(GetFullPath("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset")))
			{
				flag |= CreateConfigAssetIfMissing();
			}
			if (RepairConfigScriptReference())
			{
				flag = true;
				AssetDatabase.ImportAsset("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset", ImportAssetOptions.ForceSynchronousImport);
			}
			UGUIParser uGUIParser = AssetDatabase.LoadAssetAtPath<UGUIParser>("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset");
			if (uGUIParser == null && File.Exists(GetFullPath("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset")) && RecreateBrokenConfigAsset())
			{
				flag = true;
				uGUIParser = AssetDatabase.LoadAssetAtPath<UGUIParser>("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset");
			}
			if (uGUIParser == null && CreateConfigAssetIfMissing())
			{
				flag = true;
				uGUIParser = AssetDatabase.LoadAssetAtPath<UGUIParser>("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset");
			}
			if (uGUIParser != null && NeedsJsonRestore(uGUIParser) && ImportConfigJson(uGUIParser))
			{
				flag = true;
			}
			if (flag)
			{
				AssetDatabase.SaveAssets();
			}
		}
		finally
		{
			s_IsRepairing = false;
		}
	}

	internal static void ScheduleEnsureConfigReady()
	{
		if (!s_IsScheduled)
		{
			s_IsScheduled = true;
			EditorApplication.delayCall = (EditorApplication.CallbackFunction)Delegate.Combine(EditorApplication.delayCall, new EditorApplication.CallbackFunction(OnDelayCall));
		}
	}

	private static void OnDelayCall()
	{
		s_IsScheduled = false;
		TryEnsureConfigReady();
	}

	private static bool CreateConfigAssetIfMissing()
	{
		if (File.Exists(GetFullPath("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset")))
		{
			return false;
		}
		if (AssetDatabase.LoadAssetAtPath<UGUIParser>("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset") != null)
		{
			return false;
		}
		string text = Path.GetDirectoryName("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset")?.Replace("\\", "/");
		if (!string.IsNullOrWhiteSpace(text) && !AssetDatabase.IsValidFolder(text))
		{
			return false;
		}
		AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UGUIParser>(), "Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset");
		AssetDatabase.ImportAsset("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset", ImportAssetOptions.ForceSynchronousImport);
		return true;
	}

	private static bool RecreateBrokenConfigAsset()
	{
		if (AssetDatabase.LoadAssetAtPath<UGUIParser>("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset") != null)
		{
			return false;
		}
		if (!AssetDatabase.DeleteAsset("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset"))
		{
			return false;
		}
		return CreateConfigAssetIfMissing();
	}

	private static bool RepairConfigScriptReference()
	{
		string fullPath = GetFullPath("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset");
		if (!File.Exists(fullPath))
		{
			return false;
		}
		string text = File.ReadAllText(fullPath, Encoding.UTF8);
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		Match match = ScriptReferenceRegex.Match(text);
		if (match.Success && TryGetCurrentScriptReference(out var guid, out var fileId))
		{
			string text2 = $"{match.Groups[1].Value}{{fileID: {fileId}, guid: {guid}, type: 3}}";
			if (string.Equals(match.Value, text2, StringComparison.Ordinal))
			{
				return false;
			}
			string contents = text.Substring(0, match.Index) + text2 + text.Substring(match.Index + match.Length);
			File.WriteAllText(fullPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			return true;
		}
		return false;
	}

	private static bool NeedsJsonRestore(UGUIParser config)
	{
		SerializedObject serializedObject = new SerializedObject(config);
		SerializedProperty serializedProperty = serializedObject.FindProperty("rules");
		if (serializedProperty != null && serializedProperty.isArray && serializedProperty.arraySize > 0)
		{
			if (serializedObject.FindProperty("uiFormTemplate") == null)
			{
				return true;
			}
			return false;
		}
		return true;
	}

	private static bool ImportConfigJson(UGUIParser config)
	{
		string fullPath = GetFullPath("Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.json");
		if (!File.Exists(fullPath))
		{
			Debug.LogWarning("Psd2UIForm: 配置修复失败，未找到 JSON 配置文件: Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.json");
			return false;
		}
		string text = File.ReadAllText(fullPath, Encoding.UTF8);
		if (!string.IsNullOrWhiteSpace(text))
		{
			ConfigSnapshot configSnapshot = JsonUtility.FromJson<ConfigSnapshot>(text);
			if (configSnapshot != null && configSnapshot.rules != null && configSnapshot.rules.Length != 0)
			{
				SerializedObject serializedObject = new SerializedObject(config);
				serializedObject.FindProperty("defaultTextType").enumValueIndex = (int)configSnapshot.defaultTextType;
				serializedObject.FindProperty("defaultImageType").enumValueIndex = (int)configSnapshot.defaultImageType;
				serializedObject.FindProperty("forceUseTMP").boolValue = configSnapshot.forceUseTMP;
				serializedObject.FindProperty("readmeDoc").stringValue = configSnapshot.readmeDoc ?? string.Empty;
				serializedObject.FindProperty("convertZh2En").boolValue = configSnapshot.convertZh2En;
				serializedObject.FindProperty("nineSliceBorderTolerance").intValue = Mathf.Clamp(configSnapshot.nineSliceBorderTolerance, 0, 255);
				serializedObject.FindProperty("sharedAssetsOutput").stringValue = configSnapshot.sharedAssetsOutput ?? string.Empty;
				serializedObject.FindProperty("sharedPrefabOutput").stringValue = configSnapshot.sharedPrefabOutput ?? string.Empty;
				serializedObject.FindProperty("uiFormTemplate").objectReferenceValue = LoadGuidAsset<GameObject>(configSnapshot.uiFormTemplateGuid);
				SerializedProperty serializedProperty = serializedObject.FindProperty("rules");
				serializedProperty.ClearArray();
				for (int i = 0; i < configSnapshot.rules.Length; i++)
				{
					RuleSnapshot ruleSnapshot = configSnapshot.rules[i];
					serializedProperty.InsertArrayElementAtIndex(i);
					SerializedProperty arrayElementAtIndex = serializedProperty.GetArrayElementAtIndex(i);
					arrayElementAtIndex.FindPropertyRelative("UIType").enumValueIndex = (int)ruleSnapshot.UIType;
					arrayElementAtIndex.FindPropertyRelative("UITypeDesc").stringValue = ruleSnapshot.UITypeDesc ?? string.Empty;
					arrayElementAtIndex.FindPropertyRelative("UIHelper").stringValue = ruleSnapshot.UIHelper ?? string.Empty;
					arrayElementAtIndex.FindPropertyRelative("Comment").stringValue = ruleSnapshot.Comment ?? string.Empty;
					SerializedProperty serializedProperty2 = arrayElementAtIndex.FindPropertyRelative("TypeMatches");
					serializedProperty2.ClearArray();
					if (ruleSnapshot.TypeMatches != null)
					{
						for (int j = 0; j < ruleSnapshot.TypeMatches.Length; j++)
						{
							serializedProperty2.InsertArrayElementAtIndex(j);
							serializedProperty2.GetArrayElementAtIndex(j).stringValue = ruleSnapshot.TypeMatches[j] ?? string.Empty;
						}
					}
					arrayElementAtIndex.FindPropertyRelative("UIPrefab").objectReferenceValue = LoadGuidAsset<GameObject>(ruleSnapshot.UIPrefabGuid);
				}
				serializedObject.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(config);
				Debug.Log("Psd2UIForm: 已自动从 JSON 恢复配置 Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.json");
				return true;
			}
			return false;
		}
		return false;
	}

	private static T LoadGuidAsset<T>(string guid) where T : UnityEngine.Object
	{
		if (string.IsNullOrWhiteSpace(guid))
		{
			return null;
		}
		string text = AssetDatabase.GUIDToAssetPath(guid);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return AssetDatabase.LoadAssetAtPath<T>(text);
		}
		return null;
	}

	private static bool TryGetCurrentScriptReference(out string guid, out long fileId)
	{
		guid = null;
		fileId = 0L;
		UGUIParser uGUIParser = ScriptableObject.CreateInstance<UGUIParser>();
		try
		{
			MonoScript monoScript = MonoScript.FromScriptableObject(uGUIParser);
			return monoScript != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(monoScript, out guid, out fileId);
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(uGUIParser);
		}
	}

	private static string GetFullPath(string assetPath)
	{
		string text = Directory.GetParent(Application.dataPath)?.FullName;
		if (!string.IsNullOrWhiteSpace(text))
		{
			string path = assetPath.Replace('/', Path.DirectorySeparatorChar);
			return Path.Combine(text, path);
		}
		return assetPath;
	}

	internal static bool ContainsRelevantAsset(string[] paths)
	{
		if (paths == null)
		{
			return false;
		}
		foreach (string text in paths)
		{
			if (!string.IsNullOrWhiteSpace(text) && (string.Equals(text, "Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.asset", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Assets/Plugins/PSD2UIForm/Psd2UIFormConfig.json", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Assets/Plugins/PSD2UIForm/Scripts/", StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}
		return false;
	}
}
}
