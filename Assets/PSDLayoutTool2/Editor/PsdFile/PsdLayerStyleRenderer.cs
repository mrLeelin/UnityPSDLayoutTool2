namespace PhotoshopFile
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 在解码后的图层像素上重建 Photoshop 的形状填充与颜色叠加。
    /// 只处理非文字图层；文字层的颜色叠加由 TMP 文字样式负责。
    /// </summary>
    internal static class PsdLayerStyleRenderer
    {
        /// <summary>
        /// 就地修改像素（Unity 纹理行序：第 0 行是图层底边），在用户蒙版与图层不透明度之前调用。
        /// </summary>
        internal static void Apply(Layer layer, Color32[] colors)
        {
            PsdLayerShapeStyle style = layer?.ShapeStyle;
            if (style == null || layer.IsTextLayer || colors == null || colors.Length == 0)
            {
                return;
            }

            if (style.HasVectorMask && IsFullyTransparent(colors))
            {
                RebuildShape(layer, style, colors);
                return;
            }

            if (style.ColorOverlayEnabled)
            {
                ApplyColorOverlay(style, colors);
            }
        }

        /// <summary>
        /// 描述该图层导出后与 Photoshop 外观不一致的原因（导出为空、未渲染的样式、不支持的填充）。
        /// 没有问题时返回空列表。
        /// </summary>
        internal static List<string> DescribeExportIssues(Layer layer, Color32[] decodedColors)
        {
            var issues = new List<string>();
            PsdLayerShapeStyle style = layer?.ShapeStyle;
            if (layer == null || layer.IsTextLayer || style == null)
            {
                return issues;
            }

            // 普通位图层本就全透明（如点击热区）不算问题；只有 Photoshop 会画出内容时才报告。
            bool photoshopDrawsSomething = style.HasVectorMask || style.ColorOverlayEnabled ||
                                           style.UnrenderedEffects.Count > 0 ||
                                           !string.IsNullOrEmpty(style.UnsupportedShapeFill);
            if (photoshopDrawsSomething && decodedColors != null && decodedColors.Length > 0 &&
                IsFullyTransparent(decodedColors))
            {
                issues.Add(DescribeEmptyReason(style));
            }

            if (!string.IsNullOrEmpty(style.UnsupportedShapeFill))
            {
                issues.Add("形状使用了" + style.UnsupportedShapeFill + "（未导出）");
            }

            if (style.UnrenderedEffects.Count > 0)
            {
                issues.Add("未导出的图层样式：" + string.Join("、", style.UnrenderedEffects));
            }

            return issues;
        }

        private static string DescribeEmptyReason(PsdLayerShapeStyle style)
        {
            if (!style.HasVectorMask)
            {
                return "导出结果为空（像素全透明，外观只来自图层样式）";
            }

            if (!style.ColorOverlayEnabled && !style.ShapeFillEnabled)
            {
                return "导出结果为空（矢量形状的填充已关闭，且没有颜色叠加）";
            }

            return "导出结果为空（矢量形状没有可用的纯色填充）";
        }

        private static void RebuildShape(Layer layer, PsdLayerShapeStyle style, Color32[] colors)
        {
            Color fill;
            float fillAlpha;
            if (style.ColorOverlayEnabled)
            {
                // 形状填充关闭时 Photoshop 仍以矢量形状作为图层区域，颜色叠加按自身不透明度画在上面。
                bool hasBase = style.ShapeFillEnabled && style.HasShapeFillColor;
                fill = hasBase
                    ? Color.Lerp(style.ShapeFillColor, style.ColorOverlayColor, style.ColorOverlayOpacity)
                    : style.ColorOverlayColor;
                fillAlpha = hasBase ? 1f : style.ColorOverlayOpacity;
            }
            else if (style.ShapeFillEnabled && style.HasShapeFillColor)
            {
                fill = style.ShapeFillColor;
                fillAlpha = 1f;
            }
            else
            {
                return;
            }

            PsdVectorShape shape = style.BuildVectorShape(layer.PsdFile.Width, layer.PsdFile.Height);
            if (shape == null)
            {
                return;
            }

            int width = (int)layer.Rect.width;
            int height = (int)layer.Rect.height;
            float[] coverage = shape.Rasterize(layer.Rect);
            Color32 rgb = fill;
            for (int row = 0; row < height; row++)
            {
                int textureRow = (height - 1 - row) * width;
                for (int column = 0; column < width; column++)
                {
                    float alpha = coverage[row * width + column] * fillAlpha;
                    colors[textureRow + column] = new Color32(rgb.r, rgb.g, rgb.b, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
        }

        private static void ApplyColorOverlay(PsdLayerShapeStyle style, Color32[] colors)
        {
            Color overlay = style.ColorOverlayColor;
            float opacity = style.ColorOverlayOpacity;
            for (int index = 0; index < colors.Length; index++)
            {
                Color32 source = colors[index];
                Color blended = Color.Lerp(source, overlay, opacity);
                Color32 result = blended;
                result.a = source.a;
                colors[index] = result;
            }
        }

        private static bool IsFullyTransparent(Color32[] colors)
        {
            for (int index = 0; index < colors.Length; index++)
            {
                if (colors[index].a != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
