namespace UGF.EditorTools.Psd2UGUI
{
	/// <summary>
	/// <see cref="GUIType"/> 的纯逻辑判定规则（运行期侧）。
	///
	/// 这些判定只依赖枚举取值，不触碰任何编辑器 API，因此放在运行期侧，
	/// 供运行期组件（PsdLayerNode）与编辑器工具共用，避免拆分为 Runtime/Editor 两个程序集时出现反向依赖。
	/// 方法与 UGUIParser 中同名方法逻辑逐行一致；UGUIParser 的同名方法现转发到此处，调用点无需改动。
	/// </summary>
	public static class UITypeRules
	{
		public static bool IsPrimaryUIType(GUIType uiType)
		{
			if (uiType == GUIType.Null)
			{
				return false;
			}
			return uiType <= (GUIType)100;
		}

		public static bool IsAuxiliaryUIType(GUIType uiType)
		{
			return uiType > (GUIType)100;
		}

		public static bool IsPanelOrNull(GUIType uiType)
		{
			if (uiType != GUIType.Null)
			{
				return uiType == GUIType.Panel;
			}
			return true;
		}

		public static bool IsCompositeControlType(GUIType uiType)
		{
			if ((uint)(uiType - 4) > 5u && (uint)(uiType - 13) > 5u)
			{
				return false;
			}
			return true;
		}

		public static bool IsLeafVisualType(GUIType uiType)
		{
			if ((uint)(uiType - 1) > 2u && (uint)(uiType - 10) > 2u)
			{
				return false;
			}
			return true;
		}

		public static bool CanOwnAuxiliaryType(GUIType uiType, GUIType uiType2)
		{
			switch (uiType2)
			{
			default:
				return false;
			case GUIType.Background:
				return IsPrimaryUIType(uiType);
			case GUIType.Button_Highlight:
			case GUIType.Button_Press:
			case GUIType.Button_Select:
			case GUIType.Button_Disable:
			case GUIType.Button_Text:
				if (uiType != GUIType.Button)
				{
					return uiType == GUIType.TMPButton;
				}
				return true;
			case GUIType.Dropdown_Label:
			case GUIType.Dropdown_Arrow:
				if (uiType != GUIType.Dropdown)
				{
					return uiType == GUIType.TMPDropdown;
				}
				return true;
			case GUIType.InputField_Placeholder:
			case GUIType.InputField_Text:
				if (uiType != GUIType.InputField)
				{
					return uiType == GUIType.TMPInputField;
				}
				return true;
			case GUIType.Toggle_Checkmark:
			case GUIType.Toggle_Label:
				if (uiType != GUIType.Toggle)
				{
					return uiType == GUIType.TMPToggle;
				}
				return true;
			case GUIType.Slider_Fill:
			case GUIType.Slider_Handle:
				return uiType == GUIType.Slider;
			case GUIType.ScrollView_Viewport:
			case GUIType.ScrollView_HorizontalBarBG:
			case GUIType.ScrollView_HorizontalBar:
			case GUIType.ScrollView_VerticalBarBG:
			case GUIType.ScrollView_VerticalBar:
				return uiType == GUIType.ScrollView;
			}
		}

		public static bool CanOwnNestedControlType(GUIType uiType, GUIType uiType2)
		{
			switch (uiType)
			{
			default:
				return false;
			case GUIType.ToggleGroup:
				if (uiType2 != GUIType.Toggle)
				{
					return uiType2 == GUIType.TMPToggle;
				}
				return true;
			case GUIType.Dropdown:
			case GUIType.TMPDropdown:
				if (uiType2 != GUIType.ScrollView && uiType2 != GUIType.Toggle)
				{
					return uiType2 == GUIType.TMPToggle;
				}
				return true;
			}
		}
	}
}
