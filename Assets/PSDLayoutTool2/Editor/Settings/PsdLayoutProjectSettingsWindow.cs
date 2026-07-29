namespace PsdLayoutTool2
{
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// 在独立窗口中编辑项目配置，避免主 Inspector 锁定时无法切换到配置资产。
    /// </summary>
    internal sealed class PsdLayoutProjectSettingsWindow : EditorWindow
    {
        private const string WindowUxmlGuid = "dfb437fe04b840047a8caf32f8f042f2";
        private const string WindowUssGuid = "7b18789d5bd973241823b9a388bc81aa";

        private PsdLayoutProjectSettings settings;
        private UnityEditor.Editor settingsEditor;
        private StyleSheet windowStyleSheet;

        internal static void Open(PsdLayoutProjectSettings targetSettings)
        {
            PsdLayoutProjectSettingsWindow window = GetWindow<PsdLayoutProjectSettingsWindow>(
                true,
                "PSD Layout Tool 全局配置",
                true);
            window.settings = targetSettings;
            window.minSize = new Vector2(460f, 500f);
            window.Show();
            window.Rebuild();
            window.Focus();
        }

        private void CreateGUI()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();

            string windowUxmlPath = AssetDatabase.GUIDToAssetPath(WindowUxmlGuid);
            string windowUssPath = AssetDatabase.GUIDToAssetPath(WindowUssGuid);
            VisualTreeAsset windowUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(windowUxmlPath);
            StyleSheet windowUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(windowUssPath);
            if (windowUxml == null || windowUss == null)
            {
                rootVisualElement.Add(new HelpBox(
                    "项目配置窗口的 UXML 或 USS 资源缺失。",
                    HelpBoxMessageType.Error));
                return;
            }

            if (windowStyleSheet != windowUss)
            {
                rootVisualElement.styleSheets.Add(windowUss);
                windowStyleSheet = windowUss;
            }

            windowUxml.CloneTree(rootVisualElement);

            if (settings == null)
            {
                settings = PsdLayoutProjectSettingsAsset.GetOrCreate();
            }

            VisualElement settingsHost = rootVisualElement.Q("settings-host");
            if (settingsHost == null)
            {
                rootVisualElement.Clear();
                rootVisualElement.Add(new HelpBox(
                    "项目配置窗口的 UXML 结构不完整。",
                    HelpBoxMessageType.Error));
                return;
            }

            var scrollView = new ScrollView(ScrollViewMode.Vertical) { name = "settings-scroll" };
            scrollView.AddToClassList("psd-settings-window__scroll");
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;

            UnityEditor.Editor.CreateCachedEditor(settings, typeof(PsdLayoutProjectSettingsEditor), ref settingsEditor);
            VisualElement settingsPanel = settingsEditor.CreateInspectorGUI();
            if (settingsPanel == null)
            {
                scrollView.Add(new HelpBox(
                    "无法创建项目配置面板。",
                    HelpBoxMessageType.Error));
            }
            else
            {
                scrollView.Add(settingsPanel);
            }

            settingsHost.Add(scrollView);
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
