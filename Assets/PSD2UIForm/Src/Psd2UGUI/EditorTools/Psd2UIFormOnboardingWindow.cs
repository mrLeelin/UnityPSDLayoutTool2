using System;
using System.IO;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    internal sealed class Psd2UIFormOnboardingWindow : EditorWindow
    {
        private const string EditorAsmdefFileName = "cn.efunstudio.psd2ugui.asmdef";

        private bool dontShowAgain = true;

        private static Psd2UIFormOnboardingWindow s_OnboardingWindowObfuscationSentinel;

        internal static void ShowWindow()
        {
            Psd2UIFormOnboardingWindow window = EditorWindow.GetWindow<Psd2UIFormOnboardingWindow>(true, "Psd2UIForm 新手引导", true);
            ((EditorWindow)window).minSize = new Vector2(420f, 260f);
            ((EditorWindow)window).Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Psd2UIForm 新手引导", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("第一步：准备 PSD 文件", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            EditorGUILayout.LabelField("- 把你的 PSD 文件放入工程", EditorStyles.wordWrappedLabel, Array.Empty<GUILayoutOption>());
            EditorGUILayout.LabelField("或直接使用示例 PSD 文件测试", EditorStyles.wordWrappedLabel, Array.Empty<GUILayoutOption>());
            if (GUILayout.Button("点击选中示例PSD文件：Psd2UguiForm_UGUI.psd", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(24f) }))
            {
                LocateSamplePsd();
            }
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("第二步：解析 PSD", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            EditorGUILayout.LabelField("选中PSD文件, 鼠标右键单击 PSD 文件", EditorStyles.wordWrappedLabel, Array.Empty<GUILayoutOption>());
            EditorGUILayout.LabelField("选择菜单：Psd2UIForm Editor", EditorStyles.wordWrappedLabel, Array.Empty<GUILayoutOption>());
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("第三步：生成 UIForm", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            EditorGUILayout.LabelField("在编辑界面点击“生成UIForm”按钮，自动生成 UI 预制体", EditorStyles.wordWrappedLabel, Array.Empty<GUILayoutOption>());
            GUILayout.FlexibleSpace();
            dontShowAgain = EditorGUILayout.ToggleLeft("下次不再提示", dontShowAgain, Array.Empty<GUILayoutOption>());
            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            if (GUILayout.Button("视频教程", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(28f) }))
            {
                Application.OpenURL("https://www.bilibili.com/video/BV1SVUkBWE2v");
            }
            if (GUILayout.Button("知道了", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(28f) }))
            {
                if (dontShowAgain)
                {
                    Psd2UIFormOnboarding.MarkShown();
                }
                ((EditorWindow)this).Close();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private static void LocateSamplePsd()
        {
            string assetPathUnderPluginRoot = GetAssetPathUnderPluginRoot("Examples/Psd2UguiForm_UGUI.psd");
            if (!string.IsNullOrWhiteSpace(assetPathUnderPluginRoot))
            {
                Object val = AssetDatabase.LoadMainAssetAtPath(assetPathUnderPluginRoot);
                if (val != (Object)null)
                {
                    EditorGUIUtility.PingObject(val);
                    Selection.activeObject = val;
                    return;
                }
            }
            EditorUtility.DisplayDialog("未找到示例 PSD", "请确认示例文件已导入工程：Psd2UguiForm_UGUI.psd", "确定");
        }

        private static string GetAssetPathUnderPluginRoot(string relativeAssetPath)
        {
            string pluginRootAssetPath = GetPluginRootAssetPath();
            if (!string.IsNullOrWhiteSpace(pluginRootAssetPath) && !string.IsNullOrWhiteSpace(relativeAssetPath))
            {
                return pluginRootAssetPath.TrimEnd('/') + "/" + NormalizeAssetPath(relativeAssetPath).TrimStart('/');
            }
            return string.Empty;
        }

        private static string GetPluginRootAssetPath()
        {
            string[] array = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension("cn.efunstudio.psd2ugui.asmdef"));
            for (int i = 0; i < array.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(array[i]));
                if (string.Equals(Path.GetFileName(path), "cn.efunstudio.psd2ugui.asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    string text = NormalizeAssetPath(Path.GetDirectoryName(path));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return NormalizeAssetPath(Path.GetDirectoryName(text));
                    }
                }
            }
            return string.Empty;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path.Replace('\\', '/').Trim();
            }
            return string.Empty;
        }

        internal static bool IsOnboardingWindowObfuscationSentinelNull()
        {
            return (object)s_OnboardingWindowObfuscationSentinel == null;
        }

        internal static Psd2UIFormOnboardingWindow GetOnboardingWindowObfuscationSentinel()
        {
            return s_OnboardingWindowObfuscationSentinel;
        }
    }
}
