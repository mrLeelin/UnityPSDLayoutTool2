using System.Text;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

using Object = UnityEngine.Object;
namespace LayerNodeIdUtilityNamespace
{
    internal sealed class LayerNodeIdUtility
    {
        internal static LayerNodeIdUtility s_ObfuscationSentinel;

        internal static string GetStableNodeId(object value, object value2)
        {
            if (!((Object)value2 == (Object)null))
            {
                if (((PsdLayerNode)value2).BindPsdLayerIndex >= 0)
                {
                    return $"psd:{((PsdLayerNode)value2).BindPsdLayerIndex}";
                }
                if (string.IsNullOrWhiteSpace(((PsdLayerNode)value2).GetGeneratedNodeId()))
                {
                    return BuildGeneratedNodeId(value, ((Component)value2).transform);
                }
                return ((PsdLayerNode)value2).GetGeneratedNodeId();
            }
            return string.Empty;
        }

        internal static bool IsGeneratedLayerGroup(object value)
        {
            if (!((Object)value != (Object)null) || ((PsdLayerNode)value).BindPsdLayerIndex >= 0)
            {
                return false;
            }
            return ((PsdLayerNode)value).LayerType == PsdLayerType.LayerGroup;
        }

        private static string BuildGeneratedNodeId(object value, object value2)
        {
            StringBuilder stringBuilder = new StringBuilder(128);
            stringBuilder.Append("gen:");
            AppendGeneratedNodePath(stringBuilder, value, value2);
            return stringBuilder.ToString();
        }

        private static void AppendGeneratedNodePath(object value, object value2, object value3)
        {
            if (value != null && !((Object)value3 == (Object)null))
            {
                if ((Object)(object)((Transform)value3).parent != (Object)null && ((Object)value2 == (Object)null || (Object)(object)((Transform)value3).parent != (Object)(object)((Component)value2).transform))
                {
                    AppendGeneratedNodePath(value, value2, ((Transform)value3).parent);
                    ((StringBuilder)value).Append('/');
                }
                ((StringBuilder)value).Append(SanitizeNodeNameSegment(((Object)value3).name));
                ((StringBuilder)value).Append('@');
                ((StringBuilder)value).Append(((Transform)value3).GetSiblingIndex());
            }
        }

        private static string SanitizeNodeNameSegment(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return "node";
            }
            StringBuilder stringBuilder = new StringBuilder(((string)value).Length);
            for (int i = 0; i < ((string)value).Length; i++)
            {
                char c = ((string)value)[i];
                stringBuilder.Append((char.IsLetterOrDigit(c) || c == '_' || c == '-') ? c : '_');
            }
            if (stringBuilder.Length > 0)
            {
                return stringBuilder.ToString();
            }
            return "node";
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LayerNodeIdUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
