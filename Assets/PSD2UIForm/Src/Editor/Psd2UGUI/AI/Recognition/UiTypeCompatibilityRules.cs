using System;
using System.Collections.Generic;
using AiPatchValidatorNamespace;
using UGF.EditorTools.Psd2UGUI;

namespace UiTypeCompatibilityRulesNamespace
{
    internal sealed class UiTypeCompatibilityRules
    {
        internal enum UiRoleContentKind
        {

        }

        internal sealed class UiTypeRule
        {
            public GUIType OwnerType;

            public bool RequiresGroupCarrier;

            public GUIType[] AllowedRoleTypes = Array.Empty<GUIType>();

            public GUIType[] RequiredRoleTypes = Array.Empty<GUIType>();

            public GUIType[] AllowedDirectChildControlTypes = Array.Empty<GUIType>();

            internal static UiTypeRule s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static UiTypeRule GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private static readonly HashSet<GUIType> s_SupportedOwnerTypes = new HashSet<GUIType>
        {
            GUIType.Null,
            GUIType.Image,
            GUIType.Text,
            GUIType.Button,
            GUIType.Dropdown,
            GUIType.InputField,
            GUIType.Toggle,
            GUIType.Slider,
            GUIType.ScrollView,
            GUIType.Mask,
            GUIType.FillColor,
            GUIType.Panel,
            GUIType.ToggleGroup
        };

        private static readonly HashSet<GUIType> s_BaseLayerTypes = new HashSet<GUIType>
        {
            GUIType.Null,
            GUIType.Image,
            GUIType.Text,
            GUIType.Mask,
            GUIType.FillColor,
            GUIType.Panel
        };

        private static readonly HashSet<GUIType> s_AuxiliaryRoleTypes = new HashSet<GUIType>
        {
            GUIType.Background,
            GUIType.Button_Highlight,
            GUIType.Button_Press,
            GUIType.Button_Select,
            GUIType.Button_Disable,
            GUIType.Button_Text,
            GUIType.Dropdown_Label,
            GUIType.Dropdown_Arrow,
            GUIType.InputField_Placeholder,
            GUIType.InputField_Text,
            GUIType.Toggle_Checkmark,
            GUIType.Toggle_Label,
            GUIType.Slider_Fill,
            GUIType.Slider_Handle,
            GUIType.ScrollView_Viewport,
            GUIType.ScrollView_HorizontalBarBG,
            GUIType.ScrollView_HorizontalBar,
            GUIType.ScrollView_VerticalBarBG,
            GUIType.ScrollView_VerticalBar
        };

        private static readonly Dictionary<GUIType, UiRoleContentKind> s_AuxiliaryRoleContentKinds = new Dictionary<GUIType, UiRoleContentKind>
        {
            {
                GUIType.Background,
                (UiRoleContentKind)1
            },
            {
                GUIType.Button_Highlight,
                (UiRoleContentKind)1
            },
            {
                GUIType.Button_Press,
                (UiRoleContentKind)1
            },
            {
                GUIType.Button_Select,
                (UiRoleContentKind)1
            },
            {
                GUIType.Button_Disable,
                (UiRoleContentKind)1
            },
            {
                GUIType.Button_Text,
                (UiRoleContentKind)2
            },
            {
                GUIType.Dropdown_Label,
                (UiRoleContentKind)2
            },
            {
                GUIType.Dropdown_Arrow,
                (UiRoleContentKind)1
            },
            {
                GUIType.InputField_Placeholder,
                (UiRoleContentKind)2
            },
            {
                GUIType.InputField_Text,
                (UiRoleContentKind)2
            },
            {
                GUIType.Toggle_Checkmark,
                (UiRoleContentKind)1
            },
            {
                GUIType.Toggle_Label,
                (UiRoleContentKind)2
            },
            {
                GUIType.Slider_Fill,
                (UiRoleContentKind)1
            },
            {
                GUIType.Slider_Handle,
                (UiRoleContentKind)1
            },
            {
                GUIType.ScrollView_Viewport,
                (UiRoleContentKind)1
            },
            {
                GUIType.ScrollView_HorizontalBarBG,
                (UiRoleContentKind)1
            },
            {
                GUIType.ScrollView_HorizontalBar,
                (UiRoleContentKind)1
            },
            {
                GUIType.ScrollView_VerticalBarBG,
                (UiRoleContentKind)1
            },
            {
                GUIType.ScrollView_VerticalBar,
                (UiRoleContentKind)1
            }
        };

