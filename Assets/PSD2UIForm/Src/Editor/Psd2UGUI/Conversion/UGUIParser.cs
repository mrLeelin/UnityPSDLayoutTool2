using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using PsdLayerExtensionsNamespace;
using AssetNameSanitizerNamespace;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.TextCore;
using UnityEngine.UI;
using TextGradientColorStopNamespace;
using PsdTextStyleInfoNamespace;
using Psd2UIFormPluginPathResolverNamespace;
using UGF.EditorTools.Psd2UGUI.NineSlice;

namespace UGF.EditorTools.Psd2UGUI
{
    [CanEditMultipleObjects]
    [CreateAssetMenu(fileName = "Psd2UIFormConfig", menuName = "ScriptableObject/Psd2UIForm Config")]
    public sealed class UGUIParser : ScriptableObject
    {
        private enum LayerTagKind
        {

        }

        private sealed class ParsedLayerTag
        {
            public string RawText;

            public string CanonicalValue;

            public LayerTagKind Kind;

            public GUIType UIType;

            public Image.Type ImageType;

            private static ParsedLayerTag s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static ParsedLayerTag GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class LayerNameParseResult
        {
            public string OriginalName;

            public string BaseName;

            public bool IsAssetReference;

            public bool IsPrefabReference;

            public List<ParsedLayerTag> Tags = new List<ParsedLayerTag>();

            public HashSet<string> CanonicalTagValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<LayerTagKind, ParsedLayerTag> TagsByKind = new Dictionary<LayerTagKind, ParsedLayerTag>();

            public List<string> Warnings = new List<string>();

            public GUIType ResolvedUIType;

            public bool HasExplicitUIType;

            public bool HasImageTypeOverride;

            public Image.Type ResolvedImageType;

            public bool HasTextBackendOverride;

            public bool UseTMPTextBackend;

            public bool UseUGUITextBackend;

            internal static LayerNameParseResult s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static LayerNameParseResult GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class TagFamilyOption
        {
            public string Id;

            public string Suffix;

            public string Label;

            private static TagFamilyOption s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static TagFamilyOption GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class TagAliasMapping
        {
            public string MainTag;

            public string ExportTag;

            public string RoleTag;

            public string ImageTypeTag;

            public string TextBackendTag;

            private static TagAliasMapping s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static TagAliasMapping GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class RasterizeTagSets
        {
            public readonly HashSet<string> MainTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public readonly HashSet<string> ExportTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public readonly HashSet<string> RoleTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            private static RasterizeTagSets s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static RasterizeTagSets GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        [StructLayout(LayoutKind.Auto)]
        [CompilerGenerated]
        private struct TagConfigScriptBuilderContext
        {
            public StringBuilder Builder;
        }

        [StructLayout(LayoutKind.Auto)]
        [CompilerGenerated]
        private struct TagConfigPropertyWriteState
        {
            public bool HasWrittenProperty;
        }

        [StructLayout(LayoutKind.Auto)]
        [CompilerGenerated]
        private struct LayerTypeResolutionContext
        {
            public PsdLayerType LayerType;

            public UGUIParser Parser;
        }

        [HideInInspector]
        [SerializeField]
        private GUIType defaultTextType = GUIType.Text;

        [SerializeField]
        [HideInInspector]
        private GUIType defaultImageType = GUIType.Image;

        [SerializeField]
        [HideInInspector]
        private bool forceUseTMP;

        [SerializeField]
        private GameObject uiFormTemplate;

        [SerializeField]
        private UGUIParseRule[] rules;

        [SerializeField]
        [HideInInspector]
        private string readmeDoc = "使用说明";

        [HideInInspector]
        [SerializeField]
        private bool convertZh2En = true;

        [HideInInspector]
        [SerializeField]
        private string sharedAssetsOutput = "Assets/SharedUIAssets";

        [HideInInspector]
        [SerializeField]
        private string sharedPrefabOutput = "Assets/SharedPrefab";

        [HideInInspector]
        [SerializeField]
        private AiProviderConfig aiProviderConfig = new AiProviderConfig();

        private static UGUIParser mInstance = null;

        private static readonly Dictionary<string, Material> s_TmpEffectMaterialCache = new Dictionary<string, Material>();

        private static readonly Dictionary<int, Material> s_BaseMaterialByEffectMaterialId = new Dictionary<int, Material>();

        private static readonly int s_FaceDilatePropertyId = Shader.PropertyToID("_FaceDilate");

        private static readonly int s_ShaderFlagsPropertyId = Shader.PropertyToID("_ShaderFlags");

        private static readonly int s_BevelOffsetPropertyId = Shader.PropertyToID("_BevelOffset");

        private static readonly int s_BevelWidthPropertyId = Shader.PropertyToID("_BevelWidth");

        private static readonly int s_BevelClampPropertyId = Shader.PropertyToID("_BevelClamp");

        private static readonly int s_BevelRoundnessPropertyId = Shader.PropertyToID("_BevelRoundness");

        private static readonly int s_SpecularColorPropertyId = Shader.PropertyToID("_SpecularColor");

        private static readonly int s_SpecularPowerPropertyId = Shader.PropertyToID("_SpecularPower");

        private static readonly int s_ReflectivityPropertyId = Shader.PropertyToID("_Reflectivity");

        private static readonly int s_ReflectFaceColorPropertyId = Shader.PropertyToID("_ReflectFaceColor");

        private static readonly int s_ReflectOutlineColorPropertyId = Shader.PropertyToID("_ReflectOutlineColor");

        private static readonly int s_DiffusePropertyId = Shader.PropertyToID("_Diffuse");

        private static readonly int s_AmbientPropertyId = Shader.PropertyToID("_Ambient");

        private static UGUIParser s_UGUIParserObfuscationSentinel;

        internal static UGUIParser Instance
        {
            get
            {
                if ((Object)(object)mInstance == (Object)null)
                {
                    mInstance = AssetDatabase.LoadAssetAtPath<UGUIParser>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:UGUIParser").FirstOrDefault()));
                }
                return mInstance;
            }
        }

        [SpecialName]
        internal string GetSharedAssetsOutputDirectory()
        {
            return sharedAssetsOutput;
        }

        [SpecialName]
        internal string GetSharedPrefabOutputDirectory()
        {
            return sharedPrefabOutput;
        }

        [SpecialName]
        internal AiProviderConfig GetAiProviderConfig()
        {
            return aiProviderConfig ?? (aiProviderConfig = new AiProviderConfig());
        }

        [SpecialName]
        internal GUIType GetDefaultTextType()
        {
            return ApplyForcedTMPType(defaultTextType);
        }

        [SpecialName]
        internal GUIType GetDefaultImageType()
        {
            return defaultImageType;
        }

        [SpecialName]
        internal GameObject GetUIFormTemplate()
        {
            return uiFormTemplate;
        }

        [SpecialName]
        internal bool IsChineseNameConversionEnabled()
        {
            return convertZh2En;
        }

        [SpecialName]
        internal bool IsForceTMPEnabled()
        {
            return forceUseTMP;
        }

        private static int GetObjectInstanceId(object value)
        {
            return ((Object)value).GetInstanceID();
        }

        private static string GetObjectInstanceIdKey(object value)
        {
            if ((Object)value != (Object)null)
            {
                return GetObjectInstanceId(value).ToString();
            }
            return "0";
        }

        private static void SetTMPWordWrapping(object value, bool enabled)
        {
            ((TMP_Text)value).enableWordWrapping = enabled;
        }

        internal static bool IsPrimaryUIType(GUIType uiType)
        {
            return UITypeRules.IsPrimaryUIType(uiType);
        }

        internal static bool IsAuxiliaryUIType(GUIType uiType)
        {
            return UITypeRules.IsAuxiliaryUIType(uiType);
        }

        internal static bool IsPanelOrNull(GUIType uiType)
        {
            return UITypeRules.IsPanelOrNull(uiType);
        }

        internal static bool IsCompositeControlType(GUIType uiType)
        {
            return UITypeRules.IsCompositeControlType(uiType);
        }

        internal static bool IsLeafVisualType(GUIType uiType)
        {
            return UITypeRules.IsLeafVisualType(uiType);
        }

        internal static bool IsNestedContainerType(GUIType uiType)
        {
            if (uiType != GUIType.Toggle && uiType != GUIType.ScrollView && uiType != GUIType.TMPToggle)
            {
                return false;
            }
            return true;
        }

        internal static bool CanOwnDependencyNodes(GUIType uiType)
        {
            if (IsAuxiliaryUIType(uiType))
            {
                return true;
            }
            return IsNestedContainerType(uiType);
        }

        internal static bool CanOwnAuxiliaryType(GUIType uiType, GUIType uiType2)
        {
            return UITypeRules.CanOwnAuxiliaryType(uiType, uiType2);
        }

        internal static bool CanOwnNestedControlType(GUIType uiType, GUIType uiType2)
        {
            return UITypeRules.CanOwnNestedControlType(uiType, uiType2);
        }

        internal GUIType ApplyForcedTMPType(GUIType uiType)
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

        private UGUIParseRule ResolveRuleForForcedTMP(UGUIParseRule value)
        {
            if (value != null)
            {
                GUIType gUIType = ApplyForcedTMPType(value.UIType);
                if (gUIType == value.UIType)
                {
                    return value;
                }
                return FindRule(gUIType) ?? value;
            }
            return null;
        }

        private static GUIType ConvertTMPTypeToUGUI(GUIType uiType)
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

        private static GUIType ConvertUGUITypeToTMP(GUIType uiType)
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

        private static bool SupportsTextBackendOverride(GUIType uiType)
        {
            GUIType gUIType = ConvertTMPTypeToUGUI(uiType);
            if ((uint)(gUIType - 3) <= 4u)
            {
                return true;
            }
            return false;
        }

        private static bool RequiresSpriteBorder(Image.Type type)
        {
            if (type != Image.Type.Sliced)
            {
                return type == Image.Type.Tiled;
            }
            return true;
        }

        private static bool TryParseBuiltInLayerTag(object value, out ParsedLayerTag result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return false;
            }
            switch (((string)value).Trim().ToLowerInvariant())
            {
            case "sliced":
                result = new ParsedLayerTag
                {
                    RawText = (string)value,
                    CanonicalValue = "sliced",
                    Kind = (LayerTagKind)2,
                    ImageType = Image.Type.Sliced
                };
                return true;
            case "tiled":
                result = new ParsedLayerTag
                {
                    RawText = (string)value,
                    CanonicalValue = "tiled",
                    Kind = (LayerTagKind)2,
                    ImageType = Image.Type.Tiled
                };
                return true;
            case "tmp":
                result = new ParsedLayerTag
                {
                    RawText = (string)value,
                    CanonicalValue = "tmp",
                    Kind = (LayerTagKind)3
                };
                return true;
            case "ugui":
                result = new ParsedLayerTag
                {
                    RawText = (string)value,
                    CanonicalValue = "ugui",
                    Kind = (LayerTagKind)3
                };
                return true;
            default:
                return false;
            case "filled":
                result = new ParsedLayerTag
                {
                    RawText = (string)value,
                    CanonicalValue = "filled",
                    Kind = (LayerTagKind)2,
                    ImageType = Image.Type.Filled
                };
                return true;
            case "simple":
                result = new ParsedLayerTag
                {
                    RawText = (string)value,
                    CanonicalValue = "simple",
                    Kind = (LayerTagKind)2,
                    ImageType = Image.Type.Simple
                };
                return true;
            }
        }

        private bool TryParseConfiguredLayerTag(string text3, out ParsedLayerTag result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(text3) || rules == null)
            {
                return false;
            }
            string text = text3.Trim().ToLowerInvariant();
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
                        result = new ParsedLayerTag
                        {
                            RawText = text3,
                            CanonicalValue = text2,
                            Kind = (IsAuxiliaryUIType(uGUIParseRule.UIType) ? ((LayerTagKind)1) : ((LayerTagKind)0)),
                            UIType = uGUIParseRule.UIType
                        };
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryParseLayerTag(string text, out ParsedLayerTag result)
        {
            if (TryParseBuiltInLayerTag(text, out result))
            {
                return true;
            }
            return TryParseConfiguredLayerTag(text, out result);
        }

        private bool ParseLayerNameTags(string name, PsdLayerType value2, out LayerNameParseResult result, bool enabled = false)
        {
            result = new LayerNameParseResult();
            result.OriginalName = name ?? string.Empty;
            string text = (string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim());
            if (!string.IsNullOrEmpty(text))
            {
                if (text.StartsWith("refp ", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsPrefabReference = true;
                    text = text.Substring("refp ".Length).Trim();
                }
                else if (text.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsAssetReference = true;
                    text = text.Substring("ref ".Length).Trim();
                }
                List<ParsedLayerTag> list = new List<ParsedLayerTag>();
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
                result.Tags.AddRange(list);
                result.BaseName = text.Trim();
                for (int i = 0; i < result.Tags.Count; i++)
                {
                    ParsedLayerTag parsedLayerTag = result.Tags[i];
                    result.CanonicalTagValues.Add(parsedLayerTag.CanonicalValue);
                    if (result.TagsByKind.TryGetValue(parsedLayerTag.Kind, out var value) && !string.Equals(value.CanonicalValue, parsedLayerTag.CanonicalValue, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add($"{parsedLayerTag.Kind}: {value.CanonicalValue} -> {parsedLayerTag.CanonicalValue}");
                    }
                    result.TagsByKind[parsedLayerTag.Kind] = parsedLayerTag;
                }
                ResolveLayerNameParseResult(result, value2);
                if (enabled && result.Warnings.Count > 0)
                {
                    for (int j = 0; j < result.Warnings.Count; j++)
                    {
                        Debug.LogWarning((object)("Layer tag warning [" + name + "]: " + result.Warnings[j]));
                    }
                }
                return true;
            }
            ResolveLayerNameParseResult(result, value2);
            return true;
        }

        private void ResolveLayerNameParseResult(LayerNameParseResult value5, PsdLayerType value6)
        {
            LayerTypeResolutionContext value7 = default(LayerTypeResolutionContext);
            value7.LayerType = value6;
            value7.Parser = this;
            if (value5 == null)
            {
                return;
            }
            GUIType gUIType = GetDefaultUIType(ref value7);
            if (value5.TagsByKind.TryGetValue((LayerTagKind)0, out var value))
            {
                GUIType configuredUiType = value.UIType;
                if (ConvertTMPTypeToUGUI(configuredUiType) == GUIType.Text && value7.LayerType != PsdLayerType.TextLayer)
                {
                    value5.Warnings.Add("main tag '" + value.CanonicalValue + "' ignored on non-text layer");
                }
                else if ((configuredUiType == GUIType.Panel || configuredUiType == GUIType.ToggleGroup) && value7.LayerType != PsdLayerType.LayerGroup)
                {
                    value5.Warnings.Add("main tag '" + value.CanonicalValue + "' ignored on non-group layer");
                }
                else
                {
                    gUIType = configuredUiType;
                    value5.HasExplicitUIType = true;
                }
            }
            if (value5.TagsByKind.TryGetValue((LayerTagKind)1, out var value2))
            {
                gUIType = value2.UIType;
                value5.HasExplicitUIType = true;
            }
            if (value5.TagsByKind.TryGetValue((LayerTagKind)3, out var value3))
            {
                value5.HasTextBackendOverride = true;
                value5.UseTMPTextBackend = string.Equals(value3.CanonicalValue, "tmp", StringComparison.OrdinalIgnoreCase);
                value5.UseUGUITextBackend = string.Equals(value3.CanonicalValue, "ugui", StringComparison.OrdinalIgnoreCase);
                if (SupportsTextBackendOverride(gUIType))
                {
                    gUIType = (value5.UseTMPTextBackend ? ConvertUGUITypeToTMP(gUIType) : ConvertTMPTypeToUGUI(gUIType));
                }
            }
            else
            {
                gUIType = ApplyForcedTMPType(gUIType);
            }
            value5.ResolvedUIType = gUIType;
            if (value5.TagsByKind.TryGetValue((LayerTagKind)2, out var value4))
            {
                value5.HasImageTypeOverride = true;
                value5.ResolvedImageType = value4.ImageType;
            }
        }

        internal Type GetHelperComponentType(GUIType uiType)
        {
            if (uiType == GUIType.Null)
            {
                return null;
            }
            UGUIParseRule uGUIParseRule = FindRule(uiType);
            if (uGUIParseRule != null && !string.IsNullOrWhiteSpace(uGUIParseRule.UIHelper))
            {
                string typeName = uGUIParseRule.UIHelper;
                Type type = Type.GetType(typeName);
                if (type == null)
                {
                    // Type.GetType 不带程序集名只搜调用方程序集；Helper 类在 Runtime 程序集里。
                    foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(typeName);
                        if (type != null)
                        {
                            break;
                        }
                    }
                }
                return type;
            }
            return null;
        }

        internal UGUIParseRule[] GetRules()
        {
            return rules ?? Array.Empty<UGUIParseRule>();
        }

        internal UGUIParseRule FindRule(GUIType uiType)
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

        internal string BuildLayerObjectName(string name, int value = -1)
        {
            string text = ((!string.IsNullOrWhiteSpace(name)) ? name.Trim() : string.Empty);
            string text2 = text;
            bool num = text2.StartsWith("ref ", StringComparison.OrdinalIgnoreCase);
            bool flag = text2.StartsWith("refp ", StringComparison.OrdinalIgnoreCase);
            if (convertZh2En && !string.IsNullOrEmpty(text))
            {
                text = AssetNameSanitizer.TransliterateChinese(text);
                text2 = text;
            }
            if (num || flag)
            {
                string text3 = (flag ? "refp " : "ref ");
                string text4 = ((text2.Length > text3.Length) ? text2.Substring(text3.Length).Trim() : string.Empty);
                text4 = RemoveRecognizedLayerTags(text4);
                string text5 = AssetNameSanitizer.SanitizeReferenceName(text4);
                text = (text3 + (string.IsNullOrEmpty(text5) ? text4 : text5)).TrimEnd();
            }
            else
            {
                text = RemoveRecognizedLayerTags(text);
                text = AssetNameSanitizer.SanitizeReferenceName(text);
            }
            if (string.IsNullOrEmpty(text))
            {
                text = ((value >= 0) ? $"PsdLayer-{value}" : "PsdLayer");
            }
            return text;
        }

        internal bool TryResolveRuleForNode(PsdLayerNode layerNode, out UGUIParseRule result)
        {
            result = null;
            if (!((Object)(object)layerNode == (Object)null))
            {
                string text = ((layerNode.GetBoundPsdLayer() != null) ? layerNode.GetBoundPsdLayer().GetLayerName() : layerNode.GetSourceLayerName());
                ParseLayerNameTags(text, layerNode.LayerType, out var value, true);
                if (value.ResolvedUIType != GUIType.Null)
                {
                    result = ResolveRuleForForcedTMP(FindRule(value.ResolvedUIType));
                }
                return result != null;
            }
            return false;
        }

        internal static bool TryExtractDotSuffix(object text, out string result)
        {
            result = null;
            if (!string.IsNullOrWhiteSpace((string)text) && !((string)text).EndsWith('.'.ToString()))
            {
                int num = -1;
                int num2 = ((string)text).Length - 1;
                while (num2 >= 0)
                {
                    if (((string)text)[num2] != '.')
                    {
                        num2--;
                        continue;
                    }
                    num = num2;
                    break;
                }
                if (num <= 0)
                {
                    return false;
                }
                result = ((string)text).Substring(num);
                return true;
            }
            return false;
        }

        internal string RemoveRecognizedLayerTags(string text2)
        {
            if (!string.IsNullOrWhiteSpace(text2))
            {
                ParseLayerNameTags(text2, PsdLayerType.Unknown, out var value);
                string text = value.BaseName;
                if (!value.IsPrefabReference)
                {
                    if (value.IsAssetReference)
                    {
                        return ("ref " + text).TrimEnd();
                    }
                    return text;
                }
                return ("refp " + text).TrimEnd();
            }
            return string.Empty;
        }

        private string GetLayerNameWithoutTags(string text)
        {
            return RemoveRecognizedLayerTags(text);
        }

        private bool IsRecognizedLayerTag(string text)
        {
            ParsedLayerTag parsedLayerTag;
            return TryParseLayerTag(text, out parsedLayerTag);
        }

        internal static void ApplyNodeRectToUI(object value, object value2, bool enabled = true, bool enabled2 = true, bool enabled3 = true, int value3 = 0)
        {
            if (!((Object)value2 == (Object)null) && !((Object)value == (Object)null))
            {
                Rect val = ((PsdLayerNode)value).GetLayerRect();
                RectTransform component = ((Component)value2).GetComponent<RectTransform>();
                if (enabled2)
                {
                    component.SetSizeWithCurrentAnchors((RectTransform.Axis)0, val.size.x + (float)value3);
                }
                if (enabled3)
                {
                    component.SetSizeWithCurrentAnchors((RectTransform.Axis)1, val.size.y + (float)value3);
                }
                if (enabled)
                {
                    Rect rect = component.rect;
                    Vector2 size = rect.size;
                    Vector2 val2 = (component.pivot - Vector2.one * 0.5f) * size;
                    Vector3 position = default(Vector3);
                    position = new Vector3(val.position.x + val2.x, val.position.y + val2.y, ((Transform)component).position.z);
                    ((Transform)component).position = position;
                }
            }
        }

        internal static Texture2D ExportAndLoadTexture(object value)
        {
            if (!((Object)value != (Object)null))
            {
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(((PsdLayerNode)value).ExportImageAsset(false, (string)null, (string)null, true, false, false));
        }

        internal Image.Type ResolveImageType(PsdLayerNode layerNode, Image.Type type)
        {
            if (!((Object)(object)layerNode == (Object)null))
            {
                string text = ((!string.IsNullOrWhiteSpace(layerNode.GetSourceLayerName())) ? layerNode.GetSourceLayerName() : layerNode.GetBoundPsdLayer()?.GetLayerName());
                ParseLayerNameTags(text, layerNode.LayerType, out var value);
                if (value.HasImageTypeOverride)
                {
                    return value.ResolvedImageType;
                }
                return type;
            }
            return type;
        }

        internal Sprite ApplyImageSprite(PsdLayerNode layerNode, Image image)
        {
            if (!((Object)(object)layerNode == (Object)null) && !((Object)(object)image == (Object)null))
            {
                Image.Type val = (image.type = ResolveImageType(layerNode, image.type));
                return image.sprite = ExportAndLoadSprite(layerNode, RequiresSpriteBorder(val));
            }
            return null;
        }

        internal static Sprite ExportAndLoadSprite(object value, bool enabled = false)
        {
            if ((Object)value != (Object)null)
            {
                PsdLayerNode node = (PsdLayerNode)value;
                string text = node.ExportImageAsset(true, (string)null, (string)null, true, false, false);
                Sprite val = PsdLayerNode.LoadSpriteAtPath(text);
                if ((Object)(object)val != (Object)null)
                {
                    if (enabled)
                    {
                        Psd2UIFormConverterEditor.EnsureNineSliceBorder(text, node.GetSourceLayerName());
                        if (ScriptableSingleton<Psd2UIFormSettings>.Instance.AutoCropMinimalNineSlice)
                        {
                            RightClickExtension.TryCropMinimalNineSlice(text);
                        }
                        val = PsdLayerNode.LoadSpriteAtPath(text) ?? val;
                    }
                    return val;
                }
            }
            return null;
        }

        /// <summary>
        /// Calculate nine-slice border using PSDLayoutTool2's triple inference algorithm.
        /// </summary>
        internal static Vector4 CalculateNineSliceBorder(Texture2D texture, string layerName = null)
        {
            if (texture == null)
            {
                return Vector4.zero;
            }

            // 1. Try parse naming rules (compatible with old |sliced tag)
            Psd2UiNineSliceNameRule rule = null;
            if (!string.IsNullOrEmpty(layerName))
            {
                // Compatible with old tags
                if (layerName.Contains("|sliced") || layerName.Contains("|九宫格") ||
                    layerName.Contains("[sliced]") || layerName.Contains("[九宫格]"))
                {
                    rule = new Psd2UiNineSliceNameRule(Psd2UiNineSliceMode.NineSlice, null);
                }
                else
                {
                    Psd2UiNineSliceNameRules.TryParse(layerName, out rule);
                }

                // If has explicit border, return directly
                if (rule != null && rule.HasExplicitBorder)
                {
                    var border = rule.ExplicitBorder;
                    return new Vector4(border.Left, border.Bottom, border.Right, border.Top);
                }
            }

            // 2. Use triple inference algorithm
            Psd2UiNineSliceRaster raster = TextureToRaster(texture);
            Psd2UiNineSliceInference inference;

            if (Psd2UiNineSliceAnalyzer.TryInfer(raster, out inference))
            {
                var border = inference.Border;

                #if UNITY_EDITOR
                Debug.Log($"[Psd2UI NineSlice] {layerName ?? texture.name}: " +
                          $"Border=({border.Left}, {border.Top}, {border.Right}, {border.Bottom}) " +
                          $"Method={inference.Method} Confidence={inference.Confidence}");
                #endif

                return new Vector4(border.Left, border.Bottom, border.Right, border.Top);
            }

            #if UNITY_EDITOR
            Debug.LogWarning($"[Psd2UI NineSlice] {layerName ?? texture.name}: " +
                             "Inference failed - texture may be too small or fully transparent");
            #endif

            return Vector4.zero;
        }

        /// <summary>
        /// Convert Unity Texture2D to nine-slice raster data.
        /// </summary>
        internal static Psd2UiNineSliceRaster TextureToRaster(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            byte[] bytes = new byte[pixels.Length * 4];

            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4 + 0] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }

            return new Psd2UiNineSliceRaster(texture.width, texture.height, bytes);
        }

        /// <summary>
        /// Backward compatibility: keep old function signature.
        /// </summary>
        internal static Vector4 CalculateNineSliceBorder(object value, byte value2 = 0, int value3 = -1)
        {
            if (value == null)
            {
                return Vector4.zero;
            }

            // Forward to new implementation (ignore old alphaThreshold and tolerance parameters)
            return CalculateNineSliceBorder((Texture2D)value, layerName: null);
        }

        internal static PsdTextStyleInfo ApplyLegacyTextStyle(object value, object value2)
        {
            if ((Object)value2 == (Object)null)
            {
                return default(PsdTextStyleInfo);
            }
            ((Component)value2).gameObject.SetActive((Object)value != (Object)null);
            if (!((Object)value != (Object)null) || !((PsdLayerNode)value).TryGetTextStyleInfo(out PsdTextStyleInfo textInfo))
            {
                return default(PsdTextStyleInfo);
            }
            bool flag = ShouldEnableTextWrapping(value, in textInfo);
            Font val = ResolveLegacyFont(textInfo.FontName);
            if ((Object)(object)val != (Object)null)
            {
                ((Text)value2).font = val;
            }
            ((Text)value2).text = textInfo.Text;
            ((Text)value2).fontSize = textInfo.FontSize;
            ((Text)value2).fontStyle = textInfo.LegacyFontStyle;
            ((Graphic)value2).color = textInfo.Color;
            ((Text)value2).resizeTextForBestFit = false;
            ((Text)value2).lineSpacing = CalculateLegacyLineSpacing(value2, in textInfo);
            ((Text)value2).horizontalOverflow = flag ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            ((Text)value2).verticalOverflow = VerticalWrapMode.Overflow;
            ApplyLegacyTextEffects(value2, in textInfo);
            return textInfo;
        }

        internal static void ApplyTMPTextStyle(object value, object value2)
        {
            if ((Object)value2 == (Object)null)
            {
                return;
            }
            ((Component)value2).gameObject.SetActive((Object)value != (Object)null);
            if (!((Object)value != (Object)null) || !((PsdLayerNode)value).TryGetTextStyleInfo(out PsdTextStyleInfo textInfo))
            {
                return;
            }
            bool flag = ShouldEnableTextWrapping(value, in textInfo);
            TMP_FontAsset val = ResolveTMPFontAsset(textInfo.FontName) ?? ((TMP_Text)value2).font;
            if ((Object)(object)val != (Object)null)
            {
                Material val2 = EnsureTMPFontMaterial(val);
                EnsureTMPFontContainsCharacters(val, textInfo.Text);
                val2 = EnsureTMPFontMaterial(val) ?? val2;
                ((TMP_Text)value2).font = val;
                if ((Object)(object)val2 != (Object)null)
                {
                    ((TMP_Text)value2).fontSharedMaterial = val2;
                }
            }
            ((TMP_Text)value2).text = textInfo.Text ?? string.Empty;
            ((TMP_Text)value2).fontSize = textInfo.FontSize;
            ((TMP_Text)value2).fontStyle = textInfo.TMPFontStyle;
            ((TMP_Text)value2).characterSpacing = textInfo.CharacterSpacing;
            ((TMP_Text)value2).lineSpacing = CalculateTMPLineSpacing(value2, in textInfo);
            ((TMP_Text)value2).enableAutoSizing = false;
            SetTMPWordWrapping(value2, flag);
            ((TMP_Text)value2).overflowMode = (TextOverflowModes)0;
            ((TMP_Text)value2).margin = Vector4.zero;
            ((Graphic)value2).color = ((!HasTextGradient(in textInfo)) ? textInfo.Color : Color.white);
            ApplyTMPMaterialEffects(value2, in textInfo);
            ((TMP_Text)value2).ForceMeshUpdate(false, false);
            ApplyTMPTextGradient(value, value2, in textInfo);
        }

        private static float CalculateLegacyLineSpacing(object value, in PsdTextStyleInfo textInfo)
        {
            if (textInfo.IsLineSpacingAuto || textInfo.LineSpacing <= 0f)
            {
                return 1f;
            }
            float num = Mathf.Max(1f, (float)textInfo.FontSize);
            Font val = (((Object)value != (Object)null) ? ((Text)value).font : null);
            if ((Object)(object)val != (Object)null && val.fontSize > 0 && val.lineHeight > 0)
            {
                num = (float)val.lineHeight * ((float)textInfo.FontSize / (float)val.fontSize);
            }
            float num2 = textInfo.LineSpacing / Mathf.Max(1f, num);
            if (!float.IsNaN(num2) && !float.IsInfinity(num2))
            {
                return Mathf.Max(0.01f, num2);
            }
            return 1f;
        }

        private static bool ShouldEnableTextWrapping(object value, in PsdTextStyleInfo textInfo)
        {
            if ((Object)value == (Object)null)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(textInfo.Text) && (textInfo.Text.IndexOf('\n') >= 0 || textInfo.Text.IndexOf('\r') >= 0))
            {
                return true;
            }
            float num = Mathf.Max((float)textInfo.FontSize * 1.35f, 1f);
            Rect val = ((PsdLayerNode)value).GetLayerRect();
            return val.height > num;
        }

        private static float CalculateTMPLineSpacing(object value, in PsdTextStyleInfo textInfo)
        {
            if (!textInfo.IsLineSpacingAuto && textInfo.LineSpacing > 0f)
            {
                float num = Mathf.Max(1f, (float)textInfo.FontSize);
                float num2 = num;
                TMP_FontAsset val = (((Object)value != (Object)null) ? ((TMP_Text)value).font : null);
                if ((Object)(object)val != (Object)null)
                {
                    FaceInfo faceInfo = val.faceInfo;
                    if ((float)faceInfo.pointSize > 0f && faceInfo.lineHeight > 0f)
                    {
                        float num3 = num / (float)faceInfo.pointSize;
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

        private static void RemoveLegacyTextEffects(object value)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            Shadow[] components = ((Component)value).GetComponents<Shadow>();
            foreach (Shadow val in components)
            {
                if ((Object)(object)val != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)val);
                }
            }
        }

        private static void ApplyLegacyTextEffects(object value, in PsdTextStyleInfo textInfo)
        {
            if (!((Object)value == (Object)null))
            {
                RemoveLegacyTextEffects(value);
                if (textInfo.HasOutline)
                {
                    Outline obj = ((Component)value).gameObject.AddComponent<Outline>();
                    ((Behaviour)obj).enabled = true;
                    float value2 = textInfo.LegacyOutlineSize;
                    ((Shadow)obj).effectColor = textInfo.OutlineColor;
                    ((Shadow)obj).effectDistance = new Vector2(value2, value2);
                    ((Shadow)obj).useGraphicAlpha = true;
                }
                if (textInfo.HasShadow && !textInfo.IsInnerShadow)
                {
                    Shadow obj2 = ((Component)value).gameObject.AddComponent<Shadow>();
                    ((Behaviour)obj2).enabled = true;
                    obj2.effectColor = textInfo.ShadowColor;
                    obj2.effectDistance = textInfo.ShadowOffset;
                    obj2.useGraphicAlpha = true;
                }
            }
        }

        private static void ApplyTMPTextGradient(object value, object value2, in PsdTextStyleInfo textInfo)
        {
            if (!((Object)value2 == (Object)null))
            {
                ((TMP_Text)value2).enableVertexGradient = false;
                ((TMP_Text)value2).colorGradientPreset = null;
                ((TMP_Text)value2).colorGradient = new VertexGradient(textInfo.Color);
                if (!TryApplyTMPTextGradient(value2, in textInfo))
                {
                    ((Graphic)value2).color = textInfo.Color;
                }
                else
                {
                    ((Graphic)value2).color = Color.white;
                }
            }
        }

        private static bool HasTextGradient(in PsdTextStyleInfo textInfo)
        {
            if (textInfo.HasGradient && textInfo.GradientStops != null)
            {
                return textInfo.GradientStops.Length >= 2;
            }
            return false;
        }

        private static bool TryApplyTMPTextGradient(object value, in PsdTextStyleInfo textInfo)
        {
            if ((Object)value == (Object)null || !HasTextGradient(in textInfo))
            {
                return false;
            }
            ((TMP_Text)value).enableVertexGradient = true;
            ((TMP_Text)value).colorGradientPreset = null;
            ((TMP_Text)value).colorGradient = BuildTMPVertexGradient(in textInfo);
            ((Graphic)value).color = Color.white;
            ((TMP_Text)value).havePropertiesChanged = true;
            ((Graphic)value).SetVerticesDirty();
            ((TMP_Text)value).ForceMeshUpdate(false, false);
            return true;
        }

        private static VertexGradient BuildTMPVertexGradient(in PsdTextStyleInfo textInfo)
        {
            Vector2 val = GetGradientDirection(textInfo.GradientAngle);
            Vector2[] array = (Vector2[])(object)new Vector2[4]
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
                float num3 = Vector2.Dot(array[i], val);
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
                topLeft = (Color32)(EvaluateTextGradientColor(in textInfo, (Vector2.Dot(array[0], val) - num) / num4)),
                topRight = (Color32)(EvaluateTextGradientColor(in textInfo, (Vector2.Dot(array[1], val) - num) / num4)),
                bottomLeft = (Color32)(EvaluateTextGradientColor(in textInfo, (Vector2.Dot(array[2], val) - num) / num4)),
                bottomRight = (Color32)(EvaluateTextGradientColor(in textInfo, (Vector2.Dot(array[3], val) - num) / num4))
            };
        }

        private static Vector2 GetGradientDirection(float value)
        {
            float num = NormalizeAngleDegrees(value) * ((float)Math.PI / 180f);
            Vector2 val = default(Vector2);
            val = new Vector2(Mathf.Cos(num), Mathf.Sin(num));
            if (val.sqrMagnitude < 0.0001f)
            {
                return Vector2.right;
            }
            return val.normalized;
        }

        private static float NormalizeAngleDegrees(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }
            value %= 360f;
            if (value < 0f)
            {
                value += 360f;
            }
            return value;
        }

        private static Color32 EvaluateTextGradientColor(in PsdTextStyleInfo textInfo, float value)
        {
            TextGradientColorStop[] values = textInfo.GradientStops;
            if (values != null && values.Length != 0)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    value = 0f;
                }
                value = Mathf.Clamp01(value);
                if (textInfo.IsGradientReversed)
                {
                    value = 1f - value;
                }
                if (value <= values[0].Position)
                {
                    return (Color32)(values[0].Color);
                }
                if (value >= values[values.Length - 1].Position)
                {
                    return (Color32)(values[values.Length - 1].Color);
                }
                int num = 0;
                TextGradientColorStop value2;
                TextGradientColorStop value3;
                while (true)
                {
                    if (num < values.Length - 1)
                    {
                        value2 = values[num];
                        value3 = values[num + 1];
                        if (!(value > value3.Position))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return (Color32)(values[values.Length - 1].Color);
                }
                if (value3.Position - value2.Position <= 0.0001f)
                {
                    return (Color32)(value3.Color);
                }
                float num2 = Mathf.InverseLerp(value2.Position, value3.Position, value);
                return (Color32)(Color.Lerp(value2.Color, value3.Color, num2));
            }
            return (Color32)(textInfo.Color);
        }

