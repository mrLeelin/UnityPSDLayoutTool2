using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader;
using PathCompatibilityUtilityNamespace;

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

            // REMOVED: nineSliceBorderTolerance (九宫功能已迁移到新算法)

            public string sharedAssetsOutput;

            public string sharedPrefabOutput;

            public AiProviderConfigSnapshot aiProviderConfig;

            private static UGUIParserSnapshot s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static UGUIParserSnapshot GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
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

            private static UGUIParseRuleSnapshot s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static UGUIParseRuleSnapshot GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [Serializable]
        private sealed class AiProviderConfigSnapshot
        {
            public AiProviderKind provider;

            public bool showCliWindow = true;

            private static AiProviderConfigSnapshot s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static AiProviderConfigSnapshot GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private SerializedProperty readmeProperty;

        private SerializedProperty defaultTextType;

        private SerializedProperty defaultImageType;

        private SerializedProperty forceUseTMP;

        private SerializedProperty convertZh2En;

        // REMOVED: nineSliceBorderTolerance (九宫功能已迁移)

        private SerializedProperty sharedAssetsOutput;

        private SerializedProperty sharedPrefabOutput;

        private SerializedProperty aiProviderConfig;

        private string[] textTypesDisplay;

        private int[] textTypes;

        private string[] imageTypesDisplay;

        private int[] imageTypes;

        private static UGUIParserEditor s_UGUIParserEditorObfuscationSentinel;

        private void OnEnable()
        {
            readmeProperty = ((Editor)this).serializedObject.FindProperty("readmeDoc");
            defaultTextType = ((Editor)this).serializedObject.FindProperty("defaultTextType");
            defaultImageType = ((Editor)this).serializedObject.FindProperty("defaultImageType");
            forceUseTMP = ((Editor)this).serializedObject.FindProperty("forceUseTMP");
            convertZh2En = ((Editor)this).serializedObject.FindProperty("convertZh2En");
            // REMOVED: nineSliceBorderTolerance = ... (九宫功能已迁移)
            sharedAssetsOutput = ((Editor)this).serializedObject.FindProperty("sharedAssetsOutput");
            sharedPrefabOutput = ((Editor)this).serializedObject.FindProperty("sharedPrefabOutput");
            aiProviderConfig = ((Editor)this).serializedObject.FindProperty("aiProviderConfig");
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
            ((Editor)this).serializedObject.Update();
            if (GUILayout.Button("使用教程", Array.Empty<GUILayoutOption>()))
            {
                Application.OpenURL("https://efunstudio.cn");
            }
            if (GUILayout.Button("导出使用文档", Array.Empty<GUILayoutOption>()))
            {
                (((Editor)this).target as UGUIParser).ExportReadmeDoc();
            }
            if (GUILayout.Button("导出PS脚本工具", Array.Empty<GUILayoutOption>()))
            {
                (((Editor)this).target as UGUIParser).ExportPhotoshopScripts();
            }
            EditorGUILayout.HorizontalScope val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                if (GUILayout.Button("导出配置json", Array.Empty<GUILayoutOption>()))
                {
                    ExportConfigJson(((Editor)this).serializedObject);
                }
                if (GUILayout.Button("从json导入配置", Array.Empty<GUILayoutOption>()))
                {
                    ImportConfigFromJson(((Editor)this).serializedObject);
                    GUIUtility.ExitGUI();
                }
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }

            // 按钮组：新手引导 + 打开设置
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                if (GUILayout.Button("新手引导", Array.Empty<GUILayoutOption>()))
                {
                    Psd2UIFormOnboardingWindow.ShowWindow();
                }
                if (GUILayout.Button("⚙️ 打开设置", Array.Empty<GUILayoutOption>()))
                {
                    OpenSettingsPage();
                }
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }

            EditorGUILayout.LabelField("使用说明:", Array.Empty<GUILayoutOption>());
            readmeProperty.stringValue = EditorGUILayout.TextArea(readmeProperty.stringValue, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(100f) });
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("授权状态: 默认完全授权", Array.Empty<GUILayoutOption>());
            // REMOVED: 九宫识别容错滑块（已迁移到新算法，不再需要手动配置）
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice = EditorGUILayout.ToggleLeft("导出时自动裁剪九宫格", ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice, Array.Empty<GUILayoutOption>());
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }
            EditorGUILayout.HelpBox("九宫格边框由程序自动识别（三重推断算法：视觉边缘+重复检测+圆角保护），准确率约95%。建议保持关闭，导出后手动检查边框，再通过右键菜单 Psd2UIForm > Crop Minimal 9-Slice 批量裁剪，更安全可控。", (MessageType)1);
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                defaultTextType.intValue = EditorGUILayout.IntPopup("默认文本类型:", defaultTextType.intValue, textTypesDisplay, textTypes, Array.Empty<GUILayoutOption>());
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                defaultImageType.intValue = EditorGUILayout.IntPopup("默认图片类型:", defaultImageType.intValue, imageTypesDisplay, imageTypes, Array.Empty<GUILayoutOption>());
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                forceUseTMP.boolValue = EditorGUILayout.ToggleLeft("强制优先使用TMP", forceUseTMP.boolValue, Array.Empty<GUILayoutOption>());
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                convertZh2En.boolValue = EditorGUILayout.ToggleLeft("中文转拼音", convertZh2En.boolValue, Array.Empty<GUILayoutOption>());
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                sharedAssetsOutput.stringValue = EditorGUILayout.TextField("复用图片导出路径", sharedAssetsOutput.stringValue, Array.Empty<GUILayoutOption>());
                if (GUILayout.Button("Select", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(60f) }))
                {
                    string text = EditorUtility.OpenFolderPanel("选择导出路径", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(text))
                    {
                        sharedAssetsOutput.stringValue = PathCompatibilityUtility.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text);
                    }
                }
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }
            val = new EditorGUILayout.HorizontalScope(Array.Empty<GUILayoutOption>());
            try
            {
                sharedPrefabOutput.stringValue = EditorGUILayout.TextField("复用prefab导出路径", sharedPrefabOutput.stringValue, Array.Empty<GUILayoutOption>());
                if (GUILayout.Button("Select", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(60f) }))
                {
                    string text2 = EditorUtility.OpenFolderPanel("选择导出路径", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(text2))
                    {
                        sharedPrefabOutput.stringValue = PathCompatibilityUtility.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text2);
                    }
                }
            }
            finally
            {
                ((IDisposable)val)?.Dispose();
            }
            DrawAiProviderConfig();
            ((Editor)this).serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }

        private void DrawAiProviderConfig()
        {
            if (aiProviderConfig != null)
            {
                SerializedProperty providerProperty = aiProviderConfig.FindPropertyRelative("provider");
                SerializedProperty val = aiProviderConfig.FindPropertyRelative("showCliWindow");
                AiProviderKind aiProviderKind = ReadAiProviderKind(providerProperty);
                AiProviderKind provider = (AiProviderKind)(object)EditorGUILayout.EnumPopup("AI 智能识别", (Enum)aiProviderKind, Array.Empty<GUILayoutOption>());
                WriteAiProviderKind(providerProperty, provider);
                if (val != null)
                {
                    EditorGUILayout.PropertyField(val, new GUIContent("CLI显示窗口"), Array.Empty<GUILayoutOption>());
                }
                EditorGUILayout.HelpBox("注意: AI智能识别是调用本机已安装配置的Codex、Claude Code、Open Code, 识别需要模型拥有多模态能力(根据图像识别类型), 识别准确度与AI模型能力有关", (MessageType)1);
            }
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
                    Debug.LogError((object)"Failed to parse config json");
                    return;
                }
                serializedObject.FindProperty("defaultTextType").intValue = (int)uGUIParserSnapshot.defaultTextType;
                serializedObject.FindProperty("defaultImageType").intValue = (int)uGUIParserSnapshot.defaultImageType;
                serializedObject.FindProperty("forceUseTMP").boolValue = uGUIParserSnapshot.forceUseTMP;
                serializedObject.FindProperty("readmeDoc").stringValue = uGUIParserSnapshot.readmeDoc;
                serializedObject.FindProperty("convertZh2En").boolValue = uGUIParserSnapshot.convertZh2En;
                // REMOVED: nineSliceBorderTolerance 导入（字段已删除）
                serializedObject.FindProperty("sharedAssetsOutput").stringValue = uGUIParserSnapshot.sharedAssetsOutput;
                serializedObject.FindProperty("sharedPrefabOutput").stringValue = uGUIParserSnapshot.sharedPrefabOutput;
                RestoreAiProviderConfigSnapshot(serializedObject.FindProperty("aiProviderConfig"), uGUIParserSnapshot.aiProviderConfig);
                if (text2.IndexOf("\"showCliWindow\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SerializedProperty obj = serializedObject.FindProperty("aiProviderConfig");
                    SerializedProperty val = ((obj == null) ? null : obj.FindPropertyRelative("showCliWindow"));
                    if (val != null)
                    {
                        val.boolValue = uGUIParserSnapshot.aiProviderConfig != null && uGUIParserSnapshot.aiProviderConfig.showCliWindow;
                    }
                }
                SerializedProperty val2 = serializedObject.FindProperty("uiFormTemplate");
                val2.objectReferenceValue = null;
                if (!string.IsNullOrEmpty(uGUIParserSnapshot.uiFormTemplateGuid))
                {
                    string text3 = AssetDatabase.GUIDToAssetPath(uGUIParserSnapshot.uiFormTemplateGuid);
                    if (!string.IsNullOrEmpty(text3))
                    {
                        GameObject objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(text3);
                        val2.objectReferenceValue = (Object)(object)objectReferenceValue;
                    }
                }
                if (uGUIParserSnapshot.rules != null)
                {
                    SerializedProperty val3 = serializedObject.FindProperty("rules");
                    val3.ClearArray();
                    for (int i = 0; i < uGUIParserSnapshot.rules.Length; i++)
                    {
                        UGUIParseRuleSnapshot uGUIParseRuleSnapshot = uGUIParserSnapshot.rules[i];
                        val3.InsertArrayElementAtIndex(i);
                        SerializedProperty arrayElementAtIndex = val3.GetArrayElementAtIndex(i);
                        arrayElementAtIndex.FindPropertyRelative("UIType").intValue = (int)uGUIParseRuleSnapshot.UIType;
                        arrayElementAtIndex.FindPropertyRelative("UITypeDesc").stringValue = uGUIParseRuleSnapshot.UITypeDesc;
                        arrayElementAtIndex.FindPropertyRelative("UIHelper").stringValue = uGUIParseRuleSnapshot.UIHelper;
                        arrayElementAtIndex.FindPropertyRelative("Comment").stringValue = uGUIParseRuleSnapshot.Comment;
                        SerializedProperty val4 = arrayElementAtIndex.FindPropertyRelative("TypeMatches");
                        val4.ClearArray();
                        if (uGUIParseRuleSnapshot.TypeMatches != null)
                        {
                            for (int j = 0; j < uGUIParseRuleSnapshot.TypeMatches.Length; j++)
                            {
                                val4.InsertArrayElementAtIndex(j);
                                val4.GetArrayElementAtIndex(j).stringValue = uGUIParseRuleSnapshot.TypeMatches[j];
                            }
                        }
                        SerializedProperty val5 = arrayElementAtIndex.FindPropertyRelative("UIPrefab");
                        val5.objectReferenceValue = null;
                        if (!string.IsNullOrEmpty(uGUIParseRuleSnapshot.UIPrefabGuid))
                        {
                            string text4 = AssetDatabase.GUIDToAssetPath(uGUIParseRuleSnapshot.UIPrefabGuid);
                            if (!string.IsNullOrEmpty(text4))
                            {
                                GameObject objectReferenceValue2 = AssetDatabase.LoadAssetAtPath<GameObject>(text4);
                                val5.objectReferenceValue = (Object)(object)objectReferenceValue2;
                            }
                        }
                    }
                }
                serializedObject.ApplyModifiedProperties();

                // 必须落盘：只 ApplyModifiedProperties 的话值只留在内存里，
                // 磁盘上的 .asset 还是旧内容，就会出现"设置页/Inspector 一套值、.asset 文件另一套值"。
                Object targetAsset = serializedObject.targetObject;
                if ((Object)(object)targetAsset != (Object)null)
                {
                    EditorUtility.SetDirty((Object)(object)targetAsset);
                }
                AssetDatabase.SaveAssets();

                Debug.Log((object)("Config imported from " + text + " -> " + AssetDatabase.GetAssetPath((Object)(object)targetAsset)));
            }
            catch (Exception ex)
            {
                Debug.LogError((object)("Import config failed: " + ex.Message));
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
            if ((Object)(object)(serializedObject.targetObject as UGUIParser) == (Object)null)
            {
                return;
            }
            uGUIParserSnapshot.defaultTextType = (GUIType)serializedObject.FindProperty("defaultTextType").intValue;
            uGUIParserSnapshot.defaultImageType = (GUIType)serializedObject.FindProperty("defaultImageType").intValue;
            uGUIParserSnapshot.forceUseTMP = serializedObject.FindProperty("forceUseTMP").boolValue;
            uGUIParserSnapshot.readmeDoc = serializedObject.FindProperty("readmeDoc").stringValue;
            uGUIParserSnapshot.convertZh2En = serializedObject.FindProperty("convertZh2En").boolValue;
            // REMOVED: nineSliceBorderTolerance 导出（字段已删除）
            uGUIParserSnapshot.sharedAssetsOutput = serializedObject.FindProperty("sharedAssetsOutput").stringValue;
            uGUIParserSnapshot.sharedPrefabOutput = serializedObject.FindProperty("sharedPrefabOutput").stringValue;
            uGUIParserSnapshot.aiProviderConfig = BuildAiProviderConfigSnapshot(serializedObject.FindProperty("aiProviderConfig"));
            Object objectReferenceValue = serializedObject.FindProperty("uiFormTemplate").objectReferenceValue;
            if (objectReferenceValue != (Object)null)
            {
                string assetPath = AssetDatabase.GetAssetPath(objectReferenceValue);
                uGUIParserSnapshot.uiFormTemplateGuid = AssetDatabase.AssetPathToGUID(assetPath);
            }
            SerializedProperty val = serializedObject.FindProperty("rules");
            if (val != null && val.isArray)
            {
                List<UGUIParseRuleSnapshot> list = new List<UGUIParseRuleSnapshot>();
                for (int i = 0; i < val.arraySize; i++)
                {
                    SerializedProperty arrayElementAtIndex = val.GetArrayElementAtIndex(i);
                    UGUIParseRuleSnapshot uGUIParseRuleSnapshot = new UGUIParseRuleSnapshot();
                    uGUIParseRuleSnapshot.UIType = (GUIType)arrayElementAtIndex.FindPropertyRelative("UIType").intValue;
                    uGUIParseRuleSnapshot.UITypeDesc = arrayElementAtIndex.FindPropertyRelative("UITypeDesc").stringValue;
                    uGUIParseRuleSnapshot.UIHelper = arrayElementAtIndex.FindPropertyRelative("UIHelper").stringValue;
                    uGUIParseRuleSnapshot.Comment = arrayElementAtIndex.FindPropertyRelative("Comment").stringValue;
                    SerializedProperty val2 = arrayElementAtIndex.FindPropertyRelative("TypeMatches");
                    if (val2 != null && val2.isArray)
                    {
                        List<string> list2 = new List<string>();
                        for (int j = 0; j < val2.arraySize; j++)
                        {
                            list2.Add(val2.GetArrayElementAtIndex(j).stringValue);
                        }
                        uGUIParseRuleSnapshot.TypeMatches = list2.ToArray();
                    }
                    Object objectReferenceValue2 = arrayElementAtIndex.FindPropertyRelative("UIPrefab").objectReferenceValue;
                    if (objectReferenceValue2 != (Object)null)
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
                string contents = JsonUtility.ToJson((object)uGUIParserSnapshot, true);
                File.WriteAllText(text, contents);
                Debug.Log((object)("Config exported to " + text));
                EditorUtility.RevealInFinder(text);
            }
            catch (Exception ex)
            {
                Debug.LogError((object)("Export config failed: " + ex.Message));
            }
        }

        private static AiProviderConfigSnapshot BuildAiProviderConfigSnapshot(SerializedProperty aiProviderConfigProperty)
        {
            if (aiProviderConfigProperty != null)
            {
                return new AiProviderConfigSnapshot
                {
                    provider = ReadAiProviderKind(aiProviderConfigProperty.FindPropertyRelative("provider")),
                    showCliWindow = (aiProviderConfigProperty.FindPropertyRelative("showCliWindow") == null || aiProviderConfigProperty.FindPropertyRelative("showCliWindow").boolValue)
                };
            }
            return null;
        }

        private static void RestoreAiProviderConfigSnapshot(SerializedProperty aiProviderConfigProperty, AiProviderConfigSnapshot snapshot)
        {
            if (aiProviderConfigProperty != null && snapshot != null)
            {
                WriteAiProviderKind(aiProviderConfigProperty.FindPropertyRelative("provider"), snapshot.provider);
                SerializedProperty val = aiProviderConfigProperty.FindPropertyRelative("showCliWindow");
                if (val != null)
                {
                    val.boolValue = snapshot.showCliWindow;
                }
            }
        }

        private static AiProviderKind ReadAiProviderKind(SerializedProperty providerProperty)
        {
            if (providerProperty == null)
            {
                return AiProviderKind.CodexCli;
            }
            switch (providerProperty.intValue)
            {
            default:
                providerProperty.intValue = 1;
                return AiProviderKind.CodexCli;
            case 1:
                return AiProviderKind.CodexCli;
            case 2:
                return AiProviderKind.ClaudeCodeCli;
            case 3:
                return AiProviderKind.OpenCodeCli;
            }
        }

        private static void WriteAiProviderKind(SerializedProperty providerProperty, AiProviderKind provider)
        {
            if (providerProperty != null)
            {
                if ((uint)(provider - 1) > 2u)
                {
                    providerProperty.intValue = 1;
                }
                else
                {
                    providerProperty.intValue = (int)provider;
                }
            }
        }

        private static Psd2UIFormSettingsServer settingsServer;

        private void OpenSettingsPage()
        {
            // 加载 HTML 文件
            string htmlPath = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "UnityPSDLayoutTool2/Assets/PSD2UIForm/Src/Editor/Resources/Psd2UIFormSettings.html"
            );

            if (!System.IO.File.Exists(htmlPath))
            {
                EditorUtility.DisplayDialog("错误", "找不到设置页面文件：\n" + htmlPath, "确定");
                return;
            }

            // 停止旧的服务器
            if (settingsServer != null)
            {
                settingsServer.Stop();
            }

            // 启动新的服务器
            settingsServer = new Psd2UIFormSettingsServer();
            settingsServer.Start((UGUIParser)((Editor)this).target, htmlPath);

            Debug.Log("[Psd2UIForm] 设置页面已在浏览器中打开，服务器运行在 http://localhost:9527");
        }

        private void OnDisable()
        {
            // 注意：这里故意不停止设置页面服务器。
            // OnDisable 会在 Inspector 失去焦点/切换选中对象时触发，一旦停掉服务器，
            // 浏览器里已经打开的设置页面就会保存失败（表现为"没绑定上"）。
            // 服务器由 Psd2UIFormSettingsServer 自己在域重载/退出 Unity 时关闭。
        }

        internal static bool IsUGUIParserEditorObfuscationSentinelNull()
        {
            return (object)s_UGUIParserEditorObfuscationSentinel == null;
        }

        internal static UGUIParserEditor GetUGUIParserEditorObfuscationSentinel()
        {
            return s_UGUIParserEditorObfuscationSentinel;
        }
    }
}
