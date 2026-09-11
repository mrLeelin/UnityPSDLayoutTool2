using System;
using System.Collections;
using System.Collections.Generic;

namespace cn.efunstudio.psdreader.PsdParser
{
    public static class PsdTextLayerExtensions
    {
        private struct GradientColorStop
        {
            public float Location;

            public PsdColor Color;
        }

        private struct GradientAlphaStop
        {
            public float Location;

            public float Alpha;
        }
public static bool IsTextLayer(this PsdLayer layer)
        {
            if (layer != null && layer.Resources != null)
            {
                return layer.Resources.Contains("TySh.Text.EngineData");
            }
            return false;
        }

        public static bool TryGetTextLayerInfo(this PsdLayer layer, out PsdTextLayerInfo textLayerInfo)
        {
            textLayerInfo = null;
            if (!layer.IsTextLayer())
            {
                return false;
            }
            IProperties resources = layer.Resources;
            IProperties properties = resources["TySh.Text.EngineData"] as IProperties;
            IProperties primaryStyleSheetData = GetPrimaryStyleSheetData(properties);
            if (properties != null && primaryStyleSheetData != null)
            {
                textLayerInfo = new PsdTextLayerInfo();
                textLayerInfo.Text = NormalizeText(ReadString(properties, "EngineDict.Editor.Text") ?? ReadString(resources, "TySh.Text.Txt"));
                textLayerInfo.FontIndex = ReadInt(primaryStyleSheetData, "Font", -1);
                textLayerInfo.FontName = ResolveFontName(properties, textLayerInfo.FontIndex);
                textLayerInfo.FontSize = ReadSingle(primaryStyleSheetData, "FontSize", 0f) * ReadTextTransformScaleY(resources);
                PsdColor psdColor = ReadArrayColor(primaryStyleSheetData, "FillColor.Values", new PsdColor(byte.MaxValue, byte.MaxValue, byte.MaxValue));
                textLayerInfo.Color = psdColor.WithAlpha(MultiplyAlpha(psdColor.A, layer.Opacity));
                textLayerInfo.FauxBold = ReadBool(primaryStyleSheetData, "FauxBold", defaultValue: false);
                textLayerInfo.FauxItalic = ReadBool(primaryStyleSheetData, "FauxItalic", defaultValue: false);
                textLayerInfo.Underline = ReadBool(primaryStyleSheetData, "Underline", defaultValue: false);
                textLayerInfo.Strikethrough = ReadBool(primaryStyleSheetData, "Strikethrough", defaultValue: false);
                textLayerInfo.AllCaps = ReadTextAllCaps(primaryStyleSheetData);
                textLayerInfo.Tracking = ReadSingle(primaryStyleSheetData, "Tracking", 0f);
                textLayerInfo.Leading = ReadSingle(primaryStyleSheetData, "Leading", 0f);
                textLayerInfo.AutoLeading = ReadBool(primaryStyleSheetData, "AutoLeading", defaultValue: false) || ReadBool(primaryStyleSheetData, "AutoLeadingEnabled", defaultValue: false);
                textLayerInfo.Stroke = ((!TryReadStroke(resources, out var strokeInfo)) ? null : strokeInfo);
                textLayerInfo.Shadow = ((!TryReadShadow(layer, resources, out var shadowInfo)) ? null : shadowInfo);
                textLayerInfo.Gradient = ((!TryReadGradient(layer, resources, out var gradientInfo)) ? null : gradientInfo);
                textLayerInfo.InnerShadow = (TryReadInnerShadow(layer, resources, out var shadowInfo2) ? shadowInfo2 : null);
                textLayerInfo.OuterGlow = ((!TryReadGlow(layer, resources, inner: false, out var glowInfo)) ? null : glowInfo);
                textLayerInfo.InnerGlow = ((!TryReadGlow(layer, resources, inner: true, out var glowInfo2)) ? null : glowInfo2);
                textLayerInfo.Bevel = (TryReadBevel(layer, resources, out var bevelInfo) ? bevelInfo : null);
                return true;
            }
            return false;
        }

        private static IProperties GetPrimaryStyleSheetData(IProperties engineData)
        {
            if (engineData != null && engineData.Contains("EngineDict.StyleRun.RunArray[0].StyleSheet.StyleSheetData"))
            {
                return engineData["EngineDict.StyleRun.RunArray[0].StyleSheet.StyleSheetData"] as IProperties;
            }
            return null;
        }

        private static string ResolveFontName(IProperties engineData, int fontIndex)
        {
            if (engineData != null && fontIndex >= 0)
            {
                return ReadString(engineData, $"ResourceDict.FontSet[{fontIndex}].Name");
            }
            return null;
        }

