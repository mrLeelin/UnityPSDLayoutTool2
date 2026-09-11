using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LayerNodeIdUtilityNamespace;
using AiJobFileStoreNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader.PsdParser;
using AiPathUtilityNamespace;

namespace AiAnalysisPackageBuilderNamespace
{
    internal sealed class AiAnalysisPackageBuilder
    {
        private sealed class AiPackagePaths
        {
            public string _packageRootDirectory;

            public string _nodePreviewDirectory;

            public string _annotatedPreviewPath;

            public string _nodeAtlasDirectory;

            private static AiPackagePaths s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static AiPackagePaths GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private sealed class AiPackageBuildContext
        {
            public readonly Dictionary<PsdLayerNode, AiNodeAssetInfo> _nodeAssetsByNode = new Dictionary<PsdLayerNode, AiNodeAssetInfo>();

            public readonly Dictionary<string, string> _assetPathByContentHash = new Dictionary<string, string>(StringComparer.Ordinal);

            public readonly HashSet<string> _retainedPreviewPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public string _projectRootPath;

            internal static AiPackageBuildContext s_ObfuscationSentinel;

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static AiPackageBuildContext GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private struct AiNodeAssetInfo
        {
            public string _imageFile;

            public string _previewKind;

            public string _previewSourceId;

            public string _visualHash;
        }

        private struct AiDocumentBounds
        {
            public int _left;

            public int _top;

            public int _previewWidth;

            public int _previewHeight;

            public int _documentWidth;

            public int _documentHeight;
        }

        private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly Color32 _transparentColor = new Color32((byte)0, (byte)0, (byte)0, (byte)0);

        private static readonly Color32 _atlasCellBorderColor = new Color32((byte)176, (byte)176, (byte)176, byte.MaxValue);

        internal static AiAnalysisPackageBuilder s_ObfuscationSentinel;

        internal bool TryBuildAnalysisPackage(Psd2UIFormConverterEditor value3, AiJobContext aiJobContext, out string result, out string result2)
        {
            result = string.Empty;
            result2 = null;
            if (!((Object)(object)value3 == (Object)null))
            {
                if (aiJobContext != null)
                {
                    try
                    {
                        AiPackagePaths aiPackagePaths = BuildPackagePaths(value3);
                        AiPackageBuildContext value = new AiPackageBuildContext
                        {
                            _projectRootPath = Directory.GetParent(Application.dataPath).FullName
                        };
                        AiDocumentBounds value2;
                        AiAnalysisPackageDocument aiAnalysisPackageDocument = new AiAnalysisPackageDocument
                        {
                            version = "4.0",
                            document = BuildDocumentInfo(value3, aiPackagePaths, out value2),
                            config = BuildConfigInfo()
                        };
                        PsdLayerNode[] componentsInChildren = value3.GetComponentsInChildren<PsdLayerNode>(true);
                        Dictionary<PsdLayerNode, string> dictionary = BuildShortIdLookup(componentsInChildren);
                        for (int i = 0; i < componentsInChildren.Length; i++)
                        {
                            AiAnalysisNodeEntry aiAnalysisNodeEntry = BuildNodeEntry(value3, aiPackagePaths, value, componentsInChildren[i], value2, dictionary);
                            if (aiAnalysisNodeEntry != null)
                            {
                                aiAnalysisPackageDocument.nodes.Add(aiAnalysisNodeEntry);
                            }
                        }
                        DeleteStaleNodePreviews(aiPackagePaths, value);
                        BuildVisualInputs(aiAnalysisPackageDocument, aiPackagePaths, value._projectRootPath);
                        string text = JsonUtility.ToJson((object)aiAnalysisPackageDocument, false);
                        result = ComputeStringSha256Hex(text);
                        aiAnalysisPackageDocument.treeHash = result;
                        text = JsonUtility.ToJson((object)aiAnalysisPackageDocument, false);
                        AiJobFileStore.WriteTextAtomic(aiJobContext.AnalysisPackagePath, text);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        result2 = "Failed to build AI analysis package: " + ex.Message;
                        return false;
                    }
                }
                result2 = "AI job context is null.";
                return false;
            }
            result2 = "\ufffd";
            return false;
        }

        private static AiPackagePaths BuildPackagePaths(object value)
        {
            string text = Path.Combine(AiPathUtility.ResolvePath(Directory.GetParent(Application.dataPath).FullName, "Library/Psd2UIForm/AiShared"), AiPathUtility.SanitizePsdName((!((Object)value != (Object)null)) ? null : ((Psd2UIFormConverterEditor)value).GetSourcePsdAssetPath()));
            return new AiPackagePaths
            {
                _packageRootDirectory = text,
                _nodePreviewDirectory = Path.Combine(text, "node-previews"),
                _annotatedPreviewPath = Path.Combine(text, "annotated-preview.png"),
                _nodeAtlasDirectory = Path.Combine(text, "node-atlas")
            };
        }

        private static AiAnalysisDocumentInfo BuildDocumentInfo(object value, object value2, out AiDocumentBounds result)
        {
            result = default(AiDocumentBounds);
            string fullName = Directory.GetParent(Application.dataPath).FullName;
            string text = Path.Combine(((AiPackagePaths)value2)._packageRootDirectory, "preview.png");
            int value3 = 0;
            int previewTop = 0;
            int num = 0;
            int num2 = 0;
            int num3 = 0;
            int num4 = 0;
            PsdDocument psdDocument = ((Psd2UIFormConverterEditor)value).GetPsdDocument();
            byte[] array = null;
            if (psdDocument != null)
            {
                num3 = psdDocument.Width;
                num4 = psdDocument.Height;
                try
                {
                    array = Psd2UIFormConverterEditor.RenderDocumentPreviewPng(psdDocument, out value3, out previewTop, out num, out num2);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning((object)("AI 分析包预览图合成失败: " + ex.Message));
                    array = null;
                }
            }
            if (array != null && array.Length != 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(text));
                File.WriteAllBytes(text, array);
            }
            else
            {
                Sprite val = ((Psd2UIFormConverterEditor)value).GetPreviewSprite();
                if ((Object)(object)val != (Object)null && (Object)(object)val.texture != (Object)null)
                {
                    EnsureTexturePng(val.texture, text);
                    num = ((Texture)val.texture).width;
                    num2 = ((Texture)val.texture).height;
                    if (num3 <= 0)
                    {
                        num3 = num;
                    }
                    if (num4 <= 0)
                    {
                        num4 = num2;
                    }
                }
            }
            result = new AiDocumentBounds
            {
                _left = value3,
                _top = previewTop,
                _previewWidth = num,
                _previewHeight = num2,
                _documentWidth = num3,
                _documentHeight = num4
            };
            return new AiAnalysisDocumentInfo
            {
                previewImagePath = GetNormalizedRelativePath(fullName, text),
                nodePreviewDirectoryPath = GetNormalizedRelativePath(fullName, ((AiPackagePaths)value2)._nodePreviewDirectory),
                nodeAtlasDirectoryPath = GetNormalizedRelativePath(fullName, ((AiPackagePaths)value2)._nodeAtlasDirectory),
                width = num,
                height = num2
            };
        }