        private static readonly Dictionary<GUIType, UiTypeRule> s_RulesByOwnerType = CreateRules();

        private static UiTypeCompatibilityRules s_ObfuscationSentinel;

        internal static bool TryParseOwnerType(object value, out GUIType result)
        {
            return TryParseAllowedUiType(value, s_SupportedOwnerTypes, out result);
        }

        internal static bool TryParseBaseLayerType(object value, out GUIType result)
        {
            return TryParseAllowedUiType(value, s_BaseLayerTypes, out result);
        }

        internal static bool TryParseAuxiliaryRoleType(object value, out GUIType result)
        {
            return TryParseAllowedUiType(value, s_AuxiliaryRoleTypes, out result);
        }

        internal static bool IsSupportedOwnerType(GUIType uiType)
        {
            return s_SupportedOwnerTypes.Contains(NormalizeUiTypeAlias(uiType));
        }

        internal static bool IsBaseLayerType(GUIType uiType)
        {
            return s_BaseLayerTypes.Contains(NormalizeUiTypeAlias(uiType));
        }

        internal static bool IsAuxiliaryRoleType(GUIType uiType)
        {
            return s_AuxiliaryRoleTypes.Contains(NormalizeUiTypeAlias(uiType));
        }

        internal static bool IsUiTypeAlias(GUIType uiType)
        {
            if (uiType != GUIType.RawImage && (uint)(uiType - 12) > 4u)
            {
                return false;
            }
            return true;
        }

        internal static GUIType NormalizeUiTypeAlias(GUIType uiType)
        {
            return uiType switch
            {
                GUIType.TMPText => GUIType.Text, 
                GUIType.TMPButton => GUIType.Button, 
                GUIType.TMPDropdown => GUIType.Dropdown, 
                GUIType.TMPInputField => GUIType.InputField, 
                GUIType.TMPToggle => GUIType.Toggle, 
                GUIType.RawImage => GUIType.Image, 
                _ => uiType, 
            };
        }

        internal static UiTypeRule GetRule(GUIType uiType)
        {
            s_RulesByOwnerType.TryGetValue(NormalizeUiTypeAlias(uiType), out var value);
            return value;
        }

