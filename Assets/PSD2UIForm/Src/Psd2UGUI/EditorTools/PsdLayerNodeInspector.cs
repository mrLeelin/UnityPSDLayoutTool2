using System;
using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;
using TextGradientColorStopNamespace;
using PsdTextStyleInfoNamespace;

namespace UGF.EditorTools.Psd2UGUI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PsdLayerNode))]
    internal sealed class PsdLayerNodeInspector : Editor
    {
        private const float CopyButtonWidth = 52f;

        private PsdLayerNode targetLogic;

        internal static PsdLayerNodeInspector s_PsdLayerNodeInspectorObfuscationSentinel;

        private void OnEnable()
        {
            targetLogic = ((Editor)this).target as PsdLayerNode;
            targetLogic.RefreshPreviewTexture();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            ((Editor)this).serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            GUIType gUIType = (GUIType)(object)EditorGUILayout.EnumPopup("UI Type", (Enum)targetLogic.UIType, Array.Empty<GUILayoutOption>());
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(((Editor)this).targets, "Change UI Type");
                bool flag = false;
                Object[] targets = ((Editor)this).targets;
                foreach (Object val in targets)
                {
                    if (!(val == (Object)null))
                    {
                        PsdLayerNode psdLayerNode = val as PsdLayerNode;
                        if (!((Object)(object)psdLayerNode == (Object)null))
                        {
                            psdLayerNode.SetUIType(gUIType, false);
                            flag = true;
                        }
                    }
                }
                if (flag)
                {
                    Psd2UIFormConverterEditor.Instance?.RefreshAllHelperComponents();
                }
            }
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            if (GUILayout.Button("导出图片资源", Array.Empty<GUILayoutOption>()))
            {
                Object[] targets = ((Editor)this).targets;
                foreach (Object val2 in targets)
                {
                    if (!(val2 == (Object)null))
                    {
                        (val2 as PsdLayerNode)?.ExportImageAsset();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            if (targetLogic.IsPrimaryUIType() && GUILayout.Button("生成当前节点UIForm", Array.Empty<GUILayoutOption>()))
            {
                Psd2UIFormConverterEditor.Instance.ExportUIFormPrefab(targetLogic);
            }
            if (GUILayout.Button("导出Prefab", Array.Empty<GUILayoutOption>()))
            {
                Psd2UIFormConverterEditor.Instance.ExportReusablePrefab(targetLogic);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer Data", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            Rect val3 = targetLogic.GetLayerRect();
            DrawVector2FieldReadOnly("Position", val3.position);
            DrawVector2FieldReadOnly("Size", val3.size);
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(52f) }))
            {
                CopyRectTransformClipboard(val3.position, val3.size);
            }
            EditorGUILayout.EndHorizontal();
            if (targetLogic.TryGetTextStyleInfo(out var textInfo))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Text Data", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
                DrawReadOnlyField("Text Content", textInfo.Text ?? string.Empty);
                DrawReadOnlyField("Font Name", textInfo.FontName ?? string.Empty);
                DrawReadOnlyField("Font Size", FormatFloat(textInfo.FontSize));
                DrawReadOnlyField("Font Style", textInfo.LegacyFontStyle.ToString());
                DrawReadOnlyField("TMP Font Style", textInfo.TMPFontStyle.ToString());
                DrawReadOnlyField("Character Spacing", FormatFloat(textInfo.CharacterSpacing));
                DrawReadOnlyField("Line Spacing", textInfo.IsLineSpacingAuto ? "Auto" : FormatFloat(textInfo.LineSpacing));
                DrawReadOnlyField("Auto Line Spacing", textInfo.IsLineSpacingAuto ? "True" : "False");
                DrawColorFieldWithCopy("Color", textInfo.Color);
                DrawTextEffectsInfo(in textInfo);
            }
            ((Editor)this).serializedObject.ApplyModifiedProperties();
        }

        private void DrawVector2FieldReadOnly(string label, Vector2 value)
        {
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector2Field(label, value, Array.Empty<GUILayoutOption>());
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void CopyRectTransformClipboard(Vector2 positionValue, Vector2 sizeValue)
        {
            GameObject val = new GameObject("RectTransformClipboard", new Type[1] { typeof(RectTransform) })
            {
                hideFlags = (HideFlags)61
            };
            RectTransform component = val.GetComponent<RectTransform>();
            component.anchorMin = new Vector2(0.5f, 0.5f);
            component.anchorMax = new Vector2(0.5f, 0.5f);
            component.pivot = new Vector2(0.5f, 0.5f);
            component.anchoredPosition = positionValue;
            component.sizeDelta = sizeValue;
            ((Transform)component).localRotation = Quaternion.identity;
            ((Transform)component).localScale = Vector3.one;
            ComponentUtility.CopyComponent((Component)(object)component);
            Object.DestroyImmediate((Object)val);
        }

        private void DrawReadOnlyField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(label, value, Array.Empty<GUILayoutOption>());
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Copy", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(52f) }))
            {
                GUIUtility.systemCopyBuffer = value;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorFieldWithCopy(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ColorField(label, color, Array.Empty<GUILayoutOption>());
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("RGB", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(52f) }))
            {
                GUIUtility.systemCopyBuffer = ColorUtility.ToHtmlStringRGB(color);
            }
            if (GUILayout.Button("A", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(52f) }))
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
            EditorGUILayout.LabelField("Text Effects", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            EditorGUI.indentLevel += 1;
            DrawTextOutlineInfo(in textInfo);
            DrawTextShadowInfo(in textInfo);
            DrawTextGlowInfo(in textInfo);
            DrawTextBevelInfo(in textInfo);
            DrawTextGradientInfo(in textInfo);
            EditorGUI.indentLevel -= 1;
        }

        private void DrawTextOutlineInfo(in PsdTextStyleInfo textInfo)
        {
            if (!textInfo.HasOutline)
            {
                DrawReadOnlyField("Outline", "None");
                return;
            }
            DrawReadOnlyField("Outline", "Enabled");
            DrawColorFieldWithCopy("Outline Color", textInfo.OutlineColor);
            DrawReadOnlyField("Outline Size (UGUI)", FormatFloat(textInfo.LegacyOutlineSize));
            DrawReadOnlyField("Outline Size (TMP)", FormatFloat(textInfo.TMPOutlineSize));
            DrawReadOnlyField("Outline Position", textInfo.OutlineMode.ToString());
        }

        private void DrawTextShadowInfo(in PsdTextStyleInfo textInfo)
        {
            if (textInfo.HasShadow)
            {
                DrawReadOnlyField("Shadow", textInfo.IsInnerShadow ? "Inner" : "Outer");
                DrawColorFieldWithCopy("Shadow Color", textInfo.ShadowColor);
                DrawReadOnlyField("Shadow Offset", FormatVector2(textInfo.ShadowOffset));
                DrawReadOnlyField("Shadow Spread", FormatFloat(textInfo.ShadowSpread));
                DrawReadOnlyField("Shadow Softness", FormatFloat(textInfo.ShadowSoftness));
            }
            else
            {
                DrawReadOnlyField("Shadow", "None");
            }
        }

        private void DrawTextGlowInfo(in PsdTextStyleInfo textInfo)
        {
            if (textInfo.HasGlow)
            {
                DrawReadOnlyField("Glow", textInfo.IsInnerGlow ? "Inner" : "Outer");
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
            if (textInfo.HasBevel)
            {
                DrawReadOnlyField("Bevel", textInfo.IsInnerBevel ? "Inner" : "Outer");
                DrawReadOnlyField("Bevel Size", FormatFloat(textInfo.BevelSize));
                DrawReadOnlyField("Bevel Depth", FormatFloat(textInfo.BevelDepth));
                DrawReadOnlyField("Bevel Soften", FormatFloat(textInfo.BevelSoftness));
                DrawReadOnlyField("Bevel Angle", FormatFloat(textInfo.BevelAngle));
                DrawReadOnlyField("Bevel Altitude", FormatFloat(textInfo.BevelAltitude));
                DrawColorFieldWithCopy("Bevel Highlight Color", textInfo.BevelHighlightColor);
                DrawReadOnlyField("Bevel Highlight Opacity", FormatFloat(textInfo.BevelHighlightOpacity));
                DrawColorFieldWithCopy("Bevel Shadow Color", textInfo.BevelShadowColor);
                DrawReadOnlyField("Bevel Shadow Opacity", FormatFloat(textInfo.BevelShadowOpacity));
            }
            else
            {
                DrawReadOnlyField("Bevel", "None");
            }
        }

        private void DrawTextGradientInfo(in PsdTextStyleInfo textInfo)
        {
            if (textInfo.HasGradient && textInfo.GradientStops != null && textInfo.GradientStops.Length != 0)
            {
                DrawReadOnlyField("Gradient", "Enabled");
                DrawReadOnlyField("Gradient Angle", FormatFloat(textInfo.GradientAngle));
                DrawReadOnlyField("Gradient Reverse", textInfo.IsGradientReversed ? "True" : "False");
                DrawReadOnlyField("Gradient Stops", textInfo.GradientStops.Length.ToString());
                for (int i = 0; i < textInfo.GradientStops.Length; i++)
                {
                    TextGradientColorStop value = textInfo.GradientStops[i];
                    DrawReadOnlyField($"Stop {i} Pos", FormatFloat(value.Position));
                    DrawColorFieldWithCopy($"Stop {i} Color", value.Color);
                }
            }
            else
            {
                DrawReadOnlyField("Gradient", "None");
            }
        }

        public override bool HasPreviewGUI()
        {
            PsdLayerNode psdLayerNode = ((Editor)this).target as PsdLayerNode;
            if ((Object)(object)psdLayerNode != (Object)null)
            {
                return (Object)(object)psdLayerNode.PreviewTexture != (Object)null;
            }
            return false;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            PsdLayerNode psdLayerNode = ((Editor)this).target as PsdLayerNode;
            GUI.DrawTexture(r, (Texture)(object)psdLayerNode.PreviewTexture, (ScaleMode)2);
        }

        public override string GetInfoString()
        {
            return (((Editor)this).target as PsdLayerNode).GetLayerInfo();
        }

        internal static bool IsPsdLayerNodeInspectorObfuscationSentinelNull()
        {
            return (object)s_PsdLayerNodeInspectorObfuscationSentinel == null;
        }

        internal static PsdLayerNodeInspector GetPsdLayerNodeInspectorObfuscationSentinel()
        {
            return s_PsdLayerNodeInspectorObfuscationSentinel;
        }
    }
}