        private static AiAnalysisConfigInfo BuildConfigInfo()
        {
            AiAnalysisConfigInfo aiAnalysisConfigInfo = new AiAnalysisConfigInfo();
            UGUIParser uGUIParser = UGUIParser.Instance;
            if ((Object)(object)uGUIParser == (Object)null)
            {
                return aiAnalysisConfigInfo;
            }
            UGUIParseRule[] array = uGUIParser.GetRules();
            if (array == null)
            {
                return aiAnalysisConfigInfo;
            }
            foreach (UGUIParseRule uGUIParseRule in array)
            {
                if (uGUIParseRule != null && !IsTextMeshProUiType(uGUIParseRule.UIType))
                {
                    aiAnalysisConfigInfo.uiTypeRules.Add(new AiAnalysisUiTypeRuleInfo
                    {
                        uiType = uGUIParseRule.UIType.ToString(),
                        uiTypeDesc = (uGUIParseRule.UITypeDesc ?? string.Empty),
                        typeMatches = CloneStringArray(uGUIParseRule.TypeMatches)
                    });
                }
            }
            return aiAnalysisConfigInfo;
        }

        private static string[] CloneStringArray(object value)
        {
            if (value != null && ((Array)value).Length != 0)
            {
                string[] array = new string[((Array)value).Length];
                Array.Copy((Array)value, array, ((Array)value).Length);
                return array;
            }
            return Array.Empty<string>();
        }

        private static Dictionary<PsdLayerNode, string> BuildShortIdLookup(object value)
        {
            Dictionary<PsdLayerNode, string> dictionary = new Dictionary<PsdLayerNode, string>();
            if (value == null)
            {
                return dictionary;
            }
            for (int i = 0; i < ((Array)value).Length; i++)
            {
                PsdLayerNode psdLayerNode = (PsdLayerNode)((object[])value)[i];
                if (!((Object)(object)psdLayerNode == (Object)null) && !dictionary.ContainsKey(psdLayerNode))
                {
                    dictionary[psdLayerNode] = "n" + i.ToString("000");
                }
            }
            return dictionary;
        }

        private static string BuildNodeIdPath(object value2, object value3, Dictionary<PsdLayerNode, string> lookup)
        {
            if ((Object)value3 == (Object)null)
            {
                return string.Empty;
            }
            Stack<string> stack = new Stack<string>();
            Transform val = (Transform)value3;
            while ((Object)(object)val != (Object)null && ((Object)value2 == (Object)null || (Object)(object)val != (Object)(object)((Component)value2).transform))
            {
                PsdLayerNode component = ((Component)val).GetComponent<PsdLayerNode>();
                if ((Object)(object)component != (Object)null && lookup != null && lookup.TryGetValue(component, out var value))
                {
                    stack.Push(value);
                }
                val = val.parent;
            }
            if (stack.Count <= 0)
            {
                return string.Empty;
            }
            return string.Join("/", stack.ToArray());
        }

