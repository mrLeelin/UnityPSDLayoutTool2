using System.Globalization;
using PsdProtectionGuards;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using PsdTextStyles;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

[CanEditMultipleObjects]
[CustomEditor(typeof(PsdLayerNode))]
internal sealed class PsdLayerNodeInspector : Editor
{
	private const float CopyButtonWidth = 52f;

	private PsdLayerNode targetLogic;

	private void OnEnable()
	{
		targetLogic = base.target as PsdLayerNode;
		targetLogic.EnsurePreviewTexture();
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		base.serializedObject.Update();
		EditorGUI.BeginChangeCheck();
		GUIType gUIType = (GUIType)(object)EditorGUILayout.EnumPopup("UI Type", targetLogic.UIType);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObjects(base.targets, "Change UI Type");
			Object[] array = base.targets;
			foreach (Object obj in array)
			{
				if (!(obj == null))
				{
					(obj as PsdLayerNode)?.SetUiType(gUIType);
				}
			}
		}
		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("导出图片资源"))
		{
			Object[] array = base.targets;
			foreach (Object obj2 in array)
			{
				if (!(obj2 == null))
				{
					(obj2 as PsdLayerNode)?.ExportLayerImage();
				}
			}
		}
		EditorGUILayout.EndHorizontal();
		if (targetLogic.IsPrimaryUiElement() && GUILayout.Button("生成当前节点UIForm"))
		{
			Psd2UIFormConverter.Instance.ExportUiForm(targetLogic);
		}
		if (GUILayout.Button("导出Prefab"))
		{
			Psd2UIFormConverter.Instance.ExportReusablePrefab(targetLogic);
		}
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Layer Data", EditorStyles.boldLabel);
		Rect rect = targetLogic.GetLayerBounds();
		DrawVector2FieldReadOnly("Position", rect.position);
		DrawVector2FieldReadOnly("Size", rect.size);
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("Copy", GUILayout.Width(52f)))
		{
			CopyRectTransformClipboard(rect.position, rect.size);
		}
		EditorGUILayout.EndHorizontal();
		if (targetLogic.TryBuildTextStyleData(out var textInfo))
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Text Data", EditorStyles.boldLabel);
			DrawReadOnlyField("Text Content", textInfo.TextContent ?? string.Empty);
			DrawReadOnlyField("Font Name", textInfo.FontName ?? string.Empty);
			DrawReadOnlyField("Font Size", FormatFloat(textInfo.FontSize));
			DrawReadOnlyField("Font Style", textInfo.FontStyle.ToString());
			DrawReadOnlyField("TMP Font Style", textInfo.TmpFontStyle.ToString());
			DrawReadOnlyField("Character Spacing", FormatFloat(textInfo.CharacterSpacing));
			DrawReadOnlyField("Line Spacing", (!textInfo.AutoLineSpacing) ? FormatFloat(textInfo.LineSpacing) : "Auto");
			DrawReadOnlyField("Auto Line Spacing", (!textInfo.AutoLineSpacing) ? "False" : "True");
			DrawColorFieldWithCopy("Color", textInfo.TextColor);
			DrawTextEffectsInfo(in textInfo);
		}
		base.serializedObject.ApplyModifiedProperties();
	}

	private void DrawVector2FieldReadOnly(string label, Vector2 value)
	{
		EditorGUILayout.BeginHorizontal();
		EditorGUI.BeginDisabledGroup(disabled: true);
		EditorGUILayout.Vector2Field(label, value);
		EditorGUI.EndDisabledGroup();
		EditorGUILayout.EndHorizontal();
	}

	private void CopyRectTransformClipboard(Vector2 positionValue, Vector2 sizeValue)
	{
		GameObject obj = new GameObject("RectTransformClipboard", typeof(RectTransform))
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		component.pivot = new Vector2(0.5f, 0.5f);
		component.anchoredPosition = positionValue;
		component.sizeDelta = sizeValue;
		component.localRotation = Quaternion.identity;
		component.localScale = Vector3.one;
		ComponentUtility.CopyComponent(component);
		Object.DestroyImmediate(obj);
	}

	private void DrawReadOnlyField(string label, string value)
	{
		EditorGUILayout.BeginHorizontal();
		EditorGUI.BeginDisabledGroup(disabled: true);
		EditorGUILayout.TextField(label, value);
		EditorGUI.EndDisabledGroup();
		if (GUILayout.Button("Copy", GUILayout.Width(52f)))
		{
			GUIUtility.systemCopyBuffer = value;
		}
		EditorGUILayout.EndHorizontal();
	}

	private void DrawColorFieldWithCopy(string label, Color color)
	{
		EditorGUILayout.BeginHorizontal();
		EditorGUI.BeginDisabledGroup(disabled: true);
		EditorGUILayout.ColorField(label, color);
		EditorGUI.EndDisabledGroup();
		if (GUILayout.Button("RGB", GUILayout.Width(52f)))
		{
			GUIUtility.systemCopyBuffer = ColorUtility.ToHtmlStringRGB(color);
		}
		if (GUILayout.Button("A", GUILayout.Width(52f)))
		{
			GUIUtility.systemCopyBuffer = color.a.ToString("0.###", CultureInfo.InvariantCulture);
		}
		EditorGUILayout.EndHorizontal();
	}

	private static string FormatFloat(float value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static string FormatVector2(Vector2 value)
	{
		return "(" + FormatFloat(value.x) + ", " + FormatFloat(value.y) + ")";
	}

	private void DrawTextEffectsInfo(in PsdTextStyleInfo textInfo)
	{
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Text Effects", EditorStyles.boldLabel);
		EditorGUI.indentLevel++;
		DrawTextOutlineInfo(in textInfo);
		DrawTextShadowInfo(in textInfo);
		DrawTextGlowInfo(in textInfo);
		DrawTextBevelInfo(in textInfo);
		DrawTextGradientInfo(in textInfo);
		EditorGUI.indentLevel--;
	}

	private void DrawTextOutlineInfo(in PsdTextStyleInfo textInfo)
	{
		if (textInfo.OutlineEnabled)
		{
			DrawReadOnlyField("Outline", "Enabled");
			DrawColorFieldWithCopy("Outline Color", textInfo.OutlineColor);
			DrawReadOnlyField("Outline Size (UGUI)", FormatFloat(textInfo.UguiOutlineSize));
			DrawReadOnlyField("Outline Size (TMP)", FormatFloat(textInfo.TmpOutlineSize));
			DrawReadOnlyField("Outline Position", textInfo.OutlineMode.ToString());
		}
		else
		{
			DrawReadOnlyField("Outline", "None");
		}
	}

	private void DrawTextShadowInfo(in PsdTextStyleInfo textInfo)
	{
		if (!textInfo.ShadowEnabled)
		{
			DrawReadOnlyField("Shadow", "None");
			return;
		}
		DrawReadOnlyField("Shadow", (!textInfo.InnerShadow) ? "Outer" : "Inner");
		DrawColorFieldWithCopy("Shadow Color", textInfo.ShadowColor);
		DrawReadOnlyField("Shadow Offset", FormatVector2(textInfo.ShadowOffset));
		DrawReadOnlyField("Shadow Spread", FormatFloat(textInfo.ShadowSpread));
		DrawReadOnlyField("Shadow Softness", FormatFloat(textInfo.ShadowSoftness));
	}

	private void DrawTextGlowInfo(in PsdTextStyleInfo textInfo)
	{
		if (textInfo.GlowEnabled)
		{
			DrawReadOnlyField("Glow", (!textInfo.InnerGlow) ? "Outer" : "Inner");
			DrawColorFieldWithCopy("Glow Color", textInfo.GlowColor);
			DrawReadOnlyField("Glow Size", FormatFloat(textInfo.GlowSize));
			DrawReadOnlyField("Glow Spread", FormatFloat(textInfo.GlowSpread));
			DrawReadOnlyField("Glow Offset", FormatFloat(textInfo.GlowOffset));
			DrawReadOnlyField("Glow Power", FormatFloat(textInfo.GlowPower));
		}
		else
		{
			DrawReadOnlyField("Glow", "None");
		}
	}

	private void DrawTextBevelInfo(in PsdTextStyleInfo textInfo)
	{
		if (!textInfo.BevelEnabled)
		{
			DrawReadOnlyField("Bevel", "None");
			return;
		}
		DrawReadOnlyField("Bevel", textInfo.InnerBevel ? "Inner" : "Outer");
		DrawReadOnlyField("Bevel Size", FormatFloat(textInfo.BevelSize));
		DrawReadOnlyField("Bevel Depth", FormatFloat(textInfo.BevelDepth));
		DrawReadOnlyField("Bevel Soften", FormatFloat(textInfo.BevelSoften));
		DrawReadOnlyField("Bevel Angle", FormatFloat(textInfo.BevelAngle));
		DrawReadOnlyField("Bevel Altitude", FormatFloat(textInfo.BevelAltitude));
		DrawColorFieldWithCopy("Bevel Highlight Color", textInfo.BevelHighlightColor);
		DrawReadOnlyField("Bevel Highlight Opacity", FormatFloat(textInfo.BevelHighlightOpacity));
		DrawColorFieldWithCopy("Bevel Shadow Color", textInfo.BevelShadowColor);
		DrawReadOnlyField("Bevel Shadow Opacity", FormatFloat(textInfo.BevelShadowOpacity));
	}

	private void DrawTextGradientInfo(in PsdTextStyleInfo textInfo)
	{
		if (textInfo.GradientEnabled && textInfo.GradientStops != null && textInfo.GradientStops.Length != 0)
		{
			DrawReadOnlyField("Gradient", "Enabled");
			DrawReadOnlyField("Gradient Angle", FormatFloat(textInfo.GradientAngle));
			DrawReadOnlyField("Gradient Reverse", (!textInfo.GradientReverse) ? "False" : "True");
			DrawReadOnlyField("Gradient Stops", textInfo.GradientStops.Length.ToString());
			for (int i = 0; i < textInfo.GradientStops.Length; i++)
			{
				PsdUiGradientStop PsdUiGradientStop = textInfo.GradientStops[i];
				DrawReadOnlyField($"Stop {i} Pos", FormatFloat(PsdUiGradientStop.Position));
				DrawColorFieldWithCopy($"Stop {i} Color", PsdUiGradientStop.Color);
			}
		}
		else
		{
			DrawReadOnlyField("Gradient", "None");
		}
	}

	public override bool HasPreviewGUI()
	{
		PsdLayerNode psdLayerNode = base.target as PsdLayerNode;
		if (!(psdLayerNode != null))
		{
			return false;
		}
		return psdLayerNode.PreviewTexture != null;
	}

	public override void OnPreviewGUI(Rect r, GUIStyle background)
	{
		PsdLayerNode psdLayerNode = base.target as PsdLayerNode;
		GUI.DrawTexture(r, psdLayerNode.PreviewTexture, ScaleMode.ScaleToFit);
	}

	public override string GetInfoString()
	{
		return (base.target as PsdLayerNode).GetLayerBoundsDescription();
	}

	public PsdLayerNodeInspector()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private PsdLayerNodeInspector(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
