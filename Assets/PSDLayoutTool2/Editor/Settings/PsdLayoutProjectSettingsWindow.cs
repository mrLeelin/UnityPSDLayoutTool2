namespace PsdLayoutTool2
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 在独立窗口中编辑项目配置，避免主 Inspector 锁定时无法切换到配置资产。
    /// </summary>
    internal sealed class PsdLayoutProjectSettingsWindow : EditorWindow
    {
        private PsdLayoutProjectSettings settings;
        private UnityEditor.Editor settingsEditor;
        private Vector2 scrollPosition;

        internal static void Open(PsdLayoutProjectSettings targetSettings)
        {
            PsdLayoutProjectSettingsWindow window = GetWindow<PsdLayoutProjectSettingsWindow>(
                true,
                "PSD Layout Tool 全局配置",
                true);
            window.settings = targetSettings;
            window.minSize = new Vector2(420f, 420f);
            window.Show();
            window.Focus();
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                settings = PsdLayoutProjectSettingsAsset.GetOrCreate();
            }

            EditorGUILayout.ObjectField("配置文件", settings, typeof(PsdLayoutProjectSettings), false);
            EditorGUILayout.Space();

            UnityEditor.Editor.CreateCachedEditor(settings, typeof(PsdLayoutProjectSettingsEditor), ref settingsEditor);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            settingsEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void OnDisable()
        {
            if (settingsEditor != null)
            {
                DestroyImmediate(settingsEditor);
            }
        }
    }
}
