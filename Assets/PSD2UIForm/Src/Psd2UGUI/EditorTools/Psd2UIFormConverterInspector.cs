using System;
using System.IO;
using AiHierarchyAnalysisOrchestratorNamespace;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader;
using PathCompatibilityUtilityNamespace;

namespace UGF.EditorTools.Psd2UGUI
{
    [CustomEditor(typeof(Psd2UIFormConverter))]
    internal sealed class Psd2UIFormConverterInspector : Editor
    {
        private Psd2UIFormConverter targetLogic;

        private GUIContent parsePsd2NodesBt;

        private GUIContent exportUISpritesBt;

        private GUIContent aiAutoFixBt;

        private GUIContent applyAiResultBt;

        private GUIContent normalizeStructureBt;

        private GUIContent generateUIFormBt;

        private GUIContent openParserConfigBt;

        private GUILayoutOption btHeight;

        private bool metadataDebugFoldout;

        private int metadataDebugSelection;

        private bool metadataDebugShowJson;

        private Vector2 metadataDebugScroll;

        internal static Psd2UIFormConverterInspector s_Psd2UIFormConverterInspectorObfuscationSentinel;

        private void OnEnable()
        {
            btHeight = GUILayout.Height(30f);
            targetLogic = ((Editor)this).target as Psd2UIFormConverter;
            parsePsd2NodesBt = new GUIContent("解析psd图层", "把psd图层解析为可编辑节点树");
            exportUISpritesBt = new GUIContent("导出Images", "导出勾选的psd图层为碎图");
            aiAutoFixBt = new GUIContent("AI自动识别UI类型", "导出当前节点树和预览图，调用AI自动识别并修正UI类型与结构");
            applyAiResultBt = new GUIContent("应用AI结果", "应用当前 AI 识别数据 到节点树");
            normalizeStructureBt = new GUIContent("修正树结构", "按本地 owner 规则修正层级，并刷新主控件对子控件的引用");
            generateUIFormBt = new GUIContent("生成UIForm", "根据解析后的节点树生成UIForm Prefab");
            openParserConfigBt = new GUIContent("打开设置", "选中并定位PSD2UIForm全局解析和生成规则配置");
            if (ScriptableSingleton<Psd2UIFormSettings>.Instance.CompressImage)
            {
                ScriptableSingleton<Psd2UIFormSettings>.Instance.CompressImage = false;
                ScriptableSingleton<Psd2UIFormSettings>.SaveInstance();
            }
            if (string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir))
            {
                Debug.LogWarning((object)"UIForm输出路径为空!");
            }
        }

        private void OnDisable()
        {
            ScriptableSingleton<Psd2UIFormSettings>.SaveInstance();
        }