        private static void ApplyTMPMaterialEffects(object value, in PsdTextStyleInfo textInfo)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            RemoveLegacyTextEffects(value);
            Material val = ResolveBaseTMPMaterial(value);
            if ((Object)(object)val == (Object)null)
            {
                return;
            }
            if (!textInfo.HasOutline && !textInfo.HasShadow && !textInfo.HasGlow && !textInfo.HasBevel)
            {
                if ((Object)(object)((TMP_Text)value).fontSharedMaterial != (Object)(object)val)
                {
                    ((TMP_Text)value).fontSharedMaterial = val;
                    ((TMP_Text)value).UpdateMeshPadding();
                    ((Graphic)value).SetMaterialDirty();
                }
                return;
            }
            Material val2 = GetOrCreateTMPEffectMaterial(((TMP_Text)value).font, val, ((TMP_Text)value).fontSize, in textInfo);
            if ((Object)(object)val2 != (Object)null && (Object)(object)((TMP_Text)value).fontSharedMaterial != (Object)(object)val2)
            {
                ((TMP_Text)value).fontSharedMaterial = val2;
                ((TMP_Text)value).UpdateMeshPadding();
                ((TMP_Text)value).havePropertiesChanged = true;
                ((Graphic)value).SetMaterialDirty();
            }
        }

