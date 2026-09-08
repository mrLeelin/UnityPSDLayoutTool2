using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using PsdProtectionGuards;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using PsdTextStyles;
using PsdLayerUtilities;
using PsdProtectionRuntime;
using PsdPathUtilities;

namespace UGF.EditorTools.Psd2UGUI
{

[CreateAssetMenu(fileName = "Psd2UIFormConfig", menuName = "ScriptableObject/Psd2UIForm Config")]
[CanEditMultipleObjects]
public sealed class UGUIParser : ScriptableObject
{
	private enum LayerTagCategory
	{

	}

	private sealed class LayerNameTag
	{
		public string OriginalToken;

		public string CanonicalToken;

		public LayerTagCategory Category;

		public GUIType UiType;

		public Image.Type ImageType;

		public LayerNameTag()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private sealed class ParsedLayerName
	{
		public string OriginalName;

		public string BaseName;

		public bool HasImageReference;

		public bool HasPrefabReference;

		public List<LayerNameTag> Tags;

		public HashSet<string> CanonicalTokens;

		public Dictionary<LayerTagCategory, LayerNameTag> EffectiveTagsByCategory;

		public List<string> Warnings;

		public GUIType ResolvedUiType;

		public GUIType ResolvedRoleUiType;

		public bool HasExplicitMainTag;

		public bool HasExplicitImageType;

		public Image.Type ResolvedImageType;

		public bool HasExplicitTextBackend;

		public bool UseTmpBackend;

		public bool UseUguiBackend;

		public ParsedLayerName()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Tags = new List<LayerNameTag>();
			CanonicalTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			EffectiveTagsByCategory = new Dictionary<LayerTagCategory, LayerNameTag>();
			Warnings = new List<string>();
		}
	}

	private sealed class PhotoshopTagMenuItem
	{
		public string Id;

		public string Suffix;

		public string Label;