        private static bool TryReadStroke(IProperties resources, out PsdTextStrokeInfo strokeInfo)
        {
            strokeInfo = null;
            if (resources != null)
            {
                if ((TrySelectFirstEnabledEffect(resources, "lfx2.frameFXMulti.Items", out var effectProps) || TrySelectFirstEnabledEffect(resources, "lfx2.FrFXMulti.Items", out effectProps)) && TryCreateStrokeInfo(effectProps, out strokeInfo))
                {
                    return true;
                }
                if (resources.Contains("lfx2.FrFX"))
                {
                    effectProps = resources["lfx2.FrFX"] as IProperties;
                    if (TryCreateStrokeInfo(effectProps, out strokeInfo))
                    {
                        return true;
                    }
                }
                if (resources.Contains("lfx2.frameFX"))
                {
                    effectProps = resources["lfx2.frameFX"] as IProperties;
                    if (TryCreateStrokeInfo(effectProps, out strokeInfo))
                    {
                        return true;
                    }
                }
                effectProps = FindEffectProps((!resources.Contains("lfx2")) ? null : (resources["lfx2"] as IProperties), "frfx", "framefx", "stroke");
                if (TryCreateStrokeInfo(effectProps, out strokeInfo))
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        private static bool TryCreateStrokeInfo(IProperties strokeProps, out PsdTextStrokeInfo strokeInfo)
        {
            strokeInfo = null;
            if (strokeProps != null)
            {
                float opacity = ReadUnitFloat(strokeProps, "Opct", 1f, 100f);
                strokeInfo = new PsdTextStrokeInfo
                {
                    Enabled = ReadEffectEnabled(strokeProps, defaultWhenNoFlag: true),
                    Size = ReadUnitFloat(strokeProps, "Sz", 0f),
                    Opacity = opacity,
                    Color = ReadDescriptorColor(strokeProps, "Clr", opacity),
                    Position = ReadStrokePosition(strokeProps, "Styl"),
                    BlendModeKey = ReadEnumerateValue(strokeProps, "Md"),
                    PaintTypeKey = ReadEnumerateValue(strokeProps, "PntT")
                };
                return true;
            }
            return false;
        }

        private static bool TryReadShadow(PsdLayer layer, IProperties resources, out PsdTextShadowInfo shadowInfo)
        {
            shadowInfo = null;
            if (resources == null)
            {
                return false;
            }
            if ((TrySelectFirstEnabledEffect(resources, "lfx2.dropShadowMulti.Items", out var effectProps) || TrySelectFirstEnabledEffect(resources, "lfx2.DrShMulti.Items", out effectProps)) && TryCreateShadowInfo(layer, resources, effectProps, out shadowInfo))
            {
                return true;
            }
            if (resources.Contains("lfx2.DrSh"))
            {
                IProperties shadowProps = resources["lfx2.DrSh"] as IProperties;
                if (TryCreateShadowInfo(layer, resources, shadowProps, out shadowInfo))
                {
                    return true;
                }
            }
            if (resources.Contains("lfx2.dropShadow"))
            {
                effectProps = resources["lfx2.dropShadow"] as IProperties;
                if (TryCreateShadowInfo(layer, resources, effectProps, out shadowInfo))
                {
                    return true;
                }
            }
            effectProps = FindEffectProps((!resources.Contains("lfx2")) ? null : (resources["lfx2"] as IProperties), "drsh", "dropshadow");
            if (!TryCreateShadowInfo(layer, resources, effectProps, out shadowInfo))
            {
                return false;
            }
            return true;
        }

        private static bool TryCreateShadowInfo(PsdLayer layer, IProperties resources, IProperties shadowProps, out PsdTextShadowInfo shadowInfo)
        {
            shadowInfo = null;
            if (shadowProps != null)
            {
                bool useGlobalAngle = ReadBool(shadowProps, "uglg", defaultValue: false);
                float num = ReadUnitFloat(shadowProps, "Opct", 1f, 100f);
                float num2 = layer?.Opacity ?? 1f;
                float opacity = Math.Max(0f, Math.Min(1f, num * num2));
                shadowInfo = new PsdTextShadowInfo
                {
                    Enabled = ReadEffectEnabled(shadowProps, defaultWhenNoFlag: true),
                    UseGlobalAngle = useGlobalAngle,
                    Opacity = num,
                    Angle = ResolveShadowAngle(layer, resources, shadowProps, useGlobalAngle),
                    Distance = ReadUnitFloat(shadowProps, "Dstn", 0f),
                    Spread = ReadUnitFloat(shadowProps, "Ckmt", 0f, 100f),
                    Blur = ReadUnitFloat(shadowProps, "blur", 0f),
                    Color = ReadDescriptorColor(shadowProps, "Clr", opacity),
                    BlendModeKey = ReadEnumerateValue(shadowProps, "Md")
                };
                return true;
            }
            return false;
        }

        private static float ResolveShadowAngle(PsdLayer layer, IProperties resources, IProperties shadowProps, bool useGlobalAngle)
        {
            float value;
            if (useGlobalAngle)
            {
                if (TryReadUnitFloat(resources, "lfx2.gagl", out value))
                {
                    return value;
                }
                if (TryReadUnitFloat(resources, "lfx2.globalLightingAngle", out value))
                {
                    return value;
                }
                if (TryReadSingle(resources, "lfx2.gagl", out value))
                {
                    return value;
                }
                if (TryReadSingle(resources, "lfx2.globalLightingAngle", out value))
                {
                    return value;
                }
                IProperties props = ((layer != null && layer.Document != null) ? layer.Document.ImageResources : null);
                if (TryReadSingle(props, "GlobalAngle.Angle", out value))
                {
                    return value;
                }
                if (TryReadSingle(props, "1037.Angle", out value))
                {
                    return value;
                }
            }
            if (TryReadUnitFloat(shadowProps, "lagl", out value))
            {
                return value;
            }
            if (TryReadSingle(shadowProps, "lagl", out value))
            {
                return value;
            }
            return 0f;
        }

        private static bool TryReadInnerShadow(PsdLayer layer, IProperties resources, out PsdTextShadowInfo shadowInfo)
        {
            shadowInfo = null;
            if (resources != null)
            {
                if ((TrySelectFirstEnabledEffect(resources, "lfx2.innerShadowMulti.Items", out var effectProps) || TrySelectFirstEnabledEffect(resources, "lfx2.IrShMulti.Items", out effectProps)) && TryCreateShadowInfo(layer, resources, effectProps, out shadowInfo))
                {
                    return true;
                }
                if (resources.Contains("lfx2.IrSh"))
                {
                    effectProps = resources["lfx2.IrSh"] as IProperties;
                    if (TryCreateShadowInfo(layer, resources, effectProps, out shadowInfo))
                    {
                        return true;
                    }
                }
                if (resources.Contains("lfx2.innerShadow"))
                {
                    effectProps = resources["lfx2.innerShadow"] as IProperties;
                    if (TryCreateShadowInfo(layer, resources, effectProps, out shadowInfo))
                    {
                        return true;
                    }
                }
                effectProps = FindEffectProps(resources.Contains("lfx2") ? (resources["lfx2"] as IProperties) : null, "irsh", "innershadow");
                if (!TryCreateShadowInfo(layer, resources, effectProps, out shadowInfo))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool TryReadGlow(PsdLayer layer, IProperties resources, bool inner, out PsdTextGlowInfo glowInfo)
        {
            glowInfo = null;
            if (resources == null)
            {
                return false;
            }
            string path = (inner ? "lfx2.innerGlowMulti.Items" : "lfx2.outerGlowMulti.Items");
            string path2 = ((!inner) ? "lfx2.OrGlMulti.Items" : "lfx2.IrGlMulti.Items");
            if ((!TrySelectFirstEnabledEffect(resources, path, out var effectProps) && !TrySelectFirstEnabledEffect(resources, path2, out effectProps)) || !TryCreateGlowInfo(layer, effectProps, inner, out glowInfo))
            {
                string property = ((!inner) ? "lfx2.OrGl" : "lfx2.IrGl");
                if (resources.Contains(property))
                {
                    effectProps = resources[property] as IProperties;
                    if (TryCreateGlowInfo(layer, effectProps, inner, out glowInfo))
                    {
                        return true;
                    }
                }
                string property2 = ((!inner) ? "lfx2.outerGlow" : "lfx2.innerGlow");
                if (resources.Contains(property2))
                {
                    effectProps = resources[property2] as IProperties;
                    if (TryCreateGlowInfo(layer, effectProps, inner, out glowInfo))
                    {
                        return true;
                    }
                }
                effectProps = FindEffectProps((!resources.Contains("lfx2")) ? null : (resources["lfx2"] as IProperties), inner ? "irgl" : "orgl", (!inner) ? "outerglow" : "innerglow");
                if (!TryCreateGlowInfo(layer, effectProps, inner, out glowInfo))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        private static bool TryCreateGlowInfo(PsdLayer layer, IProperties glowProps, bool inner, out PsdTextGlowInfo glowInfo)
        {
            glowInfo = null;
            if (glowProps == null)
            {
                return false;
            }
            bool enabled = ReadEffectEnabled(glowProps, defaultWhenNoFlag: true);
            float num = ReadOpacity(glowProps, "Opct", 1f);
            float size = ReadUnitFloatFirst(glowProps, 0f, "blur", "Sz", "size");
            float val = ReadUnitFloat(glowProps, "Ckmt", 0f, 100f);
            float num2 = layer?.Opacity ?? 1f;
            float opacity = Math.Max(0f, Math.Min(1f, num * num2));
            PsdColor color = ReadDescriptorColor(glowProps, "Clr", opacity);
            glowInfo = new PsdTextGlowInfo
            {
                Enabled = enabled,
                Inner = inner,
                Opacity = num,
                Size = size,
                Spread = Math.Max(0f, Math.Min(1f, val)),
                Color = color,
                BlendModeKey = ReadEnumerateValue(glowProps, "Md")
            };
            return true;
        }

        private static bool TryReadBevel(PsdLayer layer, IProperties resources, out PsdTextBevelInfo bevelInfo)
        {
            bevelInfo = null;
            if (resources == null)
            {
                return false;
            }
            if ((!TrySelectFirstEnabledEffect(resources, "lfx2.bevelEmbossMulti.Items", out var effectProps) && !TrySelectFirstEnabledEffect(resources, "lfx2.ebblMulti.Items", out effectProps)) || !TryCreateBevelInfo(layer, resources, effectProps, out bevelInfo))
            {
                if (resources.Contains("lfx2.ebbl"))
                {
                    effectProps = resources["lfx2.ebbl"] as IProperties;
                    if (TryCreateBevelInfo(layer, resources, effectProps, out bevelInfo))
                    {
                        return true;
                    }
                }
                if (resources.Contains("lfx2.bevelEmboss"))
                {
                    effectProps = resources["lfx2.bevelEmboss"] as IProperties;
                    if (TryCreateBevelInfo(layer, resources, effectProps, out bevelInfo))
                    {
                        return true;
                    }
                }
                effectProps = FindEffectProps(resources.Contains("lfx2") ? (resources["lfx2"] as IProperties) : null, "ebbl", "bevel", "bvl");
                if (!TryCreateBevelInfo(layer, resources, effectProps, out bevelInfo))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        private static bool TryCreateBevelInfo(PsdLayer layer, IProperties resources, IProperties bevelProps, out PsdTextBevelInfo bevelInfo)
        {
            bevelInfo = null;
            if (bevelProps == null)
            {
                return false;
            }
            bool enabled = ReadEffectEnabled(bevelProps, defaultWhenNoFlag: true);
            float size = ReadUnitFloatFirst(bevelProps, 0f, "Sz", "size", "blur");
            float soften = ReadUnitFloatFirst(bevelProps, 0f, "Sftn", "Soften", "blur");
            float num = ReadOpacity(bevelProps, "srgR", 1f);
            if (num <= 0f)
            {
                num = ReadOpacity(bevelProps, "Depth", 1f);
            }
            bool useGlobalAngle = ReadBool(bevelProps, "uglg", defaultValue: false);
            float angle = ResolveShadowAngle(layer, resources, bevelProps, useGlobalAngle);
            float altitude = ReadUnitFloatFirst(bevelProps, 0f, "Lald", "altitude");
            string styleKey = ReadEnumerateValueFirst(bevelProps, "bvlS", "Style", "style");
            float num2 = ReadOpacityFirst(bevelProps, 1f, "hglO", "highlightOpacity", "HglO");
            float num3 = ReadOpacityFirst(bevelProps, 1f, "sdwO", "shadowOpacity", "SdwO");
            PsdColor highlightColor = ReadDescriptorColorFirst(bevelProps, new PsdColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, ConvertOpacityToByte(num2)), num2, "hglC", "highlightColor", "HglC");
            PsdColor shadowColor = ReadDescriptorColorFirst(bevelProps, new PsdColor(0, 0, 0, ConvertOpacityToByte(num3)), num3, "sdwC", "shadowColor", "SdwC");
            bevelInfo = new PsdTextBevelInfo
            {
                Enabled = enabled,
                Inner = IsInnerBevelStyle(styleKey),
                Depth = num,
                Size = size,
                Soften = soften,
                UseGlobalAngle = useGlobalAngle,
                Angle = angle,
                Altitude = altitude,
                StyleKey = styleKey,
                HighlightColor = highlightColor,
                HighlightOpacity = num2,
                ShadowColor = shadowColor,
                ShadowOpacity = num3
            };
            return true;
        }

        private static bool TryReadGradient(PsdLayer layer, IProperties resources, out PsdTextGradientInfo gradientInfo)
        {
            gradientInfo = null;
            if (resources != null)
            {
                IProperties effectProps = null;
                if (TrySelectFirstEnabledEffect(resources, "lfx2.gradientFillMulti.Items", out effectProps))
                {
                    return TryCreateGradientInfo(layer, effectProps, out gradientInfo);
                }
                if (!TrySelectFirstEnabledEffect(resources, "lfx2.GrFlMulti.Items", out effectProps))
                {
                    if (resources.Contains("lfx2.GrFl"))
                    {
                        effectProps = resources["lfx2.GrFl"] as IProperties;
                        if (TryCreateGradientInfo(layer, effectProps, out gradientInfo))
                        {
                            return true;
                        }
                    }
                    if (resources.Contains("lfx2.gradientFill"))
                    {
                        effectProps = resources["lfx2.gradientFill"] as IProperties;
                        if (TryCreateGradientInfo(layer, effectProps, out gradientInfo))
                        {
                            return true;
                        }
                    }
                    effectProps = FindGradientEffectProps((!resources.Contains("lfx2")) ? null : (resources["lfx2"] as IProperties));
                    if (TryCreateGradientInfo(layer, effectProps, out gradientInfo))
                    {
                        return true;
                    }
                    return false;
                }
                return TryCreateGradientInfo(layer, effectProps, out gradientInfo);
            }
            return false;
        }

        private static IProperties FindGradientEffectProps(IProperties lfx2)
        {
            return FindEffectProps(lfx2, "grfl", "gradient");
        }

        private static IProperties FindEffectProps(IProperties lfx2, params string[] tokens)
        {
            if (lfx2 != null && tokens != null && tokens.Length != 0)
            {
                foreach (KeyValuePair<string, object> item in lfx2)
                {
                    string text = item.Key ?? string.Empty;
                    bool flag = false;
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        if (text.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                    {
                        continue;
                    }
                    if (!(item.Value is IProperties properties))
                    {
                        if (item.Value is IList list)
                        {
                            IProperties properties2 = SelectEffectFromList(list);
                            if (properties2 != null)
                            {
                                return properties2;
                            }
                        }
                        continue;
                    }
                    if (properties.Contains("Items"))
                    {
                        IProperties properties3 = SelectEffectFromList(ReadObject(properties, "Items") as IList);
                        if (properties3 != null)
                        {
                            return properties3;
                        }
                    }
                    return properties;
                }
                return null;
            }
            return null;
        }

        private static bool TrySelectFirstEnabledEffect(IProperties resources, string path, out IProperties effectProps)
        {
            effectProps = null;
            if (ReadObject(resources, path) is IList { Count: not 0 } list)
            {
                effectProps = SelectEffectFromList(list);
                return effectProps != null;
            }
            return false;
        }

        private static IProperties SelectEffectFromList(IList list)
        {
            if (list != null && list.Count != 0)
            {
                IProperties properties = null;
                {
                    foreach (object item in list)
                    {
                        if (item is IProperties properties2)
                        {
                            if (properties == null)
                            {
                                properties = properties2;
                            }
                            if (ReadEffectEnabled(properties2, defaultWhenNoFlag: false))
                            {
                                return properties2;
                            }
                        }
                    }
                    return properties;
                }
            }
            return null;
        }

        private static bool ReadEffectEnabled(IProperties props, bool defaultWhenNoFlag)
        {
            if (props == null)
            {
                return false;
            }
            if (!props.Contains("enab"))
            {
                if (!props.Contains("enabled"))
                {
                    if (!props.Contains("present"))
                    {
                        return defaultWhenNoFlag;
                    }
                    return ReadBool(props, "present", defaultWhenNoFlag);
                }
                return ReadBool(props, "enabled", defaultWhenNoFlag);
            }
            return ReadBool(props, "enab", defaultWhenNoFlag);
        }

        private static bool TryCreateGradientInfo(PsdLayer layer, IProperties gradientProps, out PsdTextGradientInfo gradientInfo)
        {
            gradientInfo = null;
            if (gradientProps != null)
            {
                IProperties properties = ReadGradientDescriptor(gradientProps);
                if (properties != null)
                {
                    List<GradientColorStop> list = ReadGradientColorStops(properties);
                    if (list != null && list.Count != 0)
                    {
                        List<GradientAlphaStop> alphaStops = ReadGradientAlphaStops(properties);
                        float num = ReadOpacity(gradientProps, "Opct", 1f);
                        float num2 = ReadUnitFloat(gradientProps, "Angl", float.NaN);
                        if (float.IsNaN(num2) || float.IsInfinity(num2))
                        {
                            num2 = ReadSingle(gradientProps, "Angl", 0f);
                        }
                        bool reverse = ReadBool(gradientProps, "Rvrs", defaultValue: false);
                        bool enabled = ReadEffectEnabled(gradientProps, defaultWhenNoFlag: true);
                        string text = ReadEnumerateValue(gradientProps, "Type");
                        if (string.IsNullOrEmpty(text))
                        {
                            text = ReadEnumerateValue(gradientProps, "GrdT");
                        }
                        string blendModeKey = ReadEnumerateValue(gradientProps, "Md");
                        float num3 = layer?.Opacity ?? 1f;
                        float combinedOpacity = Math.Max(0f, Math.Min(1f, num * num3));
                        PsdTextGradientStop[] array = BuildGradientStops(list, alphaStops, combinedOpacity);
                        if (array == null || array.Length == 0)
                        {
                            return false;
                        }
                        gradientInfo = new PsdTextGradientInfo
                        {
                            Enabled = enabled,
                            Angle = num2,
                            Reverse = reverse,
                            Opacity = num,
                            StyleKey = text,
                            BlendModeKey = blendModeKey,
                            Stops = array
                        };
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static IProperties ReadGradientDescriptor(IProperties gradientProps)
        {
            if (gradientProps == null)
            {
                return null;
            }
            if (gradientProps.Contains("Clrs") || gradientProps.Contains("Clrs.Items"))
            {
                return gradientProps;
            }
            if (!gradientProps.Contains("Grad"))
            {
                if (gradientProps.Contains("Grdn"))
                {
                    return ReadObject(gradientProps, "Grdn") as IProperties;
                }
                if (!gradientProps.Contains("Gradient"))
                {
                    return null;
                }
                return ReadObject(gradientProps, "Gradient") as IProperties;
            }
            return ReadObject(gradientProps, "Grad") as IProperties;
        }

        private static List<GradientColorStop> ReadGradientColorStops(IProperties gradientDescriptor)
        {
            IList list = (ReadObject(gradientDescriptor, "Clrs.Items") as IList) ?? (ReadObject(gradientDescriptor, "Clrs") as IList);
            if (list != null && list.Count != 0)
            {
                List<GradientColorStop> list2 = new List<GradientColorStop>();
                foreach (object item in list)
                {
                    if (item is IProperties props)
                    {
                        float location = ReadStopLocation(props, "Lctn", 0f);
                        PsdColor color = ReadDescriptorColor(props, "Clr", 1f);
                        list2.Add(new GradientColorStop
                        {
                            Location = location,
                            Color = color
                        });
                    }
                }
                if (list2.Count == 0)
                {
                    return null;
                }
                list2.Sort((GradientColorStop a, GradientColorStop b) => a.Location.CompareTo(b.Location));
                return list2;
            }
            return null;
        }

        private static List<GradientAlphaStop> ReadGradientAlphaStops(IProperties gradientDescriptor)
        {
            IList list = (ReadObject(gradientDescriptor, "Trns.Items") as IList) ?? (ReadObject(gradientDescriptor, "Trns") as IList);
            if (list != null && list.Count != 0)
            {
                List<GradientAlphaStop> list2 = new List<GradientAlphaStop>();
                foreach (object item in list)
                {
                    if (item is IProperties props)
                    {
                        float location = ReadStopLocation(props, "Lctn", 0f);
                        float alpha = ReadOpacity(props, "Opct", 1f);
                        list2.Add(new GradientAlphaStop
                        {
                            Location = location,
                            Alpha = alpha
                        });
                    }
                }
                if (list2.Count == 0)
                {
                    return null;
                }
                list2.Sort((GradientAlphaStop a, GradientAlphaStop b) => a.Location.CompareTo(b.Location));
                return list2;
            }
            return null;
        }

        private static PsdTextGradientStop[] BuildGradientStops(List<GradientColorStop> colorStops, List<GradientAlphaStop> alphaStops, float combinedOpacity)
        {
            if (colorStops != null && colorStops.Count != 0)
            {
                PsdTextGradientStop[] array = new PsdTextGradientStop[colorStops.Count];
                for (int i = 0; i < colorStops.Count; i++)
                {
                    GradientColorStop gradientColorStop = colorStops[i];
                    float num = EvaluateAlpha(alphaStops, gradientColorStop.Location);
                    byte alpha = ConvertOpacityToByte(Math.Max(0f, Math.Min(1f, num * combinedOpacity)));
                    array[i] = new PsdTextGradientStop
                    {
                        Location = Math.Max(0f, Math.Min(1f, gradientColorStop.Location)),
                        Color = gradientColorStop.Color.WithAlpha(alpha)
                    };
                }
                return array;
            }
            return null;
        }

        private static float EvaluateAlpha(List<GradientAlphaStop> alphaStops, float location)
        {
            if (alphaStops != null && alphaStops.Count != 0)
            {
                if (location <= alphaStops[0].Location)
                {
                    return alphaStops[0].Alpha;
                }
                if (location >= alphaStops[alphaStops.Count - 1].Location)
                {
                    return alphaStops[alphaStops.Count - 1].Alpha;
                }
                int num = 0;
                GradientAlphaStop gradientAlphaStop;
                GradientAlphaStop gradientAlphaStop2;
                while (true)
                {
                    if (num < alphaStops.Count - 1)
                    {
                        gradientAlphaStop = alphaStops[num];
                        gradientAlphaStop2 = alphaStops[num + 1];
                        if (!(location > gradientAlphaStop2.Location))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return alphaStops[alphaStops.Count - 1].Alpha;
                }
                float num2 = gradientAlphaStop2.Location - gradientAlphaStop.Location;
                if (num2 <= 0f)
                {
                    return gradientAlphaStop2.Alpha;
                }
                float num3 = (location - gradientAlphaStop.Location) / num2;
                return gradientAlphaStop.Alpha + (gradientAlphaStop2.Alpha - gradientAlphaStop.Alpha) * num3;
            }
            return 1f;
        }

        private static float ReadStopLocation(IProperties props, string path, float defaultValue)
        {
            object obj = ReadObject(props, path);
            if (obj != null)
            {
                try
                {
                    float num = Convert.ToSingle(obj);
                    if (num <= 1f)
                    {
                        return Math.Max(0f, Math.Min(1f, num));
                    }
                    if (num <= 100f)
                    {
                        return Math.Max(0f, Math.Min(1f, num / 100f));
                    }
                    return Math.Max(0f, Math.Min(1f, num / 4096f));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private static float ReadOpacity(IProperties props, string path, float defaultValue)
        {
            float num = ReadUnitFloat(props, path, -1f, 100f);
            if (num < 0f)
            {
                num = ReadSingle(props, path, defaultValue);
                if (num > 1f)
                {
                    num /= 100f;
                }
            }
            return Math.Max(0f, Math.Min(1f, num));
        }

        private static float ReadOpacityFirst(IProperties props, float defaultValue, params string[] paths)
        {
            if (props != null && paths != null && paths.Length != 0)
            {
                int num = 0;
                float opacity;
                while (true)
                {
                    if (num < paths.Length)
                    {
                        if (TryReadOpacity(props, paths[num], out opacity))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return Math.Max(0f, Math.Min(1f, defaultValue));
                }
                return opacity;
            }
            return Math.Max(0f, Math.Min(1f, defaultValue));
        }

        private static bool TryReadOpacity(IProperties props, string path, out float opacity)
        {
            opacity = 0f;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            float num = ReadUnitFloat(props, path, -1f, 100f);
            if (num >= 0f)
            {
                opacity = Math.Max(0f, Math.Min(1f, num));
                return true;
            }
            if (TryReadSingle(props, path, out opacity))
            {
                if (opacity > 1f)
                {
                    opacity /= 100f;
                }
                opacity = Math.Max(0f, Math.Min(1f, opacity));
                return true;
            }
            return false;
        }

        private static PsdTextStrokePosition ReadStrokePosition(IProperties props, string path)
        {
            switch (ReadEnumerateValue(props, path))
            {
            case "InsF":
            case "InFx":
            case "Insd":
                return PsdTextStrokePosition.Inside;
            default:
                return PsdTextStrokePosition.Unknown;
            case "CtrF":
            case "Ctrs":
                return PsdTextStrokePosition.Center;
            case "OutF":
                return PsdTextStrokePosition.Outside;
            }
        }

        private static string NormalizeText(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
            {
                return rawText;
            }
            return rawText.TrimEnd('\r', '\n');
        }

        private static bool ReadTextAllCaps(IProperties styleData)
        {
            if (ReadBool(styleData, "AllCaps", defaultValue: false))
            {
                return true;
            }
            object obj = ReadObject(styleData, "FontCaps");
            if (obj != null)
            {
                if (obj is string text)
                {
                    if (!string.Equals(text, "0", StringComparison.Ordinal) && !string.Equals(text, "normal", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        if (text.IndexOf("small", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            if (text.IndexOf("caps", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                return text.IndexOf("upper", StringComparison.OrdinalIgnoreCase) >= 0;
                            }
                            return true;
                        }
                        return false;
                    }
                    return false;
                }
                try
                {
                    return Convert.ToInt32(obj) != 0;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private static float ReadTextTransformScaleY(IProperties resources)
        {
            if (resources != null && resources.Contains("TySh.Transforms"))
            {
                object obj = resources["TySh.Transforms"];
                if (obj is double[] array && array.Length > 3)
                {
                    double num = array[3];
                    if (double.IsNaN(num) || double.IsInfinity(num))
                    {
                        return 1f;
                    }
                    return (float)num;
                }
                if (obj is float[] array2 && array2.Length > 3)
                {
                    float num2 = array2[3];
                    if (float.IsNaN(num2) || float.IsInfinity(num2))
                    {
                        return 1f;
                    }
                    return num2;
                }
                if (obj is IList { Count: >3 } list)
                {
                    try
                    {
                        return Convert.ToSingle(list[3]);
                    }
                    catch
                    {
                        return 1f;
                    }
                }
                return 1f;
            }
            return 1f;
        }

        private static PsdColor ReadArrayColor(IProperties props, string path, PsdColor defaultColor)
        {
            if (props != null && props.Contains(path))
            {
                if (!(props[path] is IList { Count: >=4 } list))
                {
                    return defaultColor;
                }
                return new PsdColor(ConvertNormalizedToByte(list[1]), ConvertNormalizedToByte(list[2]), ConvertNormalizedToByte(list[3]), ConvertNormalizedToByte(list[0]));
            }
            return defaultColor;
        }

        private static PsdColor ReadDescriptorColor(IProperties props, string path, float opacity)
        {
            return new PsdColor(ConvertToByte(ReadObject(props, path + ".Rd"), 0), ConvertToByte(ReadObject(props, path + ".Grn"), 0), ConvertToByte(ReadObject(props, path + ".Bl"), 0), ConvertOpacityToByte(opacity));
        }

        private static PsdColor ReadDescriptorColorFirst(IProperties props, PsdColor defaultColor, float opacity, params string[] paths)
        {
            if (props != null && paths != null && paths.Length != 0)
            {
                int num = 0;
                while (true)
                {
                    if (num < paths.Length)
                    {
                        if (HasDescriptorColor(props, paths[num]))
                        {
                            break;
                        }
                        num++;
                        continue;
                    }
                    return defaultColor;
                }
                return ReadDescriptorColor(props, paths[num], opacity);
            }
            return defaultColor;
        }

        private static bool HasDescriptorColor(IProperties props, string path)
        {
            if (props != null && !string.IsNullOrEmpty(path))
            {
                if (ReadObject(props, path + ".Rd") != null || ReadObject(props, path + ".Grn") != null)
                {
                    return true;
                }
                return ReadObject(props, path + ".Bl") != null;
            }
            return false;
        }

        private static string ReadString(IProperties props, string path)
        {
            return ReadObject(props, path) as string;
        }

        private static bool ReadBool(IProperties props, string path, bool defaultValue)
        {
            object obj = ReadObject(props, path);
            if (obj is bool)
            {
                return (bool)obj;
            }
            return defaultValue;
        }

        private static int ReadInt(IProperties props, string path, int defaultValue)
        {
            object obj = ReadObject(props, path);
            if (obj != null)
            {
                try
                {
                    return Convert.ToInt32(obj);
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private static float ReadSingle(IProperties props, string path, float defaultValue)
        {
            object obj = ReadObject(props, path);
            if (obj != null)
            {
                try
                {
                    return Convert.ToSingle(obj);
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private static float ReadUnitFloat(IProperties props, string path, float defaultValue)
        {
            return ReadUnitFloat(props, path, defaultValue, 1f);
        }

        private static float ReadUnitFloat(IProperties props, string path, float defaultValue, float divisor)
        {
            object obj = ReadObject(props, path + ".Value");
            if (obj != null)
            {
                try
                {
                    return Convert.ToSingle(obj) / divisor;
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private static float ReadUnitFloatFirst(IProperties props, float defaultValue, params string[] paths)
        {
            if (props != null && paths != null && paths.Length != 0)
            {
                int num = 0;
                float num2;
                while (true)
                {
                    if (num < paths.Length)
                    {
                        if (!TryReadUnitFloat(props, paths[num], out var value))
                        {
                            num2 = ReadSingle(props, paths[num], float.NaN);
                            if (!float.IsNaN(num2) && !float.IsInfinity(num2))
                            {
                                break;
                            }
                            num++;
                            continue;
                        }
                        return value;
                    }
                    return defaultValue;
                }
                return num2;
            }
            return defaultValue;
        }

        private static bool TryReadUnitFloat(IProperties props, string path, out float value)
        {
            value = 0f;
            object obj = ReadObject(props, path + ".Value");
            if (obj != null)
            {
                try
                {
                    value = Convert.ToSingle(obj);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private static bool TryReadSingle(IProperties props, string path, out float value)
        {
            value = 0f;
            object obj = ReadObject(props, path);
            if (obj == null)
            {
                return false;
            }
            try
            {
                value = Convert.ToSingle(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadEnumerateValue(IProperties props, string path)
        {
            return ReadString(props, path + ".Value");
        }

        private static string ReadEnumerateValueFirst(IProperties props, params string[] paths)
        {
            if (props != null && paths != null && paths.Length != 0)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string text = ReadEnumerateValue(props, paths[i]);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
                return null;
            }
            return null;
        }

        private static bool IsInnerBevelStyle(string styleKey)
        {
            if (!string.IsNullOrEmpty(styleKey))
            {
                if (styleKey.IndexOf("inner", StringComparison.OrdinalIgnoreCase) < 0 && styleKey.IndexOf("inrb", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return string.Equals(styleKey, "InrB", StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }
            return false;
        }

        private static object ReadObject(IProperties props, string path)
        {
            if (props != null && !string.IsNullOrEmpty(path) && props.Contains(path))
            {
                return props[path];
            }
            return null;
        }

        private static byte ConvertOpacityToByte(float opacity)
        {
            opacity = Math.Max(0f, Math.Min(1f, opacity));
            return (byte)Math.Round(opacity * 255f, MidpointRounding.AwayFromZero);
        }

        private static byte MultiplyAlpha(byte alpha, float opacity)
        {
            opacity = Math.Max(0f, Math.Min(1f, opacity));
            return (byte)Math.Round((float)(int)alpha * opacity, MidpointRounding.AwayFromZero);
        }

        private static byte ConvertNormalizedToByte(object value)
        {
            if (value == null)
            {
                return 0;
            }
            try
            {
                double num = Convert.ToDouble(value);
                if (num <= 1.0)
                {
                    return (byte)Math.Round(Math.Max(0.0, Math.Min(1.0, num)) * 255.0, MidpointRounding.AwayFromZero);
                }
                return (byte)Math.Round(Math.Max(0.0, Math.Min(255.0, num)), MidpointRounding.AwayFromZero);
            }
            catch
            {
                return 0;
            }
        }

        private static byte ConvertToByte(object value, byte defaultValue)
        {
            if (value != null)
            {
                try
                {
                    return (byte)Math.Round(Math.Max(0.0, Math.Min(255.0, Convert.ToDouble(value))), MidpointRounding.AwayFromZero);
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        internal static bool IsObfuscationSentinelValid()

        {

            return true;

        }
}
}