        private static Material ResolveBaseTMPMaterial(object value2)
        {
            Material fontSharedMaterial = ((TMP_Text)value2).fontSharedMaterial;
            Material val = (((Object)(object)((TMP_Text)value2).font != (Object)null) ? EnsureTMPFontMaterial(((TMP_Text)value2).font) : null);
            if ((Object)(object)fontSharedMaterial != (Object)null && s_BaseMaterialByEffectMaterialId.TryGetValue(GetObjectInstanceId(fontSharedMaterial), out var value) && (Object)(object)value != (Object)null && (Object)(object)val != (Object)null && HaveMatchingMainTextures(value, val))
            {
                return value;
            }
            if ((Object)(object)fontSharedMaterial != (Object)null && (Object)(object)val != (Object)null && HaveMatchingMainTextures(fontSharedMaterial, val))
            {
                return fontSharedMaterial;
            }
            return val;
        }

        private static bool HaveMatchingMainTextures(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null))
            {
                Texture texture = ((Material)value).GetTexture(ShaderUtilities.ID_MainTex);
                Texture texture2 = ((Material)value2).GetTexture(ShaderUtilities.ID_MainTex);
                if (!((Object)(object)texture == (Object)null) && !((Object)(object)texture2 == (Object)null))
                {
                    return GetObjectInstanceId(texture) == GetObjectInstanceId(texture2);
                }
                return false;
            }
            return false;
        }

        private static Material GetOrCreateTMPEffectMaterial(object value2, object value3, float value4, in PsdTextStyleInfo textInfo)
        {
            if ((Object)value2 != (Object)null)
            {
                Material val = EnsureTMPFontMaterial(value2);
                if ((Object)(object)val != (Object)null)
                {
                    value3 = val;
                }
            }
            if ((Object)value3 == (Object)null)
            {
                return null;
            }
            value4 = Mathf.Max(1f, value4);
            ShaderUtilities.GetShaderPropertyIDs();
            float num = GetMaterialGradientScale(value3);
            float num2 = ((!textInfo.HasOutline) ? 0f : Mathf.Clamp01(textInfo.TMPOutlineSize / num));
            float num3 = num2 * 0.5f;
            float num4 = 0f;
            if (textInfo.HasOutline)
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
            bool flag = textInfo.HasShadow && textInfo.IsInnerShadow;
            if (textInfo.HasShadow)
            {
                float num10 = Mathf.Clamp(textInfo.ShadowOffset.x / num, -1f, 1f);
                float num11 = Mathf.Clamp(textInfo.ShadowOffset.y / num, -1f, 1f);
                float num12 = Mathf.Clamp01(textInfo.ShadowSpread);
                float num13 = Mathf.Clamp01(textInfo.ShadowSoftness / num);
                float num14 = Mathf.Clamp01(num13 * num12);
                float num15 = ((!flag) ? num14 : (0f - num14));
                float num16 = Mathf.Clamp01(num13 - num14);
                float num17 = CalculateAvailableUnderlayRange(value3, num4);
                if (flag)
                {
                    num10 = 0f - num10;
                    num11 = 0f - num11;
                }
                ClampUnderlayParameters(num17, ref num10, ref num11, ref num15, ref num16, flag);
                num6 = NormalizeByRange(num10, num17);
                num7 = NormalizeByRange(num11, num17);
                num8 = NormalizeByRange(num15, num17);
                num9 = NormalizeByRange(num16, num17);
            }
            float num18 = 0f;
            float num19 = 0f;
            float num20 = 0f;
            float num21 = 0f;
            Color color = textInfo.GlowColor;
            if (textInfo.HasGlow)
            {
                float num22 = Mathf.Clamp01(textInfo.GlowSize / num);
                float num23 = Mathf.Clamp01(textInfo.GlowSpread);
                float num24 = Mathf.Clamp01((textInfo.GlowPower > 0f) ? textInfo.GlowPower : 0.75f);
                if (!textInfo.IsInnerGlow)
                {
                    num18 = 0f;
                    num19 = Mathf.Clamp01(num22 * Mathf.Lerp(2f, 1.35f, num23));
                    num20 = 0f;
                    num21 = Mathf.Lerp(0.2f, 0.45f, num23);
                }
                else
                {
                    float num25 = Mathf.Clamp01(num24 * 0.1f);
                    num18 = num22;
                    num19 = 0f;
                    num20 = 0f;
                    num21 = Mathf.Lerp(num25, Mathf.Min(1f, num25 * 2f), num23);
                }
            }
            float num26 = (textInfo.HasBevel ? Mathf.Clamp01(textInfo.BevelSize / num) : 0f);
            float num27 = ((textInfo.HasBevel && textInfo.BevelSize > 0f) ? Mathf.Clamp01(textInfo.BevelSoftness / textInfo.BevelSize) : 0f);
            float num28 = ((!textInfo.HasBevel) ? 0f : Mathf.Clamp(num26 * Mathf.Lerp(0.5f, 0.375f, num27), 0f, 0.5f));
            float num29 = ((!textInfo.HasBevel) ? 0f : Mathf.Clamp(num28 - num3 * Mathf.Lerp(1f, 0.65f, num27), -0.5f, 0.5f));
            float num30 = (textInfo.HasBevel ? (((textInfo.BevelDepth > 1f) ? Mathf.Clamp01(textInfo.BevelDepth / 100f) : Mathf.Clamp01(textInfo.BevelDepth)) * Mathf.Lerp(1f, 0.82f, num27)) : 0f);
            float num31 = ((!textInfo.HasBevel) ? 0f : Mathf.Clamp((textInfo.IsInnerBevel ? (-1f) : 1f) * num26 * Mathf.Lerp(0.12f, 0.04f, num27), -0.5f, 0.5f));
            float num32 = ((!textInfo.HasBevel) ? 0f : Mathf.Clamp01(num27 * Mathf.Lerp(0.45f, 0.75f, num26)));
            float num33 = (textInfo.HasBevel ? Mathf.Lerp(0.08f, 0.92f, num27) : 0f);
            float num34 = (textInfo.HasBevel ? ConvertLightAngleToRadians(textInfo.BevelAngle) : 0f);
            float num35 = ((!textInfo.HasBevel || textInfo.IsInnerBevel) ? 0f : 1f);
            Color clear = Color.clear;
            Color black = Color.black;
            Color black2 = Color.black;
            float num36 = 0f;
            float num37 = 10f;
            float num38 = 0f;
            float num39 = 1f;
            if (textInfo.HasBevel)
            {
                float num40 = Mathf.Clamp01(textInfo.BevelAltitude / 90f);
                float num41 = Mathf.Clamp01(textInfo.BevelHighlightOpacity);
                float num42 = Mathf.Clamp01(textInfo.BevelShadowOpacity) * (1f - CalculateLuminance(textInfo.BevelShadowColor));
                float num43 = num41 * Mathf.Lerp(0.65f, 1f, num30);
                float num44 = Mathf.Clamp01(Mathf.Lerp(0.35f, 1f, num42));
                clear = new Color(textInfo.BevelShadowColor.r * num44, textInfo.BevelShadowColor.g * num44, textInfo.BevelShadowColor.b * num44, 1f);
                float num45 = Mathf.Clamp01(Mathf.Lerp(0.22f, 0.72f, num43) * Mathf.Lerp(0.85f, 1.05f, num40));
                black = new Color(textInfo.BevelHighlightColor.r * num45, textInfo.BevelHighlightColor.g * num45, textInfo.BevelHighlightColor.b * num45, 1f);
                Color val2 = ((!textInfo.HasOutline) ? textInfo.BevelHighlightColor : Color.Lerp(textInfo.OutlineColor, textInfo.BevelHighlightColor, 0.65f));
                float num46 = Mathf.Clamp01(num45 * (textInfo.HasOutline ? 1f : 0.9f));
                black2 = new Color(val2.r * num46, val2.g * num46, val2.b * num46, 1f);
                num36 = ((num43 > 0f) ? Mathf.Clamp(Mathf.Lerp(0.2f, 2.2f, num43) * Mathf.Lerp(0.8f, 1.1f, num40), 0f, 4f) : 0f);
                num37 = Mathf.Lerp(14f, 6f, Mathf.Clamp01(num33 + (1f - num40) * 0.25f));
                num38 = ((num42 > 0f) ? Mathf.Clamp01(Mathf.Lerp(0.15f, 0.8f, num42) * Mathf.Lerp(1.1f, 0.75f, num40)) : 0f);
                num39 = ((num42 > 0f) ? Mathf.Clamp01(1f - num42 * Mathf.Lerp(0.75f, 0.45f, num40)) : 1f);
            }
            Color32 val3 = (Color32)(textInfo.OutlineColor);
            Color32 val4 = (Color32)(textInfo.ShadowColor);
            Color32 val5 = (Color32)(color);
            int num47 = Mathf.RoundToInt(num3 * 10000f);
            int num48 = Mathf.RoundToInt(num4 * 10000f);
            int num49 = Mathf.RoundToInt(num6 * 10000f);
            int num50 = Mathf.RoundToInt(num7 * 10000f);
            int num51 = Mathf.RoundToInt(num5 * 10000f);
            int num52 = Mathf.RoundToInt(num8 * 10000f);
            int num53 = Mathf.RoundToInt(num9 * 10000f);
            int num54 = Mathf.RoundToInt(num19 * 10000f);
            int num55 = Mathf.RoundToInt(num18 * 10000f);
            int num56 = Mathf.RoundToInt(num20 * 10000f);
            int num57 = Mathf.RoundToInt(num21 * 10000f);
            int num58 = Mathf.RoundToInt(num30 * 10000f);
            int num59 = Mathf.RoundToInt(num31 * 10000f);
            int num60 = Mathf.RoundToInt(num29 * 10000f);
            int num61 = Mathf.RoundToInt(num32 * 10000f);
            int num62 = Mathf.RoundToInt(num33 * 10000f);
            int num63 = Mathf.RoundToInt(num34 * 10000f);
            Color32 val6 = (Color32)(clear);
            int num64 = Mathf.RoundToInt(num36 * 10000f);
            int num65 = Mathf.RoundToInt(num37 * 10000f);
            int num66 = Mathf.RoundToInt(num38 * 10000f);
            int num67 = Mathf.RoundToInt(num39 * 10000f);
            int num68 = Mathf.RoundToInt(num35 * 10000f);
            string text = $"o:{(textInfo.HasOutline ? 1 : 0)}|op:{(int)textInfo.OutlineMode}|ow:{num47}|fd:{num48}|os:{num51}|oc:{val3.r},{val3.g},{val3.b},{val3.a}" + $"|s:{(textInfo.HasShadow ? 1 : 0)}|si:{(flag ? 1 : 0)}|so:{num49},{num50}|sd:{num52}|ss:{num53}|sc:{val4.r},{val4.g},{val4.b},{val4.a}" + $"|g:{(textInfo.HasGlow ? 1 : 0)}|gi:{(textInfo.IsInnerGlow ? 1 : 0)}|go:{num54}|giw:{num55}|gof:{num56}|gp:{num57}|gc:{val5.r},{val5.g},{val5.b},{val5.a}" + $"|b:{(textInfo.HasBevel ? 1 : 0)}|bi:{(textInfo.IsInnerBevel ? 1 : 0)}|ba:{num58}|bo:{num59}|bw:{num60}|bc:{num61}|br:{num62}|la:{num63}|bsc:{val6.r},{val6.g},{val6.b},{val6.a}|bsp:{num64}|brv:{num65}|bdf:{num66}|bam:{num67}|bf:{num68}";
            string key = GetObjectInstanceIdKey(value2) + "|" + text;
            if (s_TmpEffectMaterialCache.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
            {
                SyncTMPMaterialAtlasProperties(value, value2);
                return value;
            }
            string name = GenerateTMPEffectMaterialName(value2, value3, in textInfo);
            Material val7 = LoadExistingTMPEffectMaterial(value2, text);
            if ((Object)(object)val7 != (Object)null)
            {
                SyncTMPMaterialAtlasProperties(val7, value2);
                ApplyTMPMaterialProperties(val7, num3, num4, num5, val3, textInfo.HasOutline, num6, num7, num8, num9, val4, textInfo.HasShadow, flag, val5, num20, num18, num19, num21, textInfo.HasGlow, num30, num31, num29, num32, num33, num34, clear, black, black2, num36, num37, num38, num39, num35, textInfo.HasBevel);
                EditorUtility.SetDirty((Object)(object)val7);
                s_TmpEffectMaterialCache[key] = val7;
                s_BaseMaterialByEffectMaterialId[GetObjectInstanceId(val7)] = (Material)value3;
                return val7;
            }
            Material val8 = CloneDistanceFieldMaterial(value3, true);
            if ((Object)(object)val8 == (Object)null)
            {
                return null;
            }
            ((Object)val8).name = name;
            SyncTMPMaterialAtlasProperties(val8, value2);
            ApplyTMPMaterialProperties(val8, num3, num4, num5, val3, textInfo.HasOutline, num6, num7, num8, num9, val4, textInfo.HasShadow, flag, val5, num20, num18, num19, num21, textInfo.HasGlow, num30, num31, num29, num32, num33, num34, clear, black, black2, num36, num37, num38, num39, num35, textInfo.HasBevel);
            if ((Object)value2 != (Object)null)
            {
                SaveTMPEffectMaterialAsset(value2, val8, text);
            }
            s_TmpEffectMaterialCache[key] = val8;
            s_BaseMaterialByEffectMaterialId[GetObjectInstanceId(val8)] = (Material)value3;
            return val8;
        }

        private static float GetMaterialGradientScale(object value)
        {
            if (!((Object)value == (Object)null) && ((Material)value).HasProperty(ShaderUtilities.ID_GradientScale))
            {
                return Mathf.Max(1f, ((Material)value).GetFloat(ShaderUtilities.ID_GradientScale));
            }
            return 10f;
        }

        private static float GetFontWeightPadding(object value)
        {
            if (!((Object)value == (Object)null))
            {
                float num = ((!((Material)value).HasProperty(ShaderUtilities.ID_WeightNormal)) ? 0f : ((Material)value).GetFloat(ShaderUtilities.ID_WeightNormal));
                float num2 = (((Material)value).HasProperty(ShaderUtilities.ID_WeightBold) ? ((Material)value).GetFloat(ShaderUtilities.ID_WeightBold) : 0f);
                return Mathf.Max(num, num2) / 4f;
            }
            return 0f;
        }

        private static float CalculateAvailableUnderlayRange(object value, float value2)
        {
            if ((Object)value == (Object)null || !((Material)value).HasProperty(ShaderUtilities.ID_GradientScale))
            {
                return 0f;
            }
            float num = Mathf.Max(1f, ((Material)value).GetFloat(ShaderUtilities.ID_GradientScale));
            float num2 = (GetFontWeightPadding(value) + value2) * (num - 1f);
            return Mathf.Max(0f, num - 1f - num2) / num;
        }

        private static void ClampUnderlayParameters(float value, ref float value2, ref float value3, ref float value4, ref float value5, bool enabled = false)
        {
            if (value <= 0f)
            {
                value2 = 0f;
                value3 = 0f;
                value4 = 0f;
                value5 = 0f;
                return;
            }
            value2 = Mathf.Clamp(value2, -1f, 1f);
            value3 = Mathf.Clamp(value3, -1f, 1f);
            value4 = Mathf.Clamp(value4, enabled ? (-1f) : 0f, 1f);
            value5 = Mathf.Clamp(value5, 0f, 1f);
            float num = Mathf.Max(Mathf.Abs(value2), Mathf.Abs(value3));
            if (num > value && num > 0f)
            {
                float num2 = value / num;
                value2 *= num2;
                value3 *= num2;
                value4 = 0f;
                value5 = 0f;
            }
            else
            {
                float num3 = value - num;
                float num4 = Mathf.Min(Mathf.Abs(value4), num3);
                value4 = Mathf.Sign(value4) * num4;
                num3 -= num4;
                value5 = Mathf.Min(value5, num3);
            }
        }

        private static float NormalizeByRange(float value, float value2)
        {
            if (value2 <= 0f)
            {
                return 0f;
            }
            return value / value2;
        }

        private static float ConvertLightAngleToRadians(float value)
        {
            return Mathf.Repeat(90f - value, 360f) * ((float)Math.PI / 180f);
        }

        private static float CalculateLuminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }

        private static void ApplyTMPMaterialProperties(object value, float value2, float value3, float value4, Color32 value5, bool enabled, float value6, float value7, float value8, float value9, Color32 value10, bool enabled2, bool enabled3, Color32 value11, float value12, float value13, float value14, float value15, bool enabled4, float value16, float value17, float value18, float value19, float value20, float value21, Color color, Color color2, Color color3, float value22, float value23, float value24, float value25, float value26, bool enabled5)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            if (((Material)value).HasProperty(s_FaceDilatePropertyId))
            {
                ((Material)value).SetFloat(s_FaceDilatePropertyId, enabled ? value3 : 0f);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_OutlineWidth, value2);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_OutlineSoftness))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_OutlineSoftness, value4);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_OutlineColor))
            {
                ((Material)value).SetColor(ShaderUtilities.ID_OutlineColor, (!enabled) ? Color.clear : (Color32)(value5));
            }
            if (enabled)
            {
                ((Material)value).EnableKeyword(ShaderUtilities.Keyword_Outline);
            }
            else
            {
                ((Material)value).DisableKeyword(ShaderUtilities.Keyword_Outline);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                ((Material)value).SetColor(ShaderUtilities.ID_UnderlayColor, enabled2 ? (Color32)(value10) : Color.clear);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_UnderlayOffsetX, value6);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_UnderlayOffsetY, value7);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_UnderlaySoftness))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_UnderlaySoftness, value9);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_UnderlayDilate))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_UnderlayDilate, value8);
            }
            if (enabled2)
            {
                if (enabled3)
                {
                    ((Material)value).DisableKeyword(ShaderUtilities.Keyword_Underlay);
                    ((Material)value).EnableKeyword("UNDERLAY_INNER");
                }
                else
                {
                    ((Material)value).EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    ((Material)value).DisableKeyword("UNDERLAY_INNER");
                }
            }
            else
            {
                ((Material)value).DisableKeyword(ShaderUtilities.Keyword_Underlay);
                ((Material)value).DisableKeyword("UNDERLAY_INNER");
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_GlowColor))
            {
                ((Material)value).SetColor(ShaderUtilities.ID_GlowColor, enabled4 ? (Color32)(value11) : Color.clear);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_GlowOffset))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_GlowOffset, value12);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_GlowInner))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_GlowInner, value13);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_GlowOuter))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_GlowOuter, value14);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_GlowPower))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_GlowPower, value15);
            }
            if (!enabled4)
            {
                ((Material)value).DisableKeyword(ShaderUtilities.Keyword_Glow);
            }
            else
            {
                ((Material)value).EnableKeyword(ShaderUtilities.Keyword_Glow);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_BevelAmount))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_BevelAmount, value16);
            }
            if (((Material)value).HasProperty(s_ShaderFlagsPropertyId))
            {
                ((Material)value).SetFloat(s_ShaderFlagsPropertyId, (!enabled5) ? 0f : value26);
            }
            if (((Material)value).HasProperty(s_BevelOffsetPropertyId))
            {
                ((Material)value).SetFloat(s_BevelOffsetPropertyId, value17);
            }
            if (((Material)value).HasProperty(s_BevelWidthPropertyId))
            {
                ((Material)value).SetFloat(s_BevelWidthPropertyId, value18);
            }
            if (((Material)value).HasProperty(s_BevelClampPropertyId))
            {
                ((Material)value).SetFloat(s_BevelClampPropertyId, value19);
            }
            if (((Material)value).HasProperty(s_BevelRoundnessPropertyId))
            {
                ((Material)value).SetFloat(s_BevelRoundnessPropertyId, value20);
            }
            if (((Material)value).HasProperty(ShaderUtilities.ID_LightAngle))
            {
                ((Material)value).SetFloat(ShaderUtilities.ID_LightAngle, value21);
            }
            if (((Material)value).HasProperty(s_SpecularColorPropertyId))
            {
                ((Material)value).SetColor(s_SpecularColorPropertyId, enabled5 ? color : Color.clear);
            }
            if (((Material)value).HasProperty(s_ReflectFaceColorPropertyId))
            {
                ((Material)value).SetColor(s_ReflectFaceColorPropertyId, (!enabled5) ? Color.black : color2);
            }
            if (((Material)value).HasProperty(s_ReflectOutlineColorPropertyId))
            {
                ((Material)value).SetColor(s_ReflectOutlineColorPropertyId, (!enabled5) ? Color.black : color3);
            }
            if (((Material)value).HasProperty(s_SpecularPowerPropertyId))
            {
                ((Material)value).SetFloat(s_SpecularPowerPropertyId, enabled5 ? value22 : 0f);
            }
            if (((Material)value).HasProperty(s_ReflectivityPropertyId))
            {
                ((Material)value).SetFloat(s_ReflectivityPropertyId, enabled5 ? value23 : 10f);
            }
            if (((Material)value).HasProperty(s_DiffusePropertyId))
            {
                ((Material)value).SetFloat(s_DiffusePropertyId, enabled5 ? value24 : 0f);
            }
            if (((Material)value).HasProperty(s_AmbientPropertyId))
            {
                ((Material)value).SetFloat(s_AmbientPropertyId, enabled5 ? value25 : 1f);
            }
            if (enabled5)
            {
                ((Material)value).EnableKeyword(ShaderUtilities.Keyword_Bevel);
            }
            else
            {
                ((Material)value).DisableKeyword(ShaderUtilities.Keyword_Bevel);
            }
            ShaderUtilities.UpdateShaderRatios((Material)value);
        }

        private static string GenerateTMPEffectMaterialName(object value, object value2, in PsdTextStyleInfo textInfo)
        {
            string text = BuildTextEffectName(in textInfo);
            string text2 = GetAssetDirectory(value, value2);
            if (!string.IsNullOrWhiteSpace(text2))
            {
                int num = 0;
                string[] array = AssetDatabase.FindAssets("t:Material", new string[1] { text2 });
                for (int i = 0; i < array.Length; i++)
                {
                    string text3 = AssetDatabase.GUIDToAssetPath(array[i]);
                    if (!string.IsNullOrWhiteSpace(text3) && TryParseMaterialSequence(Path.GetFileNameWithoutExtension(text3), text, out var num2) && num2 > num)
                    {
                        num = num2;
                    }
                }
                return $"{text}_{num + 1}";
            }
            return text + "_1";
        }

        private static Material LoadExistingTMPEffectMaterial(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !string.IsNullOrWhiteSpace((string)value2))
            {
                if (TryFindTMPMaterialAsset(value, value2, out var val, out var _))
                {
                    SyncTMPMaterialAtlasProperties(val, value);
                    return val;
                }
                return null;
            }
            return null;
        }

        private static void SaveTMPEffectMaterialAsset(object value, object value2, object value3)
        {
            if ((Object)value == (Object)null || (Object)value2 == (Object)null || string.IsNullOrWhiteSpace((string)value3))
            {
                return;
            }
            string assetPath = AssetDatabase.GetAssetPath((Object)value);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            SyncTMPMaterialAtlasProperties(value2, value);
            if (!TryFindTMPMaterialAsset(value, value3, out var val, out var text))
            {
                text = BuildMaterialAssetPath(assetPath, ((Object)value2).name, false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }
                AssetDatabase.CreateAsset((Object)value2, text);
                value2 = AssetDatabase.LoadAssetAtPath<Material>(text) ?? value2;
            }
            else if ((Object)(object)val != (Object)value2)
            {
                EditorUtility.CopySerialized((Object)value2, (Object)(object)val);
                Object.DestroyImmediate((Object)value2);
                value2 = val;
            }
            SyncTMPMaterialAtlasProperties(value2, value);
            StampTMPMaterialImporter(value, text, value3);
            EditorUtility.SetDirty((Object)value2);
            EditorUtility.SetDirty((Object)value);
            AssetDatabase.SaveAssets();
        }

        private static string BuildMaterialAssetPath(object value, object value2, bool enabled)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && !string.IsNullOrWhiteSpace((string)value2))
            {
                string text = Path.GetDirectoryName((string)value)?.Replace("\\", "/");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    string text2 = text + "/" + (string)value2 + ".mat";
                    if (!enabled)
                    {
                        return text2;
                    }
                    return AssetDatabase.GenerateUniqueAssetPath(text2);
                }
                return null;
            }
            return null;
        }

        private static string BuildTextEffectName(in PsdTextStyleInfo textInfo)
        {
            List<string> list = new List<string>(4);
            if (textInfo.HasOutline)
            {
                list.Add("Outline");
            }
            if (textInfo.HasShadow)
            {
                list.Add(textInfo.IsInnerShadow ? "InnerShadow" : "Shadow");
            }
            if (textInfo.HasGlow)
            {
                list.Add((!textInfo.IsInnerGlow) ? "Glow" : "InnerGlow");
            }
            if (textInfo.HasBevel)
            {
                list.Add("Bevel");
            }
            if (list.Count <= 0)
            {
                return "Text";
            }
            return string.Join("_", list);
        }

        private static bool TryParseMaterialSequence(object value, object value2, out int result)
        {
            result = 0;
            if (!string.IsNullOrWhiteSpace((string)value) && !string.IsNullOrWhiteSpace((string)value2))
            {
                string text = (string)value2 + "_";
                if (((string)value).StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    return int.TryParse(((string)value).Substring(text.Length), out result);
                }
                return false;
            }
            return false;
        }

        private static string GetAssetDirectory(object value, object value2)
        {
            string text = (((Object)value != (Object)null) ? AssetDatabase.GetAssetPath((Object)value) : ((!((Object)value2 != (Object)null)) ? null : AssetDatabase.GetAssetPath((Object)value2)));
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return Path.GetDirectoryName(text)?.Replace("\\", "/");
        }

        private static string GetFontAssetIdentity(object value)
        {
            if (!((Object)value == (Object)null))
            {
                string assetPath = AssetDatabase.GetAssetPath((Object)value);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    string text = AssetDatabase.AssetPathToGUID(assetPath);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
                return ((Object)value).name;
            }
            return "TMP_FONT_NULL";
        }

        private static string BuildTMPMaterialSignature(object value, object value2)
        {
            return "PSD2UI_TMPFX_SIG:" + GetFontAssetIdentity(value) + "|" + (string)value2;
        }

        private static string BuildTMPMaterialAssetName(object value, object value2)
        {
            uint num = (uint)Animator.StringToHash((string)value2);
            string arg = (((Object)value != (Object)null) ? ((Object)value).name : "TMP Font");
            return string.Format("{0}{1}{2:X8}", arg, "__PSD2UI_TMPFX__", num);
        }

        private static bool TryFindTMPMaterialAsset(object value, object value2, out Material result, out string result2)
        {
            result = null;
            result2 = null;
            if (!((Object)value == (Object)null) && !string.IsNullOrWhiteSpace((string)value2))
            {
                string text = GetAssetDirectory(value, null);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    string b = BuildTMPMaterialSignature(value, value2);
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
                                if (!((Object)(object)atPath == (Object)null) && string.Equals(atPath.userData, b, StringComparison.Ordinal))
                                {
                                    result = AssetDatabase.LoadAssetAtPath<Material>(text2);
                                    if (!((Object)(object)result == (Object)null))
                                    {
                                        break;
                                    }
                                }
                            }
                            num++;
                            continue;
                        }
                        string assetPath = AssetDatabase.GetAssetPath((Object)value);
                        string text3 = BuildTMPMaterialAssetName(value, value2);
                        string text4 = BuildMaterialAssetPath(assetPath, text3, false);
                        if (!string.IsNullOrWhiteSpace(text4))
                        {
                            result = AssetDatabase.LoadAssetAtPath<Material>(text4);
                            if (!((Object)(object)result == (Object)null))
                            {
                                result2 = text4;
                                StampTMPMaterialImporter(value, result2, value2);
                                return true;
                            }
                            return false;
                        }
                        return false;
                    }
                    result2 = text2;
                    return true;
                }
                return false;
            }
            return false;
        }

        private static void StampTMPMaterialImporter(object value, object value2, object value3)
        {
            if (string.IsNullOrWhiteSpace((string)value2) || string.IsNullOrWhiteSpace((string)value3))
            {
                return;
            }
            AssetImporter atPath = AssetImporter.GetAtPath((string)value2);
            if (!((Object)(object)atPath == (Object)null))
            {
                string text = BuildTMPMaterialSignature(value, value3);
                if (!string.Equals(atPath.userData, text, StringComparison.Ordinal))
                {
                    atPath.userData = text;
                    atPath.SaveAndReimport();
                }
            }
        }

        private static string GetTMPFontBaseName(object value, object value2)
        {
            string text = ((!string.IsNullOrWhiteSpace((string)value2)) ? Path.GetFileNameWithoutExtension((string)value2) : ((!((Object)value != (Object)null)) ? "TMP Font" : ((Object)value).name));
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "TMP Font";
            }
            if (text.EndsWith(" SDF", StringComparison.OrdinalIgnoreCase))
            {
                return text.Substring(0, text.Length - " SDF".Length);
            }
            return text;
        }

        private static Texture2D GetTMPFontAtlasTexture(object value)
        {
            if ((Object)value == (Object)null)
            {
                return null;
            }
            Texture2D[] atlasTextures = ((TMP_FontAsset)value).atlasTextures;
            if (atlasTextures != null)
            {
                for (int i = 0; i < atlasTextures.Length; i++)
                {
                    if ((Object)(object)atlasTextures[i] != (Object)null)
                    {
                        return atlasTextures[i];
                    }
                }
            }
            string assetPath = AssetDatabase.GetAssetPath((Object)value);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }
            Texture2D val = null;
            Object[] array = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (array != null)
            {
                int num = 0;
                Texture2D val2;
                while (true)
                {
                    if (num >= array.Length)
                    {
                        return val;
                    }
                    Object obj = array[num];
                    val2 = (Texture2D)(object)((obj is Texture2D) ? obj : null);
                    if ((object)val2 != null)
                    {
                        if ((object)val == null)
                        {
                            val = val2;
                        }
                        if (((Object)val2).name.IndexOf("Atlas", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            break;
                        }
                    }
                    num++;
                }
                return val2;
            }
            return null;
        }

        private static bool SyncTMPMaterialAtlasProperties(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null))
            {
                ShaderUtilities.GetShaderPropertyIDs();
                Texture2D val = GetTMPFontAtlasTexture(value2);
                if (!((Object)(object)val == (Object)null))
                {
                    bool flag = false;
                    if (((Material)value).HasProperty(ShaderUtilities.ID_MainTex) && (Object)(object)((Material)value).GetTexture(ShaderUtilities.ID_MainTex) != (Object)(object)val)
                    {
                        ((Material)value).SetTexture(ShaderUtilities.ID_MainTex, (Texture)(object)val);
                        flag = true;
                    }
                    float num = ((((Texture)val).width > 0) ? ((Texture)val).width : Mathf.Max(1, ((TMP_FontAsset)value2).atlasWidth));
                    float num2 = ((((Texture)val).height > 0) ? ((Texture)val).height : Mathf.Max(1, ((TMP_FontAsset)value2).atlasHeight));
                    float num3 = Mathf.Max(1f, (float)((TMP_FontAsset)value2).atlasPadding + 1f);
                    if (((Material)value).HasProperty(ShaderUtilities.ID_TextureWidth) && !Mathf.Approximately(((Material)value).GetFloat(ShaderUtilities.ID_TextureWidth), num))
                    {
                        ((Material)value).SetFloat(ShaderUtilities.ID_TextureWidth, num);
                        flag = true;
                    }
                    if (((Material)value).HasProperty(ShaderUtilities.ID_TextureHeight) && !Mathf.Approximately(((Material)value).GetFloat(ShaderUtilities.ID_TextureHeight), num2))
                    {
                        ((Material)value).SetFloat(ShaderUtilities.ID_TextureHeight, num2);
                        flag = true;
                    }
                    if (((Material)value).HasProperty(ShaderUtilities.ID_GradientScale) && !Mathf.Approximately(((Material)value).GetFloat(ShaderUtilities.ID_GradientScale), num3))
                    {
                        ((Material)value).SetFloat(ShaderUtilities.ID_GradientScale, num3);
                        flag = true;
                    }
                    if (((Material)value).HasProperty(ShaderUtilities.ID_WeightNormal) && !Mathf.Approximately(((Material)value).GetFloat(ShaderUtilities.ID_WeightNormal), ((TMP_FontAsset)value2).normalStyle))
                    {
                        ((Material)value).SetFloat(ShaderUtilities.ID_WeightNormal, ((TMP_FontAsset)value2).normalStyle);
                        flag = true;
                    }
                    if (((Material)value).HasProperty(ShaderUtilities.ID_WeightBold) && !Mathf.Approximately(((Material)value).GetFloat(ShaderUtilities.ID_WeightBold), ((TMP_FontAsset)value2).boldStyle))
                    {
                        ((Material)value).SetFloat(ShaderUtilities.ID_WeightBold, ((TMP_FontAsset)value2).boldStyle);
                        flag = true;
                    }
                    ShaderUtilities.UpdateShaderRatios((Material)value);
                    if (flag)
                    {
                        EditorUtility.SetDirty((Object)value);
                    }
                    return flag;
                }
                return false;
            }
            return false;
        }

        private static Material EnsureTMPFontMaterial(object value2)
        {
            if ((Object)value2 == (Object)null)
            {
                return null;
            }
            string assetPath = AssetDatabase.GetAssetPath((Object)value2);
            Texture2D val = GetTMPFontAtlasTexture(value2);
            bool flag = false;
            if ((Object)(object)val != (Object)null)
            {
                if (((TMP_FontAsset)value2).atlasTextures != null && ((TMP_FontAsset)value2).atlasTextures.Length != 0)
                {
                    if ((Object)(object)((TMP_FontAsset)value2).atlasTextures[0] == (Object)null)
                    {
                        ((TMP_FontAsset)value2).atlasTextures[0] = val;
                        flag = true;
                    }
                }
                else
                {
                    ((TMP_FontAsset)value2).atlasTextures = (Texture2D[])(object)new Texture2D[1] { val };
                    flag = true;
                }
            }
            Material val2 = ((TMP_Asset)value2).material;
            string value = ((!((Object)(object)val2 != (Object)null)) ? null : AssetDatabase.GetAssetPath((Object)(object)val2));
            if (((Object)(object)val2 == (Object)null || string.IsNullOrWhiteSpace(value)) && !string.IsNullOrWhiteSpace(assetPath) && assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                string text = GetTMPFontBaseName(value2, assetPath) + " Atlas Material";
                string text2 = BuildMaterialAssetPath(assetPath, text, false);
                Material val3 = ((!string.IsNullOrWhiteSpace(text2)) ? AssetDatabase.LoadAssetAtPath<Material>(text2) : null);
                if ((Object)(object)val3 != (Object)null)
                {
                    val2 = val3;
                }
                else
                {
                    Material val4 = ((!((Object)(object)TMP_Settings.defaultFontAsset != (Object)null)) ? null : ((TMP_Asset)TMP_Settings.defaultFontAsset).material);
                    if ((Object)(object)val2 == (Object)null)
                    {
                        val2 = CloneDistanceFieldMaterial(val4, true);
                        if ((Object)(object)val2 == (Object)null)
                        {
                            Shader val5 = Shader.Find("TextMeshPro/Distance Field");
                            if ((Object)(object)val5 != (Object)null)
                            {
                                val2 = new Material(val5);
                            }
                        }
                    }
                    if ((Object)(object)val2 != (Object)null)
                    {
                        ((Object)val2).name = text;
                        AssetDatabase.CreateAsset((Object)(object)val2, text2);
                        val2 = AssetDatabase.LoadAssetAtPath<Material>(text2) ?? val2;
                    }
                }
                if ((Object)(object)val2 != (Object)null && (Object)(object)((TMP_Asset)value2).material != (Object)(object)val2)
                {
                    ((TMP_Asset)value2).material = val2;
                    flag = true;
                }
            }
            if ((Object)(object)val2 != (Object)null && SyncTMPMaterialAtlasProperties(val2, value2))
            {
                flag = true;
            }
            if (flag)
            {
                EditorUtility.SetDirty((Object)value2);
                if ((Object)(object)val != (Object)null)
                {
                    EditorUtility.SetDirty((Object)(object)val);
                }
                if ((Object)(object)val2 != (Object)null)
                {
                    EditorUtility.SetDirty((Object)(object)val2);
                }
                AssetDatabase.SaveAssets();
            }
            return ((TMP_Asset)value2).material;
        }

        private static Material CloneDistanceFieldMaterial(object value, bool enabled)
        {
            if (!((Object)value == (Object)null))
            {
                ShaderUtilities.GetShaderPropertyIDs();
                Shader val = (enabled ? Shader.Find("TextMeshPro/Distance Field") : null);
                Material val2;
                if ((Object)(object)val != (Object)null && (Object)(object)((Material)value).shader != (Object)(object)val)
                {
                    val2 = new Material(val);
                    val2.CopyPropertiesFromMaterial((Material)value);
                }
                else
                {
                    val2 = new Material((Material)value);
                }
                Texture texture = ((Material)value).GetTexture(ShaderUtilities.ID_MainTex);
                if ((Object)(object)texture != (Object)null && val2.HasProperty(ShaderUtilities.ID_MainTex))
                {
                    val2.SetTexture(ShaderUtilities.ID_MainTex, texture);
                }
                return val2;
            }
            return null;
        }

        internal static string NormalizeFontName(object name)
        {
            if (string.IsNullOrWhiteSpace((string)name))
            {
                return string.Empty;
            }
            return Regex.Replace(Regex.Replace((string)name, "[^A-Za-z0-9]+", " "), "\\s+", " ").Trim();
        }

        private static string CompactFontName(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            return Regex.Replace(NormalizeFontName(value), "\\s+", string.Empty);
        }

        private static int ScoreFontNameMatch(object value, object value2, object value3, object value4)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = ((string)value).Trim();
                if (string.Equals(text, (string)value2, StringComparison.OrdinalIgnoreCase))
                {
                    return 3;
                }
                string text2 = NormalizeFontName(text);
                if (!string.Equals(text, (string)value3, StringComparison.OrdinalIgnoreCase) && !string.Equals(text2, (string)value2, StringComparison.OrdinalIgnoreCase) && !string.Equals(text2, (string)value3, StringComparison.OrdinalIgnoreCase))
                {
                    string text3 = CompactFontName(text2);
                    if (!string.IsNullOrEmpty(text3) && string.Equals(text3, (string)value4, StringComparison.OrdinalIgnoreCase))
                    {
                        return 1;
                    }
                    return 0;
                }
                return 2;
            }
            return 0;
        }

        private static int GetBestFontNameMatchScore(object value, object value2, object value3, params string[] candidates)
        {
            int num = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                int num2 = ScoreFontNameMatch(candidates[i], value, value2, value3);
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

        private static string[] CollectFontNameCandidates(object value, object value2, object value3)
        {
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if ((Object)value2 != (Object)null && !string.IsNullOrWhiteSpace(((TrueTypeFontImporter)value2).fontTTFName))
            {
                hashSet.Add(((TrueTypeFontImporter)value2).fontTTFName.Trim());
            }
            if ((Object)value3 != (Object)null && !string.IsNullOrWhiteSpace(((Object)value3).name))
            {
                hashSet.Add(((Object)value3).name.Trim());
            }
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension((string)value);
            if (!string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                hashSet.Add(fileNameWithoutExtension.Trim());
            }
            foreach (string item in ReadFontNamesFromMeta(value))
            {
                hashSet.Add(item);
            }
            return hashSet.ToArray();
        }

        private static IEnumerable<string> ReadFontNamesFromMeta(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                yield break;
            }
            string text = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }
            string path = ((string)value + ".meta").Replace('/', Path.DirectorySeparatorChar);
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
                    if (!string.Equals(text2, "fontNames:", StringComparison.Ordinal))
                    {
                        if (string.Equals(text2, "fontNames: []", StringComparison.Ordinal))
                        {
                            yield break;
                        }
                    }
                    else
                    {
                        readingFontNames = true;
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

        internal static TMP_FontAsset ResolveTMPFontAsset(object text4)
        {
            if (!string.IsNullOrWhiteSpace((string)text4))
            {
                string text = NormalizeFontName(text4);
                string text2 = CompactFontName(text4);
                Font val = ResolveLegacyFont(text4);
                string text3 = null;
                if ((Object)(object)val != (Object)null)
                {
                    string assetPath = AssetDatabase.GetAssetPath((Object)(object)val);
                    if (!string.IsNullOrWhiteSpace(assetPath))
                    {
                        text3 = AssetDatabase.AssetPathToGUID(assetPath);
                    }
                }
                TMP_FontAsset val2 = null;
                int num = 0;
                string[] array = AssetDatabase.FindAssets("t:TMP_FontAsset");
                int num2 = 0;
                TMP_FontAsset val3;
                while (true)
                {
                    if (num2 < array.Length)
                    {
                        val3 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(array[num2]));
                        if (!((Object)(object)val3 == (Object)null))
                        {
                            if ((Object)(object)val != (Object)null && MatchesSourceFont(val3, val, text3))
                            {
                                break;
                            }
                            string[] array2 = new string[3];
                            FaceInfo faceInfo = val3.faceInfo;
                            array2[0] = faceInfo.familyName;
                            array2[1] = ((Object)val3).name;
                            array2[2] = ((!((Object)(object)val3.sourceFontFile != (Object)null)) ? null : ((Object)val3.sourceFontFile).name);
                            int num3 = GetBestFontNameMatchScore(text4, text, text2, array2);
                            if (num3 > num)
                            {
                                val2 = val3;
                                num = num3;
                            }
                        }
                        num2++;
                        continue;
                    }
                    if (!((Object)(object)val2 != (Object)null))
                    {
                        if ((Object)(object)val != (Object)null)
                        {
                            return CreateTMPFontAsset(val);
                        }
                        return null;
                    }
                    EnsureTMPFontMaterial(val2);
                    return val2;
                }
                EnsureTMPFontMaterial(val3);
                return val3;
            }
            return null;
        }

        private static bool MatchesSourceFont(object value, object value2, object value3)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null))
            {
                if ((Object)(object)((TMP_FontAsset)value).sourceFontFile == (Object)value2)
                {
                    return true;
                }
                if (!string.IsNullOrWhiteSpace((string)value3))
                {
                    if (string.Equals(((TMP_FontAsset)value).creationSettings.sourceFontFileGUID, (string)value3, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if ((Object)(object)((TMP_FontAsset)value).sourceFontFile != (Object)null)
                    {
                        string assetPath = AssetDatabase.GetAssetPath((Object)(object)((TMP_FontAsset)value).sourceFontFile);
                        if (!string.IsNullOrWhiteSpace(assetPath) && string.Equals(AssetDatabase.AssetPathToGUID(assetPath), (string)value3, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            return false;
        }

        private static TMP_FontAsset CreateTMPFontAsset(object value)
        {
            if (!((Object)value == (Object)null))
            {
                if ((Object)(object)TMP_Settings.instance == (Object)null)
                {
                    Debug.LogWarning((object)"Unable to create TMP font asset because TMP Essential Resources are missing.");
                    return null;
                }
                ShaderUtilities.GetShaderPropertyIDs();
                string assetPath = AssetDatabase.GetAssetPath((Object)value);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    AssetImporter atPath = AssetImporter.GetAtPath(assetPath);
                    TrueTypeFontImporter val = (TrueTypeFontImporter)(object)((atPath is TrueTypeFontImporter) ? atPath : null);
                    if ((Object)(object)val != (Object)null && !val.includeFontData)
                    {
                        val.includeFontData = true;
                        ((AssetImporter)val).SaveAndReimport();
                        value = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
                        if ((Object)value == (Object)null)
                        {
                            return null;
                        }
                    }
                    string text = AssetDatabase.AssetPathToGUID(assetPath);
                    TMP_FontAsset val2 = FindTMPFontAssetBySourceFont(value, text);
                    if (!((Object)(object)val2 != (Object)null))
                    {
                        string text2 = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
                        if (string.IsNullOrWhiteSpace(text2))
                        {
                            return null;
                        }
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath);
                        string text3 = AssetDatabase.GenerateUniqueAssetPath(text2 + "/" + fileNameWithoutExtension + " SDF.asset");
                        TMP_FontAsset val3 = TMP_FontAsset.CreateFontAsset((Font)value);
                        if (!((Object)(object)val3 == (Object)null))
                        {
                            ((Object)val3).name = Path.GetFileNameWithoutExtension(text3);
                            Texture2D val4 = ((val3.atlasTextures != null && val3.atlasTextures.Length != 0) ? val3.atlasTextures[0] : null);
                            if ((Object)(object)val4 != (Object)null)
                            {
                                ((Object)val4).name = fileNameWithoutExtension + " Atlas";
                            }
                            Material material = ((TMP_Asset)val3).material;
                            Material val5 = CloneDistanceFieldMaterial(material, true);
                            if ((Object)(object)val5 != (Object)null)
                            {
                                ((Object)val5).name = fileNameWithoutExtension + " Atlas Material";
                                if ((Object)(object)val4 != (Object)null && val5.HasProperty(ShaderUtilities.ID_MainTex))
                                {
                                    val5.SetTexture(ShaderUtilities.ID_MainTex, (Texture)(object)val4);
                                }
                                if (val5.HasProperty(ShaderUtilities.ID_TextureWidth))
                                {
                                    val5.SetFloat(ShaderUtilities.ID_TextureWidth, (float)val3.atlasWidth);
                                }
                                if (val5.HasProperty(ShaderUtilities.ID_TextureHeight))
                                {
                                    val5.SetFloat(ShaderUtilities.ID_TextureHeight, (float)val3.atlasHeight);
                                }
                                if (val5.HasProperty(ShaderUtilities.ID_GradientScale))
                                {
                                    val5.SetFloat(ShaderUtilities.ID_GradientScale, (float)val3.atlasPadding + 1f);
                                }
                                if (val5.HasProperty(ShaderUtilities.ID_WeightNormal))
                                {
                                    val5.SetFloat(ShaderUtilities.ID_WeightNormal, val3.normalStyle);
                                }
                                if (val5.HasProperty(ShaderUtilities.ID_WeightBold))
                                {
                                    val5.SetFloat(ShaderUtilities.ID_WeightBold, val3.boldStyle);
                                }
                                ((TMP_Asset)val3).material = val5;
                            }
                            AssetDatabase.CreateAsset((Object)(object)val3, text3);
                            if ((Object)(object)val4 != (Object)null)
                            {
                                AssetDatabase.AddObjectToAsset((Object)(object)val4, (Object)(object)val3);
                            }
                            if ((Object)(object)((TMP_Asset)val3).material != (Object)null)
                            {
                                string text4 = BuildMaterialAssetPath(text3, ((Object)((TMP_Asset)val3).material).name, true);
                                AssetDatabase.CreateAsset((Object)(object)((TMP_Asset)val3).material, text4);
                            }
                            if ((Object)(object)material != (Object)null && (Object)(object)material != (Object)(object)((TMP_Asset)val3).material)
                            {
                                Object.DestroyImmediate((Object)(object)material);
                            }
                            FontAssetCreationSettings creationSettings = val3.creationSettings;
                            creationSettings.sourceFontFileName = ((Object)value).name;
                            creationSettings.sourceFontFileGUID = text;
                            creationSettings.pointSizeSamplingMode = 0;
                            FaceInfo faceInfo = val3.faceInfo;
                            creationSettings.pointSize = Mathf.RoundToInt((float)faceInfo.pointSize);
                            creationSettings.padding = val3.atlasPadding;
                            creationSettings.packingMode = 0;
                            creationSettings.atlasWidth = val3.atlasWidth;
                            creationSettings.atlasHeight = val3.atlasHeight;
                            creationSettings.characterSetSelectionMode = 7;
                            creationSettings.characterSequence = string.Empty;
                            creationSettings.referencedFontAssetGUID = string.Empty;
                            creationSettings.referencedTextAssetGUID = string.Empty;
                            creationSettings.fontStyle = 0;
                            creationSettings.fontStyleModifier = 0f;
                            creationSettings.renderMode = (int)val3.atlasRenderMode;
                            creationSettings.includeFontFeatures = false;
                            val3.creationSettings = creationSettings;
                            EditorUtility.SetDirty((Object)(object)val3);
                            if ((Object)(object)val4 != (Object)null)
                            {
                                EditorUtility.SetDirty((Object)(object)val4);
                            }
                            if ((Object)(object)((TMP_Asset)val3).material != (Object)null)
                            {
                                EditorUtility.SetDirty((Object)(object)((TMP_Asset)val3).material);
                            }
                            AssetDatabase.SaveAssets();
                            TMP_FontAsset val6 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(text3);
                            if ((Object)(object)val6 == (Object)null)
                            {
                                AssetDatabase.ImportAsset(text3, (ImportAssetOptions)8);
                                val6 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(text3);
                            }
                            if ((object)val6 == null)
                            {
                                val6 = val3;
                            }
                            EnsureTMPFontMaterial(val6);
                            return val6;
                        }
                        return null;
                    }
                    EnsureTMPFontMaterial(val2);
                    return val2;
                }
                return null;
            }
            return null;
        }

        private static TMP_FontAsset FindTMPFontAssetBySourceFont(object value, object value2)
        {
            if ((Object)value == (Object)null)
            {
                return null;
            }
            string[] array = AssetDatabase.FindAssets("t:TMP_FontAsset");
            int num = 0;
            TMP_FontAsset val;
            while (true)
            {
                if (num < array.Length)
                {
                    val = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(array[num]));
                    if (MatchesSourceFont(val, value, value2))
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return null;
            }
            return val;
        }

        private static void EnsureTMPFontContainsCharacters(object value, object value2)
        {
            if ((Object)value == (Object)null || string.IsNullOrEmpty((string)value2))
            {
                return;
            }
            EnsureTMPFontMaterial(value);
            if ((int)((TMP_FontAsset)value).atlasPopulationMode != 1 || ((TMP_FontAsset)value).HasCharacters((string)value2))
            {
                return;
            }
            string text = default(string);
            ((TMP_FontAsset)value).TryAddCharacters((string)value2, out text, false);
            EnsureTMPFontMaterial(value);
            EditorUtility.SetDirty((Object)value);
            if ((Object)(object)((TMP_Asset)value).material != (Object)null)
            {
                EditorUtility.SetDirty((Object)(object)((TMP_Asset)value).material);
            }
            if (((TMP_FontAsset)value).atlasTextures != null)
            {
                for (int i = 0; i < ((TMP_FontAsset)value).atlasTextures.Length; i++)
                {
                    if ((Object)(object)((TMP_FontAsset)value).atlasTextures[i] != (Object)null)
                    {
                        EditorUtility.SetDirty((Object)(object)((TMP_FontAsset)value).atlasTextures[i]);
                    }
                }
            }
            AssetDatabase.SaveAssets();
        }

        internal static Font ResolveLegacyFont(object text4)
        {
            if (string.IsNullOrWhiteSpace((string)text4))
            {
                return null;
            }
            string text = NormalizeFontName(text4);
            string text2 = CompactFontName(text4);
            string[] array = AssetDatabase.FindAssets("t:font");
            Font result = null;
            int num = 0;
            string[] array2 = array;
            for (int i = 0; i < array2.Length; i++)
            {
                string text3 = AssetDatabase.GUIDToAssetPath(array2[i]);
                AssetImporter atPath = AssetImporter.GetAtPath(text3);
                TrueTypeFontImporter val = (TrueTypeFontImporter)(object)((atPath is TrueTypeFontImporter) ? atPath : null);
                Font val2 = AssetDatabase.LoadAssetAtPath<Font>(text3);
                if ((Object)(object)val2 == (Object)null)
                {
                    continue;
                }
                int num2 = GetBestFontNameMatchScore(text4, text, text2, CollectFontNameCandidates(text3, val, val2));
                if (num2 > num)
                {
                    result = val2;
                    num = num2;
                    if (num >= 3)
                    {
                        break;
                    }
                }
            }
            return result;
        }

        internal static Color GetRepresentativeColor(object value, Color color)
        {
            if ((Object)value != (Object)null && ((PsdLayerNode)value).TryGetRepresentativeColor(out Color result))
            {
                return result;
            }
            return color;
        }

        internal void ExportReadmeDoc()
        {
            string text = EditorUtility.SaveFolderPanel("选择文档导出路径", Application.dataPath, (string)null);
            if (string.IsNullOrWhiteSpace(text) || !Directory.Exists(text))
            {
                return;
            }
            string text2 = Path.Combine(text, "Psd2UGUI设计师使用文档.doc");
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
                    foreach (string text3 in typeMatches)
                    {
                        stringBuilder.Append("." + text3 + ", ");
                    }
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine();
                }
            }
            try
            {
                File.WriteAllText(text2, stringBuilder.ToString(), Encoding.UTF8);
                EditorUtility.RevealInFinder(text2);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static string GetTagFamilyDisplayName(object value)
        {
            if ((string)value == "main")
            {
                return "结构标签";
            }
            if ((string)value == "export")
            {
                return "导出标签";
            }
            if ((string)value == "textBackend")
            {
                return "文本后端";
            }
            if ((string)value == "imageType")
            {
                return "Image Type";
            }
            if (!((string)value == "role"))
            {
                return (string)value;
            }
            return "角色标签";
        }

        private static string GetDefaultTagForUIType(GUIType uiType)
        {
            switch (uiType)
            {
            default:
                return uiType.ToString().ToLowerInvariant();
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
            case GUIType.Panel:
                return "panel";
            case GUIType.ToggleGroup:
                return "tgg";
            }
        }

        private string GetPreferredTagForUIType(GUIType uiType)
        {
            UGUIParseRule uGUIParseRule = FindRule(uiType);
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
            return GetDefaultTagForUIType(uiType);
        }

        private static bool TryGetTagFamilyForUIType(GUIType uiType, out string result)
        {
            switch (uiType)
            {
            default:
                result = null;
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
                result = "role";
                return true;
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
            case GUIType.Panel:
            case GUIType.ToggleGroup:
                result = "main";
                return true;
            }
        }

        private static bool IsRasterizedMainUIType(GUIType uiType)
        {
            if (uiType != GUIType.Image)
            {
                return uiType == GUIType.RawImage;
            }
            return true;
        }

        private static bool IsRasterizedRoleUIType(GUIType uiType)
        {
            switch (uiType)
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

        private static GUIType GetUGUIAliasType(GUIType uiType)
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

        private static string EscapeJavaScriptString(object value)
        {
            if (string.IsNullOrEmpty((string)value))
            {
                return string.Empty;
            }
            return ((string)value).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string NormalizeTagLabel(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                return ((string)value).Replace("\\r", " ").Replace("\\n", " ").Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .Trim();
            }
            return string.Empty;
        }

        private static void AddTagFamilyOption(Dictionary<string, List<TagFamilyOption>> lookup, object value2, object value3, object value4)
        {
            string text = (string)value3;
            if (!string.IsNullOrWhiteSpace((string)value2) && !string.IsNullOrWhiteSpace(text))
            {
                if (!lookup.TryGetValue((string)value2, out var value))
                {
                    value = new List<TagFamilyOption>();
                    lookup.Add((string)value2, value);
                }
                if (!value.Any((TagFamilyOption item) => string.Equals(item.Id, text, StringComparison.OrdinalIgnoreCase)))
                {
                    value.Add(new TagFamilyOption
                    {
                        Id = text,
                        Suffix = "." + text,
                        Label = (string.IsNullOrWhiteSpace((string)value4) ? text : NormalizeTagLabel(value4))
                    });
                }
            }
        }

        private static void AddTagAliasMapping(Dictionary<string, TagAliasMapping> lookup, object value2, string text = null, string text2 = null, string text3 = null, string text4 = null, string text5 = null)
        {
            if (!string.IsNullOrWhiteSpace((string)value2))
            {
                value2 = ((string)value2).Trim().TrimStart('.').ToLowerInvariant();
                if (!lookup.TryGetValue((string)value2, out var value))
                {
                    value = new TagAliasMapping();
                    lookup.Add((string)value2, value);
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    value.MainTag = text;
                }
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    value.ExportTag = text2;
                }
                if (!string.IsNullOrWhiteSpace(text3))
                {
                    value.RoleTag = text3;
                }
                if (!string.IsNullOrWhiteSpace(text4))
                {
                    value.ImageTypeTag = text4;
                }
                if (!string.IsNullOrWhiteSpace(text5))
                {
                    value.TextBackendTag = text5;
                }
            }
        }

        private void CollectRuleTagConfiguration(UGUIParseRule value, Dictionary<string, List<TagFamilyOption>> lookup, Dictionary<string, TagAliasMapping> lookup2, RasterizeTagSets value2)
        {
            if (value == null || value.UIType == GUIType.Null || value.TypeMatches == null || value.TypeMatches.Length == 0)
            {
                return;
            }
            string text = GetPreferredTagForUIType(value.UIType);
            string text2 = (string.IsNullOrWhiteSpace(value.UITypeDesc) ? value.UIType.ToString() : $"{value.UIType} {value.UITypeDesc}");
            switch (value.UIType)
            {
            case GUIType.TMPText:
            case GUIType.TMPButton:
            case GUIType.TMPDropdown:
            case GUIType.TMPInputField:
            case GUIType.TMPToggle:
            {
                GUIType gUIType = GetUGUIAliasType(value.UIType);
                string text3 = GetPreferredTagForUIType(gUIType);
                for (int j = 0; j < value.TypeMatches.Length; j++)
                {
                    AddTagAliasMapping(lookup2, value.TypeMatches[j], text3, null, null, null, "tmp");
                }
                return;
            }
            case GUIType.Image:
            {
                AddTagFamilyOption(lookup, "export", text, text2);
                for (int i = 0; i < value.TypeMatches.Length; i++)
                {
                    AddTagAliasMapping(lookup2, value.TypeMatches[i], null, text);
                }
                value2.ExportTags.Add(text);
                return;
            }
            }
            if (!TryGetTagFamilyForUIType(value.UIType, out var text4))
            {
                return;
            }
            AddTagFamilyOption(lookup, text4, text, text2);
            for (int k = 0; k < value.TypeMatches.Length; k++)
            {
                string text5 = value.TypeMatches[k];
                if (!string.IsNullOrWhiteSpace(text5))
                {
                    if (string.Equals(text4, "main", StringComparison.OrdinalIgnoreCase))
                    {
                        AddTagAliasMapping(lookup2, text5, text);
                    }
                    else
                    {
                        AddTagAliasMapping(lookup2, text5, null, null, text);
                    }
                }
            }
            if (IsRasterizedMainUIType(value.UIType))
            {
                value2.MainTags.Add(text);
            }
            else if (IsRasterizedRoleUIType(value.UIType))
            {
                value2.RoleTags.Add(text);
            }
        }

        private string BuildTagConfigurationScript()
        {
            Dictionary<string, List<TagFamilyOption>> dictionary = new Dictionary<string, List<TagFamilyOption>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TagAliasMapping> dictionary2 = new Dictionary<string, TagAliasMapping>(StringComparer.OrdinalIgnoreCase);
            RasterizeTagSets value2 = new RasterizeTagSets();
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    CollectRuleTagConfiguration(rules[i], dictionary, dictionary2, value2);
                }
            }
            AddTagFamilyOption(dictionary, "textBackend", "tmp", "TMP文本后端");
            AddTagFamilyOption(dictionary, "textBackend", "ugui", "原生文本后端");
            AddTagAliasMapping(dictionary2, "tmp", null, null, null, null, "tmp");
            AddTagAliasMapping(dictionary2, "ugui", null, null, null, null, "ugui");
            AddTagFamilyOption(dictionary, "imageType", "simple", "普通");
            AddTagFamilyOption(dictionary, "imageType", "sliced", "九宫格");
            AddTagFamilyOption(dictionary, "imageType", "tiled", "平铺");
            AddTagFamilyOption(dictionary, "imageType", "filled", "填充");
            AddTagAliasMapping(dictionary2, "simple", null, null, null, "simple");
            AddTagAliasMapping(dictionary2, "sliced", null, null, null, "sliced");
            AddTagAliasMapping(dictionary2, "tiled", null, null, null, "tiled");
            AddTagAliasMapping(dictionary2, "filled", null, null, null, "filled");
            string[] array = new string[5] { "export", "main", "textBackend", "imageType", "role" };
            TagConfigScriptBuilderContext value3 = default(TagConfigScriptBuilderContext);
            value3.Builder = new StringBuilder();
            value3.Builder.AppendLine("var TAG_CONFIG = {");
            value3.Builder.AppendLine("    canonicalOrder: [\"export\", \"main\", \"textBackend\", \"imageType\", \"role\"],");
            value3.Builder.AppendLine("    reuseMarkers: [");
            value3.Builder.AppendLine("        { \"id\": \"ref\", \"prefix\": \"ref \", \"label\": \"ref 复用共享图片资源\" },");
            value3.Builder.AppendLine("        { \"id\": \"refp\", \"prefix\": \"refp \", \"label\": \"refp 复用共享预制体\" }");
            value3.Builder.AppendLine("    ],");
            value3.Builder.AppendLine("    familyLabels: {");
            for (int j = 0; j < array.Length; j++)
            {
                string text = array[j];
                value3.Builder.Append("        \"").Append(text).Append("\": \"")
                    .Append(EscapeJavaScriptString(GetTagFamilyDisplayName(text)))
                    .Append("\"");
                value3.Builder.AppendLine((j < array.Length - 1) ? "," : string.Empty);
            }
            value3.Builder.AppendLine("    },");
            value3.Builder.AppendLine("    families: {");
            for (int k = 0; k < array.Length; k++)
            {
                string text2 = array[k];
                dictionary.TryGetValue(text2, out var value);
                value = value ?? new List<TagFamilyOption>();
                value3.Builder.Append("        \"").Append(text2).Append("\": [")
                    .AppendLine();
                for (int l = 0; l < value.Count; l++)
                {
                    TagFamilyOption value4 = value[l];
                    value3.Builder.Append("            { \"id\": \"").Append(EscapeJavaScriptString(value4.Id)).Append("\", \"suffix\": \"")
                        .Append(EscapeJavaScriptString(value4.Suffix))
                        .Append("\", \"label\": \"")
                        .Append(EscapeJavaScriptString(value4.Label))
                        .Append("\" }");
                    value3.Builder.AppendLine((l >= value.Count - 1) ? string.Empty : ",");
                }
                value3.Builder.Append("        ]");
                value3.Builder.AppendLine((k >= array.Length - 1) ? string.Empty : ",");
            }
            value3.Builder.AppendLine("    },");
            value3.Builder.AppendLine("    rasterizeAsImage: {");
            AppendRasterizeTagMap("main", value2.MainTags, true, ref value3);
            AppendRasterizeTagMap("export", value2.ExportTags, true, ref value3);
            AppendRasterizeTagMap("role", value2.RoleTags, false, ref value3);
            value3.Builder.AppendLine("    },");
            value3.Builder.AppendLine("    aliasMap: {");
            string[] array2 = dictionary2.Keys.OrderBy((string alias) => alias, StringComparer.OrdinalIgnoreCase).ToArray();
            TagConfigPropertyWriteState value5 = default(TagConfigPropertyWriteState);
            for (int num = 0; num < array2.Length; num++)
            {
                string text3 = array2[num];
                TagAliasMapping value6 = dictionary2[text3];
                value3.Builder.Append("        \"").Append(EscapeJavaScriptString(text3)).Append("\": {");
                value5.HasWrittenProperty = false;
                AppendOptionalAliasProperty("main", value6.MainTag, ref value3, ref value5);
                AppendOptionalAliasProperty("export", value6.ExportTag, ref value3, ref value5);
                AppendOptionalAliasProperty("role", value6.RoleTag, ref value3, ref value5);
                AppendOptionalAliasProperty("imageType", value6.ImageTypeTag, ref value3, ref value5);
                AppendOptionalAliasProperty("textBackend", value6.TextBackendTag, ref value3, ref value5);
                value3.Builder.Append(" }");
                value3.Builder.AppendLine((num >= array2.Length - 1) ? string.Empty : ",");
            }
            value3.Builder.AppendLine("    }");
            value3.Builder.Append("};");
            return value3.Builder.ToString();
        }

        private string BuildReuseDefaultsScript()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("var REUSE_DEFAULT_SETTINGS = {");
            stringBuilder.Append("    imageRoot: \"").Append(EscapeJavaScriptString(ResolveAbsoluteOutputPath(sharedAssetsOutput))).AppendLine("\",");
            stringBuilder.Append("    prefabRoot: \"").Append(EscapeJavaScriptString(ResolveAbsoluteOutputPath(sharedPrefabOutput))).AppendLine("\"");
            stringBuilder.Append("};");
            return stringBuilder.ToString();
        }

        private static string ResolveAbsoluteOutputPath(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = ((string)value).Trim();
                string text3;
                if (!Path.IsPathRooted(text))
                {
                    string text2 = Directory.GetParent(Application.dataPath)?.FullName;
                    if (string.IsNullOrWhiteSpace(text2))
                    {
                        return string.Empty;
                    }
                    text3 = Path.GetFullPath(Path.Combine(text2, text));
                }
                else
                {
                    text3 = text;
                }
                return text3.Replace('\\', '/');
            }
            return string.Empty;
        }

        private static bool ReplaceScriptConfigurationBlock(object value, object value2, object value3, object value4, out string result)
        {
            result = null;
            string path = Psd2UIFormPluginPathResolver.AssetPathToAbsolutePath(value);
            if (!File.Exists(path))
            {
                result = "PS脚本文件不存在：\n" + (string)value;
                return false;
            }
            string input = File.ReadAllText(path, Encoding.UTF8);
            Regex regex = new Regex((string)value2, RegexOptions.Multiline);
            if (regex.IsMatch(input))
            {
                string contents = regex.Replace(input, (string)value3, 1);
                File.WriteAllText(path, contents, Encoding.UTF8);
                AssetDatabase.ImportAsset((string)value);
                return true;
            }
            result = "PS脚本中未找到 " + (string)value4 + "：\n" + (string)value;
            return false;
        }

        private static bool UpdateTagConfigurationBlock(object value, object value2, out string result)
        {
            return ReplaceScriptConfigurationBlock(value, "var\\s+TAG_CONFIG\\s*=\\s*\\{[\\s\\S]*?\\};", value2, "TAG_CONFIG", out result);
        }

        private static bool UpdateReuseDefaultsBlock(object value, object value2, out string result)
        {
            return ReplaceScriptConfigurationBlock(value, "var\\s+REUSE_DEFAULT_SETTINGS\\s*=\\s*\\{[\\s\\S]*?\\};", value2, "REUSE_DEFAULT_SETTINGS", out result);
        }

        private static string NormalizeInstallPath(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            string text = Environment.ExpandEnvironmentVariables(((string)value).Trim().Trim('"'));
            if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                text = Path.GetDirectoryName(text);
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            try
            {
                return Path.GetFullPath(text);
            }
            catch
            {
                return null;
            }
        }

        private static void AddExistingInstallPath(HashSet<string> texts, object value)
        {
            string text = NormalizeInstallPath(value);
            if (!string.IsNullOrWhiteSpace(text) && Directory.Exists(text))
            {
                texts.Add(text);
            }
        }

        private static Type ResolveType(object value)
        {
            Type type = Type.GetType((string)value);
            if (!(type != null))
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    type = assemblies[i].GetType((string)value, throwOnError: false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                return null;
            }
            return type;
        }

        private static object ParseEnumValue(Type type, object value)
        {
            if (type == null)
            {
                return null;
            }
            return Enum.Parse(type, (string)value);
        }

        private static object InvokeReflectedMethod(object value, object value2, object value3, params object[] args)
        {
            if (value == null)
            {
                return null;
            }
            Type type = (value as Type) ?? value.GetType();
            MethodInfo method;
            if (value3 != null)
            {
                method = type.GetMethod((string)value2, (Type[])value3);
                if ((object)method != null)
                {
                    goto IL_0033;
                }
            }
            else
            {
                method = type.GetMethod((string)value2);
                if ((object)method != null)
                {
                    goto IL_0033;
                }
            }
            return null;
            IL_0033:
            return method.Invoke((!(value is Type)) ? value : null, args);
        }

        private static string GetRegistryStringValue(object value, object value2)
        {
            return InvokeReflectedMethod(value, "GetValue", new Type[1] { typeof(string) }, value2) as string;
        }

        private static object OpenRegistrySubKey(object value, object value2)
        {
            return InvokeReflectedMethod(value, "OpenSubKey", new Type[1] { typeof(string) }, value2);
        }

        private static string[] GetRegistrySubKeyNames(object value)
        {
            return (InvokeReflectedMethod(value, "GetSubKeyNames", Type.EmptyTypes) as string[]) ?? Array.Empty<string>();
        }

        private static object OpenRegistryBaseKey(object value, object value2)
        {
            Type type = ResolveType("Microsoft.Win32.RegistryKey");
            Type type2 = ResolveType("Microsoft.Win32.RegistryHive");
            Type type3 = ResolveType("Microsoft.Win32.RegistryView");
            if (!(type == null) && !(type2 == null) && !(type3 == null))
            {
                object obj = ParseEnumValue(type2, value);
                object obj2 = ParseEnumValue(type3, value2);
                if (obj != null && obj2 != null)
                {
                    return InvokeReflectedMethod(type, "OpenBaseKey", new Type[2] { type2, type3 }, obj, obj2);
                }
                return null;
            }
            return null;
        }

        private static void CollectRegistryInstallPaths(object value, HashSet<string> texts)
        {
            if (value != null)
            {
                AddExistingInstallPath(texts, GetRegistryStringValue(value, "ApplicationPath"));
                AddExistingInstallPath(texts, GetRegistryStringValue(value, "InstallPath"));
                AddExistingInstallPath(texts, GetRegistryStringValue(value, "Path"));
                AddExistingInstallPath(texts, GetRegistryStringValue(value, null));
            }
        }

        private static void CollectPhotoshopRegistryPaths(object value, object value2, HashSet<string> texts)
        {
            object obj = OpenRegistryBaseKey(value, value2);
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
                            CollectRegistryInstallPaths(obj3, texts);
                            object obj4 = ((obj3 == null) ? null : OpenRegistrySubKey(obj3, "ApplicationPath"));
                            using (obj4 as IDisposable)
                            {
                                CollectRegistryInstallPaths(obj4, texts);
                            }
                        }
                    }
                }
            }
        }

        private static void CollectPhotoshopUninstallPaths(object value, object value2, HashSet<string> texts)
        {
            object obj = OpenRegistryBaseKey(value, value2);
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
                            string text = GetRegistryStringValue(obj3, "DisplayName");
                            if (!string.IsNullOrWhiteSpace(text) && text.IndexOf("Adobe Photoshop", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                AddExistingInstallPath(texts, GetRegistryStringValue(obj3, "InstallLocation"));
                                AddExistingInstallPath(texts, GetRegistryStringValue(obj3, "DisplayIcon"));
                            }
                        }
                    }
                }
            }
        }

        private static string[] FindPhotoshopScriptDirectories()
        {
            if ((int)Application.platform != 0)
            {
                if ((int)Application.platform != 7)
                {
                    return Array.Empty<string>();
                }
                HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string[] array = new string[2] { "LocalMachine", "CurrentUser" };
                string[] array2 = new string[2] { "Registry64", "Registry32" };
                for (int i = 0; i < array.Length; i++)
                {
                    for (int j = 0; j < array2.Length; j++)
                    {
                        try
                        {
                            CollectPhotoshopRegistryPaths(array[i], array2[j], hashSet);
                        }
                        catch
                        {
                        }
                        try
                        {
                            CollectPhotoshopUninstallPaths(array[i], array2[j], hashSet);
                        }
                        catch
                        {
                        }
                    }
                }
                return hashSet.Select((string path) => Path.Combine(path, "Presets", "Scripts")).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string path) => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            return FindMacPhotoshopScriptDirectories();
        }

        private static string[] FindMacPhotoshopScriptDirectories()
        {
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> list = new List<string>();
            try
            {
                if (Directory.Exists("/Applications"))
                {
                    list.AddRange(Directory.GetDirectories("/Applications", "Adobe Photoshop*.app", SearchOption.TopDirectoryOnly));
                }
            }
            catch
            {
            }
            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                string path = Path.Combine(folderPath, "Applications");
                try
                {
                    if (Directory.Exists(path))
                    {
                        list.AddRange(Directory.GetDirectories(path, "Adobe Photoshop*.app", SearchOption.TopDirectoryOnly));
                    }
                }
                catch
                {
                }
            }
            for (int i = 0; i < list.Count; i++)
            {
                AddMacPhotoshopScriptDirectories(hashSet, list[i]);
            }
            return hashSet.OrderBy((string result) => result, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static void AddMacPhotoshopScriptDirectories(HashSet<string> texts, object value)
        {
            if (texts == null || string.IsNullOrWhiteSpace((string)value))
            {
                return;
            }
            string[] array = new string[3]
            {
                Path.Combine((string)value, "Presets", "Scripts"),
                Path.Combine((string)value, "Contents", "Required", "Presets", "Scripts"),
                Path.Combine((string)value, "Contents", "Resources", "Presets", "Scripts")
            };
            foreach (string text in array)
            {
                if (Directory.Exists(text))
                {
                    texts.Add(text);
                }
            }
        }

        private static void DeployPhotoshopScripts(out List<string> result, out List<string> result2)
        {
            result = new List<string>();
            result2 = new List<string>();
            string[] array = FindPhotoshopScriptDirectories();
            if (array.Length == 0)
            {
                result2.Add("未找到 Photoshop 安装目录，已只更新工程内 jsx 文件。");
                return;
            }
            string[] array2 = new string[2]
            {
                Psd2UIFormPluginPathResolver.GetPluginAbsolutePath("PSScript/PSD2UGUI-LayerTagMenu.jsx"),
                Psd2UIFormPluginPathResolver.GetPluginAbsolutePath("PSScript/PSD2UIForm-导出PSD.jsx")
            };
            for (int i = 0; i < array2.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(array2[i]) || !File.Exists(array2[i]))
                {
                    result2.Add("本地脚本不存在，无法自动部署：\n" + (array2[i] ?? "(null)"));
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
                    result.Add(text);
                }
                catch (Exception ex)
                {
                    result2.Add("覆盖 Photoshop 脚本目录失败：\n" + text + "\n" + ex.Message);
                }
            }
        }

        internal void ExportPhotoshopScripts()
        {
            if (rules == null || rules.Length == 0)
            {
                return;
            }
            string text = BuildTagConfigurationScript();
            string text2 = BuildReuseDefaultsScript();
            List<string> list = new List<string>();
            string text3 = Psd2UIFormPluginPathResolver.CombinePluginAssetPath("PSScript/PSD2UGUI-LayerTagMenu.jsx");
            string text4 = Psd2UIFormPluginPathResolver.CombinePluginAssetPath("PSScript/PSD2UIForm-导出PSD.jsx");
            string[] array = new string[2] { text3, text4 };
            for (int i = 0; i < array.Length; i++)
            {
                if (!UpdateTagConfigurationBlock(array[i], text, out var item))
                {
                    list.Add(item);
                }
            }
            if (!UpdateReuseDefaultsBlock(text3, text2, out var item2))
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
            EditorUtility.DisplayDialog((list3.Count > 0) ? "导出PS脚本工具 完成" : "导出PS脚本工具 成功", stringBuilder.ToString(), "确定");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(text3);
        }

        [CompilerGenerated]
        private GUIType GetDefaultUIType(ref LayerTypeResolutionContext value)
        {
            return value.LayerType switch
            {
                PsdLayerType.TextLayer => defaultTextType, 
                PsdLayerType.LayerGroup => GUIType.Null, 
                PsdLayerType.FillLayer => GUIType.FillColor, 
                _ => defaultImageType, 
            };
        }

        [CompilerGenerated]
        private static void AppendRasterizeTagMap(object value2, IEnumerable<string> texts, bool enabled, ref TagConfigScriptBuilderContext value3)
        {
            string[] array = texts.Where((string value) => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string value) => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            value3.Builder.Append("        \"").Append((string)value2).Append("\": {");
            if (array.Length != 0)
            {
                value3.Builder.AppendLine();
                for (int num = 0; num < array.Length; num++)
                {
                    value3.Builder.Append("            \"").Append(EscapeJavaScriptString(array[num])).Append("\": true");
                    value3.Builder.AppendLine((num < array.Length - 1) ? "," : string.Empty);
                }
                value3.Builder.Append("        }");
            }
            else
            {
                value3.Builder.Append("}");
            }
            value3.Builder.AppendLine(enabled ? "," : string.Empty);
        }

        [CompilerGenerated]
        private static void AppendOptionalAliasProperty(object value, object value2, ref TagConfigScriptBuilderContext value3, ref TagConfigPropertyWriteState value4)
        {
            if (!string.IsNullOrWhiteSpace((string)value2))
            {
                if (value4.HasWrittenProperty)
                {
                    value3.Builder.Append(", ");
                }
                value3.Builder.Append("\"").Append((string)value).Append("\": \"")
                    .Append(EscapeJavaScriptString(value2))
                    .Append("\"");
                value4.HasWrittenProperty = true;
            }
        }

        internal static bool IsUGUIParserObfuscationSentinelNull()
        {
            return (object)s_UGUIParserObfuscationSentinel == null;
        }

        internal static UGUIParser GetUGUIParserObfuscationSentinel()
        {
            return s_UGUIParserObfuscationSentinel;
        }
    }
}