		public PhotoshopTagMenuItem()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private sealed class PhotoshopTagAlias
	{
		public string MainTag;

		public string RoleTag;

		public string ImageTypeTag;

		public string TextBackendTag;

		public PhotoshopTagAlias()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private sealed class PhotoshopRasterizationTags
	{
		public readonly HashSet<string> MainTags;

		public readonly HashSet<string> RoleTags;

		public PhotoshopRasterizationTags()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			MainTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			RoleTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass167_0
	{
		public string RequestedTagId;

		public _003C_003Ec__DisplayClass167_0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool MatchesRequestedTagId(PhotoshopTagMenuItem item)
		{
			return string.Equals(item.Id, RequestedTagId, StringComparison.OrdinalIgnoreCase);
		}
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	internal struct _003C_003Ec__DisplayClass170_0
	{
		public StringBuilder ScriptBuilder;
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	internal struct _003C_003Ec__DisplayClass170_1
	{
		public bool HasWrittenAliasProperty;
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct _003C_003Ec__DisplayClass75_0
	{
		public PsdLayerType SourceLayerType;

		public UGUIParser Parser;
	}

	[HideInInspector]
	[SerializeField]
	private GUIType defaultTextType = GUIType.Text;

	[HideInInspector]
	[SerializeField]
	private GUIType defaultImageType = GUIType.Image;

	[HideInInspector]
	[SerializeField]
	private bool forceUseTMP;

	[SerializeField]
	private GameObject uiFormTemplate;

	[SerializeField]
	private UGUIParseRule[] rules;

	[HideInInspector]
	[SerializeField]
	private string readmeDoc = "使用说明";

	[SerializeField]
	[HideInInspector]
	private bool convertZh2En = true;

	[SerializeField]
	[HideInInspector]
	private int nineSliceBorderTolerance = 5;

	[SerializeField]
	[HideInInspector]
	private string sharedAssetsOutput = "Assets/SharedUIAssets";

	[SerializeField]
	[HideInInspector]
	private string sharedPrefabOutput = "Assets/SharedPrefab";

	private static UGUIParser _instance;

	private static readonly Dictionary<string, Material> _tmpEffectMaterialsBySignature;

	private static readonly Dictionary<EntityId, Material> _baseMaterialsByEffectMaterialId;

	private static readonly int FaceDilatePropertyId;

	private static readonly int ShaderFlagsPropertyId;

	private static readonly int BevelOffsetPropertyId;

	private static readonly int BevelWidthPropertyId;

	private static readonly int BevelClampPropertyId;

	private static readonly int BevelRoundnessPropertyId;

	private static readonly int SpecularColorPropertyId;

	private static readonly int SpecularPowerPropertyId;

	private static readonly int ReflectivityPropertyId;

	private static readonly int ReflectFaceColorPropertyId;

	private static readonly int ReflectOutlineColorPropertyId;

	private static readonly int DiffusePropertyId;

	private static readonly int AmbientPropertyId;

	internal static UGUIParser Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = AssetDatabase.LoadAssetAtPath<UGUIParser>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:UGUIParser").FirstOrDefault()));
			}
			return _instance;
		}
	}

	[SpecialName]
	internal string GetSharedImageOutputDirectory()
	{
		return sharedAssetsOutput;
	}

	[SpecialName]
	internal string GetSharedPrefabOutputDirectory()
	{
		return sharedPrefabOutput;
	}

	[SpecialName]
	internal int GetNineSliceBorderTolerance()
	{
		return Mathf.Clamp(nineSliceBorderTolerance, 0, 255);
	}

	[SpecialName]
	internal GUIType GetDefaultTextUiType()
	{
		return NormalizeUiTypeForBackend(defaultTextType);
	}

	[SpecialName]
	internal GUIType GetDefaultImageUiType()
	{
		return defaultImageType;
	}

	[SpecialName]
	internal GameObject GetUiFormTemplate()
	{
		return uiFormTemplate;
	}

	[SpecialName]
	internal bool ShouldTransliterateChineseNames()
	{
		return convertZh2En;
	}

	[SpecialName]
	internal bool ShouldForceTmpBackend()
	{
		return forceUseTMP;
	}

	private static EntityId GetAssetEntityId(object unityObject)
	{
		return ((UnityEngine.Object)unityObject).GetEntityId();
	}

	private static string GetAssetIdentityString(object unityObject)
	{
		if (!((UnityEngine.Object)unityObject != null))
		{
			return "0";
		}
		return GetAssetEntityId(unityObject).ToString();
	}

	private static void SetTmpWordWrapping(object tmpText, bool enableWrapping)
	{
		((TMP_Text)tmpText).textWrappingMode = (enableWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap);
	}

	internal static bool IsPrimaryUiType(GUIType uiType)
	{
		return uiType <= (GUIType)100;
	}

	internal GUIType NormalizeUiTypeForBackend(GUIType uiType)
	{
		if (forceUseTMP)
		{
			return uiType switch
			{
				GUIType.Text => GUIType.TMPText, 
				GUIType.Button => GUIType.TMPButton, 
				GUIType.Dropdown => GUIType.TMPDropdown, 
				GUIType.InputField => GUIType.TMPInputField, 
				GUIType.Toggle => GUIType.TMPToggle, 
				_ => uiType, 
			};
		}
		return uiType;
	}

	private UGUIParseRule ResolveRuleForTextBackend(UGUIParseRule parseRule)
	{
		if (parseRule != null)
		{
			GUIType gUIType = NormalizeUiTypeForBackend(parseRule.UIType);
			if (gUIType == parseRule.UIType)
			{
				return parseRule;
			}
			return GetRuleForUiType(gUIType) ?? parseRule;
		}
		return null;
	}

	private static bool IsUiRoleType(GUIType uiType)
	{
		return uiType > (GUIType)100;
	}

	private static GUIType ToUguiUiType(GUIType uiType)
	{
		return uiType switch
		{
			GUIType.TMPText => GUIType.Text, 
			GUIType.TMPButton => GUIType.Button, 
			GUIType.TMPDropdown => GUIType.Dropdown, 
			GUIType.TMPInputField => GUIType.InputField, 
			GUIType.TMPToggle => GUIType.Toggle, 
			_ => uiType, 
		};
	}

	private static GUIType ToTmpUiType(GUIType uiType)
	{
		return uiType switch
		{
			GUIType.Text => GUIType.TMPText, 
			GUIType.Button => GUIType.TMPButton, 
			GUIType.Dropdown => GUIType.TMPDropdown, 
			GUIType.InputField => GUIType.TMPInputField, 
			GUIType.Toggle => GUIType.TMPToggle, 
			_ => uiType, 
		};
	}

	private static bool SupportsTextBackendSelection(GUIType uiType)
	{
		GUIType gUIType = ToUguiUiType(uiType);
		if ((uint)(gUIType - 3) <= 4u)
		{
			return true;
		}
		return false;
	}

	private static bool RequiresNineSliceBorder(Image.Type imageType)
	{
		if (imageType != Image.Type.Sliced)
		{
			return imageType == Image.Type.Tiled;
		}
		return true;
	}

	private static bool TryParseBuiltInLayerTag(object tagText, out LayerNameTag parsedTag)
	{
		parsedTag = null;
		if (string.IsNullOrWhiteSpace((string)tagText))
		{
			return false;
		}
		switch (((string)tagText).Trim().ToLowerInvariant())
		{
		case "simple":
			parsedTag = new LayerNameTag
			{
				OriginalToken = (string)tagText,
				CanonicalToken = "simple",
				Category = (LayerTagCategory)2,
				ImageType = Image.Type.Simple
			};
			return true;
		case "sliced":
			parsedTag = new LayerNameTag
			{
				OriginalToken = (string)tagText,
				CanonicalToken = "sliced",
				Category = (LayerTagCategory)2,
				ImageType = Image.Type.Sliced
			};
			return true;
		case "tiled":
			parsedTag = new LayerNameTag
			{
				OriginalToken = (string)tagText,
				CanonicalToken = "tiled",
				Category = (LayerTagCategory)2,
				ImageType = Image.Type.Tiled
			};
			return true;
		case "ugui":
			parsedTag = new LayerNameTag
			{
				OriginalToken = (string)tagText,
				CanonicalToken = "ugui",
				Category = (LayerTagCategory)3
			};
			return true;
		default:
			return false;
		case "tmp":
			parsedTag = new LayerNameTag
			{
				OriginalToken = (string)tagText,
				CanonicalToken = "tmp",
				Category = (LayerTagCategory)3
			};
			return true;
		case "filled":
			parsedTag = new LayerNameTag
			{
				OriginalToken = (string)tagText,
				CanonicalToken = "filled",
				Category = (LayerTagCategory)2,
				ImageType = Image.Type.Filled
			};
			return true;
		}
	}

	private bool TryParseConfiguredLayerTag(string tagText, out LayerNameTag parsedTag)
	{
		parsedTag = null;
		if (!string.IsNullOrWhiteSpace(tagText) && rules != null)
		{
			string text = tagText.Trim().ToLowerInvariant();
			for (int i = 0; i < rules.Length; i++)
			{
				UGUIParseRule uGUIParseRule = rules[i];
				if (uGUIParseRule?.TypeMatches == null)
				{
					continue;
				}
				for (int j = 0; j < uGUIParseRule.TypeMatches.Length; j++)
				{
					if (string.Equals(uGUIParseRule.TypeMatches[j], text, StringComparison.OrdinalIgnoreCase))
					{
						string text2 = uGUIParseRule.TypeMatches.FirstOrDefault((string item) => !string.IsNullOrWhiteSpace(item));
						text2 = (string.IsNullOrWhiteSpace(text2) ? text : text2.Trim().ToLowerInvariant());
						parsedTag = new LayerNameTag
						{
							OriginalToken = tagText,
							CanonicalToken = text2,
							Category = (IsUiRoleType(uGUIParseRule.UIType) ? ((LayerTagCategory)1) : ((LayerTagCategory)0)),
							UiType = uGUIParseRule.UIType
						};
						return true;
					}
				}
			}
			return false;
		}
		return false;
	}

	private bool TryParseLayerTag(string tagText, out LayerNameTag parsedTag)
	{
		if (!TryParseBuiltInLayerTag(tagText, out parsedTag))
		{
			return TryParseConfiguredLayerTag(tagText, out parsedTag);
		}
		return true;
	}

	private bool ParseLayerName(string layerName, PsdLayerType layerType, out ParsedLayerName parsedLayer, bool logWarnings = false)
	{
		parsedLayer = new ParsedLayerName();
		parsedLayer.OriginalName = layerName ?? string.Empty;
		string text = ((!string.IsNullOrWhiteSpace(layerName)) ? layerName.Trim() : string.Empty);
		if (!string.IsNullOrEmpty(text))
		{
			if (!text.StartsWith("refp ", StringComparison.OrdinalIgnoreCase))
			{
				if (text.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
				{
					parsedLayer.HasImageReference = true;
					text = text.Substring("ref ".Length).Trim();
				}
			}
			else
			{
				parsedLayer.HasPrefabReference = true;
				text = text.Substring("refp ".Length).Trim();
			}
			List<LayerNameTag> list = new List<LayerNameTag>();
			while (!string.IsNullOrWhiteSpace(text))
			{
				int num = text.LastIndexOf('.');
				if (num <= 0 || num >= text.Length - 1)
				{
					break;
				}
				string text2 = text.Substring(num + 1);
				if (!TryParseLayerTag(text2, out var item))
				{
					break;
				}
				list.Add(item);
				text = text.Substring(0, num).TrimEnd();
			}
			list.Reverse();
			parsedLayer.Tags.AddRange(list);
			parsedLayer.BaseName = text.Trim();
			for (int i = 0; i < parsedLayer.Tags.Count; i++)
			{
				LayerNameTag parsedTag = parsedLayer.Tags[i];
				parsedLayer.CanonicalTokens.Add(parsedTag.CanonicalToken);
				if (parsedLayer.EffectiveTagsByCategory.TryGetValue(parsedTag.Category, out var value) && !string.Equals(value.CanonicalToken, parsedTag.CanonicalToken, StringComparison.OrdinalIgnoreCase))
				{
					parsedLayer.Warnings.Add($"{parsedTag.Category}: {value.CanonicalToken} -> {parsedTag.CanonicalToken}");
				}
				parsedLayer.EffectiveTagsByCategory[parsedTag.Category] = parsedTag;
			}
			ResolveParsedLayerTypes(parsedLayer, layerType);
			if (logWarnings && parsedLayer.Warnings.Count > 0)
			{
				for (int j = 0; j < parsedLayer.Warnings.Count; j++)
				{
					Debug.LogWarning("Layer tag warning [" + layerName + "]: " + parsedLayer.Warnings[j]);
				}
			}
			return true;
		}
		ResolveParsedLayerTypes(parsedLayer, layerType);
		return true;
	}

	private void ResolveParsedLayerTypes(ParsedLayerName parsedLayer, PsdLayerType layerType)
	{
		_003C_003Ec__DisplayClass75_0 typeResolutionContext = default(_003C_003Ec__DisplayClass75_0);
		typeResolutionContext.SourceLayerType = layerType;
		typeResolutionContext.Parser = this;
		if (parsedLayer == null)
		{
			return;
		}
		if (parsedLayer.EffectiveTagsByCategory.TryGetValue((LayerTagCategory)1, out var value))
		{
			parsedLayer.ResolvedRoleUiType = value.UiType;
		}
		GUIType gUIType = GetDefaultUiTypeForLayer(ref typeResolutionContext);
		if (parsedLayer.EffectiveTagsByCategory.TryGetValue((LayerTagCategory)0, out var value2))
		{
			GUIType taggedUiType = value2.UiType;
			if (ToUguiUiType(taggedUiType) == GUIType.Text && typeResolutionContext.SourceLayerType != PsdLayerType.TextLayer)
			{
				parsedLayer.Warnings.Add("main tag '" + value2.CanonicalToken + "' ignored on non-text layer");
			}
			else
			{
				gUIType = taggedUiType;
				parsedLayer.HasExplicitMainTag = true;
			}
		}
		if (!parsedLayer.EffectiveTagsByCategory.TryGetValue((LayerTagCategory)3, out var value3))
		{
			gUIType = NormalizeUiTypeForBackend(gUIType);
		}
		else
		{
			parsedLayer.HasExplicitTextBackend = true;
			parsedLayer.UseTmpBackend = string.Equals(value3.CanonicalToken, "tmp", StringComparison.OrdinalIgnoreCase);
			parsedLayer.UseUguiBackend = string.Equals(value3.CanonicalToken, "ugui", StringComparison.OrdinalIgnoreCase);
			if (SupportsTextBackendSelection(gUIType))
			{
				gUIType = ((!parsedLayer.UseTmpBackend) ? ToUguiUiType(gUIType) : ToTmpUiType(gUIType));
			}
		}
		parsedLayer.ResolvedUiType = gUIType;
		if (parsedLayer.EffectiveTagsByCategory.TryGetValue((LayerTagCategory)2, out var value4))
		{
			parsedLayer.HasExplicitImageType = true;
			parsedLayer.ResolvedImageType = value4.ImageType;
		}
	}

	internal Type GetUiHelperType(GUIType uiType)
	{
		if (uiType != GUIType.Null)
		{
			UGUIParseRule uGUIParseRule = GetRuleForUiType(uiType);
			if (uGUIParseRule != null && !string.IsNullOrWhiteSpace(uGUIParseRule.UIHelper))
			{
				return Type.GetType(uGUIParseRule.UIHelper);
			}
			return null;
		}
		return null;
	}

	internal UGUIParseRule GetRuleForUiType(GUIType uiType)
	{
		UGUIParseRule[] array = rules;
		foreach (UGUIParseRule uGUIParseRule in array)
		{
			if (uGUIParseRule.UIType == uiType)
			{
				return uGUIParseRule;
			}
		}
		return null;
	}

	internal string BuildEditorLayerName(string layerName, int layerIndex = -1)
	{
		string text = (string.IsNullOrWhiteSpace(layerName) ? string.Empty : layerName.Trim());
		string text2 = text;
		bool num = text2.StartsWith("ref ", StringComparison.OrdinalIgnoreCase);
		bool flag = text2.StartsWith("refp ", StringComparison.OrdinalIgnoreCase);
		if (convertZh2En && !string.IsNullOrEmpty(text))
		{
			text = PsdAssetNameUtilities.TransliterateChineseToPinyin(text);
			text2 = text;
		}
		if (!(num || flag))
		{
			text = RemoveRecognizedLayerTags(text);
			text = PsdAssetNameUtilities.SanitizeLayerAssetName(text);
		}
		else
		{
			string text3 = ((!flag) ? "ref " : "refp ");
			string text4 = ((text2.Length <= text3.Length) ? string.Empty : text2.Substring(text3.Length).Trim());
			text4 = RemoveRecognizedLayerTags(text4);
			string text5 = PsdAssetNameUtilities.SanitizeLayerAssetName(text4);
			text = (text3 + ((!string.IsNullOrEmpty(text5)) ? text5 : text4)).TrimEnd();
		}
		if (string.IsNullOrEmpty(text))
		{
			text = ((layerIndex >= 0) ? $"PsdLayer-{layerIndex}" : "PsdLayer");
		}
		return text;
	}

	internal bool TryMatchLayerRuleAndRole(PsdLayerNode layerNode, out UGUIParseRule matchedRule, out GUIType roleUiType)
	{
		matchedRule = null;
		roleUiType = GUIType.Null;
		if (!(layerNode == null))
		{
			string text = ((layerNode.GetBoundPsdLayer() == null) ? layerNode.GetSourceLayerName() : layerNode.GetBoundPsdLayer().GetLayerName());
			ParseLayerName(text, layerNode.LayerType, out var parsedLayer, true);
			roleUiType = parsedLayer.ResolvedRoleUiType;
			if (parsedLayer.ResolvedUiType != GUIType.Null)
			{
				matchedRule = ResolveRuleForTextBackend(GetRuleForUiType(parsedLayer.ResolvedUiType));
			}
			return matchedRule != null;
		}
		return false;
	}

	internal bool TryMatchLayerRule(PsdLayerNode layerNode, out UGUIParseRule matchedRule)
	{
		GUIType gUIType;
		return TryMatchLayerRuleAndRole(layerNode, out matchedRule, out gUIType);
	}

	internal static bool TryGetTrailingLayerSuffix(object layerName, out string trailingSuffix)
	{
		trailingSuffix = null;
		if (!string.IsNullOrWhiteSpace((string)layerName) && !((string)layerName).EndsWith('.'.ToString()))
		{
			int num = -1;
			int num2 = ((string)layerName).Length - 1;
			while (num2 >= 0)
			{
				if (((string)layerName)[num2] != '.')
				{
					num2--;
					continue;
				}
				num = num2;
				break;
			}
			if (num > 0)
			{
				trailingSuffix = ((string)layerName).Substring(num);
				return true;
			}
			return false;
		}
		return false;
	}

	internal string RemoveRecognizedLayerTags(string layerName)
	{
		if (!string.IsNullOrWhiteSpace(layerName))
		{
			ParseLayerName(layerName, PsdLayerType.Unknown, out var parsedLayer);
			string untaggedLayerName = parsedLayer.BaseName;
			if (parsedLayer.HasPrefabReference)
			{
				return ("refp " + untaggedLayerName).TrimEnd();
			}
			if (!parsedLayer.HasImageReference)
			{
				return untaggedLayerName;
			}
			return ("ref " + untaggedLayerName).TrimEnd();
		}
		return string.Empty;
	}

	private string GetUntaggedLayerName(string layerName)
	{
		return RemoveRecognizedLayerTags(layerName);
	}

	private bool IsRecognizedLayerTag(string tagText)
	{
		LayerNameTag parsedTag;
		return TryParseLayerTag(tagText, out parsedTag);
	}

	internal static void ApplyLayerRectToUiElement(object layerNode, object uiComponent, bool applyPosition = true, bool applyWidth = true, bool applyHeight = true, int sizePadding = 0)
	{
		if (!((UnityEngine.Object)uiComponent == null) && !((UnityEngine.Object)layerNode == null))
		{
			Rect rect = ((PsdLayerNode)layerNode).GetLayerBounds();
			RectTransform component = ((Component)uiComponent).GetComponent<RectTransform>();
			if (applyWidth)
			{
				component.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.size.x + (float)sizePadding);
			}
			if (applyHeight)
			{
				component.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.size.y + (float)sizePadding);
			}
			if (applyPosition)
			{
				Vector2 size = component.rect.size;
				Vector2 vector = (component.pivot - Vector2.one * 0.5f) * size;
				Vector3 position = new Vector3(rect.position.x + vector.x, rect.position.y + vector.y, component.position.z);
				component.position = position;
			}
		}
	}

	internal static Texture2D ExportAndLoadLayerTexture(object layerNode)
	{
		if ((UnityEngine.Object)layerNode != null)
		{
			return AssetDatabase.LoadAssetAtPath<Texture2D>(((PsdLayerNode)layerNode).ExportLayerImage(false, (string)null, (string)null, true, false, false));
		}
		return null;
	}

	internal Image.Type ResolveLayerImageType(PsdLayerNode layerNode, Image.Type defaultImageType)
	{
		if (layerNode == null)
		{
			return defaultImageType;
		}
		string text = ((!string.IsNullOrWhiteSpace(layerNode.GetSourceLayerName())) ? layerNode.GetSourceLayerName() : layerNode.GetBoundPsdLayer()?.GetLayerName());
		ParseLayerName(text, layerNode.LayerType, out var parsedLayer);
		if (!parsedLayer.HasExplicitImageType)
		{
			return defaultImageType;
		}
		return parsedLayer.ResolvedImageType;
	}

	internal Sprite ApplyLayerSpriteToImage(PsdLayerNode layerNode, Image targetImage)
	{
		if (!(layerNode == null) && !(targetImage == null))
		{
			Image.Type type = (targetImage.type = ResolveLayerImageType(layerNode, targetImage.type));
			return targetImage.sprite = ExportAndLoadLayerSprite(layerNode, RequiresNineSliceBorder(type));
		}
		return null;
	}

	internal static Sprite ExportAndLoadLayerSprite(object layerNode, bool prepareNineSlice = false)
	{
		if ((UnityEngine.Object)layerNode != null)
		{
			string text = ((PsdLayerNode)layerNode).ExportLayerImage(true, (string)null, (string)null, true, false, false);
			Sprite sprite = PsdLayerNode.LoadSpriteAsset(text);
			if (sprite != null)
			{
				if (prepareNineSlice)
				{
					Psd2UIFormConverter.EnsureSpriteNineSliceBorder(text);
					if (ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice)
					{
						RightClickExtension.TryCropMinimalNineSlice(text);
					}
					sprite = PsdLayerNode.LoadSpriteAsset(text) ?? sprite;
				}
				return sprite;
			}
		}
		return null;
	}

	internal static Vector4 CalculateNineSliceBorder(object texture, byte alphaThreshold = 0, int colorTolerance = -1)
	{
		if (!((UnityEngine.Object)texture == null))
		{
			int width = ((Texture)texture).width;
			int height = ((Texture)texture).height;
			if (width > 0 && height > 0)
			{
				Color32[] pixels = ((Texture2D)texture).GetPixels32();
				byte b = (byte)((alphaThreshold == 0) ? 1 : alphaThreshold);
				int num = ((colorTolerance >= 0) ? Mathf.Clamp(colorTolerance, 0, 255) : ((!(Instance != null)) ? 5 : Instance.GetNineSliceBorderTolerance()));
				RectInt rectInt = FindVisiblePixelBounds(pixels, width, height, b);
				int xMin = rectInt.xMin;
				int num2 = rectInt.xMax - 1;
				int yMin = rectInt.yMin;
				int num3 = rectInt.yMax - 1;
				RangeInt rangeInt = FindStretchablePixelRange(pixels, width, rectInt, true, b, num);
				RangeInt rangeInt2 = FindStretchablePixelRange(pixels, width, rectInt, false, b, num);
				bool num4 = rangeInt.length > 1;
				bool flag = rangeInt2.length > 1;
				int num5 = (num4 ? Mathf.Clamp(rangeInt.start - xMin, 0, Mathf.Max(0, width - 1)) : 0);
				int num6 = (num4 ? Mathf.Clamp(num2 - (rangeInt.start + rangeInt.length - 1), 0, Mathf.Max(0, width - 1)) : 0);
				int num7 = (flag ? Mathf.Clamp(rangeInt2.start - yMin, 0, Mathf.Max(0, height - 1)) : 0);
				int num8 = (flag ? Mathf.Clamp(num3 - (rangeInt2.start + rangeInt2.length - 1), 0, Mathf.Max(0, height - 1)) : 0);
				return new Vector4(num5, num7, num6, num8);
			}
			return Vector4.zero;
		}
		return Vector4.zero;
	}

	private static RangeInt FindStretchablePixelRange(object P_0, int P_1, RectInt P_2, bool P_3, byte P_4, int P_5)
	{
		int num = ((!P_3) ? P_2.yMin : P_2.xMin);
		int num2 = (P_3 ? (P_2.xMax - 1) : (P_2.yMax - 1));
		int num3 = (P_3 ? P_2.yMin : P_2.xMin);
		int num4 = (P_3 ? (P_2.yMax - 1) : (P_2.xMax - 1));
		int num5 = num2 - num + 1;
		int num6 = num4 - num3 + 1;
		if (num5 >= 3 && num6 > 0)
		{
			P_5 = Mathf.Max(0, P_5);
			int[] array = new int[num6];
			int[] array2 = new int[num6];
			int[] array3 = new int[num6];
			int[] array4 = new int[num6];
			int[] array5 = new int[num6];
			int[] array6 = new int[num6];
			int[] array7 = new int[num6];
			int[] array8 = new int[num6];
			int start = num + num5 / 2;
			int num7 = 1;
			int num8 = int.MaxValue;
			int num9 = -1;
			float num10 = float.PositiveInfinity;
			float num11 = (float)num + (float)(num5 - 1) * 0.5f;
			for (int i = num + 1; i < num2; i++)
			{
				InitializeStretchRangeChannelBounds(P_0, P_1, P_3, i, num3, num4, P_4, array, array2, array3, array4, array5, array6, array7, array8);
				for (int j = i; j < num2 && (j <= i || TryExpandStretchRange(P_0, P_1, P_3, j, num3, num4, P_4, P_5, array, array2, array3, array4, array5, array6, array7, array8)); j++)
				{
					int num12 = j - i + 1;
					if (num12 <= 1)
					{
						continue;
					}
					int num13 = i - num;
					int num14 = num2 - j;
					if (num13 <= 0 || num14 <= 0)
					{
						continue;
					}
					int num15 = Mathf.Abs(num13 - num14);
					int num16 = Mathf.Min(num13, num14);
					float num17 = Mathf.Abs((float)i + (float)(num12 - 1) * 0.5f - num11);
					bool flag = false;
					if (num12 > num7)
					{
						flag = true;
					}
					else if (num12 == num7)
					{
						if (num15 < num8)
						{
							flag = true;
						}
						else if (num15 == num8)
						{
							if (num17 < num10 - 0.001f)
							{
								flag = true;
							}
							else if (Mathf.Abs(num17 - num10) <= 0.001f && num16 > num9)
							{
								flag = true;
							}
						}
					}
					if (flag)
					{
						start = i;
						num7 = num12;
						num8 = num15;
						num9 = num16;
						num10 = num17;
					}
				}
			}
			return new RangeInt(start, num7);
		}
		return new RangeInt(num + Mathf.Max(0, num5 / 2), 1);
	}

	private static void InitializeStretchRangeChannelBounds(object P_0, int P_1, bool P_2, int P_3, int P_4, int P_5, byte P_6, object P_7, object P_8, object P_9, object P_10, object P_11, object P_12, object P_13, object P_14)
	{
		for (int i = P_4; i <= P_5; i++)
		{
			Color32 color = ClearTransparentPixelRgb(ReadAxisAlignedPixel(P_0, P_1, P_2, P_3, i), P_6);
			int num = i - P_4;
			((int[])P_7)[num] = (((int[])P_11)[num] = color.r);
			((int[])P_8)[num] = (((int[])P_12)[num] = color.g);
			((int[])P_9)[num] = (((int[])P_13)[num] = color.b);
			((int[])P_10)[num] = (((int[])P_14)[num] = color.a);
		}
	}

	private static bool TryExpandStretchRange(object P_0, int P_1, bool P_2, int P_3, int P_4, int P_5, byte P_6, int P_7, object P_8, object P_9, object P_10, object P_11, object P_12, object P_13, object P_14, object P_15)
	{
		int num = P_4;
		while (true)
		{
			if (num <= P_5)
			{
				Color32 color = ClearTransparentPixelRgb(ReadAxisAlignedPixel(P_0, P_1, P_2, P_3, num), P_6);
				int num2 = num - P_4;
				((int[])P_8)[num2] = Mathf.Min(((int[])P_8)[num2], color.r);
				((int[])P_9)[num2] = Mathf.Min(((int[])P_9)[num2], color.g);
				((int[])P_10)[num2] = Mathf.Min(((int[])P_10)[num2], color.b);
				((int[])P_11)[num2] = Mathf.Min(((int[])P_11)[num2], color.a);
				((int[])P_12)[num2] = Mathf.Max(((int[])P_12)[num2], color.r);
				((int[])P_13)[num2] = Mathf.Max(((int[])P_13)[num2], color.g);
				((int[])P_14)[num2] = Mathf.Max(((int[])P_14)[num2], color.b);
				((int[])P_15)[num2] = Mathf.Max(((int[])P_15)[num2], color.a);
				if (((int[])P_12)[num2] - ((int[])P_8)[num2] > P_7 || ((int[])P_13)[num2] - ((int[])P_9)[num2] > P_7 || ((int[])P_14)[num2] - ((int[])P_10)[num2] > P_7 || ((int[])P_15)[num2] - ((int[])P_11)[num2] > P_7)
				{
					break;
				}
				num++;
				continue;
			}
			return true;
		}
		return false;
	}

	private static Color32 ClearTransparentPixelRgb(Color32 P_0, byte P_1)
	{
		if (P_0.a < P_1)
		{
			P_0.r = 0;
			P_0.g = 0;
			P_0.b = 0;
		}
		return P_0;
	}

	private static Color32 ReadAxisAlignedPixel(object P_0, int P_1, bool P_2, int P_3, int P_4)
	{
		int num = ((!P_2) ? P_4 : P_3);
		int num2 = Mathf.Clamp((P_2 ? P_4 : P_3) * P_1 + num, 0, ((Array)P_0).Length - 1);
		return ((Color32[])P_0)[num2];
	}

	private static RectInt FindVisiblePixelBounds(object P_0, int P_1, int P_2, byte P_3)
	{
		int num = P_1;
		int num2 = P_2;
		int num3 = -1;
		int num4 = -1;
		for (int i = 0; i < P_2; i++)
		{
			for (int j = 0; j < P_1; j++)
			{
				if (((Color32[])P_0)[i * P_1 + j].a >= P_3)
				{
					if (j < num)
					{
						num = j;
					}
					if (j > num3)
					{
						num3 = j;
					}
					if (i < num2)
					{
						num2 = i;
					}
					if (i > num4)
					{
						num4 = i;
					}
				}
			}
		}
		if (num3 >= num && num4 >= num2)
		{
			return new RectInt(num, num2, num3 - num + 1, num4 - num2 + 1);
		}
		return new RectInt(0, 0, P_1, P_2);
	}

	internal static PsdTextStyleInfo ApplyPsdTextToUguiText(object layerNode, object uguiText)
	{
		if (!((UnityEngine.Object)uguiText == null))
		{
			((Component)uguiText).gameObject.SetActive((UnityEngine.Object)layerNode != null);
			if ((UnityEngine.Object)layerNode != null && ((PsdLayerNode)layerNode).TryBuildTextStyleData(out PsdTextStyleInfo textInfo))
			{
				bool flag = ShouldWrapText(layerNode, in textInfo);
				Font font = FindUnityFontByPsdName(textInfo.FontName);
				if (font != null)
				{
					((Text)uguiText).font = font;
				}
				((Text)uguiText).text = textInfo.TextContent;
				((Text)uguiText).fontSize = textInfo.FontSize;
				((Text)uguiText).fontStyle = textInfo.FontStyle;
				((Graphic)uguiText).color = textInfo.TextColor;
				((Text)uguiText).resizeTextForBestFit = false;
				((Text)uguiText).lineSpacing = CalculateUguiLineSpacing(uguiText, in textInfo);
				((Text)uguiText).horizontalOverflow = ((!flag) ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap);
				((Text)uguiText).verticalOverflow = VerticalWrapMode.Overflow;
				ApplyUguiTextEffects(uguiText, in textInfo);
				return textInfo;
			}
			return default(PsdTextStyleInfo);
		}
		return default(PsdTextStyleInfo);
	}

	internal static void ApplyPsdTextToTmpText(object layerNode, object tmpText)
	{
		if ((UnityEngine.Object)tmpText == null)
		{
			return;
		}
		((Component)tmpText).gameObject.SetActive((UnityEngine.Object)layerNode != null);
		if (!((UnityEngine.Object)layerNode != null) || !((PsdLayerNode)layerNode).TryBuildTextStyleData(out PsdTextStyleInfo textInfo))
		{
			return;
		}
		bool flag = ShouldWrapText(layerNode, in textInfo);
		TMP_FontAsset tMP_FontAsset = FindOrCreateTmpFontAsset(textInfo.FontName) ?? ((TMP_Text)tmpText).font;
		if (tMP_FontAsset != null)
		{
			Material material = EnsureTmpFontMaterial(tMP_FontAsset);
			EnsureTmpFontCharacters(tMP_FontAsset, textInfo.TextContent);
			material = EnsureTmpFontMaterial(tMP_FontAsset) ?? material;
			((TMP_Text)tmpText).font = tMP_FontAsset;
			if (material != null)
			{
				((TMP_Text)tmpText).fontSharedMaterial = material;
			}
		}
		((TMP_Text)tmpText).text = textInfo.TextContent ?? string.Empty;
		((TMP_Text)tmpText).fontSize = textInfo.FontSize;
		((TMP_Text)tmpText).fontStyle = textInfo.TmpFontStyle;
		((TMP_Text)tmpText).characterSpacing = textInfo.CharacterSpacing;
		((TMP_Text)tmpText).lineSpacing = CalculateTmpLineSpacing(tmpText, in textInfo);
		((TMP_Text)tmpText).enableAutoSizing = false;
		SetTmpWordWrapping(tmpText, flag);
		((TMP_Text)tmpText).overflowMode = TextOverflowModes.Overflow;
		((TMP_Text)tmpText).margin = Vector4.zero;
		((Graphic)tmpText).color = (HasUsableTextGradient(in textInfo) ? Color.white : textInfo.TextColor);
		ApplyTmpTextEffects(tmpText, in textInfo);
		((TMP_Text)tmpText).ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: false);
		ApplyTmpTextColor(layerNode, tmpText, in textInfo);
	}