        private static string BuildNodeDisplayPath(object value, object value2)
        {
            if (!((Object)value2 == (Object)null))
            {
                Stack<string> stack = new Stack<string>();
                Transform val = (Transform)value2;
                while ((Object)(object)val != (Object)null && ((Object)value == (Object)null || (Object)(object)val != (Object)(object)((Component)value).transform))
                {
                    stack.Push(((Object)val).name ?? string.Empty);
                    val = val.parent;
                }
                if (stack.Count > 0)
                {
                    return string.Join("/", stack.ToArray());
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private static string ResolveSuffixMatch(object value)
        {
            if ((Object)value == (Object)null)
            {
                return string.Empty;
            }
            string text = ExtractSuffixToken(((Object)((Component)value).gameObject).name);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = ExtractSuffixToken(((PsdLayerNode)value).GetSourceLayerName());
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            UGUIParser uGUIParser = UGUIParser.Instance;
            UGUIParseRule[] array = (((Object)(object)uGUIParser != (Object)null) ? uGUIParser.GetRules() : null);
            if (array != null)
            {
                foreach (UGUIParseRule uGUIParseRule in array)
                {
                    if (uGUIParseRule == null || IsTextMeshProUiType(uGUIParseRule.UIType) || uGUIParseRule.TypeMatches == null)
                    {
                        continue;
                    }
                    for (int j = 0; j < uGUIParseRule.TypeMatches.Length; j++)
                    {
                        if (string.Equals(text, uGUIParseRule.TypeMatches[j], StringComparison.OrdinalIgnoreCase))
                        {
                            return uGUIParseRule.UIType.ToString();
                        }
                    }
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private static string ExtractSuffixToken(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                value = ((string)value).Trim();
                int num = ((string)value).LastIndexOf('.');
                if (num < 0 || num + 1 >= ((string)value).Length)
                {
                    return string.Empty;
                }
                string text = ((string)value).Substring(num + 1).Trim();
                if (text.Length != 0)
                {
                    foreach (char c in text)
                    {
                        if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                        {
                            return string.Empty;
                        }
                    }
                    return text;
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private static bool IsTextMeshProUiType(GUIType uiType)
        {
            if ((uint)(uiType - 12) <= 4u)
            {
                return true;
            }
            return false;
        }

        private static AiAnalysisNodeEntry BuildNodeEntry(object value3, object value4, object value5, object value6, AiDocumentBounds value7, Dictionary<PsdLayerNode, string> lookup)
        {
            if ((Object)value6 == (Object)null)
            {
                return null;
            }
            CalculateNodeRect(value6, value7, out var x, out var y, out var w, out var h);
            PsdTextLayerInfo psdTextLayerInfo;
            bool isTextLayer = ((PsdLayerNode)value6).TryGetTextLayerInfo(out psdTextLayerInfo);
            string imageFile = BuildNodePreviewImage(value3, value6, value4, value5);
            string id = LayerNodeIdUtility.GetStableNodeId(value3, value6);
            lookup.TryGetValue((PsdLayerNode)value6, out var value);
            ((AiPackageBuildContext)value5)._nodeAssetsByNode.TryGetValue((PsdLayerNode)value6, out var value2);
            return new AiAnalysisNodeEntry
            {
                id = id,
                shortId = (value ?? string.Empty),
                parentId = GetParentNodeId(value3, ((Component)value6).transform.parent),
                childIds = GetChildNodeIds(value3, ((Component)value6).transform),
                onlyChildId = GetOnlyChildNodeId(value3, ((Component)value6).transform),
                idPath = BuildNodeIdPath(value3, ((Component)value6).transform, lookup),
                displayPath = BuildNodeDisplayPath(value3, ((Component)value6).transform),
                name = ((Object)((Component)value6).gameObject).name,
                layerName = ((PsdLayerNode)value6).GetSourceLayerName(),
                nameTokens = BuildNameTokens(((Object)((Component)value6).gameObject).name, ((PsdLayerNode)value6).GetSourceLayerName()),
                layerType = ((PsdLayerNode)value6).LayerType.ToString(),
                isGroupLayer = (((PsdLayerNode)value6).LayerType == PsdLayerType.LayerGroup),
                isTextLayer = isTextLayer,
                isGeneratedNode = (((PsdLayerNode)value6).BindPsdLayerIndex < 0),
                uiType = ((PsdLayerNode)value6).UIType.ToString(),
                rect = new RectData
                {
                    x = x,
                    y = y,
                    w = w,
                    h = h
                },
                imageFile = imageFile,
                previewKind = (string.IsNullOrWhiteSpace(value2._previewKind) ? "empty" : value2._previewKind),
                previewSourceId = (value2._previewSourceId ?? string.Empty),
                visualHash = (value2._visualHash ?? string.Empty),
                renderLeafCount = CountRenderLeaves(value6),
                suffixMatch = ResolveSuffixMatch(value6),
                siblingIndex = ((Component)value6).transform.GetSiblingIndex(),
                childCount = ((Component)value6).transform.childCount
            };
        }

        private static string[] GetChildNodeIds(object value, object value2)
        {
            if ((Object)value == (Object)null || (Object)value2 == (Object)null || ((Transform)value2).childCount < 1)
            {
                return Array.Empty<string>();
            }
            List<string> list = new List<string>(((Transform)value2).childCount);
            for (int i = 0; i < ((Transform)value2).childCount; i++)
            {
                PsdLayerNode component = ((Component)((Transform)value2).GetChild(i)).GetComponent<PsdLayerNode>();
                if (!((Object)(object)component == (Object)null))
                {
                    string text = LayerNodeIdUtility.GetStableNodeId(value, component);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        list.Add(text);
                    }
                }
            }
            if (list.Count > 0)
            {
                return list.ToArray();
            }
            return Array.Empty<string>();
        }

        private static string GetOnlyChildNodeId(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null))
            {
                PsdLayerNode psdLayerNode = null;
                for (int i = 0; i < ((Transform)value2).childCount; i++)
                {
                    PsdLayerNode component = ((Component)((Transform)value2).GetChild(i)).GetComponent<PsdLayerNode>();
                    if (!((Object)(object)component == (Object)null))
                    {
                        if ((Object)(object)psdLayerNode != (Object)null)
                        {
                            return string.Empty;
                        }
                        psdLayerNode = component;
                    }
                }
                if ((Object)(object)psdLayerNode != (Object)null)
                {
                    return LayerNodeIdUtility.GetStableNodeId(value, psdLayerNode);
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private static string[] BuildNameTokens(object value, object value2)
        {
            List<string> list = new List<string>(8);
            AppendNameTokens(list, value);
            AppendNameTokens(list, value2);
            if (list.Count <= 0)
            {
                return Array.Empty<string>();
            }
            return list.ToArray();
        }

        private static void AppendNameTokens(List<string> texts, object value)
        {
            if (texts == null || string.IsNullOrWhiteSpace((string)value))
            {
                return;
            }
            int length = ((string)value).Length;
            StringBuilder stringBuilder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                char c = ((string)value)[i];
                if (char.IsLetterOrDigit(c))
                {
                    stringBuilder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    FlushNameToken(texts, stringBuilder);
                }
            }
            FlushNameToken(texts, stringBuilder);
        }

        private static void FlushNameToken(List<string> texts, object value)
        {
            if (texts == null || value == null || ((StringBuilder)value).Length < 1)
            {
                return;
            }
            string text = value.ToString();
            ((StringBuilder)value).Length = 0;
            int num = 0;
            while (true)
            {
                if (num < texts.Count)
                {
                    if (!string.Equals(texts[num], text, StringComparison.Ordinal))
                    {
                        num++;
                        continue;
                    }
                    break;
                }
                texts.Add(text);
                break;
            }
        }

        private static void CalculateNodeRect(object value, AiDocumentBounds value2, out float result, out float result2, out float result3, out float result4)
        {
            result = 0f;
            result2 = 0f;
            result3 = 0f;
            result4 = 0f;
            if (!((Object)value == (Object)null))
            {
                PsdLayer psdLayer = ((PsdLayerNode)value).GetBoundPsdLayer();
                if (psdLayer != null)
                {
                    result = psdLayer.Left - value2._left;
                    result2 = psdLayer.Top - value2._top;
                    result3 = psdLayer.Right - psdLayer.Left;
                    result4 = psdLayer.Bottom - psdLayer.Top;
                    return;
                }
                Rect val = ((PsdLayerNode)value).GetLayerRect();
                float num = val.width * 0.5f;
                float num2 = val.height * 0.5f;
                float num3 = ((value2._documentWidth > 0) ? value2._documentWidth : value2._previewWidth);
                float num4 = ((value2._documentHeight > 0) ? value2._documentHeight : value2._previewHeight);
                result = val.x + num3 * 0.5f - num - (float)value2._left;
                result2 = num4 * 0.5f - val.y - num2 - (float)value2._top;
                result3 = val.width;
                result4 = val.height;
            }
        }

        private static string GetParentNodeId(object value, object value2)
        {
            if (!((Object)value == (Object)null) && !((Object)value2 == (Object)null) && !((Object)value2 == (Object)(object)((Component)value).transform))
            {
                PsdLayerNode component = ((Component)value2).GetComponent<PsdLayerNode>();
                if ((Object)(object)component != (Object)null)
                {
                    return LayerNodeIdUtility.GetStableNodeId(value, component);
                }
                return "root";
            }
            return "root";
        }

        private static string BuildNodePreviewImage(object value3, object value4, object value5, object value6)
        {
            if (!((Object)value4 == (Object)null) && value5 != null && !string.IsNullOrWhiteSpace(((AiPackagePaths)value5)._nodePreviewDirectory))
            {
                if (value6 == null || !((AiPackageBuildContext)value6)._nodeAssetsByNode.TryGetValue((PsdLayerNode)value4, out var value))
                {
                    if (TryGetPreviewPassthroughChild(value4, out var psdLayerNode))
                    {
                        string text = BuildNodePreviewImage(value3, psdLayerNode, value5, value6);
                        ((AiPackageBuildContext)value6)._nodeAssetsByNode.TryGetValue(psdLayerNode, out var value2);
                        AiNodeAssetInfo value7 = new AiNodeAssetInfo
                        {
                            _imageFile = text,
                            _previewKind = "inherited",
                            _previewSourceId = LayerNodeIdUtility.GetStableNodeId(value3, psdLayerNode),
                            _visualHash = (value2._visualHash ?? ComputeFileSha256Hex(value5, text))
                        };
                        RecordNodeAssetInfo(value6, value4, value7, value5);
                        return text;
                    }
                    Texture2D val = null;
                    bool flag = false;
                    string text5 = "empty";
                    try
                    {
                        if (TryGetNodePreviewTexture(value4, out val, out flag, out text5) && !((Object)(object)val == (Object)null))
                        {
                            string text2 = SanitizeFileName(LayerNodeIdUtility.GetStableNodeId(value3, value4));
                            if (string.IsNullOrWhiteSpace(text2))
                            {
                                text2 = "node";
                            }
                            string text3 = Path.Combine(((AiPackagePaths)value5)._nodePreviewDirectory, text2 + ".png");
                            text3 = EnsureTexturePng(val, text3, (AiPackageBuildContext)value6, 4);
                            string text4 = ((value6 == null || string.IsNullOrWhiteSpace(((AiPackageBuildContext)value6)._projectRootPath)) ? Directory.GetParent(Application.dataPath).FullName : ((AiPackageBuildContext)value6)._projectRootPath);
                            AiNodeAssetInfo value8 = new AiNodeAssetInfo
                            {
                                _imageFile = GetNormalizedRelativePath(text4, text3),
                                _previewKind = text5,
                                _previewSourceId = string.Empty,
                                _visualHash = ComputeFileSha256Hex(value5, GetNormalizedRelativePath(text4, text3))
                            };
                            RecordNodeAssetInfo(value6, value4, value8, value5);
                            return value8._imageFile;
                        }
                        RecordNodeAssetInfo(value6, value4, new AiNodeAssetInfo
                        {
                            _imageFile = string.Empty,
                            _previewKind = "empty",
                            _previewSourceId = string.Empty,
                            _visualHash = string.Empty
                        }, value5);
                        return string.Empty;
                    }
                    finally
                    {
                        if (flag && (Object)(object)val != (Object)null)
                        {
                            Object.DestroyImmediate((Object)(object)val);
                        }
                    }
                }
                return value._imageFile;
            }
            return string.Empty;
        }

        private static bool TryGetNodePreviewTexture(object value, out Texture2D result, out bool result2, out string result3)
        {
            result = null;
            result2 = false;
            result3 = "empty";
            if ((Object)value == (Object)null)
            {
                return false;
            }
            if (!CanRenderCompositePreview(value) || !TryRenderCompositePreview(value, out result))
            {
                if (!((PsdLayerNode)value).RefreshPreviewTexture(false) || (Object)(object)((PsdLayerNode)value).PreviewTexture == (Object)null)
                {
                    return false;
                }
                result = ((PsdLayerNode)value).PreviewTexture;
                result3 = ((((PsdLayerNode)value).BindPsdLayerIndex >= 0) ? "self" : "generated");
                return true;
            }
            result2 = true;
            result3 = "composite";
            return true;
        }

        private static bool CanRenderCompositePreview(object value)
        {
            if ((Object)value != (Object)null && ((PsdLayerNode)value).LayerType == PsdLayerType.LayerGroup && (Object)(object)((Component)value).transform != (Object)null)
            {
                return ((Component)value).transform.childCount > 0;
            }
            return false;
        }

        private static bool TryRenderCompositePreview(object value, out Texture2D result)
        {
            result = null;
            List<PsdLayer> list = new List<PsdLayer>(Mathf.Max(1, ((Component)value).transform.childCount * 2));
            CollectCompositeLayers(value, list);
            if (list.Count < 1)
            {
                return false;
            }
            PsdRenderedImage psdRenderedImage = PsdLayerRenderer.MergeLayers(list, includeHiddenLayers: false, applyClippingMasks: true, isPreviewRender: true);
            result = CreateTextureFromRenderedImage(psdRenderedImage);
            return (Object)(object)result != (Object)null;
        }

        private static void CollectCompositeLayers(object value, List<PsdLayer> psdLayers)
        {
            if ((Object)value == (Object)null || psdLayers == null || !((Component)value).gameObject.activeSelf)
            {
                return;
            }
            bool flag = false;
            int i = 0;
            for (int childCount = ((Component)value).transform.childCount; i < childCount; i++)
            {
                PsdLayerNode component = ((Component)((Component)value).transform.GetChild(i)).GetComponent<PsdLayerNode>();
                if (!((Object)(object)component == (Object)null))
                {
                    flag = true;
                    CollectCompositeLayers(component, psdLayers);
                }
            }
            PsdLayer psdLayer = ((PsdLayerNode)value).GetBoundPsdLayer();
            if (psdLayer != null && psdLayer.IsVisible && !(((PsdLayerNode)value).LayerType == PsdLayerType.LayerGroup && flag))
            {
                psdLayers.Add(psdLayer);
            }
        }

        private static Texture2D CreateTextureFromRenderedImage(object value)
        {
            if (value != null && !((PsdRenderedImage)value).IsEmpty)
            {
                TextureFormat val = (TextureFormat)((!((PsdRenderedImage)value).IsHighBitDepth) ? 4 : 74);
                Texture2D val2 = new Texture2D(((PsdRenderedImage)value).Width, ((PsdRenderedImage)value).Height, val, false);
                ((Object)val2).hideFlags = (HideFlags)61;
                val2.alphaIsTransparency = true;
                if (((PsdRenderedImage)value).IsHighBitDepth)
                {
                    ushort[] rgba = ((PsdRenderedImage)value).Rgba64;
                    byte[] array = new byte[rgba.Length * 2];
                    Buffer.BlockCopy(rgba, 0, array, 0, array.Length);
                    val2.LoadRawTextureData(array);
                }
                else
                {
                    val2.LoadRawTextureData(((PsdRenderedImage)value).Rgba32);
                }
                val2.Apply(false, false);
                return val2;
            }
            return null;
        }

        private static bool TryGetPreviewPassthroughChild(object value, out PsdLayerNode result)
        {
            result = null;
            if (!((Object)value == (Object)null) && ((PsdLayerNode)value).LayerType == PsdLayerType.LayerGroup && (((PsdLayerNode)value).UIType == GUIType.Panel || ((PsdLayerNode)value).UIType == GUIType.Null) && !((PsdLayerNode)value).ShouldCollapseChildrenForGeneration())
            {
                PsdLayer psdLayer = ((PsdLayerNode)value).GetBoundPsdLayer();
                if (psdLayer == null || !psdLayer.IsPreviewPassthroughGroup())
                {
                    return false;
                }
                PsdLayerNode psdLayerNode = null;
                int num = 0;
                while (true)
                {
                    if (num < ((Component)value).transform.childCount)
                    {
                        PsdLayerNode component = ((Component)((Component)value).transform.GetChild(num)).GetComponent<PsdLayerNode>();
                        if (!((Object)(object)component == (Object)null) && ((Component)component).gameObject.activeSelf && (component.GetBoundPsdLayer() == null || component.GetBoundPsdLayer().IsVisible))
                        {
                            if ((Object)(object)psdLayerNode != (Object)null)
                            {
                                break;
                            }
                            psdLayerNode = component;
                        }
                        num++;
                        continue;
                    }
                    result = psdLayerNode;
                    return (Object)(object)result != (Object)null;
                }
                return false;
            }
            return false;
        }

        private static void RecordNodeAssetInfo(object value, object value2, AiNodeAssetInfo value3, object value4)
        {
            if (value != null && !((Object)value2 == (Object)null))
            {
                ((AiPackageBuildContext)value)._nodeAssetsByNode[(PsdLayerNode)value2] = value3;
                if (!string.IsNullOrWhiteSpace(value3._imageFile))
                {
                    string path = Path.Combine(((AiPackageBuildContext)value)._projectRootPath, value3._imageFile).Replace("\\", "/");
                    ((AiPackageBuildContext)value)._retainedPreviewPaths.Add(Path.GetFullPath(path).Replace("\\", "/"));
                }
            }
        }

        private static int CountRenderLeaves(object value)
        {
            if ((Object)value == (Object)null)
            {
                return 0;
            }
            int num = 0;
            int num2 = 0;
            for (int i = 0; i < ((Component)value).transform.childCount; i++)
            {
                PsdLayerNode component = ((Component)((Component)value).transform.GetChild(i)).GetComponent<PsdLayerNode>();
                if (!((Object)(object)component == (Object)null))
                {
                    num++;
                    num2 += CountRenderLeaves(component);
                }
            }
            if (((PsdLayerNode)value).GetBoundPsdLayer() != null)
            {
                if (((PsdLayerNode)value).LayerType == PsdLayerType.LayerGroup && num > 0)
                {
                    return num2;
                }
                if (num2 > 0)
                {
                    return num2;
                }
                return 1;
            }
            return num2;
        }

        private static string ComputeFileSha256Hex(object value, object value2)
        {
            string fullName = Directory.GetParent(Application.dataPath).FullName;
            string text = (string)value2;
            if (!string.IsNullOrWhiteSpace(text) && !Path.IsPathRooted(text))
            {
                text = Path.Combine(fullName, text);
            }
            if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
            {
                using (SHA256 sHA = SHA256.Create())
                {
                    byte[] buffer = File.ReadAllBytes(text);
                    byte[] array = sHA.ComputeHash(buffer);
                    StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
                    for (int i = 0; i < array.Length; i++)
                    {
                        stringBuilder.Append(array[i].ToString("x2"));
                    }
                    return stringBuilder.ToString();
                }
            }
            return string.Empty;
        }

        private static void BuildVisualInputs(object value, object value2, object value3)
        {
            if (value != null && ((AiAnalysisPackageDocument)value).document != null && value2 != null && !string.IsNullOrWhiteSpace((string)value3))
            {
                ((AiAnalysisPackageDocument)value).document.visualInputPaths.Clear();
                AddUniquePath(((AiAnalysisPackageDocument)value).document.visualInputPaths, ((AiAnalysisPackageDocument)value).document.previewImagePath);
                if (TryBuildAnnotatedPreview(value, value2, value3, out var text))
                {
                    ((AiAnalysisPackageDocument)value).document.annotatedPreviewImagePath = text;
                    AddUniquePath(((AiAnalysisPackageDocument)value).document.visualInputPaths, text);
                }
                BuildNodeAtlases(value, value2, value3);
            }
        }

        private static void AddUniquePath(List<string> texts, object value)
        {
            if (texts == null || string.IsNullOrWhiteSpace((string)value))
            {
                return;
            }
            int num = 0;
            while (true)
            {
                if (num < texts.Count)
                {
                    if (!string.Equals(texts[num], (string)value, StringComparison.OrdinalIgnoreCase))
                    {
                        num++;
                        continue;
                    }
                    break;
                }
                texts.Add((string)value);
                break;
            }
        }

        private static bool TryBuildAnnotatedPreview(object value, object value2, object value3, out string result)
        {
            result = string.Empty;
            string text = ResolvePath(value3, ((AiAnalysisPackageDocument)value).document.previewImagePath);
            if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
            {
                Texture2D val = LoadTextureFromFile(text);
                if ((Object)(object)val == (Object)null)
                {
                    return false;
                }
                try
                {
                    List<AiAnalysisNodeEntry> nodes = ((AiAnalysisPackageDocument)value).nodes;
                    if (nodes != null)
                    {
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            AiAnalysisNodeEntry aiAnalysisNodeEntry = nodes[i];
                            if (aiAnalysisNodeEntry != null && aiAnalysisNodeEntry.rect != null && !string.IsNullOrWhiteSpace(aiAnalysisNodeEntry.shortId))
                            {
                                DrawRectangleOutline(val, Mathf.RoundToInt(aiAnalysisNodeEntry.rect.x), Mathf.RoundToInt(aiAnalysisNodeEntry.rect.y), Mathf.RoundToInt(aiAnalysisNodeEntry.rect.w), Mathf.RoundToInt(aiAnalysisNodeEntry.rect.h), new Color32(byte.MaxValue, (byte)215, (byte)64, byte.MaxValue));
                                DrawNodeLabel(val, aiAnalysisNodeEntry.shortId, Mathf.RoundToInt(aiAnalysisNodeEntry.rect.x), Mathf.RoundToInt(aiAnalysisNodeEntry.rect.y));
                            }
                        }
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(((AiPackagePaths)value2)._annotatedPreviewPath));
                    val.Apply(false, false);
                    File.WriteAllBytes(((AiPackagePaths)value2)._annotatedPreviewPath, ImageConversion.EncodeToPNG(val));
                    result = GetNormalizedRelativePath(value3, ((AiPackagePaths)value2)._annotatedPreviewPath);
                    return true;
                }
                finally
                {
                    Object.DestroyImmediate((Object)(object)val);
                }
            }
            return false;
        }

        private static void DrawNodeLabel(object value, object value2, int value3, int value4)
        {
            if (!((Object)value == (Object)null) && !string.IsNullOrWhiteSpace((string)value2))
            {
                int num = Mathf.Clamp(value3, 0, Mathf.Max(0, ((Texture)value).width - 38));
                int num2 = value4 - 13;
                if (num2 < 0)
                {
                    num2 = Mathf.Clamp(value4, 0, Mathf.Max(0, ((Texture)value).height - 13));
                }
                FillRectangle(value, num, num2, 38, 13, new Color32((byte)0, (byte)0, (byte)0, (byte)190));
                DrawBitmapText(value, ((string)value2).ToUpperInvariant(), num + 3, num2 + 3, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1);
            }
        }

        private static void BuildNodeAtlases(object value, object value2, object value3)
        {
            if (value == null || ((AiAnalysisPackageDocument)value).nodes == null || ((AiAnalysisPackageDocument)value).nodes.Count == 0 || value2 == null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(((AiPackagePaths)value2)._nodeAtlasDirectory) && Directory.Exists(((AiPackagePaths)value2)._nodeAtlasDirectory))
            {
                string[] files = Directory.GetFiles(((AiPackagePaths)value2)._nodeAtlasDirectory, "node-atlas-*.png", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    AiJobFileStore.DeleteFileIfExists(files[i]);
                }
            }
            Directory.CreateDirectory(((AiPackagePaths)value2)._nodeAtlasDirectory);
            int num = 32;
            int num2 = (((AiAnalysisPackageDocument)value).nodes.Count + 32 - 1) / 32;
            for (int j = 0; j < num2; j++)
            {
                Texture2D val = new Texture2D(2048, 880, (TextureFormat)4, false);
                FillRectangle(val, 0, 0, ((Texture)val).width, ((Texture)val).height, _transparentColor);
                for (int k = 0; k < num; k++)
                {
                    int num3 = j * num + k;
                    if (num3 >= ((AiAnalysisPackageDocument)value).nodes.Count)
                    {
                        break;
                    }
                    AiAnalysisNodeEntry aiAnalysisNodeEntry = ((AiAnalysisPackageDocument)value).nodes[num3];
                    int num4 = k % 8;
                    int num5 = k / 8;
                    int num6 = num4 * 256;
                    int num7 = num5 * 220;
                    int num8 = num6 + 8;
                    int num9 = num7 + 8;
                    int num10 = 240;
                    int num11 = 170;
                    int num12 = num6 + 8;
                    int num13 = num7 + 220 - 34 + 4;
                    int num14 = 240;
                    int num15 = 26;
                    DrawRectangleBorder(val, num6, num7, 256, 220, _atlasCellBorderColor, 1);
                    if (aiAnalysisNodeEntry != null)
                    {
                        int num16 = num8 + 12;
                        int num17 = num9 + 12;
                        int num18 = num10 - 24;
                        int num19 = num11 - 24;
                        if (num18 <= 0 || num19 <= 0)
                        {
                            num16 = num8;
                            num17 = num9;
                            num18 = num10;
                            num19 = num11;
                        }
                        DrawNodePreviewIntoAtlas(val, aiAnalysisNodeEntry, value3, ((AiAnalysisPackageDocument)value).document.nodePreviewDirectoryPath, num16, num17, num18, num19, out var imageRect);
                        string page = $"node-atlas-{j:000}.png";
                        aiAnalysisNodeEntry.atlas = new AiAtlasRef
                        {
                            page = page,
                            cell = k,
                            label = (aiAnalysisNodeEntry.shortId ?? string.Empty),
                            imageRect = imageRect,
                            labelRect = new RectData
                            {
                                x = num12,
                                y = num13,
                                w = num14,
                                h = num15
                            }
                        };
                        string text = aiAnalysisNodeEntry.shortId + " " + AbbreviateLayerType(aiAnalysisNodeEntry.layerType) + " " + AbbreviateUiType(aiAnalysisNodeEntry.uiType);
                        DrawBitmapTextClipped(val, text.ToUpperInvariant(), num12, num13, num14, new Color32((byte)245, (byte)245, (byte)245, byte.MaxValue), 2);
                    }
                }
                string text2 = Path.Combine(((AiPackagePaths)value2)._nodeAtlasDirectory, $"node-atlas-{j:000}.png");
                NormalizeTransparentPixels(val);
                val.Apply(false, false);
                File.WriteAllBytes(text2, ImageConversion.EncodeToPNG(val));
                AddUniquePath(((AiAnalysisPackageDocument)value).document.visualInputPaths, GetNormalizedRelativePath(value3, text2));
                Object.DestroyImmediate((Object)(object)val);
            }
        }

        private static void DrawNodePreviewIntoAtlas(object value, object value2, object value3, object value4, int value5, int value6, int value7, int value8, out RectData result)
        {
            result = new RectData
            {
                x = value5,
                y = value6,
                w = 0f,
                h = 0f
            };
            if ((Object)value == (Object)null || value2 == null || string.IsNullOrWhiteSpace(((AiAnalysisNodeEntry)value2).imageFile))
            {
                return;
            }
            string text = ResolveNodeImagePath(value3, value4, ((AiAnalysisNodeEntry)value2).imageFile);
            if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
            {
                return;
            }
            Texture2D val = LoadTextureFromFile(text);
            if ((Object)(object)val == (Object)null)
            {
                return;
            }
            try
            {
                float num = Mathf.Min((float)value7 / (float)Mathf.Max(1, ((Texture)val).width), (float)value8 / (float)Mathf.Max(1, ((Texture)val).height));
                int num2 = Mathf.Max(1, Mathf.RoundToInt((float)((Texture)val).width * num));
                int num3 = Mathf.Max(1, Mathf.RoundToInt((float)((Texture)val).height * num));
                int num4 = value5 + (value7 - num2) / 2;
                int num5 = value6 + (value8 - num3) / 2;
                DrawScaledTexture(val, value, num4, num5, num2, num3);
                result = new RectData
                {
                    x = num4,
                    y = num5,
                    w = num2,
                    h = num3
                };
            }
            finally
            {
                Object.DestroyImmediate((Object)(object)val);
            }
        }

        private static string AbbreviateLayerType(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                if ((string)value == "LayerGroup")
                {
                    return "G";
                }
                if ((string)value == "TextLayer")
                {
                    return "T";
                }
                if ((string)value == "FillLayer")
                {
                    return "F";
                }
                if (!((string)value == "Layer"))
                {
                    if (((string)value).Length <= 3)
                    {
                        return (string)value;
                    }
                    return ((string)value).Substring(0, 3);
                }
                return "L";
            }
            return "L";
        }

        private static string AbbreviateUiType(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return "NULL";
            }
            switch (value)
            {
            case "Toggle_Label":
                return "TG_LBL";
            case "Toggle_Checkmark":
                return "TG_MARK";
            case "Button_Text":
                return "BT_TXT";
            case "Null":
                return "NULL";
            case "InputField_Placeholder":
                return "IPT_PH";
            case "Dropdown":
                return "DPD";
            case "ScrollView":
                return "SV";
            case "Background":
                return "BG";
            case "Dropdown_Label":
                return "DPD_LBL";
            case "Slider_Fill":
                return "SLD_FILL";
            case "RawImage":
                return "RAW";
            case "Panel":
                return "PNL";
            case "InputField_Text":
                return "IPT_TXT";
            case "Slider_Handle":
                return "SLD_HDL";
            case "Text":
                return "TXT";
            case "Dropdown_Arrow":
                return "DPD_ARR";
            case "Button":
                return "BTN";
            case "FillColor":
                return "COL";
            case "ToggleGroup":
                return "TGG";
            case "InputField":
                return "IPT";
            case "Mask":
                return "MSK";
            case "Slider":
                return "SLD";
            case "Toggle":
                return "TGL";
            case "Image":
                return "IMG";
            default:
                if (((string)value).Length <= 8)
                {
                    return (string)value;
                }
                return ((string)value).Substring(0, 8);
            }
        }

        private static Texture2D LoadTextureFromFile(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && File.Exists((string)value))
            {
                Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false);
                if (ImageConversion.LoadImage(val, File.ReadAllBytes((string)value)))
                {
                    return val;
                }
                Object.DestroyImmediate((Object)(object)val);
                return null;
            }
            return null;
        }

        private static string ResolvePath(object value, object value2)
        {
            if (string.IsNullOrWhiteSpace((string)value2))
            {
                return string.Empty;
            }
            if (!Path.IsPathRooted((string)value2))
            {
                if (!string.IsNullOrWhiteSpace((string)value))
                {
                    return Path.Combine((string)value, (string)value2).Replace("\\", "/");
                }
                return (string)value2;
            }
            return (string)value2;
        }

        private static string ResolveNodeImagePath(object value, object value2, object value3)
        {
            if (!string.IsNullOrWhiteSpace((string)value3))
            {
                if (Path.IsPathRooted((string)value3))
                {
                    return (string)value3;
                }
                if (((string)value3).IndexOf('/') >= 0 || ((string)value3).IndexOf('\\') >= 0)
                {
                    return ResolvePath(value, value3);
                }
                string text = ResolvePath(value, value2);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return Path.Combine(text, (string)value3).Replace("\\", "/");
                }
                return string.Empty;
            }
            return string.Empty;
        }

        private static void DrawRectangleOutline(object value, int value2, int value3, int value4, int value5, Color32 value6)
        {
            if (!((Object)value == (Object)null) && value4 > 0 && value5 > 0)
            {
                for (int i = 0; i < value4; i++)
                {
                    SetTopLeftPixel(value, value2 + i, value3, (Color32)(value6));
                    SetTopLeftPixel(value, value2 + i, value3 + value5 - 1, (Color32)(value6));
                }
                for (int j = 0; j < value5; j++)
                {
                    SetTopLeftPixel(value, value2, value3 + j, (Color32)(value6));
                    SetTopLeftPixel(value, value2 + value4 - 1, value3 + j, (Color32)(value6));
                }
            }
        }

        private static void DrawRectangleBorder(object value, int value2, int value3, int value4, int value5, Color32 value6, int value7)
        {
            if (!((Object)value == (Object)null) && value4 > 0 && value5 > 0 && value7 > 0)
            {
                for (int i = 0; i < value7; i++)
                {
                    DrawRectangleOutline(value, value2 + i, value3 + i, value4 - i * 2, value5 - i * 2, value6);
                }
            }
        }

        private static void FillRectangle(object value, int value2, int value3, int value4, int value5, Color32 value6)
        {
            if ((Object)value == (Object)null || value4 <= 0 || value5 <= 0)
            {
                return;
            }
            for (int i = 0; i < value5; i++)
            {
                for (int j = 0; j < value4; j++)
                {
                    SetTopLeftPixel(value, value2 + j, value3 + i, (Color32)(value6));
                }
            }
        }

        private static void DrawScaledTexture(object value, object value2, int value3, int value4, int value5, int value6)
        {
            if ((Object)value == (Object)null || (Object)value2 == (Object)null || value5 <= 0 || value6 <= 0)
            {
                return;
            }
            for (int i = 0; i < value6; i++)
            {
                float num = ((value6 > 1) ? ((float)i / (float)(value6 - 1)) : 0f);
                for (int j = 0; j < value5; j++)
                {
                    float num2 = ((value5 > 1) ? ((float)j / (float)(value5 - 1)) : 0f);
                    Color val = SampleTextureBilinear(value, num2, 1f - num);
                    Color val2 = GetTopLeftPixel(value2, value3 + j, value4 + i);
                    SetTopLeftPixel(value2, value3 + j, value4 + i, AlphaComposite(val, val2));
                }
            }
        }

        private static Color SampleTextureBilinear(object value, float value2, float value3)
        {
            float num = (float)((Texture)value).width + 2f;
            float num2 = (float)((Texture)value).height + 2f;
            float num3 = Mathf.Clamp01(value2) * (num - 1f) - 1f;
            float num4 = Mathf.Clamp01(value3) * (num2 - 1f) - 1f;
            int num5 = Mathf.FloorToInt(num3);
            int num6 = Mathf.FloorToInt(num4);
            int num7 = num5 + 1;
            int num8 = num6 + 1;
            float num9 = num3 - (float)num5;
            float num10 = num4 - (float)num6;
            Color val = GetPixelOrClear(value, num5, num6);
            Color val2 = GetPixelOrClear(value, num7, num6);
            Color val3 = GetPixelOrClear(value, num5, num8);
            Color val4 = GetPixelOrClear(value, num7, num8);
            float num11 = (1f - num9) * (1f - num10);
            float num12 = num9 * (1f - num10);
            float num13 = (1f - num9) * num10;
            float num14 = num9 * num10;
            float num15 = val.a * num11 + val2.a * num12 + val3.a * num13 + val4.a * num14;
            if (num15 <= 0.0001f)
            {
                return Color.clear;
            }
            float num16 = (val.r * val.a * num11 + val2.r * val2.a * num12 + val3.r * val3.a * num13 + val4.r * val4.a * num14) / num15;
            float num17 = (val.g * val.a * num11 + val2.g * val2.a * num12 + val3.g * val3.a * num13 + val4.g * val4.a * num14) / num15;
            float num18 = (val.b * val.a * num11 + val2.b * val2.a * num12 + val3.b * val3.a * num13 + val4.b * val4.a * num14) / num15;
            return new Color(num16, num17, num18, num15);
        }

        private static Color GetPixelOrClear(object value, int value2, int value3)
        {
            if (value2 >= 0 && value3 >= 0 && value2 < ((Texture)value).width && value3 < ((Texture)value).height)
            {
                return ((Texture2D)value).GetPixel(value2, value3);
            }
            return Color.clear;
        }

        private static Color AlphaComposite(Color color, Color color2)
        {
            float a = color.a;
            float a2 = color2.a;
            float num = 1f - a;
            float num2 = a + a2 * num;
            if (num2 <= 0.0001f)
            {
                return Color.clear;
            }
            float num3 = (color.r * a + color2.r * a2 * num) / num2;
            float num4 = (color.g * a + color2.g * a2 * num) / num2;
            float num5 = (color.b * a + color2.b * a2 * num) / num2;
            return new Color(num3, num4, num5, num2);
        }

        private static Color GetTopLeftPixel(object value, int value2, int value3)
        {
            if (!((Object)value == (Object)null) && value2 >= 0 && value3 >= 0 && value2 < ((Texture)value).width && value3 < ((Texture)value).height)
            {
                return ((Texture2D)value).GetPixel(value2, ((Texture)value).height - 1 - value3);
            }
            return Color.clear;
        }

        private static void SetTopLeftPixel(object value, int value2, int value3, Color color)
        {
            if (!((Object)value == (Object)null) && value2 >= 0 && value3 >= 0 && value2 < ((Texture)value).width && value3 < ((Texture)value).height)
            {
                ((Texture2D)value).SetPixel(value2, ((Texture)value).height - 1 - value3, color);
            }
        }

        private static void DrawBitmapText(object value, object value2, int value3, int value4, Color32 value5, int value6)
        {
            if ((Object)value == (Object)null || string.IsNullOrEmpty((string)value2))
            {
                return;
            }
            if (value6 < 1)
            {
                value6 = 1;
            }
            int num = value3;
            int num2 = ((Texture)value).width - 1;
            for (int i = 0; i < ((string)value2).Length; i++)
            {
                string[] array = GetBitmapGlyph(((string)value2)[i]);
                if (array == null)
                {
                    num += 4 * value6;
                    continue;
                }
                for (int j = 0; j < array.Length; j++)
                {
                    string text = array[j];
                    for (int k = 0; k < text.Length; k++)
                    {
                        if (text[k] != '1')
                        {
                            continue;
                        }
                        for (int l = 0; l < value6; l++)
                        {
                            for (int m = 0; m < value6; m++)
                            {
                                SetTopLeftPixel(value, num + k * value6 + m, value4 + j * value6 + l, (Color32)(value5));
                            }
                        }
                    }
                }
                num += 6 * value6;
                if (num > num2)
                {
                    break;
                }
            }
        }

        private static void DrawBitmapTextClipped(object value, object value2, int value3, int value4, int value5, Color32 value6, int value7)
        {
            if ((Object)value == (Object)null || string.IsNullOrEmpty((string)value2) || value5 <= 0)
            {
                return;
            }
            if (value7 < 1)
            {
                value7 = 1;
            }
            int num = value3;
            int num2 = value3 + value5;
            for (int i = 0; i < ((string)value2).Length; i++)
            {
                string[] array = GetBitmapGlyph(((string)value2)[i]);
                int num3 = ((array != null) ? (5 * value7) : (4 * value7));
                if (num + num3 > num2)
                {
                    break;
                }
                if (array != null)
                {
                    for (int j = 0; j < array.Length; j++)
                    {
                        string text = array[j];
                        for (int k = 0; k < text.Length; k++)
                        {
                            if (text[k] != '1')
                            {
                                continue;
                            }
                            int num4 = num + k * value7;
                            if (num4 >= num2)
                            {
                                continue;
                            }
                            for (int l = 0; l < value7; l++)
                            {
                                for (int m = 0; m < value7; m++)
                                {
                                    int num5 = num4 + m;
                                    if (num5 < num2)
                                    {
                                        SetTopLeftPixel(value, num5, value4 + j * value7 + l, (Color32)(value6));
                                    }
                                }
                            }
                        }
                    }
                    num += 6 * value7;
                }
                else
                {
                    num += 4 * value7;
                }
            }
        }

        private static string[] GetBitmapGlyph(char value)
        {
            return char.ToUpperInvariant(value) switch
            {
                '|' => new string[7] { "00100", "00100", "00100", "00100", "00100", "00100", "00100" }, 
                ' ' => new string[7] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" }, 
                '-' => new string[7] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" }, 
                '.' => new string[7] { "00000", "00000", "00000", "00000", "00000", "01100", "01100" }, 
                '/' => new string[7] { "00001", "00010", "00010", "00100", "01000", "01000", "10000" }, 
                '0' => new string[7] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" }, 
                '1' => new string[7] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" }, 
                '2' => new string[7] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" }, 
                '3' => new string[7] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" }, 
                '4' => new string[7] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" }, 
                '5' => new string[7] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" }, 
                '6' => new string[7] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" }, 
                '7' => new string[7] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" }, 
                '8' => new string[7] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" }, 
                '9' => new string[7] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" }, 
                ':' => new string[7] { "00000", "00100", "00100", "00000", "00100", "00100", "00000" }, 
                'A' => new string[7] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" }, 
                'B' => new string[7] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" }, 
                'C' => new string[7] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" }, 
                'D' => new string[7] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" }, 
                'E' => new string[7] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" }, 
                'F' => new string[7] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" }, 
                'G' => new string[7] { "01111", "10000", "10000", "10111", "10001", "10001", "01110" }, 
                'H' => new string[7] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" }, 
                'I' => new string[7] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" }, 
                'J' => new string[7] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" }, 
                'K' => new string[7] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" }, 
                'L' => new string[7] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" }, 
                'M' => new string[7] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" }, 
                'N' => new string[7] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" }, 
                'O' => new string[7] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" }, 
                'P' => new string[7] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" }, 
                'Q' => new string[7] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" }, 
                'R' => new string[7] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" }, 
                'S' => new string[7] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" }, 
                'T' => new string[7] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" }, 
                'U' => new string[7] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" }, 
                'V' => new string[7] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" }, 
                'W' => new string[7] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" }, 
                'X' => new string[7] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" }, 
                'Y' => new string[7] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" }, 
                'Z' => new string[7] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" }, 
                '_' => new string[7] { "00000", "00000", "00000", "00000", "00000", "00000", "11111" }, 
                _ => null, 
            };
        }

        private static void DeleteStaleNodePreviews(object value, object value2)
        {
            if (value == null || value2 == null)
            {
                return;
            }
            string text = ((AiPackagePaths)value)._nodePreviewDirectory;
            if (string.IsNullOrWhiteSpace(text) || !Directory.Exists(text))
            {
                return;
            }
            string[] files = Directory.GetFiles(text, "*.png", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string item = Path.GetFullPath(files[i]).Replace("\\", "/");
                if (!((AiPackageBuildContext)value2)._retainedPreviewPaths.Contains(item))
                {
                    AiJobFileStore.DeleteFileIfExists(files[i]);
                }
            }
        }

        private static string EnsureTexturePng(object value, object value2, AiPackageBuildContext value3 = null, int value4 = 0)
        {
            if ((Object)value == (Object)null || string.IsNullOrWhiteSpace((string)value2))
            {
                return (string)value2;
            }
            if (value4 <= 0 && File.Exists((string)value2))
            {
                value3?._retainedPreviewPaths.Add(Path.GetFullPath((string)value2).Replace("\\", "/"));
                return (string)value2;
            }
            return WriteTexturePng(value, value2, value3, value4);
        }

        private static string WriteTexturePng(object value2, object value3, object value4, int value5 = 0)
        {
            if ((Object)value2 == (Object)null || string.IsNullOrWhiteSpace((string)value3))
            {
                return (string)value3;
            }
            int width = ((Texture)value2).width;
            int height = ((Texture)value2).height;
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, (RenderTextureFormat)0, (RenderTextureReadWrite)2);
            RenderTexture active = RenderTexture.active;
            try
            {
                Graphics.Blit((Texture)value2, temporary);
                RenderTexture.active = temporary;
                Texture2D val = new Texture2D(width, height, (TextureFormat)4, false);
                val.ReadPixels(new Rect(0f, 0f, (float)width, (float)height), 0, 0);
                val.Apply(false, false);
                val = AddTransparentPadding(val, value5);
                NormalizeTransparentPixels(val);
                byte[] array = ImageConversion.EncodeToPNG(val);
                string text = ComputeImageContentHash(((Texture)val).width, ((Texture)val).height, array);
                if (value4 != null && !string.IsNullOrEmpty(text) && ((AiPackageBuildContext)value4)._assetPathByContentHash.TryGetValue(text, out var value) && !string.IsNullOrWhiteSpace(value) && File.Exists(value))
                {
                    ((AiPackageBuildContext)value4)._retainedPreviewPaths.Add(Path.GetFullPath(value).Replace("\\", "/"));
                    Object.DestroyImmediate((Object)(object)val);
                    return value;
                }
                Directory.CreateDirectory(Path.GetDirectoryName((string)value3));
                File.WriteAllBytes((string)value3, array);
                if (value4 != null && !string.IsNullOrEmpty(text))
                {
                    ((AiPackageBuildContext)value4)._assetPathByContentHash[text] = (string)value3;
                }
                ((AiPackageBuildContext)value4)?._retainedPreviewPaths.Add(Path.GetFullPath((string)value3).Replace("\\", "/"));
                Object.DestroyImmediate((Object)(object)val);
                return (string)value3;
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Texture2D AddTransparentPadding(object value, int value2)
        {
            if (!((Object)value == (Object)null) && value2 > 0)
            {
                int width = ((Texture)value).width;
                int height = ((Texture)value).height;
                int num = width + value2 * 2;
                int num2 = height + value2 * 2;
                Texture2D val = new Texture2D(num, num2, (TextureFormat)4, false);
                Color32[] array = (Color32[])(object)new Color32[num * num2];
                Color32[] pixels = ((Texture2D)value).GetPixels32();
                for (int i = 0; i < height; i++)
                {
                    Array.Copy(pixels, i * width, array, (i + value2) * num + value2, width);
                }
                val.SetPixels32(array);
                val.Apply(false, false);
                Object.DestroyImmediate((Object)value);
                return val;
            }
            return (Texture2D)value;
        }

        private static void NormalizeTransparentPixels(object value)
        {
            if ((Object)value == (Object)null)
            {
                return;
            }
            Color32[] pixels = ((Texture2D)value).GetPixels32();
            if (pixels == null || pixels.Length < 1)
            {
                return;
            }
            bool flag = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0 && (pixels[i].r != 0 || pixels[i].g != 0 || pixels[i].b != 0))
                {
                    pixels[i] = _transparentColor;
                    flag = true;
                }
            }
            if (flag)
            {
                ((Texture2D)value).SetPixels32(pixels);
                ((Texture2D)value).Apply(false, false);
            }
        }

