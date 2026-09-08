using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

internal static class Psd2UIFormEditorNoticeUtility
{
	private static readonly Color VersionUpdateBackgroundColor;

	private static readonly Color VersionUpdateTextColor;

	private const float WarningIconSize = 20f;

	private const float WarningIconSpacing = 8f;

	private static GUIStyle s_versionUpdateStyle;

	private static GUIStyle s_versionUpdateButtonStyle;

	private static GUIContent s_warningIconContent;

	internal static bool DrawVersionUpdateNotice(string message, string buttonLabel)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return false;
		}
		GUIStyle versionUpdateStyle = GetVersionUpdateStyle();
		GUIStyle versionUpdateButtonStyle = GetVersionUpdateButtonStyle();
		GUIContent content = new GUIContent(message);
		GUIContent content2 = new GUIContent(string.IsNullOrWhiteSpace(buttonLabel) ? "下载" : buttonLabel);
		GUIContent warningIconContent = GetWarningIconContent();
		float num = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 56f);
		float num2 = 68f;
		float num3 = Mathf.Max(80f, num - (float)versionUpdateStyle.padding.horizontal);
		float width = Mathf.Max(60f, num3 - num2 - 20f - 8f - 8f);
		float a = versionUpdateStyle.CalcHeight(content, width);
		float num4 = Mathf.Max(EditorGUIUtility.singleLineHeight + 2f, versionUpdateButtonStyle.CalcHeight(content2, num2));
		float height = Mathf.Max(Mathf.Max(a, num4), 20f) + (float)versionUpdateStyle.padding.vertical;
		Rect controlRect = EditorGUILayout.GetControlRect(false, height);
		controlRect = EditorGUI.IndentedRect(controlRect);
		EditorGUI.DrawRect(controlRect, VersionUpdateBackgroundColor);
		Rect rect = new Rect(controlRect.x + (float)versionUpdateStyle.padding.left, controlRect.y + (float)versionUpdateStyle.padding.top, Mathf.Max(1f, controlRect.width - (float)versionUpdateStyle.padding.horizontal), Mathf.Max(1f, controlRect.height - (float)versionUpdateStyle.padding.vertical));
		Rect position = new Rect(rect.xMax - num2, rect.y + Mathf.Max(0f, (rect.height - num4) * 0.5f), num2, num4);
		Rect position2 = new Rect(rect.x, rect.y + Mathf.Max(0f, (rect.height - 20f) * 0.5f), 20f, 20f);
		Rect position3 = new Rect(position2.xMax + 8f, rect.y, Mathf.Max(1f, position.x - position2.xMax - 8f - 8f), rect.height);
		GUI.Label(position2, warningIconContent);
		GUI.Label(position3, content, versionUpdateStyle);
		return GUI.Button(position, content2, versionUpdateButtonStyle);
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
			alignment = TextAnchor.MiddleLeft,
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
			alignment = TextAnchor.MiddleCenter,
			fixedHeight = 22f
		};
		return s_versionUpdateButtonStyle;
	}

	private static GUIContent GetWarningIconContent()
	{
		if (s_warningIconContent == null)
		{
			s_warningIconContent = EditorGUIUtility.IconContent("console.warnicon");
			if (s_warningIconContent == null || s_warningIconContent.image == null)
			{
				s_warningIconContent = EditorGUIUtility.IconContent("console.warnicon.sml");
			}
			return s_warningIconContent ?? GUIContent.none;
		}
		return s_warningIconContent;
	}

	static Psd2UIFormEditorNoticeUtility()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		VersionUpdateBackgroundColor = new Color(0.36f, 0.08f, 0.08f, 1f);
		VersionUpdateTextColor = new Color(1f, 0.93f, 0.93f, 1f);
	}
}
}