	private static float CalculateUguiLineSpacing(object uguiText, in PsdTextStyleInfo textInfo)
	{
		if (!textInfo.AutoLineSpacing && textInfo.LineSpacing > 0f)
		{
			float b = Mathf.Max(1f, textInfo.FontSize);
			Font font = ((!((UnityEngine.Object)uguiText != null)) ? null : ((Text)uguiText).font);
			if (font != null && font.fontSize > 0 && font.lineHeight > 0)
			{
				b = (float)font.lineHeight * ((float)textInfo.FontSize / (float)font.fontSize);
			}
			float num = textInfo.LineSpacing / Mathf.Max(1f, b);
			if (!float.IsNaN(num) && !float.IsInfinity(num))
			{
				return Mathf.Max(0.01f, num);
			}
			return 1f;
		}
		return 1f;
	}

	private static bool ShouldWrapText(object layerNode, in PsdTextStyleInfo textInfo)
	{
		if (!((UnityEngine.Object)layerNode == null))
		{
			if (!string.IsNullOrEmpty(textInfo.TextContent) && (textInfo.TextContent.IndexOf('\n') >= 0 || textInfo.TextContent.IndexOf('\r') >= 0))
			{
				return true;
			}
			float num = Mathf.Max((float)textInfo.FontSize * 1.35f, 1f);
			return ((PsdLayerNode)layerNode).GetLayerBounds().height > num;
		}
		return false;
	}

	private static float CalculateTmpLineSpacing(object tmpText, in PsdTextStyleInfo textInfo)
	{
		if (!textInfo.AutoLineSpacing && textInfo.LineSpacing > 0f)
		{
			float num = Mathf.Max(1f, textInfo.FontSize);
			float num2 = num;
			TMP_FontAsset tMP_FontAsset = ((!((UnityEngine.Object)tmpText != null)) ? null : ((TMP_Text)tmpText).font);
			if (tMP_FontAsset != null)
			{
				FaceInfo faceInfo = tMP_FontAsset.faceInfo;
				if (faceInfo.pointSize > 0f && faceInfo.lineHeight > 0f)
				{
					float num3 = num / faceInfo.pointSize;
					num2 = faceInfo.lineHeight * num3;
				}
			}
			float num4 = (textInfo.LineSpacing - num2) * 100f / num;
			if (!float.IsNaN(num4) && !float.IsInfinity(num4))
			{
				if (num4 < 0f && num4 > -0.25f)
				{
					return 0f;
				}
				return num4;
			}
			return 0f;
		}
		return 0f;
	}

	private static void RemoveUguiTextEffects(object textComponent)
	{
		if ((UnityEngine.Object)textComponent == null)
		{
			return;
		}
		Shadow[] components = ((Component)textComponent).GetComponents<Shadow>();
		foreach (Shadow shadow in components)
		{
			if (shadow != null)
			{
				UnityEngine.Object.DestroyImmediate(shadow);
			}
		}
	}

	private static void ApplyUguiTextEffects(object uguiText, in PsdTextStyleInfo textInfo)
	{
		if (!((UnityEngine.Object)uguiText == null))
		{
			RemoveUguiTextEffects(uguiText);
			if (textInfo.OutlineEnabled)
			{
				Outline outline = ((Component)uguiText).gameObject.AddComponent<Outline>();
				outline.enabled = true;
				float outlineWidth = textInfo.UguiOutlineSize;
				outline.effectColor = textInfo.OutlineColor;
				outline.effectDistance = new Vector2(outlineWidth, outlineWidth);
				outline.useGraphicAlpha = true;
			}
			if (textInfo.ShadowEnabled && !textInfo.InnerShadow)
			{
				Shadow shadow = ((Component)uguiText).gameObject.AddComponent<Shadow>();
				shadow.enabled = true;
				shadow.effectColor = textInfo.ShadowColor;
				shadow.effectDistance = textInfo.ShadowOffset;
				shadow.useGraphicAlpha = true;
			}
		}
	}

	private static void ApplyTmpTextColor(object layerNode, object tmpText, in PsdTextStyleInfo textInfo)
	{
		if (!((UnityEngine.Object)tmpText == null))
		{
			((TMP_Text)tmpText).enableVertexGradient = false;
			((TMP_Text)tmpText).colorGradientPreset = null;
			((TMP_Text)tmpText).colorGradient = new VertexGradient(textInfo.TextColor);
			if (TryApplyTmpVertexGradient(tmpText, in textInfo))
			{
				((Graphic)tmpText).color = Color.white;
			}
			else
			{
				((Graphic)tmpText).color = textInfo.TextColor;
			}
		}
	}

	private static bool HasUsableTextGradient(in PsdTextStyleInfo textInfo)
	{
		if (textInfo.GradientEnabled && textInfo.GradientStops != null)
		{
			return textInfo.GradientStops.Length >= 2;
		}
		return false;
	}

	private static bool TryApplyTmpVertexGradient(object tmpText, in PsdTextStyleInfo textInfo)
	{
		if (!((UnityEngine.Object)tmpText == null) && HasUsableTextGradient(in textInfo))
		{
			((TMP_Text)tmpText).enableVertexGradient = true;
			((TMP_Text)tmpText).colorGradientPreset = null;
			((TMP_Text)tmpText).colorGradient = BuildTmpVertexGradient(in textInfo);
			((Graphic)tmpText).color = Color.white;
			((TMP_Text)tmpText).havePropertiesChanged = true;
			((Graphic)tmpText).SetVerticesDirty();
			((TMP_Text)tmpText).ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: false);
			return true;
		}
		return false;
	}

	private static VertexGradient BuildTmpVertexGradient(in PsdTextStyleInfo textInfo)
	{
		Vector2 rhs = GetGradientDirection(textInfo.GradientAngle);
		Vector2[] array = new Vector2[4]
		{
			new Vector2(-0.5f, 0.5f),
			new Vector2(0.5f, 0.5f),
			new Vector2(-0.5f, -0.5f),
			new Vector2(0.5f, -0.5f)
		};
		float num = float.PositiveInfinity;
		float num2 = float.NegativeInfinity;
		for (int i = 0; i < array.Length; i++)
		{
			float num3 = Vector2.Dot(array[i], rhs);
			if (num3 < num)
			{
				num = num3;
			}
			if (num3 > num2)
			{
				num2 = num3;
			}
		}
		float num4 = num2 - num;
		if (Mathf.Abs(num4) < 0.0001f)
		{
			num4 = 1f;
		}
		return new VertexGradient
		{
			topLeft = EvaluateTextGradient(in textInfo, (Vector2.Dot(array[0], rhs) - num) / num4),
			topRight = EvaluateTextGradient(in textInfo, (Vector2.Dot(array[1], rhs) - num) / num4),
			bottomLeft = EvaluateTextGradient(in textInfo, (Vector2.Dot(array[2], rhs) - num) / num4),
			bottomRight = EvaluateTextGradient(in textInfo, (Vector2.Dot(array[3], rhs) - num) / num4)
		};
	}

	private static Vector2 GetGradientDirection(float angleDegrees)
	{
		float f = NormalizeGradientAngle(angleDegrees) * ((float)Math.PI / 180f);
		Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
		if (vector.sqrMagnitude < 0.0001f)
		{
			return Vector2.right;
		}
		return vector.normalized;
	}

	private static float NormalizeGradientAngle(float angleDegrees)
	{
		if (!float.IsNaN(angleDegrees) && !float.IsInfinity(angleDegrees))
		{
			angleDegrees %= 360f;
			if (angleDegrees < 0f)
			{
				angleDegrees += 360f;
			}
			return angleDegrees;
		}
		return 0f;
	}

	private static Color32 EvaluateTextGradient(in PsdTextStyleInfo textInfo, float gradientPosition)
	{
		PsdUiGradientStop[] gradientStops = textInfo.GradientStops;
		if (gradientStops != null && gradientStops.Length != 0)
		{
			if (float.IsNaN(gradientPosition) || float.IsInfinity(gradientPosition))
			{
				gradientPosition = 0f;
			}
			gradientPosition = Mathf.Clamp01(gradientPosition);
			if (textInfo.GradientReverse)
			{
				gradientPosition = 1f - gradientPosition;
			}
			if (gradientPosition <= gradientStops[0].Position)
			{
				return gradientStops[0].Color;
			}
			if (gradientPosition >= gradientStops[gradientStops.Length - 1].Position)
			{
				return gradientStops[gradientStops.Length - 1].Color;
			}
			int num = 0;
			PsdUiGradientStop startStop;
			PsdUiGradientStop endStop;
			while (true)
			{
				if (num < gradientStops.Length - 1)
				{
					startStop = gradientStops[num];
					endStop = gradientStops[num + 1];
					if (!(gradientPosition > endStop.Position))
					{
						break;
					}
					num++;
					continue;
				}
				return gradientStops[gradientStops.Length - 1].Color;
			}
			if (endStop.Position - startStop.Position <= 0.0001f)
			{
				return endStop.Color;
			}
			float t = Mathf.InverseLerp(startStop.Position, endStop.Position, gradientPosition);
			return Color.Lerp(startStop.Color, endStop.Color, t);
		}
		return textInfo.TextColor;
	}

	private static void ApplyTmpTextEffects(object tmpText, in PsdTextStyleInfo textInfo)
	{
		if ((UnityEngine.Object)tmpText == null)
		{
			return;
		}
		RemoveUguiTextEffects(tmpText);
		Material material = ResolveTmpBaseMaterial(tmpText);
		if (material == null)
		{
			return;
		}
		if (!textInfo.OutlineEnabled && !textInfo.ShadowEnabled && !textInfo.GlowEnabled && !textInfo.BevelEnabled)
		{
			if (((TMP_Text)tmpText).fontSharedMaterial != material)
			{
				((TMP_Text)tmpText).fontSharedMaterial = material;
				((TMP_Text)tmpText).UpdateMeshPadding();
				((Graphic)tmpText).SetMaterialDirty();
			}
			return;
		}
		Material material2 = GetOrCreateTmpEffectMaterial(((TMP_Text)tmpText).font, material, ((TMP_Text)tmpText).fontSize, in textInfo);
		if (material2 != null && ((TMP_Text)tmpText).fontSharedMaterial != material2)
		{
			((TMP_Text)tmpText).fontSharedMaterial = material2;
			((TMP_Text)tmpText).UpdateMeshPadding();
			((TMP_Text)tmpText).havePropertiesChanged = true;
			((Graphic)tmpText).SetMaterialDirty();
		}
	}

	private static Material ResolveTmpBaseMaterial(object tmpText)
	{
		Material fontSharedMaterial = ((TMP_Text)tmpText).fontSharedMaterial;
		Material material = ((((TMP_Text)tmpText).font != null) ? EnsureTmpFontMaterial(((TMP_Text)tmpText).font) : null);
		if (fontSharedMaterial != null && _baseMaterialsByEffectMaterialId.TryGetValue(GetAssetEntityId(fontSharedMaterial), out var value) && value != null && material != null && MaterialsShareFontAtlas(value, material))
		{
			return value;
		}
		if (fontSharedMaterial != null && material != null && MaterialsShareFontAtlas(fontSharedMaterial, material))
		{
			return fontSharedMaterial;
		}
		return material;
	}

	private static bool MaterialsShareFontAtlas(object firstMaterial, object secondMaterial)
	{
		if (!((UnityEngine.Object)firstMaterial == null) && !((UnityEngine.Object)secondMaterial == null))
		{
			Texture texture = ((Material)firstMaterial).GetTexture(ShaderUtilities.ID_MainTex);
			Texture texture2 = ((Material)secondMaterial).GetTexture(ShaderUtilities.ID_MainTex);
			if (!(texture == null) && !(texture2 == null))
			{
				return GetAssetEntityId(texture) == GetAssetEntityId(texture2);
			}
			return false;
		}
		return false;
	}