        public override void OnInspectorGUI()
        {
            bool flag = false;
            DrawPendingUpdateTip();
            if (targetLogic.IsDocumentLoaded())
            {
                EditorGUILayout.BeginVertical((GUIStyle)("box"), Array.Empty<GUILayoutOption>());
                if (GUILayout.Button("查看使用文档", Array.Empty<GUILayoutOption>()))
                {
                    Application.OpenURL("https://efunstudio.cn");
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(openParserConfigBt, Array.Empty<GUILayoutOption>()))
                {
                    SelectUGUIParserConfig();
                    flag = true;
                }
                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                EditorGUILayout.LabelField("UI图片导出路径:", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(150f) });
                ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir = EditorGUILayout.TextField(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir, Array.Empty<GUILayoutOption>());
                if (GUILayout.Button("选择路径", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) }))
                {
                    string text = EditorUtility.OpenFolderPanel("选择导出路径", ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir, (string)null);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (!text.StartsWith("Assets/"))
                        {
                            text = PathCompatibilityUtility.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text);
                        }
                        ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir = text;
                        ScriptableSingleton<Psd2UIFormSettings>.SaveInstance();
                    }
                    flag = true;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir = EditorGUILayout.ToggleLeft("使用UIForm导出路径:", ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(150f) });
                EditorGUI.BeginDisabledGroup(!ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir);
                ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir = EditorGUILayout.TextField(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir, Array.Empty<GUILayoutOption>());
                if (GUILayout.Button("选择路径", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) }))
                {
                    string text2 = EditorUtility.OpenFolderPanel("选择导出路径", ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir, (string)null);
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        if (!text2.StartsWith("Assets/"))
                        {
                            text2 = PathCompatibilityUtility.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text2);
                        }
                        ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir = text2;
                        ScriptableSingleton<Psd2UIFormSettings>.SaveInstance();
                    }
                    flag = true;
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                if (GUILayout.Button(parsePsd2NodesBt, (GUILayoutOption[])(object)new GUILayoutOption[1] { btHeight }) && TryGetKeepExistingUITypeSelection(out var keepExistingUIType))
                {
                    Psd2UIFormConverter.CreateOrUpdateEditorPrefabFromPsd(targetLogic.GetSourcePsdAssetPath(), targetLogic, keepExistingUIType);
                }
                if (GUILayout.Button(exportUISpritesBt, (GUILayoutOption[])(object)new GUILayoutOption[1] { btHeight }))
                {
                    targetLogic.ExportMarkedLayerImages();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                if (GUILayout.Button(aiAutoFixBt, (GUILayoutOption[])(object)new GUILayoutOption[1] { btHeight }) && !AiHierarchyAnalysisOrchestrator.StartRecognitionJob(targetLogic, out var text3))
                {
                    EditorUtility.DisplayDialog("AI修正启动失败", text3, "确定");
                }
                if (GUILayout.Button(applyAiResultBt, (GUILayoutOption[])(object)new GUILayoutOption[1] { btHeight }) && !AiHierarchyAnalysisOrchestrator.TryApplyLatestPatchWithUndo(targetLogic, out var text4))
                {
                    EditorUtility.DisplayDialog("应用AI结果失败", text4, "确定");
                }
                if (GUILayout.Button(normalizeStructureBt, (GUILayoutOption[])(object)new GUILayoutOption[1] { btHeight }))
                {
                    if (!targetLogic.RunLocalNormalization(true, out var text5))
                    {
                        EditorUtility.DisplayDialog("本地归一化失败", text5, "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("本地归一化完成", text5, "确定");
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button(generateUIFormBt, (GUILayoutOption[])(object)new GUILayoutOption[1] { btHeight }))
                {
                    targetLogic.ExportUIFormPrefab();
                }
                DrawMetadataDebugPanel();
            base.OnInspectorGUI();
                if (flag)
                {
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("请打开Prefab,进入Psd2UIForm编辑界面后才能操作!", (MessageType)3);
                if (GUILayout.Button("打开编辑界面", Array.Empty<GUILayoutOption>()))
                {
                    Psd2UIFormConverter.OpenEditorPrefab(AssetDatabase.GetAssetPath(((Editor)this).target));
                }
            }
        }

        private static void SelectUGUIParserConfig()
        {
            UGUIParser uGUIParser = UGUIParser.Instance;
            if ((Object)(object)uGUIParser == (Object)null)
            {
                EditorUtility.DisplayDialog("UGUIParser配置未找到", "项目中未找到 UGUIParser 配置实例。", "确定");
                return;
            }
            Selection.activeObject = (Object)(object)uGUIParser;
            EditorGUIUtility.PingObject((Object)(object)uGUIParser);
        }

        private static void DrawPendingUpdateTip()
        {
            // 已删除 License/更新检查
        }

        private static bool TryGetKeepExistingUITypeSelection(out bool keepExistingUIType)
        {
            int num = EditorUtility.DisplayDialogComplex("重新解析PSD图层", "重新解析会重建当前节点树。\n\n是否保留现有UI类型？\n\n选择“是”会按当前节点层级尽量回填已有 UI Type，新图层仍按默认规则初始化。\n选择“否”会按PSD图层标记重新初始化 UI Type。", "是，保留现有UI类型", "否，重新解析UI类型", "取消");
            keepExistingUIType = num == 0;
            return num != 2;
        }

        private void DrawMetadataDebugPanel()
        {
            metadataDebugFoldout = EditorGUILayout.Foldout(metadataDebugFoldout, "生成元数据调试", true);
            if (!metadataDebugFoldout)
            {
                return;
            }
            EditorGUILayout.BeginVertical((GUIStyle)("box"), Array.Empty<GUILayoutOption>());
            EditorGUILayout.LabelField("数据宿主:", targetLogic.GetMetadataOwnerPath() ?? "<None>", Array.Empty<GUILayoutOption>());
            string[] array = targetLogic.GetGeneratedMetadataPrefabPaths();
            if (array == null || array.Length < 1)
            {
                metadataDebugShowJson = false;
                EditorGUILayout.HelpBox("当前未找到生成元数据。先执行一次生成UIForm或增量导出后再查看。", (MessageType)1);
            }
            else
            {
                metadataDebugSelection = Mathf.Clamp(metadataDebugSelection, 0, array.Length - 1);
                metadataDebugSelection = EditorGUILayout.Popup("目标Prefab", metadataDebugSelection, array, Array.Empty<GUILayoutOption>());
                string text = array[metadataDebugSelection];
                EditorGUILayout.LabelField("路径:", text, Array.Empty<GUILayoutOption>());
                string text2 = targetLogic.GetGeneratedMetadataJson(text);
                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                if (GUILayout.Button(metadataDebugShowJson ? "隐藏内容" : "查看元数据", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(100f) }))
                {
                    metadataDebugShowJson = !metadataDebugShowJson;
                }
                if (GUILayout.Button("复制JSON", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(100f) }))
                {
                    EditorGUIUtility.systemCopyBuffer = text2;
                }
                EditorGUILayout.EndHorizontal();
                if (metadataDebugShowJson)
                {
                    metadataDebugScroll = EditorGUILayout.BeginScrollView(metadataDebugScroll, (GUILayoutOption[])(object)new GUILayoutOption[2]
                    {
                        GUILayout.MinHeight(140f),
                        GUILayout.MaxHeight(320f)
                    });
                    EditorGUILayout.TextArea(text2 ?? string.Empty, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandHeight(true) });
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();
        }

        public override bool HasPreviewGUI()
        {
            return (Object)(object)targetLogic.GetPreviewSprite() != (Object)null;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            GUI.DrawTexture(r, (Texture)(object)targetLogic.GetPreviewSprite().texture, (ScaleMode)2);
        }

        internal static bool IsPsd2UIFormConverterInspectorObfuscationSentinelNull()
        {
            return (object)s_Psd2UIFormConverterInspectorObfuscationSentinel == null;
        }

        internal static Psd2UIFormConverterInspector GetPsd2UIFormConverterInspectorObfuscationSentinel()
        {
            return s_Psd2UIFormConverterInspectorObfuscationSentinel;
        }
    }
}
