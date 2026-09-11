using System.Collections.Generic;
using System.Linq;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using cn.efunstudio.psdreader.PsdParser;

namespace PsdLayerExtensionsNamespace
{
    internal static class PsdLayerExtensions
    {
internal static string GetLayerName(this object psdLayer)
        {
            if (psdLayer == null)
            {
                return string.Empty;
            }
            return ((PsdLayer)psdLayer).Name ?? string.Empty;
        }

        internal static PsdLayerType GetLayerType(this object psdLayer)
        {
            if (psdLayer == null)
            {
                return PsdLayerType.Unknown;
            }
            if (((PsdLayer)psdLayer).IsTextLayer())
            {
                return PsdLayerType.TextLayer;
            }
            if (!((PsdLayer)psdLayer).IsSolidFillLayer())
            {
                if (((PsdLayer)psdLayer).IsGroup)
                {
                    return PsdLayerType.LayerGroup;
                }
                return PsdLayerType.Layer;
            }
            return PsdLayerType.FillLayer;
        }

        internal static Rect GetUnityRect(this object psdLayer)
        {
            if (psdLayer == null || ((PsdLayer)psdLayer).Document == null)
            {
                return default(Rect);
            }
            Vector2Int val = default(Vector2Int);
            val = new Vector2Int(((PsdLayer)psdLayer).Document.Width, ((PsdLayer)psdLayer).Document.Height);
            return ConvertPsdBoundsToUnityRect(((PsdLayer)psdLayer).Left, ((PsdLayer)psdLayer).Top, ((PsdLayer)psdLayer).Right, ((PsdLayer)psdLayer).Bottom, val);
        }

        internal static Rect ConvertPsdBoundsToUnityRect(int value, int value2, int value3, int value4, Vector2Int value5)
        {
            float num = (float)Mathf.Abs(value3 - value) * 0.5f;
            float num2 = (float)Mathf.Abs(value4 - value2) * 0.5f;
            return new Rect((float)value + num - (float)value5.x * 0.5f, (float)value5.y - ((float)value2 + num2) - (float)value5.y * 0.5f, (float)(value3 - value), (float)(value4 - value2));
        }

        internal static PsdLayer GetLayerByTraversalIndex(this object psdDocument, int index)
        {
            if (psdDocument != null && index >= 0)
            {
                int num = 0;
                return FindLayerByTraversalIndex(((PsdDocument)psdDocument).Childs.OfType<PsdLayer>(), index, ref num);
            }
            return null;
        }

        private static PsdLayer FindLayerByTraversalIndex(IEnumerable<PsdLayer> psdLayers, int value, ref int value2)
        {
            if (psdLayers == null)
            {
                return null;
            }
            foreach (PsdLayer item in psdLayers)
            {
                PsdLayer psdLayer = FindLayerInBranchByTraversalIndex(item, value, ref value2);
                if (psdLayer != null)
                {
                    return psdLayer;
                }
            }
            return null;
        }

        private static PsdLayer FindLayerInBranchByTraversalIndex(object value, int value2, ref int value3)
        {
            if (value != null)
            {
                if (((PsdLayer)value).IsGroup)
                {
                    value3++;
                    PsdLayer psdLayer = FindLayerByTraversalIndex(((PsdLayer)value).Childs, value2, ref value3);
                    if (psdLayer != null)
                    {
                        return psdLayer;
                    }
                }
                if (value3 == value2)
                {
                    value3++;
                    return (PsdLayer)value;
                }
                value3++;
                return null;
            }
            return null;
        }

        internal static int GetLayerCount(this object psdDocument)
        {
            if (psdDocument != null && ((PsdDocument)psdDocument).Childs != null)
            {
                return CountLayersRecursively(((PsdDocument)psdDocument).Childs.OfType<PsdLayer>());
            }
            return 0;
        }

        internal static int GetLayerRecordSpan(this object psdLayer)
        {
            if (psdLayer != null)
            {
                if (!((PsdLayer)psdLayer).IsGroup)
                {
                    return 1;
                }
                return CountLayerRecordsRecursively(((PsdLayer)psdLayer).Childs) + 2;
            }
            return 0;
        }

        internal static int GetLayerRecordCount(this object psdDocument)
        {
            if (psdDocument == null || ((PsdDocument)psdDocument).Childs == null)
            {
                return 0;
            }
            return CountLayerRecordsRecursively(((PsdDocument)psdDocument).Childs.OfType<PsdLayer>());
        }

        private static int CountLayersRecursively(IEnumerable<PsdLayer> psdLayers)
        {
            int num = 0;
            if (psdLayers == null)
            {
                return num;
            }
            foreach (PsdLayer item in psdLayers)
            {
                num++;
                if (item != null && item.Childs != null && item.Childs.Length != 0)
                {
                    num += CountLayersRecursively(item.Childs);
                }
            }
            return num;
        }

        private static int CountLayerRecordsRecursively(IEnumerable<PsdLayer> psdLayers)
        {
            int num = 0;
            if (psdLayers == null)
            {
                return num;
            }
            foreach (PsdLayer item in psdLayers)
            {
                num += item.GetLayerRecordSpan();
            }
            return num;
        }

        internal static bool IsAvailable()

        {

            return true;

        }
}
}