	private static Material GetOrCreateTmpEffectMaterial(object fontAsset, object baseMaterial, float fontSize, in PsdTextStyleInfo textInfo)
	{
		if ((UnityEngine.Object)fontAsset != null)
		{
			Material material = EnsureTmpFontMaterial(fontAsset);
			if (material != null)
			{
				baseMaterial = material;
			}
		}
		if (!((UnityEngine.Object)baseMaterial == null))
		{
			fontSize = Mathf.Max(1f, fontSize);
			ShaderUtilities.GetShaderPropertyIDs();
			float num = GetTmpGradientScale(baseMaterial);
			float num2 = (textInfo.OutlineEnabled ? Mathf.Clamp01(textInfo.TmpOutlineSize / num) : 0f);
			float num3 = num2 * 0.5f;
			float num4 = 0f;
			if (textInfo.OutlineEnabled)
			{
				num4 = textInfo.OutlineMode switch
				{
					PsdTextStyleInfo.TMPOutlineMode.Center => num2 * 0.5f, 
					PsdTextStyleInfo.TMPOutlineMode.Inside => 0f, 
					PsdTextStyleInfo.TMPOutlineMode.Outside => num2, 
					_ => num2 * 0.5f, 
				};
			}
			float num5 = 0f;
			float num6 = 0f;
			float num7 = 0f;
			float num8 = 0f;
			float num9 = 0f;
			bool flag = textInfo.ShadowEnabled && textInfo.InnerShadow;
			if (textInfo.ShadowEnabled)
			{
				float num10 = Mathf.Clamp(textInfo.ShadowOffset.x / num, -1f, 1f);
				float num11 = Mathf.Clamp(textInfo.ShadowOffset.y / num, -1f, 1f);
				float num12 = Mathf.Clamp01(textInfo.ShadowSpread);
				float num13 = Mathf.Clamp01(textInfo.ShadowSoftness / num);
				float num14 = Mathf.Clamp01(num13 * num12);
				float num15 = (flag ? (0f - num14) : num14);
				float num16 = Mathf.Clamp01(num13 - num14);
				float num17 = CalculateTmpEffectPaddingBudget(baseMaterial, num4);
				if (flag)
				{
					num10 = 0f - num10;
					num11 = 0f - num11;
				}
				ClampTmpUnderlayToPadding(num17, ref num10, ref num11, ref num15, ref num16, flag);
				num6 = NormalizeEffectSize(num10, num17);
				num7 = NormalizeEffectSize(num11, num17);
				num8 = NormalizeEffectSize(num15, num17);
				num9 = NormalizeEffectSize(num16, num17);
			}
			float num18 = 0f;
			float num19 = 0f;
			float num20 = 0f;
			float num21 = 0f;
			Color d602fO7VaF = textInfo.GlowColor;
			if (textInfo.GlowEnabled)
			{
				float num22 = Mathf.Clamp01(textInfo.GlowSize / num);
				float t = Mathf.Clamp01(textInfo.GlowSpread);
				float num23 = Mathf.Clamp01((textInfo.GlowPower > 0f) ? textInfo.GlowPower : 0.75f);
				if (textInfo.InnerGlow)
				{
					float num24 = Mathf.Clamp01(num23 * 0.1f);
					num18 = num22;
					num19 = 0f;
					num20 = 0f;
					num21 = Mathf.Lerp(num24, Mathf.Min(1f, num24 * 2f), t);
				}
				else
				{
					num18 = 0f;
					num19 = Mathf.Clamp01(num22 * Mathf.Lerp(2f, 1.35f, t));
					num20 = 0f;
					num21 = Mathf.Lerp(0.2f, 0.45f, t);
				}
			}
			float num25 = ((!textInfo.BevelEnabled) ? 0f : Mathf.Clamp01(textInfo.BevelSize / num));
			float num26 = ((!textInfo.BevelEnabled || textInfo.BevelSize <= 0f) ? 0f : Mathf.Clamp01(textInfo.BevelSoften / textInfo.BevelSize));
			float num27 = ((!textInfo.BevelEnabled) ? 0f : Mathf.Clamp(num25 * Mathf.Lerp(0.5f, 0.375f, num26), 0f, 0.5f));
			float num28 = (textInfo.BevelEnabled ? Mathf.Clamp(num27 - num3 * Mathf.Lerp(1f, 0.65f, num26), -0.5f, 0.5f) : 0f);
			float num29 = (textInfo.BevelEnabled ? (((textInfo.BevelDepth > 1f) ? Mathf.Clamp01(textInfo.BevelDepth / 100f) : Mathf.Clamp01(textInfo.BevelDepth)) * Mathf.Lerp(1f, 0.82f, num26)) : 0f);
			float num30 = ((!textInfo.BevelEnabled) ? 0f : Mathf.Clamp((textInfo.InnerBevel ? (-1f) : 1f) * num25 * Mathf.Lerp(0.12f, 0.04f, num26), -0.5f, 0.5f));
			float num31 = (textInfo.BevelEnabled ? Mathf.Clamp01(num26 * Mathf.Lerp(0.45f, 0.75f, num25)) : 0f);
			float num32 = (textInfo.BevelEnabled ? Mathf.Lerp(0.08f, 0.92f, num26) : 0f);
			float num33 = (textInfo.BevelEnabled ? ConvertPhotoshopLightAngleToRadians(textInfo.BevelAngle) : 0f);
			float num34 = ((!textInfo.BevelEnabled || textInfo.InnerBevel) ? 0f : 1f);
			Color color = Color.clear;
			Color color2 = Color.black;
			Color color3 = Color.black;
			float num35 = 0f;
			float num36 = 10f;
			float num37 = 0f;
			float num38 = 1f;
			if (textInfo.BevelEnabled)
			{
				float num39 = Mathf.Clamp01(textInfo.BevelAltitude / 90f);
				float num40 = Mathf.Clamp01(textInfo.BevelHighlightOpacity);
				float num41 = Mathf.Clamp01(textInfo.BevelShadowOpacity) * (1f - CalculateColorLuminance(textInfo.BevelShadowColor));
				float num42 = num40 * Mathf.Lerp(0.65f, 1f, num29);
				float num43 = Mathf.Clamp01(Mathf.Lerp(0.35f, 1f, num41));
				color = new Color(textInfo.BevelShadowColor.r * num43, textInfo.BevelShadowColor.g * num43, textInfo.BevelShadowColor.b * num43, 1f);
				float num44 = Mathf.Clamp01(Mathf.Lerp(0.22f, 0.72f, num42) * Mathf.Lerp(0.85f, 1.05f, num39));
				color2 = new Color(textInfo.BevelHighlightColor.r * num44, textInfo.BevelHighlightColor.g * num44, textInfo.BevelHighlightColor.b * num44, 1f);
				Color color4 = ((!textInfo.OutlineEnabled) ? textInfo.BevelHighlightColor : Color.Lerp(textInfo.OutlineColor, textInfo.BevelHighlightColor, 0.65f));
				float num45 = Mathf.Clamp01(num44 * ((!textInfo.OutlineEnabled) ? 0.9f : 1f));
				color3 = new Color(color4.r * num45, color4.g * num45, color4.b * num45, 1f);
				num35 = ((num42 > 0f) ? Mathf.Clamp(Mathf.Lerp(0.2f, 2.2f, num42) * Mathf.Lerp(0.8f, 1.1f, num39), 0f, 4f) : 0f);
				num36 = Mathf.Lerp(14f, 6f, Mathf.Clamp01(num32 + (1f - num39) * 0.25f));
				num37 = ((num41 > 0f) ? Mathf.Clamp01(Mathf.Lerp(0.15f, 0.8f, num41) * Mathf.Lerp(1.1f, 0.75f, num39)) : 0f);
				num38 = ((num41 > 0f) ? Mathf.Clamp01(1f - num41 * Mathf.Lerp(0.75f, 0.45f, num39)) : 1f);
			}
			Color32 color5 = textInfo.OutlineColor;
			Color32 color6 = textInfo.ShadowColor;
			Color32 color7 = d602fO7VaF;
			int num46 = Mathf.RoundToInt(num3 * 10000f);
			int num47 = Mathf.RoundToInt(num4 * 10000f);
			int num48 = Mathf.RoundToInt(num6 * 10000f);
			int num49 = Mathf.RoundToInt(num7 * 10000f);
			int num50 = Mathf.RoundToInt(num5 * 10000f);
			int num51 = Mathf.RoundToInt(num8 * 10000f);
			int num52 = Mathf.RoundToInt(num9 * 10000f);
			int num53 = Mathf.RoundToInt(num19 * 10000f);
			int num54 = Mathf.RoundToInt(num18 * 10000f);
			int num55 = Mathf.RoundToInt(num20 * 10000f);
			int num56 = Mathf.RoundToInt(num21 * 10000f);
			int num57 = Mathf.RoundToInt(num29 * 10000f);
			int num58 = Mathf.RoundToInt(num30 * 10000f);
			int num59 = Mathf.RoundToInt(num28 * 10000f);
			int num60 = Mathf.RoundToInt(num31 * 10000f);
			int num61 = Mathf.RoundToInt(num32 * 10000f);
			int num62 = Mathf.RoundToInt(num33 * 10000f);
			Color32 color8 = color;
			int num63 = Mathf.RoundToInt(num35 * 10000f);
			int num64 = Mathf.RoundToInt(num36 * 10000f);
			int num65 = Mathf.RoundToInt(num37 * 10000f);
			int num66 = Mathf.RoundToInt(num38 * 10000f);
			int num67 = Mathf.RoundToInt(num34 * 10000f);
			string text = $"o:{(textInfo.OutlineEnabled ? 1 : 0)}|op:{(int)textInfo.OutlineMode}|ow:{num46}|fd:{num47}|os:{num50}|oc:{color5.r},{color5.g},{color5.b},{color5.a}" + $"|s:{(textInfo.ShadowEnabled ? 1 : 0)}|si:{(flag ? 1 : 0)}|so:{num48},{num49}|sd:{num51}|ss:{num52}|sc:{color6.r},{color6.g},{color6.b},{color6.a}" + $"|g:{(textInfo.GlowEnabled ? 1 : 0)}|gi:{(textInfo.InnerGlow ? 1 : 0)}|go:{num53}|giw:{num54}|gof:{num55}|gp:{num56}|gc:{color7.r},{color7.g},{color7.b},{color7.a}" + $"|b:{(textInfo.BevelEnabled ? 1 : 0)}|bi:{(textInfo.InnerBevel ? 1 : 0)}|ba:{num57}|bo:{num58}|bw:{num59}|bc:{num60}|br:{num61}|la:{num62}|bsc:{color8.r},{color8.g},{color8.b},{color8.a}|bsp:{num63}|brv:{num64}|bdf:{num65}|bam:{num66}|bf:{num67}";
			string key = GetAssetIdentityString(fontAsset) + "|" + text;
			if (_tmpEffectMaterialsBySignature.TryGetValue(key, out var value) && value != null)
			{
				SynchronizeTmpMaterialAtlas(value, fontAsset);
				return value;
			}
			string text2 = BuildUniqueTmpEffectMaterialName(fontAsset, baseMaterial, in textInfo);
			Material material2 = LoadTmpEffectMaterial(fontAsset, text);
			if (material2 != null)
			{
				SynchronizeTmpMaterialAtlas(material2, fontAsset);
				ApplyTmpEffectShaderProperties(material2, num3, num4, num5, color5, textInfo.OutlineEnabled, num6, num7, num8, num9, color6, textInfo.ShadowEnabled, flag, color7, num20, num18, num19, num21, textInfo.GlowEnabled, num29, num30, num28, num31, num32, num33, color, color2, color3, num35, num36, num37, num38, num34, textInfo.BevelEnabled);
				EditorUtility.SetDirty(material2);
				_tmpEffectMaterialsBySignature[key] = material2;
				_baseMaterialsByEffectMaterialId[GetAssetEntityId(material2)] = (Material)baseMaterial;
				return material2;
			}
			Material material3 = CloneTmpMaterialWithEffectsShader(baseMaterial, true);
			if (material3 == null)
			{
				return null;
			}
			material3.name = text2;
			SynchronizeTmpMaterialAtlas(material3, fontAsset);
			ApplyTmpEffectShaderProperties(material3, num3, num4, num5, color5, textInfo.OutlineEnabled, num6, num7, num8, num9, color6, textInfo.ShadowEnabled, flag, color7, num20, num18, num19, num21, textInfo.GlowEnabled, num29, num30, num28, num31, num32, num33, color, color2, color3, num35, num36, num37, num38, num34, textInfo.BevelEnabled);
			if ((UnityEngine.Object)fontAsset != null)
			{
				SaveTmpEffectMaterial(fontAsset, material3, text);
			}
			_tmpEffectMaterialsBySignature[key] = material3;
			_baseMaterialsByEffectMaterialId[GetAssetEntityId(material3)] = (Material)baseMaterial;
			return material3;
		}
		return null;
	}

	private static float GetTmpGradientScale(object P_0)
	{
		if (!((UnityEngine.Object)P_0 == null) && ((Material)P_0).HasProperty(ShaderUtilities.ID_GradientScale))
		{
			return Mathf.Max(1f, ((Material)P_0).GetFloat(ShaderUtilities.ID_GradientScale));
		}
		return 10f;
	}

