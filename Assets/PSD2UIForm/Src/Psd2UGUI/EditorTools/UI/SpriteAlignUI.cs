using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;
internal class SpriteAlignUI : PopupWindowContent
{
    private float cellSize;

    private Vector2 windowSize;

    private SpriteAlignment alignment;

    private Action<SpriteAlignment> callback;

    internal static SpriteAlignUI s_ObfuscationSentinel;

    private static string DisplayCamelCaseString(string camelCase)
    {
        List<char> list = new List<char>();
        list.Add(camelCase[0]);
        foreach (char item in camelCase.Skip(1))
        {
            if (!char.IsUpper(item))
            {
                list.Add(item);
                continue;
            }
            list.Add(' ');
            list.Add(item);
        }
        return new string(list.ToArray());
    }

    public static void Popup(Rect rect, SpriteAlignment alignment, Action<SpriteAlignment> callback)
    {
        SpriteAlignUI spriteAlignUI = new SpriteAlignUI();
        spriteAlignUI.SetData(rect, alignment, callback);
        PopupWindow.Show(rect, (PopupWindowContent)(object)spriteAlignUI);
    }

    public static void DrawGUILayout(GUIContent label, SpriteAlignment alignment, Action<SpriteAlignment> callback, params GUILayoutOption[] layoutOptions)
    {
        EditorGUILayout.HorizontalScope val = new EditorGUILayout.HorizontalScope(layoutOptions);
        try
        {
            if (label != GUIContent.none)
            {
                EditorGUILayout.PrefixLabel(label);
            }
            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            Rect rect = GUILayoutUtility.GetRect(10f, EditorGUIUtility.currentViewWidth, singleLineHeight, singleLineHeight, EditorStyles.popup);
            string text = DisplayCamelCaseString(alignment.ToString());
            if (GUI.Button(rect, text, EditorStyles.popup))
            {
                Popup(rect, alignment, callback);
            }
        }
        finally
        {
            ((IDisposable)val)?.Dispose();
        }
    }

    public static void DrawGUI(Rect position, GUIContent label, SpriteAlignment alignment, Action<SpriteAlignment> callback)
    {
        Rect val = position;
        if (label != GUIContent.none)
        {
            val = EditorGUI.PrefixLabel(position, label);
        }
        string text = DisplayCamelCaseString(alignment.ToString());
        if (GUI.Button(val, text, EditorStyles.popup))
        {
            SpriteAlignUI spriteAlignUI = new SpriteAlignUI();
            spriteAlignUI.SetData(val, alignment, callback);
            PopupWindow.Show(val, (PopupWindowContent)(object)spriteAlignUI);
        }
    }

    public override Vector2 GetWindowSize()
    {
        return windowSize;
    }

    public override void OnGUI(Rect rect)
    {
        float num = cellSize * 3f;
        float num2 = (rect.width - num) / 2f;
        Rect val = default(Rect);
        val = new Rect(rect);
        val.height = cellSize;
        val.width = num;
        val.x = rect.x + num2;
        Rect val2 = val;
        Rect val3 = default(Rect);
        val3 = new Rect(val2);
        val3.y = val3.y + cellSize;
        Rect rect2 = default(Rect);
        rect2 = new Rect(val3);
        rect2.y = rect2.y + cellSize;
        SpriteAlignment val4 = alignment;
        bool flag = (int)alignment == 1;
        bool flag2 = (int)alignment == 2;
        bool flag3 = (int)alignment == 3;
        bool flag4 = (int)alignment == 4;
        bool flag5 = (int)alignment == 0;
        bool flag6 = (int)alignment == 5;
        bool flag7 = (int)alignment == 6;
        bool flag8 = (int)alignment == 7;
        bool flag9 = (int)alignment == 8;
        SpriteAlignment[] aligns = new SpriteAlignment[3]
        {
            SpriteAlignment.TopLeft,
            SpriteAlignment.TopCenter,
            SpriteAlignment.TopRight
        };
        DrawAlignmentRow(new bool[3] { flag, flag2, flag3 }, aligns, val2, ref alignment, allowChange: true);
        aligns = (SpriteAlignment[])(object)new SpriteAlignment[3]
        {
            (SpriteAlignment)4,
            default(SpriteAlignment),
            (SpriteAlignment)5
        };
        DrawAlignmentRow(new bool[3] { flag4, flag5, flag6 }, aligns, val3, ref alignment, alignment == val4);
        aligns = new SpriteAlignment[3]
        {
            SpriteAlignment.BottomLeft,
            SpriteAlignment.BottomCenter,
            SpriteAlignment.BottomRight
        };
        DrawAlignmentRow(new bool[3] { flag7, flag8, flag9 }, aligns, rect2, ref alignment, alignment == val4);
        val = new Rect(rect);
        val.yMin = rect.yMax - EditorGUIUtility.singleLineHeight;
        if (GUI.Button(val, ((SpriteAlignment)9).ToString()))
        {
            alignment = (SpriteAlignment)9;
        }
        if (alignment != val4)
        {
            ((PopupWindowContent)this).editorWindow.Close();
            if (callback != null)
            {
                callback(alignment);
            }
        }
    }

    private void DrawAlignmentRow(bool[] vals, SpriteAlignment[] aligns, Rect rect, ref SpriteAlignment align, bool allowChange)
    {
        int num = -1;
        for (int i = 0; i < vals.Length; i++)
        {
            if (vals[i])
            {
                num = i;
                break;
            }
        }
        int num2 = GUI.Toolbar(rect, num, (GUIContent[])(object)new GUIContent[3]
        {
            GUIContent.none,
            GUIContent.none,
            GUIContent.none
        });
        if (num2 > -1 && allowChange)
        {
            align = aligns[num2];
        }
    }

    private void SetData(Rect position, SpriteAlignment alignment, Action<SpriteAlignment> callback)
    {
        this.alignment = alignment;
        this.callback = callback;
        windowSize = position.size;
        cellSize = Mathf.Min(EditorGUIUtility.singleLineHeight, windowSize.x / 3f);
        windowSize.y = cellSize * 3f + EditorGUIUtility.singleLineHeight;
    }

    internal static bool IsObfuscationSentinelNull()
    {
        return s_ObfuscationSentinel == null;
    }

    internal static SpriteAlignUI GetObfuscationSentinel()
    {
        return s_ObfuscationSentinel;
    }
}
