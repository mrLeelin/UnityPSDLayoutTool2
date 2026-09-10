using System;
using PsdReaderLicenseServiceNamespace;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

using Object = UnityEngine.Object;
namespace ProjectLicenseExportWindowNamespace
{
    [MovedFrom(true, sourceNamespace: "YbQbC9guKAlGS4iofAA", sourceAssembly: "cn.efunstudio.psd2ugui", sourceClassName: "P6BN1hgxRUsLMpx7WBi")]
    internal sealed class ProjectLicenseExportWindow : EditorWindow
    {
        [SerializeField]
        private int m_ValidDays;

        internal static ProjectLicenseExportWindow s_ObfuscationSentinel;

        internal static void ShowWindow()
        {
            ProjectLicenseExportWindow window = EditorWindow.GetWindow<ProjectLicenseExportWindow>(true, "保存授权文件", true);
            ((EditorWindow)window).minSize = new Vector2(520f, 150f);
            ((EditorWindow)window).maxSize = new Vector2(520f, 150f);
            if (window.m_ValidDays <= 0)
            {
                window.m_ValidDays = PsdReaderLicenseService.GetDefaultProjectLicenseDays();
            }
            ((EditorWindow)window).ShowUtility();
        }

        private void OnEnable()
        {
            if (m_ValidDays <= 0)
            {
                m_ValidDays = PsdReaderLicenseService.GetDefaultProjectLicenseDays();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("授权文件有效期", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            EditorGUILayout.HelpBox("请输入有效期天数。授权文件过期后会自动失效并删除。", (MessageType)0);
            if (PsdReaderLicenseService.TryValidateProjectLicenseExport(out var text))
            {
                m_ValidDays = Mathf.Clamp(EditorGUILayout.IntField("时长(天)", Mathf.Clamp(Mathf.Max(1, m_ValidDays), 1, 36500), Array.Empty<GUILayoutOption>()), 1, 36500);
                EditorGUILayout.Space(6f);
                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                if (DrawPresetButton("3个月", 90) || DrawPresetButton("6个月", 180) || DrawPresetButton("1年", 365) || DrawPresetButton("2年", 730) || DrawPresetButton("3年", 1095) || DrawPresetButton("永久", 36500))
                {
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(8f);
                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                if (GUILayout.Button("保存", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(24f) }) && SaveAndClose(m_ValidDays))
                {
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("取消", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(24f) }))
                {
                    ((EditorWindow)this).Close();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(text, (MessageType)2);
                if (GUILayout.Button("关闭", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(24f) }))
                {
                    ((EditorWindow)this).Close();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private bool DrawPresetButton(string text, int value)
        {
            if (GUILayout.Button(text, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(24f) }))
            {
                return SaveAndClose(value);
            }
            return false;
        }

        private bool SaveAndClose(int value)
        {
            if (!PsdReaderLicenseService.SaveProjectLicenseFile(Mathf.Max(1, value), out var text))
            {
                EditorUtility.DisplayDialog("保存授权文件", text, "确定");
                return false;
            }
            Debug.Log((object)text);
            ((EditorWindow)this).Close();
            return true;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return (object)s_ObfuscationSentinel == null;
        }

        internal static ProjectLicenseExportWindow GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
