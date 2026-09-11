using System;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;
namespace UGF.EditorTools.Psd2UGUI
{
    internal sealed class Psd2UIFormEditorNoticeUtility
    {
        private static readonly Color VersionUpdateBackgroundColor = new Color(0.36f, 0.08f, 0.08f, 1f);

        private static readonly Color VersionUpdateTextColor = new Color(1f, 0.93f, 0.93f, 1f);

        private const float WarningIconSize = 20f;

        private const float WarningIconSpacing = 8f;

        private static GUIStyle s_versionUpdateStyle;

        private static GUIStyle s_versionUpdateButtonStyle;

        private static GUIContent s_warningIconContent;

        internal static Psd2UIFormEditorNoticeUtility s_ObfuscationSentinel;

        internal static bool DrawVersionUpdateNotice(string message, string buttonLabel)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                GUIStyle versionUpdateStyle = GetVersionUpdateStyle();
                GUIStyle versionUpdateButtonStyle = GetVersionUpdateButtonStyle();
                GUIContent val = new GUIContent(message);
                GUIContent val2 = new GUIContent(string.IsNullOrWhiteSpace(buttonLabel) ? "下载" : buttonLabel);
                GUIContent warningIconContent = GetWarningIconContent();
                float num = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 56f);
                float num2 = 68f;
                float num3 = Mathf.Max(80f, num - (float)versionUpdateStyle.padding.horizontal);
                float num4 = Mathf.Max(60f, num3 - num2 - 20f - 8f - 8f);
                float num5 = versionUpdateStyle.CalcHeight(val, num4);
                float num6 = Mathf.Max(EditorGUIUtility.singleLineHeight + 2f, versionUpdateButtonStyle.CalcHeight(val2, num2));
                float num7 = Mathf.Max(Mathf.Max(num5, num6), 20f) + (float)versionUpdateStyle.padding.vertical;
                Rect val3 = EditorGUILayout.GetControlRect(false, num7, Array.Empty<GUILayoutOption>());
                val3 = EditorGUI.IndentedRect(val3);
                EditorGUI.DrawRect(val3, VersionUpdateBackgroundColor);
                Rect val4 = default(Rect);
                val4 = new Rect(val3.x + (float)versionUpdateStyle.padding.left, val3.y + (float)versionUpdateStyle.padding.top, Mathf.Max(1f, val3.width - (float)versionUpdateStyle.padding.horizontal), Mathf.Max(1f, val3.height - (float)versionUpdateStyle.padding.vertical));
                Rect val5 = default(Rect);
                val5 = new Rect(val4.xMax - num2, val4.y + Mathf.Max(0f, (val4.height - num6) * 0.5f), num2, num6);
                Rect val6 = default(Rect);
                val6 = new Rect(val4.x, val4.y + Mathf.Max(0f, (val4.height - 20f) * 0.5f), 20f, 20f);
                Rect val7 = new Rect(val6.xMax + 8f, val4.y, Mathf.Max(1f, val5.x - val6.xMax - 8f - 8f), val4.height);
                GUI.Label(val6, warningIconContent);
                GUI.Label(val7, val, versionUpdateStyle);
                return GUI.Button(val5, val2, versionUpdateButtonStyle);
            }
            return false;
        }

        private static GUIStyle GetVersionUpdateStyle()
        {
            if (s_versionUpdateStyle != null)
            {
                return s_versionUpdateStyle;
            }
            s_versionUpdateStyle = new GUIStyle(EditorStyles.helpBox)
            {
                wordWrap = true,
                richText = false,
                alignment = (TextAnchor)3,
                padding = new RectOffset(12, 12, 8, 8)
            };
            s_versionUpdateStyle.normal.textColor = VersionUpdateTextColor;
            s_versionUpdateStyle.hover.textColor = VersionUpdateTextColor;
            s_versionUpdateStyle.active.textColor = VersionUpdateTextColor;
            s_versionUpdateStyle.focused.textColor = VersionUpdateTextColor;
            return s_versionUpdateStyle;
        }

        private static GUIStyle GetVersionUpdateButtonStyle()
        {
            if (s_versionUpdateButtonStyle != null)
            {
                return s_versionUpdateButtonStyle;
            }
            s_versionUpdateButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = (TextAnchor)4,
                fixedHeight = 22f
            };
            return s_versionUpdateButtonStyle;
        }

        private static GUIContent GetWarningIconContent()
        {
            if (s_warningIconContent != null)
            {
                return s_warningIconContent;
            }
            s_warningIconContent = EditorGUIUtility.IconContent("console.warnicon");
            if (s_warningIconContent == null || (Object)(object)s_warningIconContent.image == (Object)null)
            {
                s_warningIconContent = EditorGUIUtility.IconContent("console.warnicon.sml");
            }
            return s_warningIconContent ?? GUIContent.none;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static Psd2UIFormEditorNoticeUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