	private static float GetTmpMaximumFontWeight(object P_0)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return 0f;
		}
		float a = (((Material)P_0).HasProperty(ShaderUtilities.ID_WeightNormal) ? ((Material)P_0).GetFloat(ShaderUtilities.ID_WeightNormal) : 0f);
		float b = (((Material)P_0).HasProperty(ShaderUtilities.ID_WeightBold) ? ((Material)P_0).GetFloat(ShaderUtilities.ID_WeightBold) : 0f);
		return Mathf.Max(a, b) / 4f;
	}

	private static float CalculateTmpEffectPaddingBudget(object P_0, float P_1)
	{
		if (!((UnityEngine.Object)P_0 == null) && ((Material)P_0).HasProperty(ShaderUtilities.ID_GradientScale))
		{
			float num = Mathf.Max(1f, ((Material)P_0).GetFloat(ShaderUtilities.ID_GradientScale));
			float num2 = (GetTmpMaximumFontWeight(P_0) + P_1) * (num - 1f);
			return Mathf.Max(0f, num - 1f - num2) / num;
		}
		return 0f;
	}

	private static void ClampTmpUnderlayToPadding(float P_0, ref float P_1, ref float P_2, ref float P_3, ref float P_4, bool P_5 = false)
	{
		if (P_0 <= 0f)
		{
			P_1 = 0f;
			P_2 = 0f;
			P_3 = 0f;
			P_4 = 0f;
			return;
		}
		P_1 = Mathf.Clamp(P_1, -1f, 1f);
		P_2 = Mathf.Clamp(P_2, -1f, 1f);
		P_3 = Mathf.Clamp(P_3, (!P_5) ? 0f : (-1f), 1f);
		P_4 = Mathf.Clamp(P_4, 0f, 1f);
		float num = Mathf.Max(Mathf.Abs(P_1), Mathf.Abs(P_2));
		if (num > P_0 && num > 0f)
		{
			float num2 = P_0 / num;
			P_1 *= num2;
			P_2 *= num2;
			P_3 = 0f;
			P_4 = 0f;
		}
		else
		{
			float num3 = P_0 - num;
			float num4 = Mathf.Min(Mathf.Abs(P_3), num3);
			P_3 = Mathf.Sign(P_3) * num4;
			num3 -= num4;
			P_4 = Mathf.Min(P_4, num3);
		}
	}

	private static float NormalizeEffectSize(float P_0, float P_1)
	{
		if (P_1 <= 0f)
		{
			return 0f;
		}
		return P_0 / P_1;
	}

	private static float ConvertPhotoshopLightAngleToRadians(float P_0)
	{
		return Mathf.Repeat(90f - P_0, 360f) * ((float)Math.PI / 180f);
	}

	private static float CalculateColorLuminance(Color P_0)
	{
		return P_0.r * 0.2126f + P_0.g * 0.7152f + P_0.b * 0.0722f;
	}

	private static void ApplyTmpEffectShaderProperties(object P_0, float P_1, float P_2, float P_3, Color32 P_4, bool P_5, float P_6, float P_7, float P_8, float P_9, Color32 P_10, bool P_11, bool P_12, Color32 P_13, float P_14, float P_15, float P_16, float P_17, bool P_18, float P_19, float P_20, float P_21, float P_22, float P_23, float P_24, Color P_25, Color P_26, Color P_27, float P_28, float P_29, float P_30, float P_31, float P_32, bool P_33)
	{
		if (!((UnityEngine.Object)P_0 == null))
		{
			if (((Material)P_0).HasProperty(FaceDilatePropertyId))
			{
				((Material)P_0).SetFloat(FaceDilatePropertyId, P_5 ? P_2 : 0f);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_OutlineWidth))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_OutlineWidth, P_1);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_OutlineSoftness))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_OutlineSoftness, P_3);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_OutlineColor))
			{
				((Material)P_0).SetColor(ShaderUtilities.ID_OutlineColor, (!P_5) ? Color.clear : ((Color)P_4));
			}
			if (!P_5)
			{
				((Material)P_0).DisableKeyword(ShaderUtilities.Keyword_Outline);
			}
			else
			{
				((Material)P_0).EnableKeyword(ShaderUtilities.Keyword_Outline);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_UnderlayColor))
			{
				((Material)P_0).SetColor(ShaderUtilities.ID_UnderlayColor, P_11 ? ((Color)P_10) : Color.clear);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_UnderlayOffsetX, P_6);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_UnderlayOffsetY, P_7);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_UnderlaySoftness))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_UnderlaySoftness, P_9);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_UnderlayDilate))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_UnderlayDilate, P_8);
			}
			if (!P_11)
			{
				((Material)P_0).DisableKeyword(ShaderUtilities.Keyword_Underlay);
				((Material)P_0).DisableKeyword("UNDERLAY_INNER");
			}
			else if (!P_12)
			{
				((Material)P_0).EnableKeyword(ShaderUtilities.Keyword_Underlay);
				((Material)P_0).DisableKeyword("UNDERLAY_INNER");
			}
			else
			{
				((Material)P_0).DisableKeyword(ShaderUtilities.Keyword_Underlay);
				((Material)P_0).EnableKeyword("UNDERLAY_INNER");
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_GlowColor))
			{
				((Material)P_0).SetColor(ShaderUtilities.ID_GlowColor, P_18 ? ((Color)P_13) : Color.clear);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_GlowOffset))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_GlowOffset, P_14);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_GlowInner))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_GlowInner, P_15);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_GlowOuter))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_GlowOuter, P_16);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_GlowPower))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_GlowPower, P_17);
			}
			if (P_18)
			{
				((Material)P_0).EnableKeyword(ShaderUtilities.Keyword_Glow);
			}
			else
			{
				((Material)P_0).DisableKeyword(ShaderUtilities.Keyword_Glow);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_BevelAmount))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_BevelAmount, P_19);
			}
			if (((Material)P_0).HasProperty(ShaderFlagsPropertyId))
			{
				((Material)P_0).SetFloat(ShaderFlagsPropertyId, P_33 ? P_32 : 0f);
			}
			if (((Material)P_0).HasProperty(BevelOffsetPropertyId))
			{
				((Material)P_0).SetFloat(BevelOffsetPropertyId, P_20);
			}
			if (((Material)P_0).HasProperty(BevelWidthPropertyId))
			{
				((Material)P_0).SetFloat(BevelWidthPropertyId, P_21);
			}
			if (((Material)P_0).HasProperty(BevelClampPropertyId))
			{
				((Material)P_0).SetFloat(BevelClampPropertyId, P_22);
			}
			if (((Material)P_0).HasProperty(BevelRoundnessPropertyId))
			{
				((Material)P_0).SetFloat(BevelRoundnessPropertyId, P_23);
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_LightAngle))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_LightAngle, P_24);
			}
			if (((Material)P_0).HasProperty(SpecularColorPropertyId))
			{
				((Material)P_0).SetColor(SpecularColorPropertyId, P_33 ? P_25 : Color.clear);
			}
			if (((Material)P_0).HasProperty(ReflectFaceColorPropertyId))
			{
				((Material)P_0).SetColor(ReflectFaceColorPropertyId, (!P_33) ? Color.black : P_26);
			}
			if (((Material)P_0).HasProperty(ReflectOutlineColorPropertyId))
			{
				((Material)P_0).SetColor(ReflectOutlineColorPropertyId, P_33 ? P_27 : Color.black);
			}
			if (((Material)P_0).HasProperty(SpecularPowerPropertyId))
			{
				((Material)P_0).SetFloat(SpecularPowerPropertyId, (!P_33) ? 0f : P_28);
			}
			if (((Material)P_0).HasProperty(ReflectivityPropertyId))
			{
				((Material)P_0).SetFloat(ReflectivityPropertyId, (!P_33) ? 10f : P_29);
			}
			if (((Material)P_0).HasProperty(DiffusePropertyId))
			{
				((Material)P_0).SetFloat(DiffusePropertyId, (!P_33) ? 0f : P_30);
			}
			if (((Material)P_0).HasProperty(AmbientPropertyId))
			{
				((Material)P_0).SetFloat(AmbientPropertyId, (!P_33) ? 1f : P_31);
			}
			if (!P_33)
			{
				((Material)P_0).DisableKeyword(ShaderUtilities.Keyword_Bevel);
			}
			else
			{
				((Material)P_0).EnableKeyword(ShaderUtilities.Keyword_Bevel);
			}
			ShaderUtilities.UpdateShaderRatios((Material)P_0);
		}
	}

	private static string BuildUniqueTmpEffectMaterialName(object P_0, object P_1, in PsdTextStyleInfo textInfo)
	{
		string text = BuildTextEffectName(in textInfo);
		string text2 = GetTmpMaterialAssetDirectory(P_0, P_1);
		if (string.IsNullOrWhiteSpace(text2))
		{
			return text + "_1";
		}
		int num = 0;
		string[] array = AssetDatabase.FindAssets("t:Material", new string[1] { text2 });
		for (int i = 0; i < array.Length; i++)
		{
			string text3 = AssetDatabase.GUIDToAssetPath(array[i]);
			if (!string.IsNullOrWhiteSpace(text3) && TryParseNumberedMaterialName(Path.GetFileNameWithoutExtension(text3), text, out var num2) && num2 > num)
			{
				num = num2;
			}
		}
		return $"{text}_{num + 1}";
	}

	private static Material LoadTmpEffectMaterial(object fontAsset, object effectSignature)
	{
		if (!((UnityEngine.Object)fontAsset == null) && !string.IsNullOrWhiteSpace((string)effectSignature))
		{
			if (TryFindTmpEffectMaterialAsset(fontAsset, effectSignature, out var material, out var _))
			{
				SynchronizeTmpMaterialAtlas(material, fontAsset);
				return material;
			}
			return null;
		}
		return null;
	}

	private static void SaveTmpEffectMaterial(object fontAsset, object effectMaterial, object effectSignature)
	{
		if ((UnityEngine.Object)fontAsset == null || (UnityEngine.Object)effectMaterial == null || string.IsNullOrWhiteSpace((string)effectSignature))
		{
			return;
		}
		string assetPath = AssetDatabase.GetAssetPath((UnityEngine.Object)fontAsset);
		if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		SynchronizeTmpMaterialAtlas(effectMaterial, fontAsset);
		if (!TryFindTmpEffectMaterialAsset(fontAsset, effectSignature, out var material, out var text))
		{
			text = BuildSiblingMaterialAssetPath(assetPath, ((UnityEngine.Object)effectMaterial).name, false);
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			AssetDatabase.CreateAsset((UnityEngine.Object)effectMaterial, text);
			effectMaterial = AssetDatabase.LoadAssetAtPath<Material>(text) ?? effectMaterial;
		}
		else if (material != (UnityEngine.Object)effectMaterial)
		{
			EditorUtility.CopySerialized((UnityEngine.Object)effectMaterial, material);
			UnityEngine.Object.DestroyImmediate((UnityEngine.Object)effectMaterial);
			effectMaterial = material;
		}
		SynchronizeTmpMaterialAtlas(effectMaterial, fontAsset);
		WriteTmpEffectImporterSignature(fontAsset, text, effectSignature);
		EditorUtility.SetDirty((UnityEngine.Object)effectMaterial);
		EditorUtility.SetDirty((UnityEngine.Object)fontAsset);
		AssetDatabase.SaveAssets();
	}

	private static string BuildSiblingMaterialAssetPath(object fontAssetPath, object materialName, bool ensureUniquePath)
	{
		if (!string.IsNullOrWhiteSpace((string)fontAssetPath) && !string.IsNullOrWhiteSpace((string)materialName))
		{
			string text = Path.GetDirectoryName((string)fontAssetPath)?.Replace("\\", "/");
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			string text2 = text + "/" + (string)materialName + ".mat";
			if (!ensureUniquePath)
			{
				return text2;
			}
			return AssetDatabase.GenerateUniqueAssetPath(text2);
		}
		return null;
	}

	private static string BuildTextEffectName(in PsdTextStyleInfo textInfo)
	{
		List<string> list = new List<string>(4);
		if (textInfo.OutlineEnabled)
		{
			list.Add("Outline");
		}
		if (textInfo.ShadowEnabled)
		{
			list.Add(textInfo.InnerShadow ? "InnerShadow" : "Shadow");
		}
		if (textInfo.GlowEnabled)
		{
			list.Add((!textInfo.InnerGlow) ? "Glow" : "InnerGlow");
		}
		if (textInfo.BevelEnabled)
		{
			list.Add("Bevel");
		}
		if (list.Count > 0)
		{
			return string.Join("_", list);
		}
		return "Text";
	}

	private static bool TryParseNumberedMaterialName(object materialName, object namePrefix, out int numericSuffix)
	{
		numericSuffix = 0;
		if (!string.IsNullOrWhiteSpace((string)materialName) && !string.IsNullOrWhiteSpace((string)namePrefix))
		{
			string text = (string)namePrefix + "_";
			if (!((string)materialName).StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			return int.TryParse(((string)materialName).Substring(text.Length), out numericSuffix);
		}
		return false;
	}

	private static string GetTmpMaterialAssetDirectory(object P_0, object P_1)
	{
		string text = (((UnityEngine.Object)P_0 != null) ? AssetDatabase.GetAssetPath((UnityEngine.Object)P_0) : (((UnityEngine.Object)P_1 != null) ? AssetDatabase.GetAssetPath((UnityEngine.Object)P_1) : null));
		if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
		{
			return Path.GetDirectoryName(text)?.Replace("\\", "/");
		}
		return null;
	}

	private static string GetStableFontAssetKey(object fontAsset)
	{
		if (!((UnityEngine.Object)fontAsset == null))
		{
			string assetPath = AssetDatabase.GetAssetPath((UnityEngine.Object)fontAsset);
			if (!string.IsNullOrWhiteSpace(assetPath))
			{
				string text = AssetDatabase.AssetPathToGUID(assetPath);
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text;
				}
			}
			return ((UnityEngine.Object)fontAsset).name;
		}
		return "TMP_FONT_NULL";
	}

	private static string BuildTmpEffectImporterSignature(object fontAsset, object effectSignature)
	{
		return "PSD2UI_TMPFX_SIG:" + GetStableFontAssetKey(fontAsset) + "|" + (string)effectSignature;
	}

	private static string BuildHashedTmpEffectMaterialName(object fontAsset, object effectSignature)
	{
		uint num = (uint)Animator.StringToHash((string)effectSignature);
		string arg = ((!((UnityEngine.Object)fontAsset != null)) ? "TMP Font" : ((UnityEngine.Object)fontAsset).name);
		return string.Format("{0}{1}{2:X8}", arg, "__PSD2UI_TMPFX__", num);
	}

	private static bool TryFindTmpEffectMaterialAsset(object fontAsset, object effectSignature, out Material effectMaterial, out string materialAssetPath)
	{
		effectMaterial = null;
		materialAssetPath = null;
		if (!((UnityEngine.Object)fontAsset == null) && !string.IsNullOrWhiteSpace((string)effectSignature))
		{
			string text = GetTmpMaterialAssetDirectory(fontAsset, null);
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			string b = BuildTmpEffectImporterSignature(fontAsset, effectSignature);
			string[] array = AssetDatabase.FindAssets("t:Material", new string[1] { text });
			int num = 0;
			string text2;
			while (true)
			{
				if (num < array.Length)
				{
					text2 = AssetDatabase.GUIDToAssetPath(array[num]);
					if (!string.IsNullOrWhiteSpace(text2))
					{
						AssetImporter atPath = AssetImporter.GetAtPath(text2);
						if (!(atPath == null) && string.Equals(atPath.userData, b, StringComparison.Ordinal))
						{
							effectMaterial = AssetDatabase.LoadAssetAtPath<Material>(text2);
							if (!(effectMaterial == null))
							{
								break;
							}
						}
					}
					num++;
					continue;
				}
				string assetPath = AssetDatabase.GetAssetPath((UnityEngine.Object)fontAsset);
				string text3 = BuildHashedTmpEffectMaterialName(fontAsset, effectSignature);
				string text4 = BuildSiblingMaterialAssetPath(assetPath, text3, false);
				if (string.IsNullOrWhiteSpace(text4))
				{
					return false;
				}
				effectMaterial = AssetDatabase.LoadAssetAtPath<Material>(text4);
				if (!(effectMaterial == null))
				{
					materialAssetPath = text4;
					WriteTmpEffectImporterSignature(fontAsset, materialAssetPath, effectSignature);
					return true;
				}
				return false;
			}
			materialAssetPath = text2;
			return true;
		}
		return false;
	}

	private static void WriteTmpEffectImporterSignature(object fontAsset, object materialAssetPath, object effectSignature)
	{
		if (string.IsNullOrWhiteSpace((string)materialAssetPath) || string.IsNullOrWhiteSpace((string)effectSignature))
		{
			return;
		}
		AssetImporter atPath = AssetImporter.GetAtPath((string)materialAssetPath);
		if (!(atPath == null))
		{
			string text = BuildTmpEffectImporterSignature(fontAsset, effectSignature);
			if (!string.Equals(atPath.userData, text, StringComparison.Ordinal))
			{
				atPath.userData = text;
				atPath.SaveAndReimport();
			}
		}
	}

	private static string GetFontNameWithoutSdfSuffix(object fontAsset, object fontAssetPath)
	{
		string text = ((!string.IsNullOrWhiteSpace((string)fontAssetPath)) ? Path.GetFileNameWithoutExtension((string)fontAssetPath) : (((UnityEngine.Object)fontAsset != null) ? ((UnityEngine.Object)fontAsset).name : "TMP Font"));
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "TMP Font";
		}
		if (!text.EndsWith(" SDF", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		return text.Substring(0, text.Length - " SDF".Length);
	}

	private static Texture2D FindFontAtlasTexture(object fontAsset)
	{
		if (!((UnityEngine.Object)fontAsset == null))
		{
			Texture2D[] atlasTextures = ((TMP_FontAsset)fontAsset).atlasTextures;
			if (atlasTextures != null)
			{
				for (int i = 0; i < atlasTextures.Length; i++)
				{
					if (atlasTextures[i] != null)
					{
						return atlasTextures[i];
					}
				}
			}
			string assetPath = AssetDatabase.GetAssetPath((UnityEngine.Object)fontAsset);
			if (string.IsNullOrWhiteSpace(assetPath))
			{
				return null;
			}
			Texture2D texture2D = null;
			UnityEngine.Object[] array = AssetDatabase.LoadAllAssetsAtPath(assetPath);
			if (array == null)
			{
				return null;
			}
			for (int j = 0; j < array.Length; j++)
			{
				if (array[j] is Texture2D texture2D2)
				{
					if ((object)texture2D == null)
					{
						texture2D = texture2D2;
					}
					if (texture2D2.name.IndexOf("Atlas", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return texture2D2;
					}
				}
			}
			return texture2D;
		}
		return null;
	}

	private static bool SynchronizeTmpMaterialAtlas(object P_0, object P_1)
	{
		if (!((UnityEngine.Object)P_0 == null) && !((UnityEngine.Object)P_1 == null))
		{
			ShaderUtilities.GetShaderPropertyIDs();
			Texture2D texture2D = FindFontAtlasTexture(P_1);
			if (texture2D == null)
			{
				return false;
			}
			bool flag = false;
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_MainTex) && ((Material)P_0).GetTexture(ShaderUtilities.ID_MainTex) != texture2D)
			{
				((Material)P_0).SetTexture(ShaderUtilities.ID_MainTex, (Texture)texture2D);
				flag = true;
			}
			float num = ((texture2D.width > 0) ? texture2D.width : Mathf.Max(1, ((TMP_FontAsset)P_1).atlasWidth));
			float num2 = ((texture2D.height > 0) ? texture2D.height : Mathf.Max(1, ((TMP_FontAsset)P_1).atlasHeight));
			float num3 = Mathf.Max(1f, (float)((TMP_FontAsset)P_1).atlasPadding + 1f);
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_TextureWidth) && !Mathf.Approximately(((Material)P_0).GetFloat(ShaderUtilities.ID_TextureWidth), num))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_TextureWidth, num);
				flag = true;
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_TextureHeight) && !Mathf.Approximately(((Material)P_0).GetFloat(ShaderUtilities.ID_TextureHeight), num2))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_TextureHeight, num2);
				flag = true;
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_GradientScale) && !Mathf.Approximately(((Material)P_0).GetFloat(ShaderUtilities.ID_GradientScale), num3))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_GradientScale, num3);
				flag = true;
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_WeightNormal) && !Mathf.Approximately(((Material)P_0).GetFloat(ShaderUtilities.ID_WeightNormal), ((TMP_FontAsset)P_1).normalStyle))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_WeightNormal, ((TMP_FontAsset)P_1).normalStyle);
				flag = true;
			}
			if (((Material)P_0).HasProperty(ShaderUtilities.ID_WeightBold) && !Mathf.Approximately(((Material)P_0).GetFloat(ShaderUtilities.ID_WeightBold), ((TMP_FontAsset)P_1).boldStyle))
			{
				((Material)P_0).SetFloat(ShaderUtilities.ID_WeightBold, ((TMP_FontAsset)P_1).boldStyle);
				flag = true;
			}
			ShaderUtilities.UpdateShaderRatios((Material)P_0);
			if (flag)
			{
				EditorUtility.SetDirty((UnityEngine.Object)P_0);
			}
			return flag;
		}
		return false;
	}

	private static Material EnsureTmpFontMaterial(object P_0)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return null;
		}
		string assetPath = AssetDatabase.GetAssetPath((UnityEngine.Object)P_0);
		Texture2D texture2D = FindFontAtlasTexture(P_0);
		bool flag = false;
		if (texture2D != null)
		{
			if (((TMP_FontAsset)P_0).atlasTextures != null && ((TMP_FontAsset)P_0).atlasTextures.Length != 0)
			{
				if (((TMP_FontAsset)P_0).atlasTextures[0] == null)
				{
					((TMP_FontAsset)P_0).atlasTextures[0] = texture2D;
					flag = true;
				}
			}
			else
			{
				((TMP_FontAsset)P_0).atlasTextures = new Texture2D[1] { texture2D };
				flag = true;
			}
		}
		Material material = ((TMP_Asset)P_0).material;
		string value = ((!(material != null)) ? null : AssetDatabase.GetAssetPath(material));
		if ((material == null || string.IsNullOrWhiteSpace(value)) && !string.IsNullOrWhiteSpace(assetPath) && assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
		{
			string text = GetFontNameWithoutSdfSuffix(P_0, assetPath) + " Atlas Material";
			string text2 = BuildSiblingMaterialAssetPath(assetPath, text, false);
			Material material2 = (string.IsNullOrWhiteSpace(text2) ? null : AssetDatabase.LoadAssetAtPath<Material>(text2));
			if (material2 != null)
			{
				material = material2;
			}
			else
			{
				Material material3 = ((TMP_Settings.defaultFontAsset != null) ? TMP_Settings.defaultFontAsset.material : null);
				if (material == null)
				{
					material = CloneTmpMaterialWithEffectsShader(material3, true);
					if (material == null)
					{
						Shader shader = Shader.Find("TextMeshPro/Distance Field");
						if (shader != null)
						{
							material = new Material(shader);
						}
					}
				}
				if (material != null)
				{
					material.name = text;
					AssetDatabase.CreateAsset(material, text2);
					material = AssetDatabase.LoadAssetAtPath<Material>(text2) ?? material;
				}
			}
			if (material != null && ((TMP_Asset)P_0).material != material)
			{
				((TMP_Asset)P_0).material = material;
				flag = true;
			}
		}
		if (material != null && SynchronizeTmpMaterialAtlas(material, P_0))
		{
			flag = true;
		}
		if (flag)
		{
			EditorUtility.SetDirty((UnityEngine.Object)P_0);
			if (texture2D != null)
			{
				EditorUtility.SetDirty(texture2D);
			}
			if (material != null)
			{
				EditorUtility.SetDirty(material);
			}
			AssetDatabase.SaveAssets();
		}
		return ((TMP_Asset)P_0).material;
	}

	private static Material CloneTmpMaterialWithEffectsShader(object P_0, bool P_1)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return null;
		}
		ShaderUtilities.GetShaderPropertyIDs();
		Shader shader = (P_1 ? Shader.Find("TextMeshPro/Distance Field") : null);
		Material material;
		if (shader != null && ((Material)P_0).shader != shader)
		{
			material = new Material(shader);
			material.CopyPropertiesFromMaterial((Material)P_0);
		}
		else
		{
			material = new Material((Material)P_0);
		}
		Texture texture = ((Material)P_0).GetTexture(ShaderUtilities.ID_MainTex);
		if (texture != null && material.HasProperty(ShaderUtilities.ID_MainTex))
		{
			material.SetTexture(ShaderUtilities.ID_MainTex, texture);
		}
		return material;
	}

	internal static string NormalizeFontNameWords(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			return Regex.Replace(Regex.Replace((string)P_0, "[^A-Za-z0-9]+", " "), "\\s+", " ").Trim();
		}
		return string.Empty;
	}

	private static string NormalizeCompactFontName(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		return Regex.Replace(NormalizeFontNameWords(P_0), "\\s+", string.Empty);
	}

	private static int ScoreFontNameMatch(object P_0, object P_1, object P_2, object P_3)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return 0;
		}
		string text = ((string)P_0).Trim();
		if (string.Equals(text, (string)P_1, StringComparison.OrdinalIgnoreCase))
		{
			return 3;
		}
		string text2 = NormalizeFontNameWords(text);
		if (!string.Equals(text, (string)P_2, StringComparison.OrdinalIgnoreCase) && !string.Equals(text2, (string)P_1, StringComparison.OrdinalIgnoreCase) && !string.Equals(text2, (string)P_2, StringComparison.OrdinalIgnoreCase))
		{
			string text3 = NormalizeCompactFontName(text2);
			if (!string.IsNullOrEmpty(text3) && string.Equals(text3, (string)P_3, StringComparison.OrdinalIgnoreCase))
			{
				return 1;
			}
			return 0;
		}
		return 2;
	}

	private static int GetBestFontAliasMatchScore(object P_0, object P_1, object P_2, params string[] candidates)
	{
		int num = 0;
		for (int i = 0; i < candidates.Length; i++)
		{
			int num2 = ScoreFontNameMatch(candidates[i], P_0, P_1, P_2);
			if (num2 > num)
			{
				num = num2;
				if (num >= 3)
				{
					break;
				}
			}
		}
		return num;
	}

	private static string[] CollectFontNameAliases(object P_0, object P_1, object P_2)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if ((UnityEngine.Object)P_1 != null && !string.IsNullOrWhiteSpace(((TrueTypeFontImporter)P_1).fontTTFName))
		{
			hashSet.Add(((TrueTypeFontImporter)P_1).fontTTFName.Trim());
		}
		if ((UnityEngine.Object)P_2 != null && !string.IsNullOrWhiteSpace(((UnityEngine.Object)P_2).name))
		{
			hashSet.Add(((UnityEngine.Object)P_2).name.Trim());
		}
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension((string)P_0);
		if (!string.IsNullOrWhiteSpace(fileNameWithoutExtension))
		{
			hashSet.Add(fileNameWithoutExtension.Trim());
		}
		foreach (string item in ReadFontNamesFromMeta(P_0))
		{
			hashSet.Add(item);
		}
		return hashSet.ToArray();
	}

	private static IEnumerable<string> ReadFontNamesFromMeta(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			yield break;
		}
		string text = Directory.GetParent(Application.dataPath)?.FullName;
		if (string.IsNullOrWhiteSpace(text))
		{
			yield break;
		}
		string path = ((string)P_0 + ".meta").Replace('/', Path.DirectorySeparatorChar);
		string path2 = Path.Combine(text, path);
		if (!File.Exists(path2))
		{
			yield break;
		}
		bool readingFontNames = false;
		foreach (string item in File.ReadLines(path2))
		{
			string text2 = item.Trim();
			if (!readingFontNames)
			{
				if (string.Equals(text2, "fontNames:", StringComparison.Ordinal))
				{
					readingFontNames = true;
				}
				else if (string.Equals(text2, "fontNames: []", StringComparison.Ordinal))
				{
					yield break;
				}
			}
			else if (text2.Length != 0)
			{
				if (!text2.StartsWith("-", StringComparison.Ordinal))
				{
					yield break;
				}
				string text3 = text2.Substring(1).Trim();
				if (text3.Length >= 2 && ((text3.StartsWith("\"", StringComparison.Ordinal) && text3.EndsWith("\"", StringComparison.Ordinal)) | (text3.StartsWith("'", StringComparison.Ordinal) && text3.EndsWith("'", StringComparison.Ordinal))))
				{
					text3 = text3.Substring(1, text3.Length - 2).Trim();
				}
				if (!string.IsNullOrWhiteSpace(text3))
				{
					yield return text3;
				}
			}
		}
	}

	internal static TMP_FontAsset FindOrCreateTmpFontAsset(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return null;
		}
		string text = NormalizeFontNameWords(P_0);
		string text2 = NormalizeCompactFontName(P_0);
		Font font = FindUnityFontByPsdName(P_0);
		string text3 = null;
		if (font != null)
		{
			string assetPath = AssetDatabase.GetAssetPath(font);
			if (!string.IsNullOrWhiteSpace(assetPath))
			{
				text3 = AssetDatabase.AssetPathToGUID(assetPath);
			}
		}
		TMP_FontAsset tMP_FontAsset = null;
		int num = 0;
		string[] array = AssetDatabase.FindAssets("t:TMP_FontAsset");
		int num2 = 0;
		TMP_FontAsset tMP_FontAsset2;
		while (true)
		{
			if (num2 < array.Length)
			{
				tMP_FontAsset2 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(array[num2]));
				if (!(tMP_FontAsset2 == null))
				{
					if (font != null && TmpFontUsesSourceFont(tMP_FontAsset2, font, text3))
					{
						break;
					}
					int num3 = GetBestFontAliasMatchScore(P_0, text, text2, tMP_FontAsset2.faceInfo.familyName, tMP_FontAsset2.name, (!(tMP_FontAsset2.sourceFontFile != null)) ? null : tMP_FontAsset2.sourceFontFile.name);
					if (num3 > num)
					{
						tMP_FontAsset = tMP_FontAsset2;
						num = num3;
					}
				}
				num2++;
				continue;
			}
			if (!(tMP_FontAsset != null))
			{
				if (font != null)
				{
					return CreateDynamicTmpFontAsset(font);
				}
				return null;
			}
			EnsureTmpFontMaterial(tMP_FontAsset);
			return tMP_FontAsset;
		}
		EnsureTmpFontMaterial(tMP_FontAsset2);
		return tMP_FontAsset2;
	}

	private static bool TmpFontUsesSourceFont(object P_0, object P_1, object P_2)
	{
		if (!((UnityEngine.Object)P_0 == null) && !((UnityEngine.Object)P_1 == null))
		{
			if (((TMP_FontAsset)P_0).sourceFontFile == (UnityEngine.Object)P_1)
			{
				return true;
			}
			if (!string.IsNullOrWhiteSpace((string)P_2))
			{
				if (string.Equals(((TMP_FontAsset)P_0).creationSettings.sourceFontFileGUID, (string)P_2, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
				if (((TMP_FontAsset)P_0).sourceFontFile != null)
				{
					string assetPath = AssetDatabase.GetAssetPath(((TMP_FontAsset)P_0).sourceFontFile);
					if (!string.IsNullOrWhiteSpace(assetPath) && string.Equals(AssetDatabase.AssetPathToGUID(assetPath), (string)P_2, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			return false;
		}
		return false;
	}

	private static TMP_FontAsset CreateDynamicTmpFontAsset(object P_0)
	{
		if (!((UnityEngine.Object)P_0 == null))
		{
			if (TMP_Settings.instance == null)
			{
				Debug.LogWarning("Unable to create TMP font asset because TMP Essential Resources are missing.");
				return null;
			}
			ShaderUtilities.GetShaderPropertyIDs();
			string assetPath = AssetDatabase.GetAssetPath((UnityEngine.Object)P_0);
			if (!string.IsNullOrWhiteSpace(assetPath))
			{
				TrueTypeFontImporter trueTypeFontImporter = AssetImporter.GetAtPath(assetPath) as TrueTypeFontImporter;
				if (trueTypeFontImporter != null && !trueTypeFontImporter.includeFontData)
				{
					trueTypeFontImporter.includeFontData = true;
					trueTypeFontImporter.SaveAndReimport();
					P_0 = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
					if ((UnityEngine.Object)P_0 == null)
					{
						return null;
					}
				}
				string text = AssetDatabase.AssetPathToGUID(assetPath);
				TMP_FontAsset tMP_FontAsset = FindTmpFontBySource(P_0, text);
				if (!(tMP_FontAsset != null))
				{
					string text2 = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
					if (!string.IsNullOrWhiteSpace(text2))
					{
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath);
						string text3 = AssetDatabase.GenerateUniqueAssetPath(text2 + "/" + fileNameWithoutExtension + " SDF.asset");
						TMP_FontAsset tMP_FontAsset2 = TMP_FontAsset.CreateFontAsset((Font)P_0);
						if (tMP_FontAsset2 == null)
						{
							return null;
						}
						tMP_FontAsset2.name = Path.GetFileNameWithoutExtension(text3);
						Texture2D texture2D = ((tMP_FontAsset2.atlasTextures == null || tMP_FontAsset2.atlasTextures.Length == 0) ? null : tMP_FontAsset2.atlasTextures[0]);
						if (texture2D != null)
						{
							texture2D.name = fileNameWithoutExtension + " Atlas";
						}
						Material material = tMP_FontAsset2.material;
						Material material2 = CloneTmpMaterialWithEffectsShader(material, true);
						if (material2 != null)
						{
							material2.name = fileNameWithoutExtension + " Atlas Material";
							if (texture2D != null && material2.HasProperty(ShaderUtilities.ID_MainTex))
							{
								material2.SetTexture(ShaderUtilities.ID_MainTex, texture2D);
							}
							if (material2.HasProperty(ShaderUtilities.ID_TextureWidth))
							{
								material2.SetFloat(ShaderUtilities.ID_TextureWidth, tMP_FontAsset2.atlasWidth);
							}
							if (material2.HasProperty(ShaderUtilities.ID_TextureHeight))
							{
								material2.SetFloat(ShaderUtilities.ID_TextureHeight, tMP_FontAsset2.atlasHeight);
							}
							if (material2.HasProperty(ShaderUtilities.ID_GradientScale))
							{
								material2.SetFloat(ShaderUtilities.ID_GradientScale, (float)tMP_FontAsset2.atlasPadding + 1f);
							}
							if (material2.HasProperty(ShaderUtilities.ID_WeightNormal))
							{
								material2.SetFloat(ShaderUtilities.ID_WeightNormal, tMP_FontAsset2.normalStyle);
							}
							if (material2.HasProperty(ShaderUtilities.ID_WeightBold))
							{
								material2.SetFloat(ShaderUtilities.ID_WeightBold, tMP_FontAsset2.boldStyle);
							}
							tMP_FontAsset2.material = material2;
						}
						AssetDatabase.CreateAsset(tMP_FontAsset2, text3);
						if (texture2D != null)
						{
							AssetDatabase.AddObjectToAsset(texture2D, tMP_FontAsset2);
						}
						if (tMP_FontAsset2.material != null)
						{
							string path = BuildSiblingMaterialAssetPath(text3, tMP_FontAsset2.material.name, true);
							AssetDatabase.CreateAsset(tMP_FontAsset2.material, path);
						}
						if (material != null && material != tMP_FontAsset2.material)
						{
							UnityEngine.Object.DestroyImmediate(material);
						}
						FontAssetCreationSettings creationSettings = tMP_FontAsset2.creationSettings;
						creationSettings.sourceFontFileName = ((UnityEngine.Object)P_0).name;
						creationSettings.sourceFontFileGUID = text;
						creationSettings.pointSizeSamplingMode = 0;
						creationSettings.pointSize = Mathf.RoundToInt(tMP_FontAsset2.faceInfo.pointSize);
						creationSettings.padding = tMP_FontAsset2.atlasPadding;
						creationSettings.packingMode = 0;
						creationSettings.atlasWidth = tMP_FontAsset2.atlasWidth;
						creationSettings.atlasHeight = tMP_FontAsset2.atlasHeight;
						creationSettings.characterSetSelectionMode = 7;
						creationSettings.characterSequence = string.Empty;
						creationSettings.referencedFontAssetGUID = string.Empty;
						creationSettings.referencedTextAssetGUID = string.Empty;
						creationSettings.fontStyle = 0;
						creationSettings.fontStyleModifier = 0f;
						creationSettings.renderMode = (int)tMP_FontAsset2.atlasRenderMode;
						creationSettings.includeFontFeatures = false;
						tMP_FontAsset2.creationSettings = creationSettings;
						EditorUtility.SetDirty(tMP_FontAsset2);
						if (texture2D != null)
						{
							EditorUtility.SetDirty(texture2D);
						}
						if (tMP_FontAsset2.material != null)
						{
							EditorUtility.SetDirty(tMP_FontAsset2.material);
						}
						AssetDatabase.SaveAssets();
						TMP_FontAsset tMP_FontAsset3 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(text3);
						if (tMP_FontAsset3 == null)
						{
							AssetDatabase.ImportAsset(text3, ImportAssetOptions.ForceSynchronousImport);
							tMP_FontAsset3 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(text3);
						}
						if ((object)tMP_FontAsset3 == null)
						{
							tMP_FontAsset3 = tMP_FontAsset2;
						}
						EnsureTmpFontMaterial(tMP_FontAsset3);
						return tMP_FontAsset3;
					}
					return null;
				}
				EnsureTmpFontMaterial(tMP_FontAsset);
				return tMP_FontAsset;
			}
			return null;
		}
		return null;
	}

	private static TMP_FontAsset FindTmpFontBySource(object P_0, object P_1)
	{
		if ((UnityEngine.Object)P_0 == null)
		{
			return null;
		}
		string[] array = AssetDatabase.FindAssets("t:TMP_FontAsset");
		int num = 0;
		TMP_FontAsset tMP_FontAsset;
		while (true)
		{
			if (num < array.Length)
			{
				tMP_FontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(array[num]));
				if (TmpFontUsesSourceFont(tMP_FontAsset, P_0, P_1))
				{
					break;
				}
				num++;
				continue;
			}
			return null;
		}
		return tMP_FontAsset;
	}

	private static void EnsureTmpFontCharacters(object P_0, object P_1)
	{
		if ((UnityEngine.Object)P_0 == null || string.IsNullOrEmpty((string)P_1))
		{
			return;
		}
		EnsureTmpFontMaterial(P_0);
		if (((TMP_FontAsset)P_0).atlasPopulationMode != AtlasPopulationMode.Dynamic || ((TMP_FontAsset)P_0).HasCharacters((string)P_1))
		{
			return;
		}
		((TMP_FontAsset)P_0).TryAddCharacters((string)P_1, out string _, includeFontFeatures: false);
		EnsureTmpFontMaterial(P_0);
		EditorUtility.SetDirty((UnityEngine.Object)P_0);
		if (((TMP_Asset)P_0).material != null)
		{
			EditorUtility.SetDirty(((TMP_Asset)P_0).material);
		}
		if (((TMP_FontAsset)P_0).atlasTextures != null)
		{
			for (int i = 0; i < ((TMP_FontAsset)P_0).atlasTextures.Length; i++)
			{
				if (((TMP_FontAsset)P_0).atlasTextures[i] != null)
				{
					EditorUtility.SetDirty(((TMP_FontAsset)P_0).atlasTextures[i]);
				}
			}
		}
		AssetDatabase.SaveAssets();
	}

	internal static Font FindUnityFontByPsdName(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			string text = NormalizeFontNameWords(P_0);
			string text2 = NormalizeCompactFontName(P_0);
			string[] array = AssetDatabase.FindAssets("t:font");
			Font result = null;
			int num = 0;
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string text3 = AssetDatabase.GUIDToAssetPath(array2[i]);
				TrueTypeFontImporter trueTypeFontImporter = AssetImporter.GetAtPath(text3) as TrueTypeFontImporter;
				Font font = AssetDatabase.LoadAssetAtPath<Font>(text3);
				if (font == null)
				{
					continue;
				}
				int num2 = GetBestFontAliasMatchScore(P_0, text, text2, CollectFontNameAliases(text3, trueTypeFontImporter, font));
				if (num2 > num)
				{
					result = font;
					num = num2;
					if (num >= 3)
					{
						break;
					}
				}
			}
			return result;
		}
		return null;
	}

	internal static Color GetLayerColorOrDefault(object P_0, Color P_1)
	{
		if ((UnityEngine.Object)P_0 != null && ((PsdLayerNode)P_0).TryGetLayerFillColor(out Color result))
		{
			return result;
		}
		return P_1;
	}

	internal void ExportDesignerDocumentation()
	{
		string text = EditorUtility.SaveFolderPanel("选择文档导出路径", Application.dataPath, null);
		if (string.IsNullOrWhiteSpace(text) || !Directory.Exists(text))
		{
			return;
		}
		string path = Path.Combine(text, "Psd2UGUI设计师使用文档.doc");
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("使用说明:");
		stringBuilder.AppendLine(readmeDoc);
		stringBuilder.AppendLine(Environment.NewLine + Environment.NewLine);
		stringBuilder.AppendLine("UI类型标识: 图层/组命名以'.类型'结尾");
		stringBuilder.AppendLine("UI类型标识列表:");
		UGUIParseRule[] array = rules;
		foreach (UGUIParseRule uGUIParseRule in array)
		{
			if (uGUIParseRule.UIType != GUIType.Null)
			{
				stringBuilder.AppendLine($"{uGUIParseRule.UIType}: {uGUIParseRule.Comment}");
				stringBuilder.Append("类型标识: ");
				string[] typeMatches = uGUIParseRule.TypeMatches;
				foreach (string text2 in typeMatches)
				{
					stringBuilder.Append("." + text2 + ", ");
				}
				stringBuilder.AppendLine();
				stringBuilder.AppendLine();
			}
		}
		try
		{
			File.WriteAllText(path, stringBuilder.ToString(), Encoding.UTF8);
			EditorUtility.RevealInFinder(path);
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static string GetTagCategoryLabel(object P_0)
	{
		if (!((string)P_0 == "main"))
		{
			if ((string)P_0 == "textBackend")
			{
				return "文本后端";
			}
			if ((string)P_0 == "imageType")
			{
				return "Image Type";
			}
			if (!((string)P_0 == "role"))
			{
				return (string)P_0;
			}
			return "角色标签";
		}
		return "结构标签";
	}

	private static string GetDefaultUiTagToken(GUIType P_0)
	{
		switch (P_0)
		{
		default:
			return P_0.ToString().ToLowerInvariant();
		case GUIType.Background:
			return "bg";
		case GUIType.Button_Highlight:
			return "onover";
		case GUIType.Button_Press:
			return "press";
		case GUIType.Button_Select:
			return "select";
		case GUIType.Button_Disable:
			return "disable";
		case GUIType.Button_Text:
			return "bttxt";
		case GUIType.Dropdown_Label:
			return "dpdlb";
		case GUIType.Dropdown_Arrow:
			return "dpdicon";
		case GUIType.InputField_Placeholder:
			return "placeholder";
		case GUIType.InputField_Text:
			return "ipttxt";
		case GUIType.Toggle_Checkmark:
			return "mark";
		case GUIType.Toggle_Label:
			return "tglb";
		case GUIType.Slider_Fill:
			return "fill";
		case GUIType.Slider_Handle:
			return "handle";
		case GUIType.ScrollView_Viewport:
			return "vpt";
		case GUIType.ScrollView_HorizontalBarBG:
			return "hbarbg";
		case GUIType.ScrollView_HorizontalBar:
			return "hbar";
		case GUIType.ScrollView_VerticalBarBG:
			return "vbarbg";
		case GUIType.ScrollView_VerticalBar:
			return "vbar";
		case GUIType.Image:
			return "img";
		case GUIType.RawImage:
			return "rimg";
		case GUIType.Slider:
			return "sld";
		case GUIType.ScrollView:
			return "sv";
		case GUIType.Mask:
			return "msk";
		case GUIType.FillColor:
			return "col";
		case GUIType.Text:
		case GUIType.TMPText:
			return "txt";
		case GUIType.Button:
		case GUIType.TMPButton:
			return "bt";
		case GUIType.Dropdown:
		case GUIType.TMPDropdown:
			return "dpd";
		case GUIType.InputField:
		case GUIType.TMPInputField:
			return "ipt";
		case GUIType.Toggle:
		case GUIType.TMPToggle:
			return "tg";
		}
	}

	private string GetCanonicalUiTagToken(GUIType P_0)
	{
		UGUIParseRule uGUIParseRule = GetRuleForUiType(P_0);
		if (uGUIParseRule?.TypeMatches != null)
		{
			for (int i = 0; i < uGUIParseRule.TypeMatches.Length; i++)
			{
				string text = uGUIParseRule.TypeMatches[i];
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text.Trim().TrimStart('.').ToLowerInvariant();
				}
			}
		}
		return GetDefaultUiTagToken(P_0);
	}

	private static bool TryGetUiTagCategory(GUIType P_0, out string P_1)
	{
		switch (P_0)
		{
		case GUIType.Image:
		case GUIType.RawImage:
		case GUIType.Text:
		case GUIType.Button:
		case GUIType.Dropdown:
		case GUIType.InputField:
		case GUIType.Toggle:
		case GUIType.Slider:
		case GUIType.ScrollView:
		case GUIType.Mask:
		case GUIType.FillColor:
			P_1 = "main";
			return true;
		default:
			P_1 = null;
			return false;
		case GUIType.Background:
		case GUIType.Button_Highlight:
		case GUIType.Button_Press:
		case GUIType.Button_Select:
		case GUIType.Button_Disable:
		case GUIType.Button_Text:
		case GUIType.Dropdown_Label:
		case GUIType.Dropdown_Arrow:
		case GUIType.InputField_Placeholder:
		case GUIType.InputField_Text:
		case GUIType.Toggle_Checkmark:
		case GUIType.Toggle_Label:
		case GUIType.Slider_Fill:
		case GUIType.Slider_Handle:
		case GUIType.ScrollView_Viewport:
		case GUIType.ScrollView_HorizontalBarBG:
		case GUIType.ScrollView_HorizontalBar:
		case GUIType.ScrollView_VerticalBarBG:
		case GUIType.ScrollView_VerticalBar:
			P_1 = "role";
			return true;
		}
	}

	private static bool IsRasterizedMainUiType(GUIType P_0)
	{
		if (P_0 != GUIType.Image)
		{
			return P_0 == GUIType.RawImage;
		}
		return true;
	}

	private static bool IsRasterizedUiRole(GUIType P_0)
	{
		switch (P_0)
		{
		default:
			return false;
		case GUIType.Background:
		case GUIType.Button_Highlight:
		case GUIType.Button_Press:
		case GUIType.Button_Select:
		case GUIType.Button_Disable:
		case GUIType.Dropdown_Arrow:
		case GUIType.Toggle_Checkmark:
		case GUIType.Slider_Fill:
		case GUIType.Slider_Handle:
		case GUIType.ScrollView_HorizontalBarBG:
		case GUIType.ScrollView_HorizontalBar:
		case GUIType.ScrollView_VerticalBarBG:
		case GUIType.ScrollView_VerticalBar:
			return true;
		}
	}

	private static GUIType GetUguiTypeForTagAlias(GUIType P_0)
	{
		return P_0 switch
		{
			GUIType.TMPText => GUIType.Text, 
			GUIType.TMPButton => GUIType.Button, 
			GUIType.TMPDropdown => GUIType.Dropdown, 
			GUIType.TMPInputField => GUIType.InputField, 
			GUIType.TMPToggle => GUIType.Toggle, 
			_ => P_0, 
		};
	}

	private static string EscapeJavaScriptString(object P_0)
	{
		if (string.IsNullOrEmpty((string)P_0))
		{
			return string.Empty;
		}
		return ((string)P_0).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r")
			.Replace("\n", "\\n");
	}

	private static string NormalizeTagDescription(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			return ((string)P_0).Replace("\\r", " ").Replace("\\n", " ").Replace('\r', ' ')
				.Replace('\n', ' ')
				.Trim();
		}
		return string.Empty;
	}

	private static void AddPhotoshopTagMenuItem(Dictionary<string, List<PhotoshopTagMenuItem>> P_0, object P_1, object P_2, object P_3)
	{
		_003C_003Ec__DisplayClass167_0 CS_0024_003C_003E8__locals6 = new _003C_003Ec__DisplayClass167_0();
		CS_0024_003C_003E8__locals6.RequestedTagId = (string)P_2;
		if (!string.IsNullOrWhiteSpace((string)P_1) && !string.IsNullOrWhiteSpace(CS_0024_003C_003E8__locals6.RequestedTagId))
		{
			if (!P_0.TryGetValue((string)P_1, out var value))
			{
				value = new List<PhotoshopTagMenuItem>();
				P_0.Add((string)P_1, value);
			}
			if (!value.Any((PhotoshopTagMenuItem item) => string.Equals(item.Id, CS_0024_003C_003E8__locals6.RequestedTagId, StringComparison.OrdinalIgnoreCase)))
			{
				value.Add(new PhotoshopTagMenuItem
				{
					Id = CS_0024_003C_003E8__locals6.RequestedTagId,
					Suffix = "." + CS_0024_003C_003E8__locals6.RequestedTagId,
					Label = (string.IsNullOrWhiteSpace((string)P_3) ? CS_0024_003C_003E8__locals6.RequestedTagId : NormalizeTagDescription(P_3))
				});
			}
		}
	}

	private static void SetPhotoshopTagAlias(Dictionary<string, PhotoshopTagAlias> P_0, object P_1, string P_2 = null, string P_3 = null, string P_4 = null, string P_5 = null)
	{
		if (!string.IsNullOrWhiteSpace((string)P_1))
		{
			P_1 = ((string)P_1).Trim().TrimStart('.').ToLowerInvariant();
			if (!P_0.TryGetValue((string)P_1, out var value))
			{
				value = new PhotoshopTagAlias();
				P_0.Add((string)P_1, value);
			}
			if (!string.IsNullOrWhiteSpace(P_2))
			{
				value.MainTag = P_2;
			}
			if (!string.IsNullOrWhiteSpace(P_3))
			{
				value.RoleTag = P_3;
			}
			if (!string.IsNullOrWhiteSpace(P_4))
			{
				value.ImageTypeTag = P_4;
			}
			if (!string.IsNullOrWhiteSpace(P_5))
			{
				value.TextBackendTag = P_5;
			}
		}
	}

	private void AddRuleToPhotoshopTagConfig(UGUIParseRule P_0, Dictionary<string, List<PhotoshopTagMenuItem>> P_1, Dictionary<string, PhotoshopTagAlias> P_2, PhotoshopRasterizationTags P_3)
	{
		if (P_0 == null || P_0.UIType == GUIType.Null || P_0.TypeMatches == null || P_0.TypeMatches.Length == 0)
		{
			return;
		}
		string text = GetCanonicalUiTagToken(P_0.UIType);
		string text2 = (string.IsNullOrWhiteSpace(P_0.UITypeDesc) ? P_0.UIType.ToString() : $"{P_0.UIType} {P_0.UITypeDesc}");
		GUIType uIType = P_0.UIType;
		if ((uint)(uIType - 12) <= 4u)
		{
			GUIType gUIType = GetUguiTypeForTagAlias(P_0.UIType);
			string text3 = GetCanonicalUiTagToken(gUIType);
			for (int i = 0; i < P_0.TypeMatches.Length; i++)
			{
				SetPhotoshopTagAlias(P_2, P_0.TypeMatches[i], text3, null, null, "tmp");
			}
		}
		else
		{
			if (!TryGetUiTagCategory(P_0.UIType, out var text4))
			{
				return;
			}
			AddPhotoshopTagMenuItem(P_1, text4, text, text2);
			for (int j = 0; j < P_0.TypeMatches.Length; j++)
			{
				string text5 = P_0.TypeMatches[j];
				if (!string.IsNullOrWhiteSpace(text5))
				{
					if (string.Equals(text4, "main", StringComparison.OrdinalIgnoreCase))
					{
						SetPhotoshopTagAlias(P_2, text5, text);
					}
					else
					{
						SetPhotoshopTagAlias(P_2, text5, null, text);
					}
				}
			}
			if (IsRasterizedMainUiType(P_0.UIType))
			{
				P_3.MainTags.Add(text);
			}
			else if (IsRasterizedUiRole(P_0.UIType))
			{
				P_3.RoleTags.Add(text);
			}
		}
	}

	private string BuildPhotoshopTagConfiguration()
	{
		Dictionary<string, List<PhotoshopTagMenuItem>> dictionary = new Dictionary<string, List<PhotoshopTagMenuItem>>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, PhotoshopTagAlias> dictionary2 = new Dictionary<string, PhotoshopTagAlias>(StringComparer.OrdinalIgnoreCase);
		PhotoshopRasterizationTags gMNtGhUqwMQ22QPeWtr = new PhotoshopRasterizationTags();
		if (rules != null)
		{
			for (int i = 0; i < rules.Length; i++)
			{
				AddRuleToPhotoshopTagConfig(rules[i], dictionary, dictionary2, gMNtGhUqwMQ22QPeWtr);
			}
		}
		AddPhotoshopTagMenuItem(dictionary, "textBackend", "tmp", "TMP文本后端");
		AddPhotoshopTagMenuItem(dictionary, "textBackend", "ugui", "原生文本后端");
		SetPhotoshopTagAlias(dictionary2, "tmp", null, null, null, "tmp");
		SetPhotoshopTagAlias(dictionary2, "ugui", null, null, null, "ugui");
		AddPhotoshopTagMenuItem(dictionary, "imageType", "simple", "普通");
		AddPhotoshopTagMenuItem(dictionary, "imageType", "sliced", "九宫格");
		AddPhotoshopTagMenuItem(dictionary, "imageType", "tiled", "平铺");
		AddPhotoshopTagMenuItem(dictionary, "imageType", "filled", "填充");
		SetPhotoshopTagAlias(dictionary2, "simple", null, null, "simple");
		SetPhotoshopTagAlias(dictionary2, "sliced", null, null, "sliced");
		SetPhotoshopTagAlias(dictionary2, "tiled", null, null, "tiled");
		SetPhotoshopTagAlias(dictionary2, "filled", null, null, "filled");
		string[] array = new string[4] { "main", "textBackend", "imageType", "role" };
		_003C_003Ec__DisplayClass170_0 _003C_003Ec__DisplayClass170_ = default(_003C_003Ec__DisplayClass170_0);
		_003C_003Ec__DisplayClass170_.ScriptBuilder = new StringBuilder();
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("var TAG_CONFIG = {");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    canonicalOrder: [\"main\", \"textBackend\", \"imageType\", \"role\"],");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    reuseMarkers: [");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("        { \"id\": \"ref\", \"prefix\": \"ref \", \"label\": \"ref 复用共享图片资源\" },");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("        { \"id\": \"refp\", \"prefix\": \"refp \", \"label\": \"refp 复用共享预制体\" }");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    ],");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    familyLabels: {");
		for (int j = 0; j < array.Length; j++)
		{
			string text = array[j];
			_003C_003Ec__DisplayClass170_.ScriptBuilder.Append("        \"").Append(text).Append("\": \"")
				.Append(EscapeJavaScriptString(GetTagCategoryLabel(text)))
				.Append("\"");
			_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine((j < array.Length - 1) ? "," : string.Empty);
		}
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    },");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    families: {");
		for (int k = 0; k < array.Length; k++)
		{
			string text2 = array[k];
			dictionary.TryGetValue(text2, out var value);
			value = value ?? new List<PhotoshopTagMenuItem>();
			_003C_003Ec__DisplayClass170_.ScriptBuilder.Append("        \"").Append(text2).Append("\": [")
				.AppendLine();
			for (int l = 0; l < value.Count; l++)
			{
				PhotoshopTagMenuItem miU0feUrlsQCeUohoNX = value[l];
				_003C_003Ec__DisplayClass170_.ScriptBuilder.Append("            { \"id\": \"").Append(EscapeJavaScriptString(miU0feUrlsQCeUohoNX.Id)).Append("\", \"suffix\": \"")
					.Append(EscapeJavaScriptString(miU0feUrlsQCeUohoNX.Suffix))
					.Append("\", \"label\": \"")
					.Append(EscapeJavaScriptString(miU0feUrlsQCeUohoNX.Label))
					.Append("\" }");
				_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine((l >= value.Count - 1) ? string.Empty : ",");
			}
			_003C_003Ec__DisplayClass170_.ScriptBuilder.Append("        ]");
			_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine((k < array.Length - 1) ? "," : string.Empty);
		}
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    },");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    rasterizeAsImage: {");
		AppendJavaScriptTagSet("main", gMNtGhUqwMQ22QPeWtr.MainTags, true, ref _003C_003Ec__DisplayClass170_);
		AppendJavaScriptTagSet("role", gMNtGhUqwMQ22QPeWtr.RoleTags, false, ref _003C_003Ec__DisplayClass170_);
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    },");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    aliasMap: {");
		string[] array2 = dictionary2.Keys.OrderBy((string alias) => alias, StringComparer.OrdinalIgnoreCase).ToArray();
		_003C_003Ec__DisplayClass170_1 _003C_003Ec__DisplayClass170_2 = default(_003C_003Ec__DisplayClass170_1);
		for (int num = 0; num < array2.Length; num++)
		{
			string text3 = array2[num];
			PhotoshopTagAlias PhotoshopTagAlias = dictionary2[text3];
			_003C_003Ec__DisplayClass170_.ScriptBuilder.Append("        \"").Append(EscapeJavaScriptString(text3)).Append("\": {");
			_003C_003Ec__DisplayClass170_2.HasWrittenAliasProperty = false;
			AppendJavaScriptAliasProperty("main", PhotoshopTagAlias.MainTag, ref _003C_003Ec__DisplayClass170_, ref _003C_003Ec__DisplayClass170_2);
			AppendJavaScriptAliasProperty("role", PhotoshopTagAlias.RoleTag, ref _003C_003Ec__DisplayClass170_, ref _003C_003Ec__DisplayClass170_2);
			AppendJavaScriptAliasProperty("imageType", PhotoshopTagAlias.ImageTypeTag, ref _003C_003Ec__DisplayClass170_, ref _003C_003Ec__DisplayClass170_2);
			AppendJavaScriptAliasProperty("textBackend", PhotoshopTagAlias.TextBackendTag, ref _003C_003Ec__DisplayClass170_, ref _003C_003Ec__DisplayClass170_2);
			_003C_003Ec__DisplayClass170_.ScriptBuilder.Append(" }");
			_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine((num >= array2.Length - 1) ? string.Empty : ",");
		}
		_003C_003Ec__DisplayClass170_.ScriptBuilder.AppendLine("    }");
		_003C_003Ec__DisplayClass170_.ScriptBuilder.Append("};");
		return _003C_003Ec__DisplayClass170_.ScriptBuilder.ToString();
	}

	private string BuildPhotoshopReuseDefaults()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("var REUSE_DEFAULT_SETTINGS = {");
		stringBuilder.Append("    imageRoot: \"").Append(EscapeJavaScriptString(ResolvePhotoshopResourceDirectory(sharedAssetsOutput))).AppendLine("\",");
		stringBuilder.Append("    prefabRoot: \"").Append(EscapeJavaScriptString(ResolvePhotoshopResourceDirectory(sharedPrefabOutput))).AppendLine("\"");
		stringBuilder.Append("};");
		return stringBuilder.ToString();
	}

	private static string ResolvePhotoshopResourceDirectory(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		string text = ((string)P_0).Trim();
		string text2;
		if (Path.IsPathRooted(text))
		{
			text2 = text;
		}
		else
		{
			string text3 = Directory.GetParent(Application.dataPath)?.FullName;
			if (string.IsNullOrWhiteSpace(text3))
			{
				return string.Empty;
			}
			text2 = Path.GetFullPath(Path.Combine(text3, text));
		}
		return text2.Replace('\\', '/');
	}

	private static bool ReplacePhotoshopScriptConfigBlock(object P_0, object P_1, object P_2, object P_3, out string P_4)
	{
		P_4 = null;
		if (!File.Exists((string)P_0))
		{
			P_4 = "PS脚本文件不存在：\n" + (string)P_0;
			return false;
		}
		string input = File.ReadAllText((string)P_0, Encoding.UTF8);
		Regex regex = new Regex((string)P_1, RegexOptions.Multiline);
		if (regex.IsMatch(input))
		{
			string contents = regex.Replace(input, (string)P_2, 1);
			File.WriteAllText((string)P_0, contents, Encoding.UTF8);
			AssetDatabase.ImportAsset((string)P_0);
			return true;
		}
		P_4 = "PS脚本中未找到 " + (string)P_3 + "：\n" + (string)P_0;
		return false;
	}

	private static bool UpdatePhotoshopTagConfigBlock(object P_0, object P_1, out string P_2)
	{
		return ReplacePhotoshopScriptConfigBlock(P_0, "var\\s+TAG_CONFIG\\s*=\\s*\\{[\\s\\S]*?\\};", P_1, "TAG_CONFIG", out P_2);
	}

	private static bool UpdatePhotoshopReuseDefaultsBlock(object P_0, object P_1, out string P_2)
	{
		return ReplacePhotoshopScriptConfigBlock(P_0, "var\\s+REUSE_DEFAULT_SETTINGS\\s*=\\s*\\{[\\s\\S]*?\\};", P_1, "REUSE_DEFAULT_SETTINGS", out P_2);
	}

	private static string GetAbsoluteLocalScriptPath(object P_0)
	{
		string text = Directory.GetParent(Application.dataPath)?.FullName;
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		return Path.GetFullPath(Path.Combine(text, (string)P_0));
	}

	private static string NormalizePhotoshopInstallDirectory(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			string text = Environment.ExpandEnvironmentVariables(((string)P_0).Trim().Trim('"'));
			if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
			{
				text = Path.GetDirectoryName(text);
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				try
				{
					return Path.GetFullPath(text);
				}
				catch
				{
					return null;
				}
			}
			return null;
		}
		return null;
	}

	private static void AddExistingPhotoshopDirectory(HashSet<string> installationDirectories, object pathValue)
	{
		string text = NormalizePhotoshopInstallDirectory(pathValue);
		if (!string.IsNullOrWhiteSpace(text) && Directory.Exists(text))
		{
			installationDirectories.Add(text);
		}
	}

	private static Type FindLoadedRuntimeType(object typeName)
	{
		Type type = Type.GetType((string)typeName);
		if (!(type != null))
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				type = assemblies[i].GetType((string)typeName, throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}
		return type;
	}

	private static object ParseRegistryEnumValue(Type enumType, object enumName)
	{
		if (enumType == null)
		{
			return null;
		}
		return Enum.Parse(enumType, (string)enumName);
	}

	private static object InvokeReflectedRegistryMethod(object targetOrType, object methodName, object parameterTypes, params object[] args)
	{
		if (targetOrType == null)
		{
			return null;
		}
		Type type = (targetOrType as Type) ?? targetOrType.GetType();
		MethodInfo method;
		if (parameterTypes == null)
		{
			method = type.GetMethod((string)methodName);
			if ((object)method != null)
			{
				goto IL_0033;
			}
		}
		else
		{
			method = type.GetMethod((string)methodName, (Type[])parameterTypes);
			if ((object)method != null)
			{
				goto IL_0033;
			}
		}
		return null;
		IL_0033:
		return method.Invoke((!(targetOrType is Type)) ? targetOrType : null, args);
	}

	private static string ReadRegistryStringValue(object registryKey, object valueName)
	{
		return InvokeReflectedRegistryMethod(registryKey, "GetValue", new Type[1] { typeof(string) }, valueName) as string;
	}

	private static object OpenRegistrySubKey(object registryKey, object subKeyName)
	{
		return InvokeReflectedRegistryMethod(registryKey, "OpenSubKey", new Type[1] { typeof(string) }, subKeyName);
	}

	private static string[] GetRegistrySubKeyNames(object registryKey)
	{
		return (InvokeReflectedRegistryMethod(registryKey, "GetSubKeyNames", Type.EmptyTypes) as string[]) ?? Array.Empty<string>();
	}

	private static object OpenRegistryBaseKey(object registryHiveName, object registryViewName)
	{
		Type type = FindLoadedRuntimeType("Microsoft.Win32.RegistryKey");
		Type type2 = FindLoadedRuntimeType("Microsoft.Win32.RegistryHive");
		Type type3 = FindLoadedRuntimeType("Microsoft.Win32.RegistryView");
		if (!(type == null) && !(type2 == null) && !(type3 == null))
		{
			object obj = ParseRegistryEnumValue(type2, registryHiveName);
			object obj2 = ParseRegistryEnumValue(type3, registryViewName);
			if (obj != null && obj2 != null)
			{
				return InvokeReflectedRegistryMethod(type, "OpenBaseKey", new Type[2] { type2, type3 }, obj, obj2);
			}
			return null;
		}
		return null;
	}

	private static void CollectPhotoshopRegistryPaths(object registryKey, HashSet<string> installationDirectories)
	{
		if (registryKey != null)
		{
			AddExistingPhotoshopDirectory(installationDirectories, ReadRegistryStringValue(registryKey, "ApplicationPath"));
			AddExistingPhotoshopDirectory(installationDirectories, ReadRegistryStringValue(registryKey, "InstallPath"));
			AddExistingPhotoshopDirectory(installationDirectories, ReadRegistryStringValue(registryKey, "Path"));
			AddExistingPhotoshopDirectory(installationDirectories, ReadRegistryStringValue(registryKey, null));
		}
	}

	private static void FindPhotoshopDirectoriesFromAdobeRegistry(object registryHiveName, object registryViewName, HashSet<string> installationDirectories)
	{
		object obj = OpenRegistryBaseKey(registryHiveName, registryViewName);
		if (obj == null)
		{
			return;
		}
		using (obj as IDisposable)
		{
			object obj2 = OpenRegistrySubKey(obj, "SOFTWARE\\Adobe\\Photoshop");
			if (obj2 == null)
			{
				return;
			}
			using (obj2 as IDisposable)
			{
				string[] array = GetRegistrySubKeyNames(obj2);
				for (int i = 0; i < array.Length; i++)
				{
					object obj3 = OpenRegistrySubKey(obj2, array[i]);
					using (obj3 as IDisposable)
					{
						CollectPhotoshopRegistryPaths(obj3, installationDirectories);
						object obj4 = ((obj3 == null) ? null : OpenRegistrySubKey(obj3, "ApplicationPath"));
						using (obj4 as IDisposable)
						{
							CollectPhotoshopRegistryPaths(obj4, installationDirectories);
						}
					}
				}
			}
		}
	}

	private static void FindPhotoshopDirectoriesFromUninstallRegistry(object registryHiveName, object registryViewName, HashSet<string> installationDirectories)
	{
		object obj = OpenRegistryBaseKey(registryHiveName, registryViewName);
		if (obj == null)
		{
			return;
		}
		using (obj as IDisposable)
		{
			object obj2 = OpenRegistrySubKey(obj, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
			if (obj2 == null)
			{
				return;
			}
			using (obj2 as IDisposable)
			{
				string[] array = GetRegistrySubKeyNames(obj2);
				for (int i = 0; i < array.Length; i++)
				{
					object obj3 = OpenRegistrySubKey(obj2, array[i]);
					using (obj3 as IDisposable)
					{
						string text = ReadRegistryStringValue(obj3, "DisplayName");
						if (!string.IsNullOrWhiteSpace(text) && text.IndexOf("Adobe Photoshop", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							AddExistingPhotoshopDirectory(installationDirectories, ReadRegistryStringValue(obj3, "InstallLocation"));
							AddExistingPhotoshopDirectory(installationDirectories, ReadRegistryStringValue(obj3, "DisplayIcon"));
						}
					}
				}
			}
		}
	}

	private static string[] FindPhotoshopScriptDirectories()
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] array = new string[2] { "LocalMachine", "CurrentUser" };
		string[] array2 = new string[2] { "Registry64", "Registry32" };
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < array2.Length; j++)
			{
				try
				{
					FindPhotoshopDirectoriesFromAdobeRegistry(array[i], array2[j], hashSet);
				}
				catch
				{
				}
				try
				{
					FindPhotoshopDirectoriesFromUninstallRegistry(array[i], array2[j], hashSet);
				}
				catch
				{
				}
			}
		}
		return hashSet.Select((string path) => Path.Combine(path, "Presets", "Scripts")).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string path) => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static void DeployPhotoshopScripts(out List<string> P_0, out List<string> P_1)
	{
		P_0 = new List<string>();
		P_1 = new List<string>();
		string[] array = FindPhotoshopScriptDirectories();
		if (array.Length != 0)
		{
			string[] array2 = new string[2]
			{
				GetAbsoluteLocalScriptPath("Assets/Plugins/PSD2UIForm/PSScript/PSD2UGUI-LayerTagMenu.jsx"),
				GetAbsoluteLocalScriptPath("Assets/Plugins/PSD2UIForm/PSScript/PSD2UIForm-导出PSD.jsx")
			};
			for (int i = 0; i < array2.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(array2[i]) || !File.Exists(array2[i]))
				{
					P_1.Add("本地脚本不存在，无法自动部署：\n" + (array2[i] ?? "(null)"));
					return;
				}
			}
			foreach (string text in array)
			{
				try
				{
					Directory.CreateDirectory(text);
					foreach (string text2 in array2)
					{
						string destFileName = Path.Combine(text, Path.GetFileName(text2));
						File.Copy(text2, destFileName, overwrite: true);
					}
					P_0.Add(text);
				}
				catch (Exception ex)
				{
					P_1.Add("覆盖 Photoshop 脚本目录失败：\n" + text + "\n" + ex.Message);
				}
			}
		}
		else
		{
			P_1.Add("未找到 Photoshop 安装目录，已只更新工程内 jsx 文件。");
		}
	}

	internal void ExportAndDeployPhotoshopScriptConfig()
	{
		if (rules == null || rules.Length == 0)
		{
			return;
		}
		string text = BuildPhotoshopTagConfiguration();
		string text2 = BuildPhotoshopReuseDefaults();
		List<string> list = new List<string>();
		string[] array = new string[2] { "Assets/Plugins/PSD2UIForm/PSScript/PSD2UGUI-LayerTagMenu.jsx", "Assets/Plugins/PSD2UIForm/PSScript/PSD2UIForm-导出PSD.jsx" };
		for (int i = 0; i < array.Length; i++)
		{
			if (!UpdatePhotoshopTagConfigBlock(array[i], text, out var item))
			{
				list.Add(item);
			}
		}
		if (!UpdatePhotoshopReuseDefaultsBlock("Assets/Plugins/PSD2UIForm/PSScript/PSD2UGUI-LayerTagMenu.jsx", text2, out var item2))
		{
			list.Add(item2);
		}
		if (list.Count > 0)
		{
			EditorUtility.DisplayDialog("导出PS脚本工具 失败", string.Join("\n\n", list), "确定");
			return;
		}
		DeployPhotoshopScripts(out var list2, out var list3);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("工程内脚本已更新：");
		for (int j = 0; j < array.Length; j++)
		{
			stringBuilder.AppendLine(array[j]);
		}
		if (list2.Count > 0)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("已覆盖 Photoshop 脚本目录：");
			for (int k = 0; k < list2.Count; k++)
			{
				stringBuilder.AppendLine(list2[k]);
			}
		}
		if (list3.Count > 0)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("自动部署存在以下问题：");
			for (int l = 0; l < list3.Count; l++)
			{
				stringBuilder.AppendLine(list3[l]);
			}
		}
		EditorUtility.DisplayDialog((list3.Count <= 0) ? "导出PS脚本工具 成功" : "导出PS脚本工具 完成", stringBuilder.ToString(), "确定");
		Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Plugins/PSD2UIForm/PSScript/PSD2UGUI-LayerTagMenu.jsx");
	}

	public UGUIParser()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private UGUIParser(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}

	static UGUIParser()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_instance = null;
		_tmpEffectMaterialsBySignature = new Dictionary<string, Material>();
		_baseMaterialsByEffectMaterialId = new Dictionary<EntityId, Material>();
		FaceDilatePropertyId = Shader.PropertyToID("_FaceDilate");
		ShaderFlagsPropertyId = Shader.PropertyToID("_ShaderFlags");
		BevelOffsetPropertyId = Shader.PropertyToID("_BevelOffset");
		BevelWidthPropertyId = Shader.PropertyToID("_BevelWidth");
		BevelClampPropertyId = Shader.PropertyToID("_BevelClamp");
		BevelRoundnessPropertyId = Shader.PropertyToID("_BevelRoundness");
		SpecularColorPropertyId = Shader.PropertyToID("_SpecularColor");
		SpecularPowerPropertyId = Shader.PropertyToID("_SpecularPower");
		ReflectivityPropertyId = Shader.PropertyToID("_Reflectivity");
		ReflectFaceColorPropertyId = Shader.PropertyToID("_ReflectFaceColor");
		ReflectOutlineColorPropertyId = Shader.PropertyToID("_ReflectOutlineColor");
		DiffusePropertyId = Shader.PropertyToID("_Diffuse");
		AmbientPropertyId = Shader.PropertyToID("_Ambient");
	}

	[CompilerGenerated]
	private GUIType GetDefaultUiTypeForLayer(ref _003C_003Ec__DisplayClass75_0 P_0)
	{
		return P_0.SourceLayerType switch
		{
			PsdLayerType.TextLayer => defaultTextType, 
			PsdLayerType.LayerGroup => GUIType.Null, 
			PsdLayerType.FillLayer => GUIType.FillColor, 
			_ => defaultImageType, 
		};
	}

	[CompilerGenerated]
	internal static void AppendJavaScriptTagSet(object P_0, IEnumerable<string> P_1, bool P_2, ref _003C_003Ec__DisplayClass170_0 P_3)
	{
		string[] array = P_1.Where((string value) => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string value) => value, StringComparer.OrdinalIgnoreCase)
			.ToArray();
		P_3.ScriptBuilder.Append("        \"").Append((string)P_0).Append("\": {");
		if (array.Length != 0)
		{
			P_3.ScriptBuilder.AppendLine();
			for (int num = 0; num < array.Length; num++)
			{
				P_3.ScriptBuilder.Append("            \"").Append(EscapeJavaScriptString(array[num])).Append("\": true");
				P_3.ScriptBuilder.AppendLine((num < array.Length - 1) ? "," : string.Empty);
			}
			P_3.ScriptBuilder.Append("        }");
		}
		else
		{
			P_3.ScriptBuilder.Append("}");
		}
		P_3.ScriptBuilder.AppendLine(P_2 ? "," : string.Empty);
	}

	[CompilerGenerated]
	internal static void AppendJavaScriptAliasProperty(object P_0, object P_1, ref _003C_003Ec__DisplayClass170_0 P_2, ref _003C_003Ec__DisplayClass170_1 P_3)
	{
		if (!string.IsNullOrWhiteSpace((string)P_1))
		{
			if (P_3.HasWrittenAliasProperty)
			{
				P_2.ScriptBuilder.Append(", ");
			}
			P_2.ScriptBuilder.Append("\"").Append((string)P_0).Append("\": \"")
				.Append(EscapeJavaScriptString(P_1))
				.Append("\"");
			P_3.HasWrittenAliasProperty = true;
		}
	}
}
}
