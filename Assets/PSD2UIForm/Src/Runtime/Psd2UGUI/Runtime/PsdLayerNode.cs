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
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using TextGradientColorStopNamespace;
using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;
using PsdTextStyleInfoNamespace;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
#if UNITY_EDITOR
    [CanEditMultipleObjects]
#endif
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

        /// <summary>
        /// 手动九宫格（9-slice）开关。
        /// 勾选后，导出该节点图片时使用 <see cref="nineSliceLeft"/> / Top / Right / Bottom 记录的边距，
        /// 而不是命名规则或像素推断的结果。由 Hierarchy 行上的九宫格标识与九宫格设置窗口写入。
        /// </summary>
        [SerializeField]
        [HideInInspector]
        internal bool nineSliceEnabled;

        /// <summary>手动九宫格左边距（源图像素）。</summary>
        [SerializeField]
        [HideInInspector]
        internal int nineSliceLeft;

        /// <summary>手动九宫格上边距（源图像素）。</summary>
        [SerializeField]
        [HideInInspector]
        internal int nineSliceTop;

        /// <summary>手动九宫格右边距（源图像素）。</summary>
        [SerializeField]
        [HideInInspector]
        internal int nineSliceRight;

        /// <summary>手动九宫格下边距（源图像素）。</summary>
        [SerializeField]
        [HideInInspector]
        internal int nineSliceBottom;

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

        /// <summary>
        /// 该节点是否支持九宫格。只有 Image 与 Background 会参与九宫格，
        /// Hierarchy 上的九宫格标识也只对这两种类型显示。
        /// </summary>
        internal bool SupportsNineSlice => UIType == GUIType.Image || UIType == GUIType.Background ||
                                           UIType == GUIType.Slider_Fill || UIType == GUIType.Slider_Handle;

        /// <summary>
        /// 是否手动启用了九宫格。Hierarchy 行上那颗九宫标识的亮 / 暗两态取的就是这个值。
        /// 写入统一走 <c>Psd2UiNineSliceNodeState.SetEnabled</c>（带 Undo 与 SetDirty）。
        /// </summary>
        internal bool NineSliceEnabled => nineSliceEnabled;

        /// <summary>是否已经记录了非零的手动九宫格边距。</summary>
        internal bool HasNineSliceBorder =>
            nineSliceLeft > 0 || nineSliceTop > 0 || nineSliceRight > 0 || nineSliceBottom > 0;

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
            return UITypeRules.IsPrimaryUIType(UIType);
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (host != null && host.IsPsdReaderProductAvailable() && GetBoundPsdLayer() != null && GetBoundPsdLayer().Document != null)
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (host != null && host.IsChineseNameConversionEnabled() && !string.IsNullOrEmpty(text))
            {
                text = AssetNameSanitizer.TransliterateChinese(text);
            }
            if (host != null)
            {
                text = host.RemoveRecognizedLayerTags(text);
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
                IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
                if (host != null && host.IsChineseNameConversionEnabled())
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (!string.IsNullOrWhiteSpace(text) && host != null)
            {
                if (!string.IsNullOrWhiteSpace(sourceLayerName))
                {
                    string b = host.BuildLayerObjectName(sourceLayerName, BindPsdLayerIndex);
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
                        string b2 = host.BuildLayerObjectName(text2, BindPsdLayerIndex);
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (host != null)
            {
                result = host.RemoveRecognizedLayerTags(result);
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
                IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
                if (host != null)
                {
                    result = host.RemoveRecognizedLayerTags(result);
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            UIType = ((host != null) ? host.ApplyForcedTMPType(uiType) : uiType);
#if UNITY_EDITOR
            RemoveAllHelperComponents();
#endif
            if (enabled)
            {
                if (host != null && host.HasConverterInstance())
                {
                    host.RefreshAllHelperComponents();
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (host == null)
            {
                return;
            }
            UIType = host.ApplyForcedTMPType(UIType);
            if (UIType == GUIType.Null)
            {
#if UNITY_EDITOR
                RemoveAllHelperComponents();
#endif
                return;
            }
            Type type = host.GetHelperComponentType(UIType);
#if UNITY_EDITOR
            RemoveMismatchedHelperComponents(type);
#endif
            if (type != null)
            {
                UIHelperBase obj = (((Component)this).gameObject.GetComponent(type) ?? ((Component)this).gameObject.AddComponent(type)) as UIHelperBase;
                obj.ParseAndAttachUIElements();
#if UNITY_EDITOR
                EditorUtility.SetDirty((Object)(object)obj);
#endif
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
#if UNITY_EDITOR
                    EditorUtility.SetDirty((Object)(object)component);
#endif
                    break;
                }
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty((Object)(object)this);
#endif
        }

#if UNITY_EDITOR
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
#if UNITY_EDITOR
            EditorUtility.SetDirty((Object)(object)this);
#endif
        }
#endif

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

        internal string ExportImageAsset(bool enabled = false, string text10 = null, string text11 = null, bool enabled2 = true, bool enabled3 = false, bool enabled4 = false)
        {
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (host == null)
            {
                return null;
            }
            return host.ExportImageAsset(this, enabled, text10, text11, enabled2, enabled3, enabled4);
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
                IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
                PreviewTexture = ((host == null) ? null : host.AcquirePreviewTexture(text, this));
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            string text = NormalizeCacheKeyPart((host == null) ? null : host.GetSourcePsdAssetPath());
            string text2 = NormalizeCacheKeyPart((host == null) ? null : host.GetPsdAssetChangeTime());
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
                IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
                bool flag = ((host == null) ? false : host.ReleasePreviewTexture(_previewCacheKey));
                if (!flag && (Object)(object)PreviewTexture != (Object)null)
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

        internal static Sprite LoadSpriteAtPath(object path)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace((string)path))
            {
                Sprite val = AssetDatabase.LoadAssetAtPath<Sprite>((string)path);
                if (!((Object)(object)val != (Object)null))
                {
                    return AssetDatabase.LoadAllAssetsAtPath((string)path).OfType<Sprite>().FirstOrDefault();
                }
                return val;
            }
#endif
            return null;
        }

        internal static string GetExistingExportPath(object path, object path2, bool enabled = false)
        {
            if (string.IsNullOrWhiteSpace((string)path) || string.IsNullOrWhiteSpace((string)path2))
            {
                return null;
            }
            string text = Path.Combine((string)path, (string)path2 + ".png").Replace("\\", "/");
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (host != null && host.MatchesTextureExportMode(text, enabled))
            {
                return text;
            }
            return null;
        }

        internal static bool RecreateSpriteWithBorder(object text, object value, Vector4 vector, out Sprite result)
        {
            result = null;
#if UNITY_EDITOR
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
#endif
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
                IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
                if (host == null || !host.TryGetRenderedRepresentativeColor(this, out result))
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

        internal bool CanRenderSyntheticGroup()
        {
            if (GetBoundPsdLayer() == null && BindPsdLayerIndex < 0 && LayerType == PsdLayerType.LayerGroup)
            {
                if (!UITypeRules.IsPanelOrNull(UIType) && !UITypeRules.IsCompositeControlType(UIType))
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

        internal bool HasChildLayerNodes()
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
            if (UITypeRules.IsAuxiliaryUIType(UIType))
            {
                Transform val = ((Component)this).transform.parent;
                while ((Object)(object)val != (Object)null)
                {
                    PsdLayerNode component = ((Component)val).GetComponent<PsdLayerNode>();
                    if (!((Object)(object)component == (Object)null))
                    {
                        if (UITypeRules.CanOwnAuxiliaryType(component.UIType, UIType))
                        {
                            return component;
                        }
                        val = ((!UITypeRules.IsPanelOrNull(component.UIType)) ? val.parent : val.parent);
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
                if (UITypeRules.IsAuxiliaryUIType(UIType))
                {
                    return (Object)(object)FindOwnerNode() == (Object)(object)layerNode;
                }
                if (!UITypeRules.IsPrimaryUIType(UIType))
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            string text = GetExistingExportPath((host == null) ? null : host.GetSharedAssetsOutputDirectory(), GetAssetReferenceKey(), IsHighBitDepthSource());
            if (!string.IsNullOrWhiteSpace(text))
            {
#if UNITY_EDITOR
                result = (Object)(((object)LoadSpriteAtPath(text)) ?? ((object)AssetDatabase.LoadAssetAtPath<Object>(text)));
#endif
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            string text = ((host == null) ? null : host.GetSharedPrefabOutputDirectory());
            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Replace("\\", "/");
#if UNITY_EDITOR
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
#endif
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
            IPsd2UIFormEditorHost host = Psd2UIFormEditorHost.Current;
            if (host != null)
            {
                result = host.FindNodeByReferenceKey(GetAssetReferenceKey());
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
                        if (!UITypeRules.IsPanelOrNull(component.UIType))
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
                    if (UITypeRules.CanOwnNestedControlType(component.UIType, UIType))
                    {
                        return component;
                    }
                    val = (UITypeRules.IsPanelOrNull(component.UIType) ? val.parent : val.parent);
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
                if (UITypeRules.IsLeafVisualType(UIType))
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
}
