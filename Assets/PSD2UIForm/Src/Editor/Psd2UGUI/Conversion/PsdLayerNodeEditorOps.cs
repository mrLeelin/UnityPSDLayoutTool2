using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetNameSanitizerNamespace;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader;
using cn.efunstudio.psdreader.PsdParser;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>
    /// 从 <see cref="PsdLayerNode"/> 搬出的编辑器侧实现。
    ///
    /// 原因：这些成员依赖只能存在于编辑器程序集中的类型（PsdRenderedImage / PsdLayerRenderer /
    /// PsdTextureAssetUtility / Psd2UIFormConverterEditor），而 PsdLayerNode 必须位于运行期程序集中。
    /// 方法体与搬迁前逐行一致，只把 <c>this</c> 显式化为首个 <c>PsdLayerNode node</c> 参数，
    /// 因此编辑器行为不变。
    /// </summary>
    internal static class PsdLayerNodeEditorOps
    {
        internal static PsdRenderedImage RenderNodeImage(PsdLayerNode node, bool enabled)
        {
            if (!node.HasChildLayerNodes())
            {
                if (node.GetBoundPsdLayer() == null)
                {
                    if (!node.CanRenderSyntheticGroup())
                    {
                        return null;
                    }
                    return RenderChildLayerTree(node, enabled);
                }
                if (enabled)
                {
                    return node.GetBoundPsdLayer().RenderPreview();
                }
                return node.GetBoundPsdLayer().Render();
            }
            return RenderChildLayerTree(node, enabled);
        }

        private static PsdRenderedImage RenderChildLayerTree(PsdLayerNode node, bool enabled)
        {
            List<PsdLayer> list = new List<PsdLayer>(((Component)node).transform.childCount);
            int i = 0;
            for (int childCount = ((Component)node).transform.childCount; i < childCount; i++)
            {
                PsdLayerNode component = ((Component)((Component)node).transform.GetChild(i)).GetComponent<PsdLayerNode>();
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

        private static void CollectRenderablePsdLayers(PsdLayerNode value, List<PsdLayer> psdLayers)
        {
            if (value == null || psdLayers == null)
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
            if (value.GetBoundPsdLayer() != null && !(value.LayerType == PsdLayerType.LayerGroup && flag))
            {
                psdLayers.Add(value.GetBoundPsdLayer());
            }
        }

        internal static bool TrySampleRepresentativeColor(PsdRenderedImage value, out Color result)
        {
            result = default(Color);
            if (value != null && !value.IsEmpty && value.Rgba32 != null && value.Rgba32.Length >= 4)
            {
                int num = ((value.Width > 1) ? Mathf.Max(1, Mathf.RoundToInt((float)value.Width * 0.25f)) : 0);
                int num2 = ((value.Height > 1) ? Mathf.Max(1, Mathf.RoundToInt((float)value.Height * 0.25f)) : 0);
                int num3 = value.Width / 2;
                int num4 = value.Height / 2;
                uint[] array = new uint[4];
                int num5 = 0;
                if (TryReadOpaquePixel(value, num3, Mathf.Clamp(num4 - num2, 0, value.Height - 1), out var num6))
                {
                    array[num5++] = num6;
                }
                if (TryReadOpaquePixel(value, num3, Mathf.Clamp(num4 + num2, 0, value.Height - 1), out num6))
                {
                    array[num5++] = num6;
                }
                if (TryReadOpaquePixel(value, Mathf.Clamp(num3 - num, 0, value.Width - 1), num4, out num6))
                {
                    array[num5++] = num6;
                }
                if (TryReadOpaquePixel(value, Mathf.Clamp(num3 + num, 0, value.Width - 1), num4, out num6))
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

        private static bool TryReadOpaquePixel(PsdRenderedImage value, int value2, int value3, out uint result)
        {
            result = 0u;
            if (value != null && !value.IsEmpty && value.Rgba32 != null && value.Rgba32.Length >= 4)
            {
                if (value2 >= 0 && value3 >= 0 && value2 < value.Width && value3 < value.Height)
                {
                    int num = ((value.Height - 1 - value3) * value.Width + value2) * 4;
                    if (num >= 0 && num + 3 < value.Rgba32.Length)
                    {
                        byte b = value.Rgba32[num + 3];
                        if (b == 0)
                        {
                            return false;
                        }
                        result = PackRgba32(value.Rgba32[num], value.Rgba32[num + 1], value.Rgba32[num + 2], b);
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

        internal static Texture2D CreatePreviewTexture(PsdLayerNode node)
        {
            PsdRenderedImage psdRenderedImage = RenderNodeImage(node, true);
            if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
            {
                return ConvertRenderedImageToTexture(psdRenderedImage, true);
            }
            return null;
        }

        /// <summary>
        /// 供九宫格窗口使用的预览纹理：走与导出完全相同的 <c>Render()</c>，
        /// 所以边距的像素坐标和最终导出的 PNG 是同一个空间。
        ///
        /// 与 <see cref="CreatePreviewTexture"/> 的差别：不用 <c>RenderPreview()</c>，
        /// 也不经过 PsdLayerPreviewCache —— 宿主在 Prefab 隔离模式下可能取不到，
        /// 那正是"窗口打得开却提示无可用图像"的原因。调用方负责销毁返回的纹理。
        /// </summary>
        internal static Texture2D CreateNineSliceSourceTexture(PsdLayerNode node)
        {
            if ((Object)node == (Object)null)
            {
                return null;
            }

            PsdRenderedImage psdRenderedImage = RenderNodeImage(node, false);
            if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
            {
                return ConvertRenderedImageToTexture(psdRenderedImage, true);
            }
            return null;
        }

        private static Texture2D ConvertRenderedImageToTexture(PsdRenderedImage value, bool enabled)
        {
            if (value != null && !value.IsEmpty)
            {
                TextureFormat val = (TextureFormat)(value.IsHighBitDepth ? 74 : 4);
                Texture2D val2 = new Texture2D(value.Width, value.Height, val, false);
                ((Object)val2).hideFlags = (HideFlags)(enabled ? 61 : 0);
                val2.alphaIsTransparency = true;
                if (!value.IsHighBitDepth)
                {
                    val2.LoadRawTextureData(value.Rgba32);
                }
                else
                {
                    ushort[] rgba = value.Rgba64;
                    byte[] array = new byte[rgba.Length * 2];
                    Buffer.BlockCopy(rgba, 0, array, 0, array.Length);
                    val2.LoadRawTextureData(array);
                }
                val2.Apply(false, false);
                return val2;
            }
            return null;
        }

        internal static string ExportImageAsset(PsdLayerNode node, bool enabled = false, string text10 = null, string text11 = null, bool enabled2 = true, bool enabled3 = false, bool enabled4 = false)
        {
            Psd2UIFormConverterEditor value = Psd2UIFormConverterEditor.Instance;
            if (value != null && value.TryGetCachedExportPath(node, out var result))
            {
                return result;
            }
            bool flag = false;
            string text = null;
            string text2 = text10;
            string text3 = text11;
            if (value != null && string.IsNullOrEmpty(text2) && !node.HasAssetReference() && value.TryGetSharedExportTarget(node, out var text4, out var text5))
            {
                text2 = text4;
                text3 = text5;
                flag = true;
                text = text5;
                enabled = true;
            }
            if (!enabled4 && node.HasAssetReference() && value != null)
            {
                return value.ResolveOrExportReferencedImage(node, enabled3);
            }
            string result2 = null;
            if (node.UIType != GUIType.FillColor && node.LayerType != PsdLayerType.FillLayer)
            {
                PsdRenderedImage psdRenderedImage = RenderNodeImage(node, false);
                if (psdRenderedImage != null && !psdRenderedImage.IsEmpty)
                {
                    bool flag2 = node.UIType != GUIType.FillColor && node.UIType != GUIType.RawImage;
                    text2 = (string.IsNullOrWhiteSpace(text2) ? Psd2UIFormConverterEditor.Instance.GetImageExportDirectory() : text2);
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
                    string text6 = (string.IsNullOrWhiteSpace(text3) ? EnsureUniqueExportName(node, value, GetExportBaseName(node, enabled2), enabled2) : GetExportBaseName(node, enabled2));
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
                    Psd2UIFormConverterEditor.ConvertTexturesType(new string[1] { text9 }, flag2 || enabled, psdRenderedImage.IsHighBitDepth);
                    if (enabled3)
                    {
                        Psd2UIFormConverterEditor.EnsureNineSliceBorder(text9, node);
                    }
                    if (flag && value != null)
                    {
                        value.CacheExportPath(string.IsNullOrEmpty(text) ? node.GetNormalizedNodeKey() : text, text9);
                    }
                }
                return result2;
            }
            return null;
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

        private static string EnsureUniqueExportName(PsdLayerNode owner, Psd2UIFormConverterEditor value, string text, bool enabled)
        {
            if (!(value == null) && !string.IsNullOrWhiteSpace(text))
            {
                PsdLayerNode[] componentsInChildren = value.gameObject.GetComponentsInChildren<PsdLayerNode>(true);
                if (componentsInChildren != null && componentsInChildren.Length != 0)
                {
                    PsdLayerNode[] array = componentsInChildren.Where(delegate(PsdLayerNode node)
                    {
                        if (!((Object)(object)node != (Object)null))
                        {
                            return false;
                        }
                        return (Object)(object)node == (Object)(object)owner || node.ShouldExportImage();
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
                            int num2 = Array.IndexOf(array4, owner);
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

        private static int GetObjectInstanceId(object value)
        {
            return ((Object)value).GetInstanceID();
        }
    }

    internal sealed class LayerNodeExportName
    {
        public PsdLayerNode Node;

        public string Name;
    }
}
