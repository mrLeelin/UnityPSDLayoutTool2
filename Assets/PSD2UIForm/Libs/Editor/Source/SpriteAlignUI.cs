using System;
using System.Collections.Generic;
using System.Linq;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using PsdProtectionRuntime;

internal class SpriteAlignUI : PopupWindowContent
{
	private float cellSize;

	private Vector2 windowSize;

	private SpriteAlignment alignment;

	private Action<SpriteAlignment> callback;

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
		PopupWindow.Show(rect, spriteAlignUI);
	}

	public static void DrawGUILayout(GUIContent label, SpriteAlignment alignment, Action<SpriteAlignment> callback, params GUILayoutOption[] layoutOptions)
	{
		using (new EditorGUILayout.HorizontalScope(layoutOptions))
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
	}

	public static void DrawGUI(Rect position, GUIContent label, SpriteAlignment alignment, Action<SpriteAlignment> callback)
	{
		Rect rect = position;
		if (label != GUIContent.none)
		{
			rect = EditorGUI.PrefixLabel(position, label);
		}
		string text = DisplayCamelCaseString(alignment.ToString());
		if (GUI.Button(rect, text, EditorStyles.popup))
		{
			SpriteAlignUI spriteAlignUI = new SpriteAlignUI();
			spriteAlignUI.SetData(rect, alignment, callback);
			PopupWindow.Show(rect, spriteAlignUI);
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
		Rect rect2 = new Rect(rect);
		rect2.height = cellSize;
		rect2.width = num;
		rect2.x = rect.x + num2;
		Rect rect3 = rect2;
		Rect rect4 = new Rect(rect3);
		rect4.y += cellSize;
		Rect rect5 = new Rect(rect4);
		rect5.y += cellSize;
		SpriteAlignment spriteAlignment = alignment;
		bool flag = alignment == SpriteAlignment.TopLeft;
		bool flag2 = alignment == SpriteAlignment.TopCenter;
		bool flag3 = alignment == SpriteAlignment.TopRight;
		bool flag4 = alignment == SpriteAlignment.LeftCenter;
		bool flag5 = alignment == SpriteAlignment.Center;
		bool flag6 = alignment == SpriteAlignment.RightCenter;
		bool flag7 = alignment == SpriteAlignment.BottomLeft;
		bool flag8 = alignment == SpriteAlignment.BottomCenter;
		bool flag9 = alignment == SpriteAlignment.BottomRight;
		SpriteAlignment[] aligns = new SpriteAlignment[3]
		{
			SpriteAlignment.TopLeft,
			SpriteAlignment.TopCenter,
			SpriteAlignment.TopRight
		};
		DrawAlignmentRow(new bool[3] { flag, flag2, flag3 }, aligns, rect3, ref alignment, allowChange: true);
		aligns = new SpriteAlignment[3]
		{
			SpriteAlignment.LeftCenter,
			SpriteAlignment.Center,
			SpriteAlignment.RightCenter
		};
		DrawAlignmentRow(new bool[3] { flag4, flag5, flag6 }, aligns, rect4, ref alignment, alignment == spriteAlignment);
		aligns = new SpriteAlignment[3]
		{
			SpriteAlignment.BottomLeft,
			SpriteAlignment.BottomCenter,
			SpriteAlignment.BottomRight
		};
		DrawAlignmentRow(new bool[3] { flag7, flag8, flag9 }, aligns, rect5, ref alignment, alignment == spriteAlignment);
		rect2 = new Rect(rect);
		rect2.yMin = rect.yMax - EditorGUIUtility.singleLineHeight;
		if (GUI.Button(rect2, SpriteAlignment.Custom.ToString()))
		{
			alignment = SpriteAlignment.Custom;
		}
		if (alignment != spriteAlignment)
		{
			base.editorWindow.Close();
			if (callback != null)
			{
				callback(alignment);
			}
		}
	}

	private void DrawAlignmentRow(bool[] vals, SpriteAlignment[] aligns, Rect rect, ref SpriteAlignment align, bool allowChange)
	{
		int selected = -1;
		for (int i = 0; i < vals.Length; i++)
		{
			if (vals[i])
			{
				selected = i;
				break;
			}
		}
		int num = GUI.Toolbar(rect, selected, new GUIContent[3]
		{
			GUIContent.none,
			GUIContent.none,
			GUIContent.none
		});
		if (num > -1 && allowChange)
		{
			align = aligns[num];
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

	public SpriteAlignUI()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private SpriteAlignUI(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