        private static string ComputeImageContentHash(int value, int value2, object value3)
        {
            if (value3 != null && ((Array)value3).Length != 0)
            {
                using (SHA256 sHA = SHA256.Create())
                {
                    sHA.TransformBlock(BitConverter.GetBytes(value), 0, 4, null, 0);
                    sHA.TransformBlock(BitConverter.GetBytes(value2), 0, 4, null, 0);
                    sHA.TransformFinalBlock((byte[])value3, 0, ((Array)value3).Length);
                    byte[] hash = sHA.Hash;
                    StringBuilder stringBuilder = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        stringBuilder.Append(hash[i].ToString("x2"));
                    }
                    return stringBuilder.ToString();
                }
            }
            return string.Empty;
        }

        private static string ComputeStringSha256Hex(object value)
        {
            using SHA256 sHA = SHA256.Create();
            byte[] bytes = _utf8NoBom.GetBytes((string)(value ?? string.Empty));
            byte[] array = sHA.ComputeHash(bytes);
            StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
            for (int i = 0; i < array.Length; i++)
            {
                stringBuilder.Append(array[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }

        private static string GetNormalizedRelativePath(object value, object value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && !string.IsNullOrWhiteSpace((string)value2))
            {
                try
                {
                    return Path.GetRelativePath((string)value, (string)value2).Replace("\\", "/");
                }
                catch
                {
                    return ((string)value2).Replace("\\", "/");
                }
            }
            return string.Empty;
        }

        private static string SanitizeFileName(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            StringBuilder stringBuilder = new StringBuilder(((string)value).Length);
            for (int i = 0; i < ((string)value).Length; i++)
            {
                char c = ((string)value)[i];
                stringBuilder.Append((char.IsLetterOrDigit(c) || c == '_' || c == '-') ? c : '_');
            }
            return stringBuilder.ToString().Trim('_');
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiAnalysisPackageBuilder GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
