namespace PsdLayoutTool2
{
    using System.Globalization;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// Computes three independent fingerprints. Keeping these concerns separate
    /// is what lets an incremental import update pixels/text without asking the
    /// hierarchy planner to reorganize an otherwise unchanged Prefab.
    /// </summary>
    public static class PsdHierarchyFingerprints
    {
        public static string Content(PsdPrefabNodeModel node)
        {
            var value = new StringBuilder();
            Append(value, node.assetFingerprint);
            Append(value, node.visible ? "1" : "0");
            Append(value, Float(node.opacity));

            if (node.text != null)
            {
                Append(value, node.text.contents);
                Append(value, node.text.fontFamily);
                Append(value, Float(node.text.fontSize));
                Append(value, ColorValue(node.text.fillColor));
                Append(value, Float(node.text.lineHeight));
                PsdPrefabTextEffectModel effect = node.text.effect;
                if (effect != null)
                {
                    Append(value, effect.hasOutline ? "1" : "0");
                    Append(value, ColorValue(effect.outlineColor));
                    Append(value, Float(effect.outlineWidth));
                    Append(value, effect.hasShadow ? "1" : "0");
                    Append(value, ColorValue(effect.shadowColor));
                    Append(value, Float(effect.shadowOffsetX));
                    Append(value, Float(effect.shadowOffsetY));
                    Append(value, Float(effect.shadowSoftness));
                    Append(value, Float(effect.shadowDilate));
                }
            }

            if (node.nineSlice != null)
            {
                Append(value, Float(node.nineSlice.left));
                Append(value, Float(node.nineSlice.top));
                Append(value, Float(node.nineSlice.right));
                Append(value, Float(node.nineSlice.bottom));
            }

            return PsdStableLayerIdUtility.ComputeFnv1a(value.ToString());
        }

        public static string Structure(PsdPrefabNodeModel node)
        {
            string value = (node.stableId ?? string.Empty) + "|" +
                           (node.parentStableId ?? string.Empty) + "|" +
                           node.siblingIndex.ToString(CultureInfo.InvariantCulture) + "|" + node.kind;
            return PsdStableLayerIdUtility.ComputeFnv1a(value);
        }

        public static string Geometry(PsdPrefabNodeModel node)
        {
            Rect bounds = node.bounds;
            string value = (node.stableId ?? string.Empty) + "|" + Float(bounds.x) + "|" + Float(bounds.y) + "|" +
                           Float(bounds.width) + "|" + Float(bounds.height);
            return PsdStableLayerIdUtility.ComputeFnv1a(value);
        }

        private static void Append(StringBuilder target, string value)
        {
            value = value ?? string.Empty;
            target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            target.Append(':');
            target.Append(value);
            target.Append('|');
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string ColorValue(Color color)
        {
            return Float(color.r) + "," + Float(color.g) + "," + Float(color.b) + "," + Float(color.a);
        }
    }
}