        internal static bool IsRoleAllowed(GUIType uiType, GUIType uiType2)
        {
            UiTypeRule value = GetRule(uiType);
            if (value == null)
            {
                return false;
            }
            uiType2 = NormalizeUiTypeAlias(uiType2);
            int num = 0;
            while (true)
            {
                if (num < value.AllowedRoleTypes.Length)
                {
                    if (value.AllowedRoleTypes[num] == uiType2)
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                return false;
            }
            return true;
        }

        internal static bool IsDirectChildControlAllowed(GUIType uiType, GUIType uiType2)
        {
            UiTypeRule value = GetRule(uiType);
            if (value != null)
            {
                uiType2 = NormalizeUiTypeAlias(uiType2);
                int num = 0;
                while (true)
                {
                    if (num < value.AllowedDirectChildControlTypes.Length)
                    {
                        if (value.AllowedDirectChildControlTypes[num] == uiType2)
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return false;
                }
                return true;
            }
            return false;
        }

        internal static bool RequiresGroupCarrier(GUIType uiType)
        {
            return GetRule(uiType)?.RequiresGroupCarrier ?? false;
        }

        internal static GUIType[] GetAllowedRoleTypes(GUIType uiType)
        {
            UiTypeRule value = GetRule(uiType);
            object obj;
            if (value == null)
            {
                obj = null;
            }
            else
            {
                obj = value.AllowedRoleTypes;
                if (obj != null)
                {
                    goto IL_001b;
                }
            }
            obj = Array.Empty<GUIType>();
            goto IL_001b;
            IL_001b:
            return (GUIType[])obj;
        }

        internal static GUIType[] GetRequiredRoleTypes(GUIType uiType)
        {
            UiTypeRule value = GetRule(uiType);
            object obj;
            if (value == null)
            {
                obj = null;
            }
            else
            {
                obj = value.RequiredRoleTypes;
                if (obj != null)
                {
                    goto IL_001b;
                }
            }
            obj = Array.Empty<GUIType>();
            goto IL_001b;
            IL_001b:
            return (GUIType[])obj;
        }

        internal static UiRoleContentKind GetRoleContentKind(GUIType uiType)
        {
            uiType = NormalizeUiTypeAlias(uiType);
            if (s_AuxiliaryRoleContentKinds.TryGetValue(uiType, out var value))
            {
                return value;
            }
            return (UiRoleContentKind)0;
        }

        internal static bool RoleRequiresTextLayer(GUIType uiType)
        {
            return GetRoleContentKind(uiType) == (UiRoleContentKind)2;
        }

        internal static bool RoleRequiresImageLayer(GUIType uiType)
        {
            return GetRoleContentKind(uiType) == (UiRoleContentKind)1;
        }

        internal static bool RoleAcceptsMultipleImageMembers(GUIType uiType)
        {
            return RoleRequiresImageLayer(uiType);
        }

        internal static bool IsOwnerNodeCompatible(GUIType uiType, object aiAnalysisNodeEntry)
        {
            uiType = NormalizeUiTypeAlias(uiType);
            if (aiAnalysisNodeEntry == null)
            {
                return false;
            }
            if (uiType == GUIType.Text)
            {
                return ((AiAnalysisNodeEntry)aiAnalysisNodeEntry).isTextLayer;
            }
            if (RequiresGroupCarrier(uiType))
            {
                return ((AiAnalysisNodeEntry)aiAnalysisNodeEntry).isGroupLayer;
            }
            if (uiType != GUIType.Image && uiType != GUIType.Mask && uiType != GUIType.FillColor)
            {
                return true;
            }
            return !((AiAnalysisNodeEntry)aiAnalysisNodeEntry).isTextLayer;
        }

        internal static bool IsRoleNodeCompatible(GUIType uiType, object aiAnalysisNodeEntry)
        {
            if (aiAnalysisNodeEntry == null)
            {
                return false;
            }
            if (RoleRequiresTextLayer(uiType))
            {
                return ((AiAnalysisNodeEntry)aiAnalysisNodeEntry).isTextLayer;
            }
            return !((AiAnalysisNodeEntry)aiAnalysisNodeEntry).isTextLayer;
        }

        internal static GUIType ResolveBaseUiType(string label, AiAnalysisNodeEntry node)
        {
            // Base labels describe existing source nodes; only owners may synthesize a carrier.
            if (TryParseBaseLayerType(label, out var type) &&
                (type == GUIType.Null || IsOwnerNodeCompatible(type, node)))
                return type;
            return InferBaseUiType(node);
        }

        internal static GUIType InferBaseUiType(object aiAnalysisNodeEntry)
        {
            if (aiAnalysisNodeEntry != null)
            {
                if (!((AiAnalysisNodeEntry)aiAnalysisNodeEntry).isTextLayer)
                {
                    if (!((AiAnalysisNodeEntry)aiAnalysisNodeEntry).isGroupLayer)
                    {
                        if (AiPatchValidator.TryParsePatchUiType(((AiAnalysisNodeEntry)aiAnalysisNodeEntry).uiType, out var gUIType))
                        {
                            gUIType = NormalizeUiTypeAlias(gUIType);
                            if (IsBaseLayerType(gUIType) &&
                                (gUIType == GUIType.Null || IsOwnerNodeCompatible(gUIType, aiAnalysisNodeEntry)))
                            {
                                return gUIType;
                            }
                        }
                        return GUIType.Image;
                    }
                    return GUIType.Null;
                }
                return GUIType.Text;
            }
            return GUIType.Null;
        }

        private static bool TryParseAllowedUiType(object value, HashSet<GUIType> uiTypes, out GUIType result)
        {
            result = GUIType.Null;
            if (!string.IsNullOrWhiteSpace((string)value) && AiPatchValidator.TryParsePatchUiType(value, out result))
            {
                result = NormalizeUiTypeAlias(result);
                return uiTypes.Contains(result);
            }
            return false;
        }

        private static Dictionary<GUIType, UiTypeRule> CreateRules()
        {
            Dictionary<GUIType, UiTypeRule> result = new Dictionary<GUIType, UiTypeRule>();
            Add(result, GUIType.Null, requiresLayerGroupCarrier: true);
            Add(result, GUIType.Image, requiresLayerGroupCarrier: false);
            Add(result, GUIType.Text, requiresLayerGroupCarrier: false);
            Add(result, GUIType.Mask, requiresLayerGroupCarrier: false);
            Add(result, GUIType.FillColor, requiresLayerGroupCarrier: false);
            Add(result, GUIType.Panel, requiresLayerGroupCarrier: true, new GUIType[1] { GUIType.Background });
            Add(result, GUIType.ToggleGroup, requiresLayerGroupCarrier: true, new GUIType[1] { GUIType.Background }, null, new GUIType[1] { GUIType.Toggle });
            Add(result, GUIType.Button, requiresLayerGroupCarrier: true, new GUIType[6]
            {
                GUIType.Background,
                GUIType.Button_Text,
                GUIType.Button_Highlight,
                GUIType.Button_Press,
                GUIType.Button_Select,
                GUIType.Button_Disable
            }, new GUIType[1] { GUIType.Background });
            Add(result, GUIType.Dropdown, requiresLayerGroupCarrier: true, new GUIType[3]
            {
                GUIType.Background,
                GUIType.Dropdown_Label,
                GUIType.Dropdown_Arrow
            }, null, new GUIType[2]
            {
                GUIType.ScrollView,
                GUIType.Toggle
            });
            Add(result, GUIType.InputField, requiresLayerGroupCarrier: true, new GUIType[3]
            {
                GUIType.Background,
                GUIType.InputField_Placeholder,
                GUIType.InputField_Text
            });
            Add(result, GUIType.Toggle, requiresLayerGroupCarrier: true, new GUIType[3]
            {
                GUIType.Background,
                GUIType.Toggle_Checkmark,
                GUIType.Toggle_Label
            });
            Add(result, GUIType.Slider, requiresLayerGroupCarrier: true, new GUIType[3]
            {
                GUIType.Background,
                GUIType.Slider_Fill,
                GUIType.Slider_Handle
            }, new GUIType[2]
            {
                GUIType.Background,
                GUIType.Slider_Fill
            });
            Add(result, GUIType.ScrollView, requiresLayerGroupCarrier: true, new GUIType[6]
            {
                GUIType.Background,
                GUIType.ScrollView_Viewport,
                GUIType.ScrollView_HorizontalBarBG,
                GUIType.ScrollView_HorizontalBar,
                GUIType.ScrollView_VerticalBarBG,
                GUIType.ScrollView_VerticalBar
            });
            return result;
        }

        private static void Add(Dictionary<GUIType, UiTypeRule> result, GUIType ownerType, bool requiresLayerGroupCarrier, GUIType[] allowedRoles = null, GUIType[] requiredRoles = null, GUIType[] allowedDirectChildControls = null)
        {
            result[ownerType] = new UiTypeRule
            {
                OwnerType = ownerType,
                RequiresGroupCarrier = requiresLayerGroupCarrier,
                AllowedRoleTypes = (allowedRoles ?? Array.Empty<GUIType>()),
                RequiredRoleTypes = (requiredRoles ?? Array.Empty<GUIType>()),
                AllowedDirectChildControlTypes = (allowedDirectChildControls ?? Array.Empty<GUIType>())
            };
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static UiTypeCompatibilityRules GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
