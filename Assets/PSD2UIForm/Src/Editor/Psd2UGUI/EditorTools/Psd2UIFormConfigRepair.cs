using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Psd2UIFormPluginPathResolverNamespace;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    [InitializeOnLoad]
    internal sealed class Psd2UIFormConfigRepair
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

            internal static ConfigSnapshot s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ConfigSnapshot GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
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

            internal static RuleSnapshot s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static RuleSnapshot GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private const string ConfigAssetRelativePath = "Psd2UIFormConfig.asset";

        private const string ConfigJsonRelativePath = "Psd2UIFormConfig.json";

        private const string ScriptsRelativePath = "Scripts/";

        private static readonly Regex ScriptReferenceRegex;

        private static bool s_IsRepairing;

        private static bool s_IsScheduled;

        internal static Psd2UIFormConfigRepair s_ObfuscationSentinel;

        internal static string ConfigAssetPath => Psd2UIFormPluginPathResolver.CombinePluginAssetPath("Psd2UIFormConfig.asset");

        internal static string ConfigJsonPath => Psd2UIFormPluginPathResolver.CombinePluginAssetPath("Psd2UIFormConfig.json");

        private static string ScriptsAssetPathPrefix => Psd2UIFormPluginPathResolver.CombinePluginAssetPath("Scripts/");

        static Psd2UIFormConfigRepair()
        {
            ScriptReferenceRegex = new Regex("^(\\s*m_Script:\\s*)\\{fileID:\\s*-?\\d+,\\s*guid:\\s*[0-9a-fA-F]+,\\s*type:\\s*\\d+\\}\\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
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
                if (!string.IsNullOrWhiteSpace(ConfigAssetPath) && !string.IsNullOrWhiteSpace(ConfigJsonPath))
                {
                    bool flag = false;
                    if (!File.Exists(GetFullPath(ConfigAssetPath)))
                    {
                        flag |= CreateConfigAssetIfMissing();
                    }
                    if (RepairConfigScriptReference())
                    {
                        flag = true;
                        AssetDatabase.ImportAsset(ConfigAssetPath, (ImportAssetOptions)8);
                    }
                    UGUIParser uGUIParser = AssetDatabase.LoadAssetAtPath<UGUIParser>(ConfigAssetPath);
                    if ((Object)(object)uGUIParser == (Object)null && File.Exists(GetFullPath(ConfigAssetPath)) && RecreateBrokenConfigAsset())
                    {
                        flag = true;
                        uGUIParser = AssetDatabase.LoadAssetAtPath<UGUIParser>(ConfigAssetPath);
                    }
                    if ((Object)(object)uGUIParser == (Object)null && CreateConfigAssetIfMissing())
                    {
                        flag = true;
                        uGUIParser = AssetDatabase.LoadAssetAtPath<UGUIParser>(ConfigAssetPath);
                    }
                    if ((Object)(object)uGUIParser != (Object)null && NeedsJsonRestore(uGUIParser) && ImportConfigJson(uGUIParser))
                    {
                        flag = true;
                    }
                    if (flag)
                    {
                        AssetDatabase.SaveAssets();
                    }
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
                EditorApplication.delayCall = (EditorApplication.CallbackFunction)Delegate.Combine((Delegate)(object)EditorApplication.delayCall, (Delegate)new EditorApplication.CallbackFunction(OnDelayCall));
            }
        }

        private static void OnDelayCall()
        {
            s_IsScheduled = false;
            TryEnsureConfigReady();
        }

        private static bool CreateConfigAssetIfMissing()
        {
            if (!File.Exists(GetFullPath(ConfigAssetPath)))
            {
                if (!((Object)(object)AssetDatabase.LoadAssetAtPath<UGUIParser>(ConfigAssetPath) != (Object)null))
                {
                    string text = Path.GetDirectoryName(ConfigAssetPath)?.Replace("\\", "/");
                    if (!string.IsNullOrWhiteSpace(text) && !AssetDatabase.IsValidFolder(text))
                    {
                        return false;
                    }
                    AssetDatabase.CreateAsset((Object)(object)ScriptableObject.CreateInstance<UGUIParser>(), ConfigAssetPath);
                    AssetDatabase.ImportAsset(ConfigAssetPath, (ImportAssetOptions)8);
                    return true;
                }
                return false;
            }
            return false;
        }

        private static bool RecreateBrokenConfigAsset()
        {
            if (!((Object)(object)AssetDatabase.LoadAssetAtPath<UGUIParser>(ConfigAssetPath) != (Object)null))
            {
                if (!AssetDatabase.DeleteAsset(ConfigAssetPath))
                {
                    return false;
                }
                return CreateConfigAssetIfMissing();
            }
            return false;
        }

        private static bool RepairConfigScriptReference()
        {
            string fullPath = GetFullPath(ConfigAssetPath);
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
            if (!match.Success || !TryGetCurrentScriptReference(out var guid, out var fileId))
            {
                return false;
            }
            string text2 = $"{match.Groups[1].Value}{{fileID: {fileId}, guid: {guid}, type: 3}}";
            if (string.Equals(match.Value, text2, StringComparison.Ordinal))
            {
                return false;
            }
            string contents = text.Substring(0, match.Index) + text2 + text.Substring(match.Index + match.Length);
            File.WriteAllText(fullPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }

        private static bool NeedsJsonRestore(UGUIParser config)
        {
            SerializedObject val = new SerializedObject((Object)(object)config);
            SerializedProperty val2 = val.FindProperty("rules");
            if (val2 != null && val2.isArray && val2.arraySize > 0)
            {
                if (val.FindProperty("uiFormTemplate") != null)
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        private static bool ImportConfigJson(UGUIParser config)
        {
            string fullPath = GetFullPath(ConfigJsonPath);
            if (File.Exists(fullPath))
            {
                string text = File.ReadAllText(fullPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                ConfigSnapshot configSnapshot = JsonUtility.FromJson<ConfigSnapshot>(text);
                if (configSnapshot == null || configSnapshot.rules == null || configSnapshot.rules.Length == 0)
                {
                    return false;
                }
                SerializedObject val = new SerializedObject((Object)(object)config);
                // 注意：这里必须用 intValue 而不是 enumValueIndex。
                // GUIType 的值域不是连续的（Background = 101 … ScrollView_VerticalBar = 119），
                // enumValueIndex 是"枚举名数组下标"，用枚举数值去赋值会整体错位。
                val.FindProperty("defaultTextType").intValue = (int)configSnapshot.defaultTextType;
                val.FindProperty("defaultImageType").intValue = (int)configSnapshot.defaultImageType;
                val.FindProperty("forceUseTMP").boolValue = configSnapshot.forceUseTMP;
                val.FindProperty("readmeDoc").stringValue = configSnapshot.readmeDoc ?? string.Empty;
                val.FindProperty("convertZh2En").boolValue = configSnapshot.convertZh2En;

                // nineSliceBorderTolerance 已从 UGUIParser 移除（九宫功能已迁移），旧 JSON 里可能还有这个字段。
                // 直接写会 NullReferenceException，这里做存在性判断。
                SerializedProperty toleranceProperty = val.FindProperty("nineSliceBorderTolerance");
                if (toleranceProperty != null)
                {
                    toleranceProperty.intValue = Mathf.Clamp(configSnapshot.nineSliceBorderTolerance, 0, 255);
                }
                val.FindProperty("sharedAssetsOutput").stringValue = configSnapshot.sharedAssetsOutput ?? string.Empty;
                val.FindProperty("sharedPrefabOutput").stringValue = configSnapshot.sharedPrefabOutput ?? string.Empty;
                val.FindProperty("uiFormTemplate").objectReferenceValue = (Object)(object)LoadGuidAsset<GameObject>(configSnapshot.uiFormTemplateGuid);
                SerializedProperty val2 = val.FindProperty("rules");
                val2.ClearArray();
                for (int i = 0; i < configSnapshot.rules.Length; i++)
                {
                    RuleSnapshot ruleSnapshot = configSnapshot.rules[i];
                    val2.InsertArrayElementAtIndex(i);
                    SerializedProperty arrayElementAtIndex = val2.GetArrayElementAtIndex(i);
                    arrayElementAtIndex.FindPropertyRelative("UIType").intValue = (int)ruleSnapshot.UIType;
                    arrayElementAtIndex.FindPropertyRelative("UITypeDesc").stringValue = ruleSnapshot.UITypeDesc ?? string.Empty;
                    arrayElementAtIndex.FindPropertyRelative("UIHelper").stringValue = ruleSnapshot.UIHelper ?? string.Empty;
                    arrayElementAtIndex.FindPropertyRelative("Comment").stringValue = ruleSnapshot.Comment ?? string.Empty;
                    SerializedProperty val3 = arrayElementAtIndex.FindPropertyRelative("TypeMatches");
                    val3.ClearArray();
                    if (ruleSnapshot.TypeMatches != null)
                    {
                        for (int j = 0; j < ruleSnapshot.TypeMatches.Length; j++)
                        {
                            val3.InsertArrayElementAtIndex(j);
                            val3.GetArrayElementAtIndex(j).stringValue = ruleSnapshot.TypeMatches[j] ?? string.Empty;
                        }
                    }
                    arrayElementAtIndex.FindPropertyRelative("UIPrefab").objectReferenceValue = (Object)(object)LoadGuidAsset<GameObject>(ruleSnapshot.UIPrefabGuid);
                }
                val.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty((Object)(object)config);
                Debug.Log((object)("Psd2UIForm: 已自动从 JSON 恢复配置 " + ConfigJsonPath));
                return true;
            }
            Debug.LogWarning((object)("Psd2UIForm: 配置修复失败，未找到 JSON 配置文件: " + ConfigJsonPath));
            return false;
        }

        private static T LoadGuidAsset<T>(string guid) where T : Object
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return default(T);
            }
            string text = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return AssetDatabase.LoadAssetAtPath<T>(text);
            }
            return default(T);
        }

        private static bool TryGetCurrentScriptReference(out string guid, out long fileId)
        {
            guid = null;
            fileId = 0L;
            UGUIParser uGUIParser = ScriptableObject.CreateInstance<UGUIParser>();
            try
            {
                MonoScript val = MonoScript.FromScriptableObject((ScriptableObject)(object)uGUIParser);
                return (Object)(object)val != (Object)null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier((Object)(object)val, out guid, out fileId);
            }
            finally
            {
                Object.DestroyImmediate((Object)(object)uGUIParser);
            }
        }

        private static string GetFullPath(string assetPath)
        {
            return Psd2UIFormPluginPathResolver.AssetPathToAbsolutePath(assetPath);
        }

        internal static bool ContainsRelevantAsset(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }
            int num = 0;
            while (true)
            {
                if (num < paths.Length)
                {
                    string text = paths[num];
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        string scriptsAssetPathPrefix = ScriptsAssetPathPrefix;
                        if (string.Equals(text, ConfigAssetPath, StringComparison.OrdinalIgnoreCase) || string.Equals(text, ConfigJsonPath, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(scriptsAssetPathPrefix) && text.StartsWith(scriptsAssetPathPrefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            break;
                        }
                    }
                    num++;
                    continue;
                }
                return false;
            }
            return true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Psd2UIFormConfigRepair GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
