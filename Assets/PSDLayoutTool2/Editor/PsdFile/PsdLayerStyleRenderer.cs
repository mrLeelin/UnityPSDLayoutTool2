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
        /// 重建填充、应用用户蒙版后渲染样式并扩展边界；第 0 行是底边，图层不透明度在返回后应用。
        /// </summary>
        internal static Color32[] Apply(Layer layer, Color32[] colors)
        {
            PsdLayerShapeStyle style = layer?.ShapeStyle;
            if (style == null || layer.IsTextLayer || colors == null || colors.Length == 0)
            {
                ImageDecoder.ApplyUserMask(layer, colors);
                return colors;
            }

            bool rebuiltShape = false;
            if (style.HasVectorMask && IsFullyTransparent(colors))
            {
                RebuildShape(layer, style, colors);
                rebuiltShape = true;
            }

            if (style.GradientOverlay != null && !rebuiltShape)
                ApplyGradientOverlay(layer, style, colors);
            if (style.ColorOverlayEnabled && !rebuiltShape)
                ApplyColorOverlay(style, colors);

            float[] coverage = ImageDecoder.GetMaskedCoverage(layer, colors);
            ImageDecoder.ApplyUserMask(layer, colors);
            ImageDecoder.ApplyClippingLayers(layer, colors);
            Rect renderRect = GetRenderRect(layer);
            int width = (int)renderRect.width;
            int height = (int)renderRect.height;
            if (renderRect != layer.Rect)
            {
                var expanded = new Color32[width * height];
                var expandedCoverage = new float[width * height];
                int offsetX = (int)(layer.Rect.x - renderRect.x);
                int offsetY = (int)(renderRect.yMax - layer.Rect.yMax);
                for (int row = 0; row < (int)layer.Rect.height; row++)
                {
                    int source = row * (int)layer.Rect.width, target = (row + offsetY) * width + offsetX;
                    System.Array.Copy(colors, source, expanded, target, (int)layer.Rect.width);
                    System.Array.Copy(coverage, source, expandedCoverage, target, (int)layer.Rect.width);
                }
                colors = expanded;
                coverage = expandedCoverage;
            }
            ApplyRasterEffects(style, colors, coverage, width, height);
            return colors;
        }

        internal static Rect GetRenderRect(Layer layer)
        {
            if (layer == null) return default(Rect);
            Rect rect = layer.Rect;
            PsdLayerShapeStyle style = layer.ShapeStyle;
            if (style == null || layer.IsTextLayer || rect.width <= 0 || rect.height <= 0) return rect;
            float stroke = style.StrokePosition == "InsF" ? 0f :
                style.StrokeWidth * (style.StrokePosition == "CtrF" ? 0.5f : 1f);
            int padding = style.StrokeEnabled && style.StrokeOpacity > 0f && stroke > 0f
                ? Mathf.CeilToInt(stroke) + 1 : 0;
            int left = padding, right = padding, top = padding, bottom = padding;
            if (style.DropShadowEnabled && style.DropShadowColor.a > 0f)
            {
                int blur = Mathf.Clamp(Mathf.CeilToInt(style.DropShadowBlur), 0, 32) + 1;
                int dx = Mathf.RoundToInt(Mathf.Cos(style.DropShadowAngle * Mathf.Deg2Rad) * style.DropShadowDistance);
                int dy = Mathf.RoundToInt(Mathf.Sin(style.DropShadowAngle * Mathf.Deg2Rad) * style.DropShadowDistance);
                left = Mathf.Max(left, blur - dx);
                right = Mathf.Max(right, blur + dx);
                top = Mathf.Max(top, blur + dy);
                bottom = Mathf.Max(bottom, blur - dy);
            }
            return new Rect(rect.x - left, rect.y - top, rect.width + left + right, rect.height + top + bottom);
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
            bool photoshopDrawsSomething = style.HasVectorMask || style.ColorOverlayEnabled || (style.GradientOverlay != null) ||
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

            foreach (Layer clipped in layer.ClippedLayers)
            {
                if (clipped.Visible && !ImageDecoder.SupportsClippingBlend(clipped.BlendModeKey))
                    issues.Add("剪贴图层「" + clipped.Name + "」使用未支持的混合模式：" + clipped.BlendModeKey);
            }

            return issues;
        }

        private static string DescribeEmptyReason(PsdLayerShapeStyle style)
        {
            if (!style.HasVectorMask)
            {
                return "导出结果为空（像素全透明，外观只来自图层样式）";
            }

            if (!style.ColorOverlayEnabled && style.GradientOverlay == null && !style.ShapeFillEnabled)
            {
                return "导出结果为空（矢量形状的填充已关闭，且没有颜色叠加）";
            }

            return "导出结果为空（矢量形状没有可用的纯色填充）";
        }

        private static void RebuildShape(Layer layer, PsdLayerShapeStyle style, Color32[] colors)
        {
            Color fill;
            float fillAlpha;
            if (style.ColorOverlayEnabled && style.GradientOverlay == null)
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
            else if (style.GradientOverlay != null)
            {
                fill = Color.clear;
                fillAlpha = 0f;
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
            for (int row = 0; row < height; row++)
            {
                int textureRow = (height - 1 - row) * width;
                for (int column = 0; column < width; column++)
                {
                    Color pixel = fill;
                    pixel.a = fillAlpha;
                    if (style.GradientOverlay != null)
                    {
                        Color gradient = SampleGradient(layer, style, column, row);
                        float alpha = gradient.a + fillAlpha * (1f - gradient.a);
                        if (alpha > 0f)
                            pixel = (gradient * gradient.a + fill * (fillAlpha * (1f - gradient.a))) / alpha;
                        pixel.a = alpha;
                        // Color overlays sit above gradient overlays in Photoshop.
                        if (style.ColorOverlayEnabled)
                        {
                            pixel = Color.Lerp(pixel, style.ColorOverlayColor, style.ColorOverlayOpacity);
                            pixel.a = alpha;
                        }
                    }
                    pixel.a *= coverage[row * width + column];
                    colors[textureRow + column] = pixel;
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

        private static Color SampleGradient(Layer layer, PsdLayerShapeStyle style, int column, int topRow)
        {
            return style.GradientOverlay.Sample(layer.Rect.x + column + 0.5f, layer.Rect.y + topRow + 0.5f,
                layer.Rect, layer.PsdFile.Width, layer.PsdFile.Height);
        }

        private static void ApplyGradientOverlay(Layer layer, PsdLayerShapeStyle style, Color32[] colors)
        {
            int width = (int)layer.Rect.width;
            int height = (int)layer.Rect.height;
            for (int row = 0; row < height; row++) for (int column = 0; column < width; column++)
            {
                int index = row * width + column;
                Color32 source = colors[index];
                Color gradient = SampleGradient(layer, style, column, height - 1 - row);
                Color32 result = Color.Lerp(source, gradient, gradient.a);
                result.a = source.a;
                colors[index] = result;
            }
        }

        private static void ApplyRasterEffects(PsdLayerShapeStyle style, Color32[] colors, float[] coverage, int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            byte[] alpha = new byte[colors.Length];
            for (int i = 0; i < colors.Length; i++) alpha[i] = colors[i].a;
            if (style.StrokeEnabled && style.StrokeWidth > 0f)
            {
                float lo = style.StrokePosition == "OutF" ? 0f :
                    -style.StrokeWidth * (style.StrokePosition == "CtrF" ? 0.5f : 1f);
                float hi = style.StrokePosition == "InsF" ? 0f :
                    style.StrokeWidth * (style.StrokePosition == "CtrF" ? 0.5f : 1f);
                CompositeStroke(colors, StrokeCoverage(coverage, width, height, lo, hi),
                    style.StrokeColor, style.StrokeOpacity, style.StrokePosition == "OutF");
            }
            if (style.DropShadowEnabled)
            {
                byte[] shadow = OffsetBlur(alpha, width, height, style.DropShadowDistance, style.DropShadowAngle, style.DropShadowBlur);
                CompositeMask(colors, shadow, style.DropShadowColor, 1f, alpha, true);
            }
            if (style.InnerShadowEnabled)
            {
                byte[] edge = Subtract(alpha, Erode(alpha, width, height, Mathf.Max(1, Mathf.CeilToInt(style.InnerShadowDistance + style.InnerShadowBlur))), width, height);
                CompositeMask(colors, edge, style.InnerShadowColor, 1f, alpha, false);
            }
            if (style.InnerGlowEnabled)
            {
                byte[] edge = Subtract(alpha, Erode(alpha, width, height, Mathf.Max(1, Mathf.CeilToInt(style.InnerGlowSize))), width, height);
                CompositeMask(colors, edge, style.InnerGlowColor, 1f, alpha, false);
            }
        }

        // Alpha describes how the contour crosses an edge pixel. Expanding
        // alpha values themselves incorrectly carries weak coverage outward.
        private static byte[] StrokeCoverage(float[] alpha, int width, int height, float lo, float hi)
        {
            float reach = Mathf.Max(Mathf.Abs(lo), Mathf.Abs(hi)) + 1f;
            int extent = Mathf.CeilToInt(reach);
            var offsets = new List<Vector3>();
            for (int y = -extent; y <= extent; y++) for (int x = -extent; x <= extent; x++)
            {
                float distance = Mathf.Sqrt(x * x + y * y);
                if (distance <= reach) offsets.Add(new Vector3(x, y, distance));
            }
            var hard = new bool[alpha.Length];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float a = alpha[y * width + x];
                if (a != 0f && a != 1f) continue;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;
                    int px = x + dx, py = y + dy;
                    if (px >= 0 && px < width && py >= 0 && py < height &&
                        alpha[py * width + px] == 1f - a) hard[y * width + x] = true;
                }
            }
            var result = new byte[alpha.Length];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float coverage = alpha[index];
                float distance = 0.5f - coverage;
                if (alpha[index] == 0f || alpha[index] == 1f)
                {
                    bool inside = alpha[index] == 1f;
                    float partialDistance = float.PositiveInfinity, hardDistance = float.PositiveInfinity;
                    float oppositeDistance = float.PositiveInfinity;
                    distance = inside ? float.NegativeInfinity : float.PositiveInfinity;
                    foreach (Vector3 offset in offsets)
                    {
                        int px = x + (int)offset.x, py = y + (int)offset.y;
                        if (px < 0 || px >= width || py < 0 || py >= height) continue;
                        int neighbor = py * width + px;
                        float a = alpha[neighbor];
                        if (hard[neighbor]) hardDistance = Mathf.Min(hardDistance, offset.z);
                        if ((a >= 0.5f) != inside) oppositeDistance = Mathf.Min(oppositeDistance, offset.z);
                        if (a == 0f || a == 1f) continue;
                        partialDistance = Mathf.Min(partialDistance, offset.z);
                        float stated = 0.5f - a + (inside ? -offset.z : offset.z);
                        distance = inside ? Mathf.Max(distance, stated) : Mathf.Min(distance, stated);
                    }
                    if (hardDistance < partialDistance || float.IsPositiveInfinity(partialDistance))
                        distance = (inside ? -1f : 1f) * Mathf.Max(oppositeDistance - 0.5f, 0f);
                }
                float band = Mathf.Clamp01(hi - distance + 0.5f) * Mathf.Clamp01(distance - lo + 0.5f);
                result[index] = (byte)Mathf.RoundToInt(band * 255f);
            }
            return result;
        }

        private static void CompositeStroke(Color32[] colors, byte[] coverage, Color color, float opacity, bool behind)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                float alpha = coverage[i] / 255f * Mathf.Clamp01(opacity) * color.a;
                if (alpha <= 0f) continue;
                Color source = colors[i];
                // Outside stroke is behind the layer's antialiased pixels;
                // keep their photograph color rather than painting over it.
                float strokeAlpha = alpha * (1f - source.a);
                float resultAlpha = source.a + strokeAlpha;
                Color result = behind
                    ? (source * source.a + color * strokeAlpha) / resultAlpha
                    : (color * alpha + source * source.a * (1f - alpha)) / resultAlpha;
                result.a = resultAlpha;
                colors[i] = result;
            }
        }

        private static byte[] Erode(byte[] source, int width, int height, int radius)
        {
            var result = new byte[source.Length];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            { byte min = byte.MaxValue; for (int yy = -radius; yy <= radius; yy++) for (int xx = -radius; xx <= radius; xx++) { int px = x + xx, py = y + yy; min = (byte)Mathf.Min(min, px >= 0 && px < width && py >= 0 && py < height ? source[py * width + px] : 0); } result[y * width + x] = min; }
            return result;
        }

        private static byte[] Subtract(byte[] outer, byte[] inner, int width, int height)
        { var result = new byte[outer.Length]; for (int i = 0; i < result.Length; i++) result[i] = (byte)Mathf.Max(0, outer[i] - inner[i]); return result; }

        private static byte[] OffsetBlur(byte[] source, int width, int height, float distance, float angle, float blur)
        {
            int dx = Mathf.RoundToInt(Mathf.Cos(angle * Mathf.Deg2Rad) * distance);
            int dy = Mathf.RoundToInt(Mathf.Sin(angle * Mathf.Deg2Rad) * distance);
            int radius = Mathf.Clamp(Mathf.CeilToInt(blur), 0, 32);
            var result = new byte[source.Length];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { int sum = 0, count = 0; for (int yy = -radius; yy <= radius; yy++) for (int xx = -radius; xx <= radius; xx++) { int px = x - dx + xx, py = y - dy + yy; if (px >= 0 && px < width && py >= 0 && py < height) { sum += source[py * width + px]; count++; } } result[y * width + x] = (byte)(count == 0 ? 0 : sum / count); }
            return result;
        }

        private static void CompositeMask(Color32[] colors, byte[] mask, Color color, float opacity, byte[] originalAlpha, bool behind)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                float a = mask[i] / 255f * Mathf.Clamp01(opacity) * color.a;
                if (a <= 0f) continue;
                Color dst = colors[i]; // Color32 -> Color already normalizes all four channels.
                if (behind)
                {
                    float effectAlpha = a * (1f - dst.a);
                    float outputAlpha = dst.a + effectAlpha;
                    Color output = (dst * dst.a + color * effectAlpha) / outputAlpha;
                    output.a = outputAlpha;
                    colors[i] = output;
                }
                else
                {
                    // Inner effects change color while retaining the layer's coverage.
                    Color output = Color.Lerp(dst, color, a);
                    output.a = originalAlpha[i] / 255f;
                    colors[i] = output;
                }
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
