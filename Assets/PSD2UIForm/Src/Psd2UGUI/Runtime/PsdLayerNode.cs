using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ReadOnlyFieldAttributeNamespace;
using PsdLayerExtensionsNamespace;
using AssetNameSanitizerNamespace;
using TMPro;
using UnityEditor;
using UnityEngine;
using TextGradientColorStopNamespace;
using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;
using PsdTextStyleInfoNamespace;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    [CanEditMultipleObjects]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public sealed class PsdLayerNode : MonoBehaviour
    {
        [SerializeField]
        [ReadOnlyFieldAttribute]
        internal int BindPsdLayerIndex = -1;

        [ReadOnlyFieldAttribute]
        [SerializeField]
        private PsdLayerType mLayerType;

        [ReadOnlyFieldAttribute]
        [SerializeField]
        private string sourceLayerName;

        [SerializeField]
        internal bool markToExport;

        [SerializeField]
        [HideInInspector]
        private string generatedNodeId;

        [SerializeField]
        [HideInInspector]
        internal GUIType UIType;

        [SerializeField]
        [HideInInspector]
        internal bool collapseChildrenForGeneration;

        [HideInInspector]
        [SerializeField]
        internal int textSourceBindPsdLayerIndex = -1;

        [CompilerGenerated]
        private Texture2D _previewTexture;

        private Rect _layerRect;

        private WeakReference<PsdLayerNode> _referencedNodeCache;

        private string _previewCacheKey;

        private PsdLayer _boundPsdLayer;

        private PsdLayer _textSourcePsdLayer;

        private static PsdLayerNode s_PsdLayerNodeObfuscationSentinel;

        internal Texture2D PreviewTexture
        {
            [CompilerGenerated]
            get
            {
                return _previewTexture;
            }
            [CompilerGenerated]
            private set
            {
                _previewTexture = value;
            }
        }

        internal PsdLayerType LayerType => mLayerType;

        private static int GetObjectInstanceId(object value)
        {
            return ((Object)value).GetInstanceID();
        }

        [SpecialName]
        internal string GetLayerInfo()
        {
            return GetLayerRect().ToString();
        }

        [SpecialName]
        internal Rect GetLayerRect()
        {
            if (IsSyntheticGroup() && TryCalculateChildBounds(out var result))
            {
                return result;
            }
            return _layerRect;
        }

        [SpecialName]
        private void SetLayerRectCore(Rect rect)
        {
            _layerRect = rect;
        }

        [SpecialName]
        internal string GetSourceLayerName()
        {
            return sourceLayerName;
        }

        [SpecialName]
        internal string GetGeneratedNodeId()
        {
            return generatedNodeId;
        }

        [SpecialName]
        internal bool IsPrimaryUIType()
        {
            return UGUIParser.IsPrimaryUIType(UIType);
        }

        [SpecialName]
        internal bool HasAssetReference()
        {
            return !string.IsNullOrEmpty(GetAssetReferenceKey());
        }

        [SpecialName]
        internal bool HasPrefabReference()
        {
            return !string.IsNullOrEmpty(GetPrefabReferenceKey());
        }

        [SpecialName]
        internal string GetAssetReferenceKey()
        {
            if (TryParseAssetReference(out var text))
            {
                return NormalizeReferencePath(text);
            }
            return null;
        }

        [SpecialName]
        internal string GetAssetReferenceName()
        {
            if (!TryParseAssetReference(out var result))
            {
                return null;
            }
            return result;
        }

        [SpecialName]
        internal string GetPrefabReferenceKey()
        {
            if (!TryParsePrefabReference(out var text))
            {
                return null;
            }
            return NormalizeReferencePath(text);
        }

        [SpecialName]
        internal string GetPrefabReferenceName()
        {
            if (TryParsePrefabReference(out var result))
            {
                return result;
            }
            return null;
        }

        [SpecialName]
        internal string GetNormalizedNodeKey()
        {
            GameObject gameObject = ((Component)this).gameObject;
            return NormalizeNodeReferenceKey(((object)gameObject != null) ? ((Object)gameObject).name : null, true);
        }

        [SpecialName]
        internal bool IsHighBitDepthSource()
        {
            if (PsdReaderProductAccess.IsAvailable && GetBoundPsdLayer() != null && GetBoundPsdLayer().Document != null)
            {
                return GetBoundPsdLayer().Document.Depth == 16;
            }
            return false;
        }

        [SpecialName]
        internal bool ShouldCollapseChildrenForGeneration()
        {
            return collapseChildrenForGeneration;
        }

        [SpecialName]
        internal PsdLayer GetBoundPsdLayer()
        {
            return _boundPsdLayer;
        }

        [SpecialName]
        internal void BindPsdLayer(PsdLayer psdLayer)
        {
            _boundPsdLayer = psdLayer;
            if (_boundPsdLayer != null)
            {
                mLayerType = _boundPsdLayer.GetLayerType();
                SetLayerRectCore(_boundPsdLayer.GetUnityRect());
            }
        }

        internal static string NormalizeNodeReferenceKey(object key, bool enabled = false)
        {
            if (!string.IsNullOrWhiteSpace((string)key))
            {
                return ((!enabled) ? AssetNameSanitizer.SanitizeReferenceName(((string)key).Trim()) : AssetNameSanitizer.SanitizeName(((string)key).Trim())).ToLowerInvariant();
            }
            return null;
        }

        internal static string NormalizeLayerObjectName(object name, int value = -1)
        {
            string text = ((!string.IsNullOrWhiteSpace((string)name)) ? ((string)name).Trim() : string.Empty);
            UGUIParser uGUIParser = UGUIParser.Instance;
            if ((Object)(object)uGUIParser != (Object)null && uGUIParser.IsChineseNameConversionEnabled() && !string.IsNullOrEmpty(text))
            {
                text = AssetNameSanitizer.TransliterateChinese(text);
            }
            if ((Object)(object)uGUIParser != (Object)null)
            {
                text = uGUIParser.RemoveRecognizedLayerTags(text);
            }
            text = AssetNameSanitizer.SanitizeReferenceName(text);
            if (string.IsNullOrEmpty(text))
            {
                if (value < 0)
                {
                    return "PsdLayer";
                }
                return $"PsdLayer-{value}";
            }
            return text;
        }

        internal string GetGeneratedObjectName()
        {
            return NormalizeLayerObjectName(GetEffectiveLayerName(), BindPsdLayerIndex);
        }

        internal static string SanitizeReferenceNameOrNull(object name)
        {
            if (!string.IsNullOrWhiteSpace((string)name))
            {
                return AssetNameSanitizer.SanitizeReferenceName(((string)name).Trim());
            }
            return null;
        }

        internal static string NormalizeReferencePath(object path)
        {
            if (!string.IsNullOrWhiteSpace((string)path))
            {
                string text = ((string)path).Trim();
                UGUIParser uGUIParser = UGUIParser.Instance;
                if ((Object)(object)uGUIParser != (Object)null && uGUIParser.IsChineseNameConversionEnabled())
                {
                    text = AssetNameSanitizer.TransliterateChinese(text);
                }
                string text2 = AssetNameSanitizer.SanitizeRelativePath(text);
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    return text2;
                }
                return null;
            }
            return null;
        }

        internal static string GetNormalizedFileNameOrFallback(object value, string name = null)
        {
            string text = AssetNameSanitizer.GetNormalizedFileName(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return "PsdLayer";
                }
                return name;
            }
            return text;
        }

        internal void SetSourceLayerName(string name)
        {
            sourceLayerName = name;
        }

        internal void InitializeGeneratedGroup(string text, Rect rect, string text2 = null)
        {
            BindPsdLayerIndex = -1;
            markToExport = false;
            textSourceBindPsdLayerIndex = -1;
            SetCollapseAndTextSource(false, -1);
            mLayerType = PsdLayerType.LayerGroup;
            sourceLayerName = text ?? string.Empty;
            generatedNodeId = text2 ?? string.Empty;
            SetLayerRect(rect);
        }

        internal void SetGeneratedNodeId(string id)
        {
            generatedNodeId = id ?? string.Empty;
        }

        internal void SetLayerRect(Rect rect)
        {
            SetLayerRectCore(rect);
        }

        private bool IsSyntheticGroup()
        {
            if (GetBoundPsdLayer() == null && BindPsdLayerIndex < 0)
            {
                return LayerType == PsdLayerType.LayerGroup;
            }
            return false;
        }

        private bool TryCalculateChildBounds(out Rect result)
        {
            result = Rect.zero;
            bool flag = false;
            float num = 0f;
            float num2 = 0f;
            float num3 = 0f;
            float num4 = 0f;
            int i = 0;
            for (int childCount = ((Component)this).transform.childCount; i < childCount; i++)
            {
                PsdLayerNode component = ((Component)((Component)this).transform.GetChild(i)).GetComponent<PsdLayerNode>();
                if (!((Object)(object)component == (Object)null))
                {
                    Rect val = component.GetLayerRect();
                    if (!flag)
                    {
                        num = val.xMin;
                        num2 = val.yMin;
                        num3 = val.xMax;
                        num4 = val.yMax;
                        flag = true;
                    }
                    else
                    {
                        num = Mathf.Min(num, val.xMin);
                        num2 = Mathf.Min(num2, val.yMin);
                        num3 = Mathf.Max(num3, val.xMax);
                        num4 = Mathf.Max(num4, val.yMax);
                    }
                }
            }
            if (flag)
            {
                result = Rect.MinMaxRect(num, num2, num3, num4);
                return true;
            }
            return false;
        }

        private string GetEffectiveLayerName()
        {
            GameObject gameObject = ((Component)this).gameObject;
            string text = (((object)gameObject == null) ? null : ((Object)gameObject).name);
            UGUIParser uGUIParser = UGUIParser.Instance;
            if (!string.IsNullOrWhiteSpace(text) && (Object)(object)uGUIParser != (Object)null)
            {
                if (!string.IsNullOrWhiteSpace(sourceLayerName))
                {
                    string b = uGUIParser.BuildLayerObjectName(sourceLayerName, BindPsdLayerIndex);
                    if (!string.Equals(text, b, StringComparison.Ordinal))
                    {
                        return text;
                    }
                }
                else
                {
                    string text2 = GetBoundPsdLayer()?.GetLayerName();
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        string b2 = uGUIParser.BuildLayerObjectName(text2, BindPsdLayerIndex);
                        if (!string.Equals(text, b2, StringComparison.Ordinal))
                        {
                            return text;
                        }
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(sourceLayerName))
            {
                return sourceLayerName;
            }
            string text3 = GetBoundPsdLayer()?.GetLayerName();
            if (string.IsNullOrWhiteSpace(text3))
            {
                return text;
            }
            return text3;
        }

        private bool TryParseAssetReference(out string result)
        {
            result = null;
            string text = GetEffectiveLayerName();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            text = text.Trim();
            if (!text.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            result = text.Substring("ref ".Length).Trim();
            UGUIParser uGUIParser = UGUIParser.Instance;
            if ((Object)(object)uGUIParser != (Object)null)
            {
                result = uGUIParser.RemoveRecognizedLayerTags(result);
            }
            return !string.IsNullOrEmpty(result);
        }

        private bool TryParsePrefabReference(out string result)
        {
            result = null;
            string text = GetEffectiveLayerName();
            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Trim();
                if (!text.StartsWith("refp ", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                result = text.Substring("refp ".Length).Trim();
                UGUIParser uGUIParser = UGUIParser.Instance;
                if ((Object)(object)uGUIParser != (Object)null)
                {
                    result = uGUIParser.RemoveRecognizedLayerTags(result);
                }
                return !string.IsNullOrEmpty(result);
            }
            return false;
        }

        private void OnDestroy()
        {
            ReleasePreviewTexture();
        }

        internal void SetUIType(GUIType uiType, bool enabled = true)
        {
            UGUIParser uGUIParser = UGUIParser.Instance;
            UIType = (((Object)(object)uGUIParser != (Object)null) ? uGUIParser.ApplyForcedTMPType(uiType) : uiType);
            RemoveAllHelperComponents();
            if (enabled)
            {
                Psd2UIFormConverter value = Psd2UIFormConverter.Instance;
                if ((Object)(object)value != (Object)null)
                {
                    value.RefreshAllHelperComponents();
                }
                else
                {
                    SynchronizeHelperComponent(true);
                }
            }
        }

        internal void SetCollapseAndTextSource(bool enabled, int value)
        {
            collapseChildrenForGeneration = enabled;
            textSourceBindPsdLayerIndex = value;
            _textSourcePsdLayer = null;
            if (textSourceBindPsdLayerIndex >= 0)
            {
                _textSourcePsdLayer = GetBoundPsdLayer()?.Document?.GetLayerByTraversalIndex(textSourceBindPsdLayerIndex);
            }
        }

        internal void SynchronizeHelperComponent(bool enabled = false)
        {
            UGUIParser uGUIParser = UGUIParser.Instance;
            if ((Object)(object)uGUIParser != (Object)null)
            {
                UIType = uGUIParser.ApplyForcedTMPType(UIType);
            }
            if (UIType == GUIType.Null)
            {
                RemoveAllHelperComponents();
                return;
            }
            Type type = ((!((Object)(object)uGUIParser != (Object)null)) ? null : uGUIParser.GetHelperComponentType(UIType));
            RemoveMismatchedHelperComponents(type);
            if (type != null)
            {
                UIHelperBase obj = (((Component)this).gameObject.GetComponent(type) ?? ((Component)this).gameObject.AddComponent(type)) as UIHelperBase;
                obj.ParseAndAttachUIElements();
                EditorUtility.SetDirty((Object)(object)obj);
            }
            if (enabled)
            {
                Transform parent = ((Component)this).transform.parent;
                while ((Object)(object)parent != (Object)null)
                {
                    UIHelperBase component = ((Component)parent).GetComponent<UIHelperBase>();
                    if (!((Object)(object)component != (Object)null))
                    {
                        parent = parent.parent;
                        continue;
                    }
                    component.ParseAndAttachUIElements();
                    EditorUtility.SetDirty((Object)(object)component);
                    break;
                }
            }
            EditorUtility.SetDirty((Object)(object)this);
        }

        private void RemoveMismatchedHelperComponents(Type type)
        {
            UIHelperBase[] components = ((Component)this).GetComponents<UIHelperBase>();
            if (components == null)
            {
                return;
            }
            UIHelperBase[] array = components;
            foreach (UIHelperBase uIHelperBase in array)
            {
                if (!((Object)(object)uIHelperBase == (Object)null) && (!(type != null) || !(((object)uIHelperBase).GetType() == type)))
                {
                    Object.DestroyImmediate((Object)(object)uIHelperBase);
                }
            }
        }

        private void RemoveAllHelperComponents()
        {
            UIHelperBase[] components = ((Component)this).GetComponents<UIHelperBase>();
            if (components != null)
            {
                UIHelperBase[] array = components;
                for (int i = 0; i < array.Length; i++)
                {
                    Object.DestroyImmediate((Object)(object)array[i]);
                }
            }
            EditorUtility.SetDirty((Object)(object)this);
        }

        internal bool ShouldExportImage()
        {
            if (UIType != GUIType.FillColor && LayerType != PsdLayerType.FillLayer)
            {
                if (((Component)this).gameObject.activeSelf)
                {
                    return markToExport;
                }
                return false;
            }
            return false;
        }

        private static string NormalizeExportBaseName(object value, bool enabled, object value2)
        {
            string text = AssetNameSanitizer.SanitizeReferenceName(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = AssetNameSanitizer.SanitizeReferenceName(value2);
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "PsdLayer";
            }
            if (enabled)
            {
                text = text.ToLowerInvariant();
            }
            return text;
        }

        private static string GetExportBaseName(object value, bool enabled)
        {
            if (!((Object)value == (Object)null))
            {
                return NormalizeExportBaseName((!string.IsNullOrWhiteSpace(((Object)value).name)) ? ((Object)value).name : ((PsdLayerNode)value).UIType.ToString(), enabled, ((PsdLayerNode)value).UIType.ToString());
            }
            return "PsdLayer";
        }

        private string EnsureUniqueExportName(Psd2UIFormConverter value, string text, bool enabled)
        {
            if (!((Object)(object)value == (Object)null) && !string.IsNullOrWhiteSpace(text))
            {
                PsdLayerNode[] componentsInChildren = ((Component)value).GetComponentsInChildren<PsdLayerNode>(true);
                if (componentsInChildren != null && componentsInChildren.Length != 0)
                {
                    PsdLayerNode[] array = componentsInChildren.Where(delegate(PsdLayerNode node)
                    {
                        if (!((Object)(object)node != (Object)null))
                        {
                            return false;
                        }
                        return (Object)(object)node == (Object)(object)this || node.ShouldExportImage();
                    }).ToArray();
                    if (array.Length > 1)
                    {
                        LayerNodeExportName[] array2 = array.Select((PsdLayerNode node) => new LayerNodeExportName
                        {
                            Node = node,
                            Name = GetExportBaseName(node, enabled)
                        }).ToArray();
                        HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        LayerNodeExportName[] array3 = array2;
                        foreach (LayerNodeExportName anon in array3)
                        {
                            hashSet.Add(anon.Name);
                        }
                        PsdLayerNode[] array4 = (from entry in array2
                            where string.Equals(entry.Name, text, StringComparison.OrdinalIgnoreCase)
                            select entry.Node into node
                            orderby node.BindPsdLayerIndex, GetObjectInstanceId(node)
                            select node).ToArray();
                        if (array4.Length > 1)
                        {
                            int num2 = Array.IndexOf(array4, this);
                            if (num2 > 0)
                            {
                                int num3 = 1;
                                int num4 = 1;
                                while (true)
                                {
                                    if (num4 <= num2)
                                    {
                                        for (; hashSet.Contains($"{text}_{num3}"); num3++)
                                        {
                                        }
                                        if (num4 == num2)
                                        {
                                            break;
                                        }
                                        num3++;
                                        num4++;
                                        continue;
                                    }
                                    return text;
                                }
                                return $"{text}_{num3}";
                            }
                            return text;
                        }
                        return text;
                    }
                    return text;
                }
                return text;
            }
            return text;
        }

        private static string ResolveExportPathParts(object value, object value2, bool enabled, object value3, out string result)
        {
            result = (string)value;
            if (!string.IsNullOrWhiteSpace((string)value2))
            {
                string text = Path.ChangeExtension(((string)value2).Trim().Replace("\\", "/"), null);
                text = AssetNameSanitizer.SanitizeRelativePath(text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (enabled)
                    {
                        text = text.ToLowerInvariant();
                    }
                    string text2 = Path.GetDirectoryName(text)?.Replace("\\", "/");
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        result = Path.Combine((string)value, text2).Replace("\\", "/");
                    }
                    return NormalizeExportBaseName(AssetNameSanitizer.GetNormalizedFileName(text), enabled, value3);
                }
                return NormalizeExportBaseName(value3, enabled, value3);
            }
            return NormalizeExportBaseName(value3, enabled, value3);
        }

        internal string ExportImageAsset(bool enabled = false, string text10 = null, string text11 = null, bool enabled2 = true, bool enabled3 = false, bool enabled4 = false)
        {
            Psd2UIFormConverter value = Psd2UIFormConverter.Instance;
            if ((Object)(object)value != (Object)null && value.TryGetCachedExportPath(this, out var result))
            {
                return result;
            }
            bool flag = false;
            string text = null;
            string text2 = text10;
            string text3 = text11;
            if ((Object)(object)value != (Object)null && string.IsNullOrEmpty(text2) && !HasAssetReference() && value.TryGetSharedExportTarget(this, out var text4, out var text5))
            {
                text2 = text4;
                text3 = text5;
                flag = true;
                text = text5;
                enabled = true;
            }
            if (!enabled4 && HasAssetReference() && (Object)(object)value != (Object)null)
            {
                return value.ResolveOrExportReferencedImage(this, enabled3);
            }
            string result2 = null;
            if (UIType != GUIType.FillColor && LayerType != PsdLayerType.FillLayer)
            {
                PsdRenderedImage psdRenderedImage = RenderNodeImage(false);
                if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
                {
                    bool flag2 = UIType != GUIType.FillColor && UIType != GUIType.RawImage;
                    text2 = (string.IsNullOrWhiteSpace(text2) ? Psd2UIFormConverter.Instance.GetImageExportDirectory() : text2);
                    if (!Directory.Exists(text2))
                    {
                        try
                        {
                            Directory.CreateDirectory(text2);
                            AssetDatabase.Refresh();
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    }
                    string text6 = (string.IsNullOrWhiteSpace(text3) ? EnsureUniqueExportName(value, GetExportBaseName(this, enabled2), enabled2) : GetExportBaseName(this, enabled2));
                    string text7 = (string.IsNullOrWhiteSpace(text3) ? text6 : ResolveExportPathParts(text2, text3, enabled2, text6, out text2));
                    if (!Directory.Exists(text2))
                    {
                        try
                        {
                            Directory.CreateDirectory(text2);
                            AssetDatabase.Refresh();
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    }
                    string text8 = ".png";
                    string text9 = Path.Combine(text2, text7 + text8).Replace("\\", "/");
                    byte[] array = PsdTextureAssetUtility.EncodePng(psdRenderedImage);
                    if (array == null || array.Length == 0)
                    {
                        return null;
                    }
                    File.WriteAllBytes(text9, array);
                    result2 = text9;
                    AssetDatabase.Refresh();
                    Psd2UIFormConverter.ConvertTexturesType(new string[1] { text9 }, flag2 || enabled, psdRenderedImage.IsHighBitDepth);
                    if (enabled3)
                    {
                        Psd2UIFormConverter.EnsureNineSliceBorder(text9);
                    }
                    if (flag && (Object)(object)value != (Object)null)
                    {
                        value.CacheExportPath(string.IsNullOrEmpty(text) ? GetNormalizedNodeKey() : text, text9);
                    }
                }
                return result2;
            }
            return null;
        }

        internal bool RefreshPreviewTexture(bool enabled = false)
        {
            string text = BuildPreviewCacheKey();
            if (!string.IsNullOrEmpty(text))
            {
                bool flag2;
                bool flag = (flag2 = (Object)(object)PreviewTexture != (Object)null) && !string.IsNullOrEmpty(_previewCacheKey) && string.Equals(_previewCacheKey, text, StringComparison.Ordinal);
                if (!enabled && flag)
                {
                    return true;
                }
                if (!CanRenderPreview())
                {
                    ReleasePreviewTexture();
                    return false;
                }
                if (flag2 && (!flag || enabled))
                {
                    ReleasePreviewTexture();
                }
                PreviewTexture = PsdLayerPreviewCache.Acquire(text, CreatePreviewTexture);
                if ((Object)(object)PreviewTexture != (Object)null)
                {
                    _previewCacheKey = text;
                }
                return (Object)(object)PreviewTexture != (Object)null;
            }
            ReleasePreviewTexture();
            return false;
        }

        private string BuildPreviewCacheKey()
        {
            Rect val;
            if (!HasChildLayerNodes())
            {
                if (GetBoundPsdLayer() != null)
                {
                    try
                    {
                        return BuildBoundLayerPreviewCacheKey();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
                if (!CanRenderSyntheticGroup())
                {
                    return string.Empty;
                }
                List<string> list = new List<string>();
                int i = 0;
                for (int childCount = ((Component)this).transform.childCount; i < childCount; i++)
                {
                    PsdLayerNode component = ((Component)((Component)this).transform.GetChild(i)).GetComponent<PsdLayerNode>();
                    if (!((Object)(object)component == (Object)null))
                    {
                        string text = component.BuildPreviewCacheKey();
                        if (string.IsNullOrEmpty(text))
                        {
                            list.Add($"child:{i}:missing");
                        }
                        else
                        {
                            list.Add($"child:{i}:{text}");
                        }
                    }
                }
                if (list.Count < 1)
                {
                    return string.Empty;
                }
                string[] obj2 = new string[8]
                {
                    "GeneratedPreview",
                    NormalizeCacheKeyPart(UIType.ToString()),
                    NormalizeCacheKeyPart(GetSourceLayerName()),
                    null,
                    null,
                    null,
                    null,
                    null
                };
                val = GetLayerRect();
                obj2[3] = val.x.ToString(CultureInfo.InvariantCulture);
                val = GetLayerRect();
                obj2[4] = val.y.ToString(CultureInfo.InvariantCulture);
                val = GetLayerRect();
                obj2[5] = val.width.ToString(CultureInfo.InvariantCulture);
                val = GetLayerRect();
                obj2[6] = val.height.ToString(CultureInfo.InvariantCulture);
                obj2[7] = string.Join("|", list);
                return string.Join("|", obj2);
            }
            List<string> list2 = new List<string>();
            int j = 0;
            for (int childCount2 = ((Component)this).transform.childCount; j < childCount2; j++)
            {
                PsdLayerNode component2 = ((Component)((Component)this).transform.GetChild(j)).GetComponent<PsdLayerNode>();
                if (!((Object)(object)component2 == (Object)null))
                {
                    string text2 = component2.BuildPreviewCacheKey();
                    if (!string.IsNullOrEmpty(text2))
                    {
                        list2.Add($"child:{j}:{text2}");
                    }
                    else
                    {
                        list2.Add($"child:{j}:missing");
                    }
                }
            }
            if (list2.Count < 1)
            {
                return string.Empty;
            }
            string[] obj3 = new string[8]
            {
                "TreePreview",
                NormalizeCacheKeyPart(UIType.ToString()),
                NormalizeCacheKeyPart(GetSourceLayerName()),
                null,
                null,
                null,
                null,
                null
            };
            val = GetLayerRect();
            obj3[3] = val.x.ToString(CultureInfo.InvariantCulture);
            val = GetLayerRect();
            obj3[4] = val.y.ToString(CultureInfo.InvariantCulture);
            val = GetLayerRect();
            obj3[5] = val.width.ToString(CultureInfo.InvariantCulture);
            val = GetLayerRect();
            obj3[6] = val.height.ToString(CultureInfo.InvariantCulture);
            obj3[7] = string.Join("|", list2);
            return string.Join("|", obj3);
        }

        private string BuildBoundLayerPreviewCacheKey()
        {
            string text = NormalizeCacheKeyPart(Psd2UIFormConverter.Instance?.GetSourcePsdAssetPath());
            string text2 = NormalizeCacheKeyPart(Psd2UIFormConverter.Instance?.psdAssetChangeTime);
            if (string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text))
            {
                text2 = GetFileTimestampUtc(text);
            }
            string text3 = NormalizeCacheKeyPart(GetBoundPsdLayer().GetPreviewProtectionFingerprint());
            return string.Join("|", "PsdLayerPreview", text, text2, text3, BindPsdLayerIndex.ToString(CultureInfo.InvariantCulture), NormalizeCacheKeyPart(GetSourceLayerName()), GetBoundPsdLayer().Left.ToString(CultureInfo.InvariantCulture), GetBoundPsdLayer().Top.ToString(CultureInfo.InvariantCulture), GetBoundPsdLayer().Width.ToString(CultureInfo.InvariantCulture), GetBoundPsdLayer().Height.ToString(CultureInfo.InvariantCulture), (!GetBoundPsdLayer().IsGroup) ? "0" : "1", (!GetBoundPsdLayer().IsVisible) ? "0" : "1");
        }

        private static string NormalizeCacheKeyPart(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                return ((string)value).Trim().Replace("\\", "/").ToLowerInvariant();
            }
            return string.Empty;
        }

        private static string GetFileTimestampUtc(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && File.Exists((string)value))
            {
                return new FileInfo((string)value).LastWriteTimeUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        private void ReleasePreviewTexture()
        {
            if (!string.IsNullOrWhiteSpace(_previewCacheKey))
            {
                if (!PsdLayerPreviewCache.Release(_previewCacheKey) && (Object)(object)PreviewTexture != (Object)null)
                {
                    Object.DestroyImmediate((Object)(object)PreviewTexture);
                }
            }
            else if ((Object)(object)PreviewTexture != (Object)null)
            {
                Object.DestroyImmediate((Object)(object)PreviewTexture);
            }
            PreviewTexture = null;
            _previewCacheKey = string.Empty;
        }

        internal Texture2D CreatePreviewTexture()
        {
            PsdRenderedImage psdRenderedImage = RenderNodeImage(true);
            if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
            {
                return ConvertRenderedImageToTexture(psdRenderedImage, true);
            }
            return null;
        }

        private static Texture2D ConvertRenderedImageToTexture(object value, bool enabled)
        {
            if (value != null && !((PsdRenderedImage)value).IsEmpty)
            {
                TextureFormat val = (TextureFormat)(((PsdRenderedImage)value).IsHighBitDepth ? 74 : 4);
                Texture2D val2 = new Texture2D(((PsdRenderedImage)value).Width, ((PsdRenderedImage)value).Height, val, false);
                ((Object)val2).hideFlags = (HideFlags)(enabled ? 61 : 0);
                val2.alphaIsTransparency = true;
                if (!((PsdRenderedImage)value).IsHighBitDepth)
                {
                    val2.LoadRawTextureData(((PsdRenderedImage)value).Rgba32);
                }
                else
                {
                    ushort[] rgba = ((PsdRenderedImage)value).Rgba64;
                    byte[] array = new byte[rgba.Length * 2];
                    Buffer.BlockCopy(rgba, 0, array, 0, array.Length);
                    val2.LoadRawTextureData(array);
                }
                val2.Apply(false, false);
                return val2;
            }
            return null;
        }

        internal static Sprite LoadSpriteAtPath(object path)
        {
            if (!string.IsNullOrWhiteSpace((string)path))
            {
                Sprite val = AssetDatabase.LoadAssetAtPath<Sprite>((string)path);
                if (!((Object)(object)val != (Object)null))
                {
                    return AssetDatabase.LoadAllAssetsAtPath((string)path).OfType<Sprite>().FirstOrDefault();
                }
                return val;
            }
            return null;
        }

        internal static string GetExistingExportPath(object path, object path2, bool enabled = false)
        {
            if (string.IsNullOrWhiteSpace((string)path) || string.IsNullOrWhiteSpace((string)path2))
            {
                return null;
            }
            string text = Path.Combine((string)path, (string)path2 + ".png").Replace("\\", "/");
            if (PsdTextureAssetUtility.MatchesExportMode(text, enabled))
            {
                return text;
            }
            return null;
        }

        internal static bool RecreateSpriteWithBorder(object text, object value, Vector4 vector, out Sprite result)
        {
            result = null;
            if (!string.IsNullOrWhiteSpace((string)text) && !((Object)value == (Object)null))
            {
                Sprite[] array = AssetDatabase.LoadAllAssetsAtPath((string)text).OfType<Sprite>().ToArray();
                for (int i = 0; i < array.Length; i++)
                {
                    if ((Object)(object)array[i] != (Object)null)
                    {
                        Object.DestroyImmediate((Object)(object)array[i], true);
                    }
                }
                result = Sprite.Create((Texture2D)value, new Rect(0f, 0f, (float)((Texture)value).width, (float)((Texture)value).height), new Vector2(0.5f, 0.5f), 100f, 0u, (SpriteMeshType)0, vector);
                ((Object)result).name = Path.GetFileNameWithoutExtension((string)text);
                AssetDatabase.AddObjectToAsset((Object)(object)result, (string)text);
                EditorUtility.SetDirty((Object)value);
                EditorUtility.SetDirty((Object)(object)result);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset((string)text, (ImportAssetOptions)1);
                result = LoadSpriteAtPath(text);
                return (Object)(object)result != (Object)null;
            }
            return false;
        }

        internal bool TryGetRepresentativeColor(out Color result)
        {
            result = default(Color);
            if (!CanRenderPreview())
            {
                return false;
            }
            if (GetBoundPsdLayer() == null || !GetBoundPsdLayer().TryGetStructuralLayerColor(out result))
            {
                if (!TrySampleRepresentativeColor(RenderNodeImage(true), out result))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        private bool CanRenderPreview()
        {
            if (!HasChildLayerNodes() && GetBoundPsdLayer() == null)
            {
                return CanRenderSyntheticGroup();
            }
            return true;
        }

        private bool CanRenderSyntheticGroup()
        {
            if (GetBoundPsdLayer() == null && BindPsdLayerIndex < 0 && LayerType == PsdLayerType.LayerGroup)
            {
                if (!UGUIParser.IsPanelOrNull(UIType) && !UGUIParser.IsCompositeControlType(UIType))
                {
                    switch (UIType)
                    {
                    default:
                        return true;
                    case GUIType.Text:
                    case GUIType.FillColor:
                    case GUIType.TMPText:
                    case GUIType.Button_Text:
                    case GUIType.Dropdown_Label:
                    case GUIType.InputField_Placeholder:
                    case GUIType.InputField_Text:
                    case GUIType.Toggle_Label:
                        return false;
                    }
                }
                return false;
            }
            return false;
        }

        private PsdRenderedImage RenderNodeImage(bool enabled)
        {
            if (!HasChildLayerNodes())
            {
                if (GetBoundPsdLayer() == null)
                {
                    if (!CanRenderSyntheticGroup())
                    {
                        return null;
                    }
                    return RenderChildLayerTree(enabled);
                }
                if (enabled)
                {
                    return GetBoundPsdLayer().RenderPreview();
                }
                return GetBoundPsdLayer().Render();
            }
            return RenderChildLayerTree(enabled);
        }

        private bool HasChildLayerNodes()
        {
            if (LayerType == PsdLayerType.LayerGroup && !((Object)(object)((Component)this).transform == (Object)null))
            {
                int i = 0;
                for (int childCount = ((Component)this).transform.childCount; i < childCount; i++)
                {
                    if ((Object)(object)((Component)((Component)this).transform.GetChild(i)).GetComponent<PsdLayerNode>() != (Object)null)
                    {
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        private PsdRenderedImage RenderChildLayerTree(bool enabled)
        {
            List<PsdLayer> list = new List<PsdLayer>(((Component)this).transform.childCount);
            int i = 0;
            for (int childCount = ((Component)this).transform.childCount; i < childCount; i++)
            {
                PsdLayerNode component = ((Component)((Component)this).transform.GetChild(i)).GetComponent<PsdLayerNode>();
                if (!((Object)(object)component == (Object)null))
                {
                    CollectRenderablePsdLayers(component, list);
                }
            }
            if (list.Count >= 1)
            {
                return PsdLayerRenderer.MergeLayers(list, includeHiddenLayers: false, applyClippingMasks: true, enabled);
            }
            return null;
        }

        private static void CollectRenderablePsdLayers(object value, List<PsdLayer> psdLayers)
        {
            if ((Object)value == (Object)null || psdLayers == null)
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
                    CollectRenderablePsdLayers(component, psdLayers);
                }
            }
            if (((PsdLayerNode)value).GetBoundPsdLayer() != null && !(((PsdLayerNode)value).LayerType == PsdLayerType.LayerGroup && flag))
            {
                psdLayers.Add(((PsdLayerNode)value).GetBoundPsdLayer());
            }
        }

        private static bool TrySampleRepresentativeColor(object value, out Color result)
        {
            result = default(Color);
            if (value != null && !((PsdRenderedImage)value).IsEmpty && ((PsdRenderedImage)value).Rgba32 != null && ((PsdRenderedImage)value).Rgba32.Length >= 4)
            {
                int num = ((((PsdRenderedImage)value).Width > 1) ? Mathf.Max(1, Mathf.RoundToInt((float)((PsdRenderedImage)value).Width * 0.25f)) : 0);
                int num2 = ((((PsdRenderedImage)value).Height > 1) ? Mathf.Max(1, Mathf.RoundToInt((float)((PsdRenderedImage)value).Height * 0.25f)) : 0);
                int num3 = ((PsdRenderedImage)value).Width / 2;
                int num4 = ((PsdRenderedImage)value).Height / 2;
                uint[] array = new uint[4];
                int num5 = 0;
                if (TryReadOpaquePixel(value, num3, Mathf.Clamp(num4 - num2, 0, ((PsdRenderedImage)value).Height - 1), out var num6))
                {
                    array[num5++] = num6;
                }
                if (TryReadOpaquePixel(value, num3, Mathf.Clamp(num4 + num2, 0, ((PsdRenderedImage)value).Height - 1), out num6))
                {
                    array[num5++] = num6;
                }
                if (TryReadOpaquePixel(value, Mathf.Clamp(num3 - num, 0, ((PsdRenderedImage)value).Width - 1), num4, out num6))
                {
                    array[num5++] = num6;
                }
                if (TryReadOpaquePixel(value, Mathf.Clamp(num3 + num, 0, ((PsdRenderedImage)value).Width - 1), num4, out num6))
                {
                    array[num5++] = num6;
                }
                if (num5 <= 0)
                {
                    return false;
                }
                uint num7 = array[0];
                int num8 = 1;
                for (int i = 0; i < num5; i++)
                {
                    uint num9 = array[i];
                    int num10 = 1;
                    for (int j = i + 1; j < num5; j++)
                    {
                        if (array[j] == num9)
                        {
                            num10++;
                        }
                    }
                    if (num10 > num8)
                    {
                        num8 = num10;
                        num7 = num9;
                    }
                }
                result = UnpackRgba32(num7);
                return true;
            }
            return false;
        }

        private static bool TryReadOpaquePixel(object value, int value2, int value3, out uint result)
        {
            result = 0u;
            if (value != null && !((PsdRenderedImage)value).IsEmpty && ((PsdRenderedImage)value).Rgba32 != null && ((PsdRenderedImage)value).Rgba32.Length >= 4)
            {
                if (value2 >= 0 && value3 >= 0 && value2 < ((PsdRenderedImage)value).Width && value3 < ((PsdRenderedImage)value).Height)
                {
                    int num = ((((PsdRenderedImage)value).Height - 1 - value3) * ((PsdRenderedImage)value).Width + value2) * 4;
                    if (num >= 0 && num + 3 < ((PsdRenderedImage)value).Rgba32.Length)
                    {
                        byte b = ((PsdRenderedImage)value).Rgba32[num + 3];
                        if (b == 0)
                        {
                            return false;
                        }
                        result = PackRgba32(((PsdRenderedImage)value).Rgba32[num], ((PsdRenderedImage)value).Rgba32[num + 1], ((PsdRenderedImage)value).Rgba32[num + 2], b);
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        private static uint PackRgba32(byte value, byte value2, byte value3, byte value4)
        {
            return (uint)((value << 24) | (value2 << 16) | (value3 << 8) | value4);
        }

        private static Color UnpackRgba32(uint value)
        {
            return (Color32)(new Color32((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value));
        }

        internal PsdLayerNode FindFirstOwnedNode(params GUIType[] uiTps)
        {
            if (uiTps != null && uiTps.Length != 0)
            {
                for (int i = 0; i < uiTps.Length; i++)
                {
                    PsdLayerNode[] array = FindOwnedNodes(uiTps[i]);
                    if (array != null && array.Length != 0)
                    {
                        return array[0];
                    }
                }
                return null;
            }
            return null;
        }

        internal PsdLayerNode[] FindOwnedNodes(params GUIType[] uiTps)
        {
            if (uiTps == null || uiTps.Length == 0)
            {
                return null;
            }
            List<PsdLayerNode> list = new List<PsdLayerNode>(4);
            HashSet<int> hashSet = new HashSet<int>();
            for (int i = 0; i < uiTps.Length; i++)
            {
                CollectOwnedNodesByType(((Component)this).transform, uiTps[i], list, hashSet);
            }
            if (list.Count <= 0)
            {
                return null;
            }
            return list.ToArray();
        }

        internal PsdLayerNode FindOwnerNode()
        {
            if (UGUIParser.IsAuxiliaryUIType(UIType))
            {
                Transform val = ((Component)this).transform.parent;
                while ((Object)(object)val != (Object)null)
                {
                    PsdLayerNode component = ((Component)val).GetComponent<PsdLayerNode>();
                    if (!((Object)(object)component == (Object)null))
                    {
                        if (UGUIParser.CanOwnAuxiliaryType(component.UIType, UIType))
                        {
                            return component;
                        }
                        val = ((!UGUIParser.IsPanelOrNull(component.UIType)) ? val.parent : val.parent);
                    }
                    else
                    {
                        val = val.parent;
                    }
                }
                return null;
            }
            return FindCompatibleOwnerAncestor();
        }

        internal bool IsOwnedByNode(PsdLayerNode layerNode)
        {
            if (!((Object)(object)layerNode == (Object)null) && !((Object)(object)layerNode == (Object)(object)this))
            {
                if (UGUIParser.IsAuxiliaryUIType(UIType))
                {
                    return (Object)(object)FindOwnerNode() == (Object)(object)layerNode;
                }
                if (!UGUIParser.IsPrimaryUIType(UIType))
                {
                    return false;
                }
                PsdLayerNode psdLayerNode = FindCompatibleOwnerAncestor();
                if ((Object)(object)psdLayerNode != (Object)null)
                {
                    return (Object)(object)psdLayerNode == (Object)(object)layerNode;
                }
                return (Object)(object)FindNearestNonPanelAncestor() == (Object)(object)layerNode;
            }
            return false;
        }

        internal bool TryResolveReferencedAsset(out Object result)
        {
            result = null;
            if (!HasAssetReference())
            {
                return false;
            }
            string text = GetExistingExportPath(UGUIParser.Instance?.GetSharedAssetsOutputDirectory(), GetAssetReferenceKey(), IsHighBitDepthSource());
            if (!string.IsNullOrWhiteSpace(text))
            {
                result = (Object)(((object)LoadSpriteAtPath(text)) ?? ((object)AssetDatabase.LoadAssetAtPath<Object>(text)));
                return result != (Object)null;
            }
            return false;
        }

        internal bool TryResolveReferencedPrefab(out GameObject result)
        {
            result = null;
            if (!HasPrefabReference())
            {
                return false;
            }
            string text = UGUIParser.Instance?.GetSharedPrefabOutputDirectory();
            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Replace("\\", "/");
                string text2 = Path.Combine(text, GetPrefabReferenceKey() + ".prefab").Replace("\\", "/");
                result = AssetDatabase.LoadAssetAtPath<GameObject>(text2);
                if ((Object)(object)result != (Object)null)
                {
                    return true;
                }
                string text3 = AssetNameSanitizer.GetNormalizedFileName(GetPrefabReferenceKey());
                if (!string.IsNullOrWhiteSpace(text3))
                {
                    string[] array = AssetDatabase.FindAssets(text3 + " t:prefab", new string[1] { text });
                    if (array != null && array.Length != 0)
                    {
                        string[] array2 = array;
                        for (int i = 0; i < array2.Length; i++)
                        {
                            string text4 = AssetDatabase.GUIDToAssetPath(array2[i]);
                            if (!string.IsNullOrWhiteSpace(text4) && string.Equals(Path.GetFileNameWithoutExtension(text4), text3, StringComparison.OrdinalIgnoreCase))
                            {
                                result = AssetDatabase.LoadAssetAtPath<GameObject>(text4);
                                if ((Object)(object)result != (Object)null)
                                {
                                    return true;
                                }
                            }
                        }
                        string text5 = AssetDatabase.GUIDToAssetPath(array[0]);
                        if (!string.IsNullOrWhiteSpace(text5))
                        {
                            result = AssetDatabase.LoadAssetAtPath<GameObject>(text5);
                        }
                        return (Object)(object)result != (Object)null;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }

        internal bool TryResolveReferencedNode(out PsdLayerNode result)
        {
            result = null;
            if (!HasAssetReference())
            {
                return false;
            }
            if (_referencedNodeCache != null && _referencedNodeCache.TryGetTarget(out result) && (Object)(object)result != (Object)null)
            {
                return true;
            }
            Psd2UIFormConverter value = Psd2UIFormConverter.Instance;
            if (!((Object)(object)value == (Object)null))
            {
                result = value.FindNodeByReferenceKey(GetAssetReferenceKey());
                if (!((Object)(object)result != (Object)null))
                {
                    return false;
                }
                _referencedNodeCache = new WeakReference<PsdLayerNode>(result);
                return true;
            }
            return false;
        }

        internal PsdLayerNode FindDescendantByUIType(GUIType uiType)
        {
            PsdLayerNode[] componentsInChildren = ((Component)this).GetComponentsInChildren<PsdLayerNode>(true);
            if (componentsInChildren == null || componentsInChildren.Length == 0)
            {
                return null;
            }
            return componentsInChildren.FirstOrDefault((PsdLayerNode layer) => layer.UIType == uiType);
        }

        private void CollectOwnedNodesByType(Transform transform, GUIType uiType, List<PsdLayerNode> layerNodes, HashSet<int> values)
        {
            if ((Object)(object)transform == (Object)null || layerNodes == null || values == null)
            {
                return;
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                PsdLayerNode psdLayerNode = (((Object)(object)child != (Object)null) ? ((Component)child).GetComponent<PsdLayerNode>() : null);
                if ((Object)(object)psdLayerNode == (Object)null)
                {
                    continue;
                }
                if (psdLayerNode.UIType == uiType && psdLayerNode.IsOwnedByNode(this))
                {
                    int instanceID = ((Object)psdLayerNode).GetInstanceID();
                    if (values.Add(instanceID))
                    {
                        layerNodes.Add(psdLayerNode);
                    }
                }
                if (!psdLayerNode.ShouldStopOwnershipTraversal())
                {
                    CollectOwnedNodesByType(child, uiType, layerNodes, values);
                }
            }
        }

        private PsdLayerNode FindNearestNonPanelAncestor()
        {
            Transform parent = ((Component)this).transform.parent;
            PsdLayerNode component;
            while (true)
            {
                if ((Object)(object)parent != (Object)null)
                {
                    component = ((Component)parent).GetComponent<PsdLayerNode>();
                    if (!((Object)(object)component == (Object)null))
                    {
                        if (!UGUIParser.IsPanelOrNull(component.UIType))
                        {
                            break;
                        }
                        parent = parent.parent;
                    }
                    else
                    {
                        parent = parent.parent;
                    }
                    continue;
                }
                return null;
            }
            return component;
        }

        private PsdLayerNode FindCompatibleOwnerAncestor()
        {
            Transform val = ((Component)this).transform.parent;
            while ((Object)(object)val != (Object)null)
            {
                PsdLayerNode component = ((Component)val).GetComponent<PsdLayerNode>();
                if (!((Object)(object)component == (Object)null))
                {
                    if (UGUIParser.CanOwnNestedControlType(component.UIType, UIType))
                    {
                        return component;
                    }
                    val = (UGUIParser.IsPanelOrNull(component.UIType) ? val.parent : val.parent);
                }
                else
                {
                    val = val.parent;
                }
            }
            return null;
        }

        private bool ShouldStopOwnershipTraversal()
        {
            if (!ShouldCollapseChildrenForGeneration() && !HasAssetReference() && !HasPrefabReference())
            {
                if (UGUIParser.IsLeafVisualType(UIType))
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        internal bool TryGetTextLayerInfo(out PsdTextLayerInfo result)
        {
            result = null;
            return (_textSourcePsdLayer ?? GetBoundPsdLayer())?.TryGetTextLayerInfo(out result) ?? false;
        }

        internal void RebindPsdDocument(PsdDocument psdDocument)
        {
            _textSourcePsdLayer = null;
            if (psdDocument == null)
            {
                BindPsdLayer(null);
                return;
            }
            if (BindPsdLayerIndex >= 0)
            {
                BindPsdLayer(psdDocument.GetLayerByTraversalIndex(BindPsdLayerIndex));
            }
            if (textSourceBindPsdLayerIndex >= 0)
            {
                _textSourcePsdLayer = psdDocument.GetLayerByTraversalIndex(textSourceBindPsdLayerIndex);
            }
        }

        internal bool TryGetTextStyleInfo(out PsdTextStyleInfo result)
        {
            result = default(PsdTextStyleInfo);
            if (TryGetTextLayerInfo(out var psdTextLayerInfo))
            {
                result = new PsdTextStyleInfo
                {
                    Text = null,
                    FontSize = 0,
                    IsLineSpacingAuto = true,
                    CharacterSpacing = 0f,
                    LineSpacing = 0f,
                    Color = Color.white,
                    LegacyFontStyle = (FontStyle)0,
                    TMPFontStyle = (FontStyles)0,
                    FontName = null,
                    HasOutline = false,
                    OutlineColor = Color.clear,
                    LegacyOutlineSize = 0f,
                    TMPOutlineSize = 0f,
                    OutlineMode = PsdTextStyleInfo.TMPOutlineMode.Center,
                    HasShadow = false,
                    IsInnerShadow = false,
                    ShadowColor = Color.clear,
                    ShadowOffset = Vector2.zero,
                    ShadowSpread = 0f,
                    ShadowSoftness = 0f,
                    HasGlow = false,
                    IsInnerGlow = false,
                    GlowColor = Color.clear,
                    GlowSize = 0f,
                    GlowSpread = 0f,
                    GlowOffset = 0f,
                    GlowPower = 0f,
                    HasBevel = false,
                    IsInnerBevel = false,
                    BevelSize = 0f,
                    BevelDepth = 0f,
                    BevelSoftness = 0f,
                    BevelAngle = 0f,
                    BevelAltitude = 0f,
                    BevelHighlightColor = Color.white,
                    BevelHighlightOpacity = 1f,
                    BevelShadowColor = Color.black,
                    BevelShadowOpacity = 1f,
                    HasGradient = false,
                    GradientAngle = 0f,
                    IsGradientReversed = false,
                    GradientStyleKey = null,
                    GradientBlendModeKey = null,
                    GradientStops = null
                };
                result.Text = psdTextLayerInfo.Text;
                result.FontSize = Mathf.Max(1, Mathf.FloorToInt(psdTextLayerInfo.FontSize));
                result.IsLineSpacingAuto = psdTextLayerInfo.AutoLeading || psdTextLayerInfo.Leading <= 0f;
                result.Color = ConvertPsdColor(psdTextLayerInfo.Color);
                if (!psdTextLayerInfo.FauxBold || !psdTextLayerInfo.FauxItalic)
                {
                    if (psdTextLayerInfo.FauxBold)
                    {
                        result.LegacyFontStyle = (FontStyle)1;
                    }
                    else if (!psdTextLayerInfo.FauxItalic)
                    {
                        result.LegacyFontStyle = (FontStyle)0;
                    }
                    else
                    {
                        result.LegacyFontStyle = (FontStyle)2;
                    }
                }
                else
                {
                    result.LegacyFontStyle = (FontStyle)3;
                }
                if (psdTextLayerInfo.FauxItalic)
                {
                    ref FontStyles fontStyle = ref result.TMPFontStyle;
                    fontStyle = (FontStyles)((uint)fontStyle | 2u);
                }
                if (psdTextLayerInfo.FauxBold)
                {
                    ref FontStyles fontStyles = ref result.TMPFontStyle;
                    fontStyles = (FontStyles)((uint)fontStyles | 1u);
                }
                if (psdTextLayerInfo.Underline)
                {
                    ref FontStyles fontStyles2 = ref result.TMPFontStyle;
                    fontStyles2 = (FontStyles)((uint)fontStyles2 | 4u);
                }
                if (psdTextLayerInfo.Strikethrough)
                {
                    ref FontStyles fontStyles3 = ref result.TMPFontStyle;
                    fontStyles3 = (FontStyles)((uint)fontStyles3 | 0x40u);
                }
                if (psdTextLayerInfo.AllCaps)
                {
                    ref FontStyles fontStyles4 = ref result.TMPFontStyle;
                    fontStyles4 = (FontStyles)((uint)fontStyles4 | 0x10u);
                }
                result.FontName = psdTextLayerInfo.FontName;
                result.CharacterSpacing = psdTextLayerInfo.Tracking * 0.1f;
                result.LineSpacing = psdTextLayerInfo.Leading;
                PopulateTextEffectStyle(psdTextLayerInfo, ref result);
                return true;
            }
            return false;
        }

        private static void PopulateTextEffectStyle(object value, ref PsdTextStyleInfo value2)
        {
            if (value == null)
            {
                return;
            }
            if (((PsdTextLayerInfo)value).Shadow != null && ((PsdTextLayerInfo)value).Shadow.Enabled)
            {
                value2.HasShadow = true;
                value2.IsInnerShadow = false;
                value2.ShadowColor = ConvertPsdColor(((PsdTextLayerInfo)value).Shadow.Color);
                value2.ShadowOffset = (Vector2)(Quaternion.Euler(0f, 0f, ((PsdTextLayerInfo)value).Shadow.Angle) * (Vector2)(Vector2.left * ((PsdTextLayerInfo)value).Shadow.Distance));
                value2.ShadowSpread = Mathf.Clamp01(((PsdTextLayerInfo)value).Shadow.Spread);
                value2.ShadowSoftness = Mathf.Max(0f, ((PsdTextLayerInfo)value).Shadow.Blur);
            }
            else if (((PsdTextLayerInfo)value).InnerShadow != null && ((PsdTextLayerInfo)value).InnerShadow.Enabled)
            {
                value2.HasShadow = true;
                value2.IsInnerShadow = true;
                value2.ShadowColor = ConvertPsdColor(((PsdTextLayerInfo)value).InnerShadow.Color);
                value2.ShadowOffset = (Vector2)(Quaternion.Euler(0f, 0f, ((PsdTextLayerInfo)value).InnerShadow.Angle) * (Vector2)(Vector2.left * ((PsdTextLayerInfo)value).InnerShadow.Distance));
                value2.ShadowSpread = Mathf.Clamp01(((PsdTextLayerInfo)value).InnerShadow.Spread);
                value2.ShadowSoftness = Mathf.Max(0f, ((PsdTextLayerInfo)value).InnerShadow.Blur);
            }
            if (((PsdTextLayerInfo)value).Stroke != null && ((PsdTextLayerInfo)value).Stroke.Enabled)
            {
                value2.HasOutline = true;
                value2.LegacyOutlineSize = GetTMPOutlineWidth(((PsdTextLayerInfo)value).Stroke);
                value2.TMPOutlineSize = Mathf.Max(0f, ((PsdTextLayerInfo)value).Stroke.Size);
                value2.OutlineMode = ConvertStrokePositionToTMPOutlineMode(((PsdTextLayerInfo)value).Stroke.Position);
                value2.OutlineColor = ConvertPsdColor(((PsdTextLayerInfo)value).Stroke.Color);
            }
            PsdTextGlowInfo psdTextGlowInfo = ((((PsdTextLayerInfo)value).OuterGlow != null && ((PsdTextLayerInfo)value).OuterGlow.Enabled) ? ((PsdTextLayerInfo)value).OuterGlow : ((((PsdTextLayerInfo)value).InnerGlow == null || !((PsdTextLayerInfo)value).InnerGlow.Enabled) ? null : ((PsdTextLayerInfo)value).InnerGlow));
            if (psdTextGlowInfo != null)
            {
                value2.HasGlow = true;
                value2.IsInnerGlow = psdTextGlowInfo.Inner;
                value2.GlowColor = ConvertPsdColor(psdTextGlowInfo.Color);
                value2.GlowSize = Mathf.Max(0f, psdTextGlowInfo.Size);
                value2.GlowSpread = Mathf.Clamp01(psdTextGlowInfo.Spread);
                value2.GlowOffset = 0f;
                value2.GlowPower = 0.75f;
            }
            if (((PsdTextLayerInfo)value).Bevel != null && ((PsdTextLayerInfo)value).Bevel.Enabled)
            {
                value2.HasBevel = true;
                value2.IsInnerBevel = ((PsdTextLayerInfo)value).Bevel.Inner;
                value2.BevelSize = Mathf.Max(0f, ((PsdTextLayerInfo)value).Bevel.Size);
                value2.BevelDepth = Mathf.Max(0f, ((PsdTextLayerInfo)value).Bevel.Depth);
                value2.BevelSoftness = Mathf.Max(0f, ((PsdTextLayerInfo)value).Bevel.Soften);
                value2.BevelAngle = ((PsdTextLayerInfo)value).Bevel.Angle;
                value2.BevelAltitude = Mathf.Clamp(((PsdTextLayerInfo)value).Bevel.Altitude, 0f, 90f);
                value2.BevelHighlightColor = ConvertPsdColor(((PsdTextLayerInfo)value).Bevel.HighlightColor);
                value2.BevelHighlightOpacity = Mathf.Clamp01(((PsdTextLayerInfo)value).Bevel.HighlightOpacity);
                value2.BevelShadowColor = ConvertPsdColor(((PsdTextLayerInfo)value).Bevel.ShadowColor);
                value2.BevelShadowOpacity = Mathf.Clamp01(((PsdTextLayerInfo)value).Bevel.ShadowOpacity);
            }
            if (((PsdTextLayerInfo)value).Gradient != null && ((PsdTextLayerInfo)value).Gradient.Enabled && ((PsdTextLayerInfo)value).Gradient.Stops != null && ((PsdTextLayerInfo)value).Gradient.Stops.Length >= 2)
            {
                PsdTextGradientStop[] stops = ((PsdTextLayerInfo)value).Gradient.Stops;
                TextGradientColorStop[] array = new TextGradientColorStop[stops.Length];
                for (int i = 0; i < stops.Length; i++)
                {
                    PsdTextGradientStop psdTextGradientStop = stops[i];
                    array[i] = new TextGradientColorStop
                    {
                        Position = Mathf.Clamp01(psdTextGradientStop.Location),
                        Color = ConvertPsdColor(psdTextGradientStop.Color)
                    };
                }
                Array.Sort(array, (TextGradientColorStop a, TextGradientColorStop b) => a.Position.CompareTo(b.Position));
                value2.HasGradient = true;
                value2.GradientAngle = ((PsdTextLayerInfo)value).Gradient.Angle;
                value2.IsGradientReversed = ((PsdTextLayerInfo)value).Gradient.Reverse;
                value2.GradientStyleKey = ((PsdTextLayerInfo)value).Gradient.StyleKey;
                value2.GradientBlendModeKey = ((PsdTextLayerInfo)value).Gradient.BlendModeKey;
                value2.GradientStops = array;
            }
        }

        private static PsdTextStyleInfo.TMPOutlineMode ConvertStrokePositionToTMPOutlineMode(PsdTextStrokePosition value)
        {
            return value switch
            {
                PsdTextStrokePosition.Center => PsdTextStyleInfo.TMPOutlineMode.Center, 
                PsdTextStrokePosition.Inside => PsdTextStyleInfo.TMPOutlineMode.Inside, 
                _ => PsdTextStyleInfo.TMPOutlineMode.Outside, 
            };
        }

        private static float GetTMPOutlineWidth(object value)
        {
            if (value != null)
            {
                float num = Mathf.Max(0f, ((PsdTextStrokeInfo)value).Size);
                return ((PsdTextStrokeInfo)value).Position switch
                {
                    PsdTextStrokePosition.Center => num * 0.5f, 
                    PsdTextStrokePosition.Inside => 0f, 
                    _ => num, 
                };
            }
            return 0f;
        }

        private static Color ConvertPsdColor(PsdColor value)
        {
            return new Color((float)(int)value.R / 255f, (float)(int)value.G / 255f, (float)(int)value.B / 255f, (float)(int)value.A / 255f);
        }

        internal static bool IsPsdLayerNodeObfuscationSentinelNull()
        {
            return (object)s_PsdLayerNodeObfuscationSentinel == null;
        }

        internal static PsdLayerNode GetPsdLayerNodeObfuscationSentinel()
        {
            return s_PsdLayerNodeObfuscationSentinel;
        }
    }

    internal sealed class LayerNodeExportName
    {
        public PsdLayerNode Node;

        public string Name;
    }
}
