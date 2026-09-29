namespace PhotoshopFile
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 图层的矢量形状、形状填充与图层样式（导出纹理所需的部分）。
    /// Photoshop 的形状图层常把像素通道存成全透明，可见外观由矢量路径 + 填充/颜色叠加实时渲染，
    /// 只解码像素通道会导出空图；这里保存重建外观所需的数据，并记录无法重建的样式供告警。
    /// </summary>
    internal sealed class PsdLayerShapeStyle
    {
        private static readonly KeyValuePair<string, string>[] UnrenderedEffectKeys =
        {
            new KeyValuePair<string, string>("OrGl", "外发光"),
            new KeyValuePair<string, string>("ebbl", "斜面和浮雕"),
            new KeyValuePair<string, string>("ChFX", "光泽"),
            new KeyValuePair<string, string>("GrFl", "渐变叠加"),
            new KeyValuePair<string, string>("gradientFillMulti", "渐变叠加"),
            new KeyValuePair<string, string>("patternFill", "图案叠加"),
        };

        private readonly List<string> unrenderedEffects = new List<string>();
        private byte[] vectorMaskData;

        private PsdLayerShapeStyle()
        {
            ShapeFillEnabled = true;
            ColorOverlayOpacity = 1f;
        }

        /// <summary>是否带矢量蒙版/形状路径（vmsk 或 vsms）。</summary>
        internal bool HasVectorMask => vectorMaskData != null;

        /// <summary>形状填充是否开启（vstk.fillEnabled；无 vstk 时视为开启）。</summary>
        internal bool ShapeFillEnabled { get; private set; }

        /// <summary>是否读到了纯色形状填充（vscg 或纯色填充层 SoCo）。</summary>
        internal bool HasShapeFillColor { get; private set; }

        internal Color ShapeFillColor { get; private set; }

        /// <summary>无法重建的形状填充类型（渐变填充 / 图案填充），为空表示没有。</summary>
        internal string UnsupportedShapeFill { get; private set; } = string.Empty;

        internal bool ColorOverlayEnabled { get; private set; }

        internal Color ColorOverlayColor { get; private set; }

        /// <summary>颜色叠加不透明度，取值 0..1。</summary>
        internal float ColorOverlayOpacity { get; private set; }

        internal bool StrokeEnabled { get; private set; }
        internal float StrokeWidth { get; private set; }
        internal Color StrokeColor { get; private set; }
        internal float StrokeOpacity { get; private set; } = 1f;
        internal bool DropShadowEnabled { get; private set; }
        internal float DropShadowDistance { get; private set; }
        internal float DropShadowAngle { get; private set; } = 120f;
        internal float DropShadowBlur { get; private set; }
        internal Color DropShadowColor { get; private set; } = Color.black;
        internal bool InnerShadowEnabled { get; private set; }
        internal float InnerShadowDistance { get; private set; }
        internal float InnerShadowAngle { get; private set; } = 120f;
        internal float InnerShadowBlur { get; private set; }
        internal Color InnerShadowColor { get; private set; } = Color.black;
        internal bool InnerGlowEnabled { get; private set; }
        internal float InnerGlowSize { get; private set; }
        internal Color InnerGlowColor { get; private set; } = Color.white;

        /// <summary>已启用、但导出纹理时不会渲染的图层样式（中文名，去重）。</summary>
        internal IReadOnlyList<string> UnrenderedEffects => unrenderedEffects;

        internal static PsdLayerShapeStyle Read(IEnumerable<AdjustmentLayerInfo> infos)
        {
            var style = new PsdLayerShapeStyle();
            foreach (AdjustmentLayerInfo info in infos ?? Array.Empty<AdjustmentLayerInfo>())
            {
                byte[] data = info.RawData;
                switch (info.Key)
                {
                    case "vmsk":
                    case "vsms":
                        if (style.vectorMaskData == null)
                        {
                            style.vectorMaskData = data;
                        }

                        break;
                    case "vscg":
                        // vscg is the vector shape's fill-content record. Its
                        // payload starts with a version/descriptor, not an
                        // ASCII fill-kind tag, so treat it as the solid-color
                        // shape record and look for its Clr  value directly.
                        style.ReadFillContent(data, "SoCo");
                        break;
                    case "SoCo":
                    case "GdFl":
                    case "PtFl":
                        style.ReadFillContent(data, info.Key);
                        break;
                    case "vstk":
                        bool fillEnabled;
                        int ignored;
                        if (Layer.TryReadEffectEnabled(data, "fillEnabled", out fillEnabled, out ignored))
                        {
                            style.ShapeFillEnabled = fillEnabled;
                        }

                        break;
                    case "lfx2":
                    case "lmfx":
                    case "lfxs":
                        style.ReadEffects(data);
                        break;
                }
            }

            return style;
        }

        /// <summary>按文档尺寸解析矢量路径；无路径或路径被停用时返回 null。</summary>
        internal PsdVectorShape BuildVectorShape(float documentWidth, float documentHeight)
        {
            return vectorMaskData == null ? null : PsdVectorShape.Parse(vectorMaskData, documentWidth, documentHeight);
        }

        private void ReadFillContent(byte[] data, string kind)
        {
            if (kind == "SoCo")
            {
                Color color;
                if (Layer.TryReadColor(data, "Clr ", 0, out color))
                {
                    ShapeFillColor = color;
                    HasShapeFillColor = true;
                }
            }
            else if (kind == "GdFl")
            {
                UnsupportedShapeFill = "渐变填充";
            }
            else if (kind == "PtFl")
            {
                UnsupportedShapeFill = "图案填充";
            }
        }

        private void ReadEffects(byte[] data)
        {
            if (data == null)
            {
                return;
            }

            int master = Layer.FindAscii(data, "masterFXSwitch", 0, data.Length);
            if (master >= 0 && master + 18 < data.Length && data[master + 18] == 0)
            {
                return;
            }

            bool enabled;
            int start;
            if ((Layer.TryReadEffectEnabled(data, "SoFi", out enabled, out start, true) ||
                 Layer.TryReadEffectEnabled(data, "solidFillMulti", out enabled, out start, true)) && enabled)
            {
                Color overlay;
                if (Layer.TryReadColor(data, "Clr ", start, out overlay))
                {
                    double opacity;
                    ColorOverlayEnabled = true;
                    ColorOverlayColor = overlay;
                    ColorOverlayOpacity = Layer.TryReadUnitValue(data, "Opct", start, out opacity)
                        ? Mathf.Clamp01((float)opacity / 100f)
                        : 1f;

                    int blendMode = Layer.FindAscii(data, "Md  ", start, Math.Min(data.Length, start + 400));
                    if (blendMode >= 0 && Layer.FindAscii(data, "Nrml", blendMode, Math.Min(data.Length, blendMode + 64)) < 0)
                    {
                        AddUnrendered("颜色叠加（非正常混合模式，按正常模式导出）");
                    }
                }
            }

            ReadStroke(data);
            ReadShadow(data, "DrSh", false);
            ReadShadow(data, "dsdw", false);
            ReadShadow(data, "IrSh", true);
            ReadShadow(data, "innerShadowMulti", true);
            ReadInnerGlow(data);

            foreach (KeyValuePair<string, string> effect in UnrenderedEffectKeys)
            {
                if (Layer.TryReadEffectEnabled(data, effect.Key, out enabled, out start, true) && enabled)
                {
                    AddUnrendered(effect.Value);
                }
            }
        }

        private void ReadStroke(byte[] data)
        {
            bool enabled; int start;
            if (!Layer.TryReadEffectEnabled(data, "FrFX", out enabled, out start, true) &&
                !Layer.TryReadEffectEnabled(data, "frameFXMulti", out enabled, out start, true)) return;
            StrokeEnabled = true;
            double value;
            StrokeWidth = Layer.TryReadUnitValue(data, "Sz  ", start, out value) ? Mathf.Max(0f, (float)value) : 1f;
            StrokeOpacity = Layer.TryReadUnitValue(data, "Opct", start, out value) ? Mathf.Clamp01((float)value / 100f) : 1f;
            Color color;
            if (Layer.TryReadColor(data, "Clr ", start, out color)) StrokeColor = color;
        }

        private void ReadShadow(byte[] data, string key, bool inner)
        {
            bool enabled; int start;
            if (!Layer.TryReadEffectEnabled(data, key, out enabled, out start, true)) return;
            double value;
            float distance = Layer.TryReadUnitValue(data, "Dstn", start, out value) ? Mathf.Max(0f, (float)value) : 0f;
            float angle = Layer.TryReadUnitValue(data, "lagl", start, out value) ? (float)value : 120f;
            float blur = Layer.TryReadUnitValue(data, "blur", start, out value) ? Mathf.Max(0f, (float)value) : 0f;
            float opacity = Layer.TryReadUnitValue(data, "Opct", start, out value) ? Mathf.Clamp01((float)value / 100f) : 1f;
            Color color;
            if (!Layer.TryReadColor(data, "Clr ", start, out color)) color = Color.black;
            color.a *= opacity;
            if (inner) { InnerShadowEnabled = true; InnerShadowDistance = distance; InnerShadowAngle = angle; InnerShadowBlur = blur; InnerShadowColor = color; }
            else { DropShadowEnabled = true; DropShadowDistance = distance; DropShadowAngle = angle; DropShadowBlur = blur; DropShadowColor = color; }
        }

        private void ReadInnerGlow(byte[] data)
        {
            bool enabled; int start;
            if (!Layer.TryReadEffectEnabled(data, "IrGl", out enabled, out start, true)) return;
            double value;
            InnerGlowEnabled = true;
            InnerGlowSize = Layer.TryReadUnitValue(data, "blur", start, out value) ? Mathf.Max(0f, (float)value) : 0f;
            if (Layer.TryReadUnitValue(data, "Opct", start, out value))
            {
                Color innerGlowColor = InnerGlowColor;
                innerGlowColor.a = Mathf.Clamp01((float)value / 100f);
                InnerGlowColor = innerGlowColor;
            }
            Color color;
            if (Layer.TryReadColor(data, "Clr ", start, out color)) { color.a = InnerGlowColor.a; InnerGlowColor = color; }
        }

        private void AddUnrendered(string name)
        {
            if (!unrenderedEffects.Contains(name))
            {
                unrenderedEffects.Add(name);
            }
        }
    }

    /// <summary>
    /// Photoshop 矢量路径（vmsk/vsms 路径记录），坐标已换算为文档像素。
    /// 支持多子路径的合并/减去/相交/排除组合，以及蒙版反相与停用标志。
    /// </summary>
    internal sealed class PsdVectorShape
    {
        private const int RecordSize = 26;
        private const int SuperSample = 4;
        private const float FixedPointScale = 1f / (1 << 24);

        private PsdVectorShape()
        {
        }

        internal bool Inverted { get; private set; }

        internal List<PsdVectorSubpath> Subpaths { get; } = new List<PsdVectorSubpath>();

        /// <summary>解析路径数据；数据不完整、没有锚点或蒙版被停用时返回 null。</summary>
        internal static PsdVectorShape Parse(byte[] data, float documentWidth, float documentHeight)
        {
            if (data == null || data.Length < 8 || documentWidth <= 0f || documentHeight <= 0f)
            {
                return null;
            }

            uint flags = ReadUInt32(data, 4);
            if ((flags & 0x4) != 0)
            {
                return null;
            }

            var shape = new PsdVectorShape { Inverted = (flags & 0x1) != 0 };
            PsdVectorSubpath current = null;
            for (int offset = 8; offset + RecordSize <= data.Length; offset += RecordSize)
            {
                int selector = ReadUInt16(data, offset);
                switch (selector)
                {
                    case 0:
                    case 3:
                        current = new PsdVectorSubpath(selector == 0, ReadInt16(data, offset + 4));
                        shape.Subpaths.Add(current);
                        break;
                    case 1:
                    case 2:
                    case 4:
                    case 5:
                        if (current == null)
                        {
                            current = new PsdVectorSubpath(selector <= 2, 1);
                            shape.Subpaths.Add(current);
                        }

                        current.Knots.Add(new PsdBezierKnot(
                            ReadPoint(data, offset + 2, documentWidth, documentHeight),
                            ReadPoint(data, offset + 10, documentWidth, documentHeight),
                            ReadPoint(data, offset + 18, documentWidth, documentHeight)));
                        break;
                }
            }

            shape.Subpaths.RemoveAll(subpath => subpath.Knots.Count < 2);
            return shape.Subpaths.Count == 0 ? null : shape;
        }

        /// <summary>
        /// 在图层矩形范围内栅格化出 0..1 的覆盖率（4x4 超采样抗锯齿）。
        /// 结果按图层像素行排列，第 0 行对应文档坐标 rect.y（顶边）。
        /// </summary>
        internal float[] Rasterize(Rect rect)
        {
            int width = Mathf.Max(0, (int)rect.width);
            int height = Mathf.Max(0, (int)rect.height);
            var result = new float[width * height];
            if (width == 0 || height == 0)
            {
                return result;
            }

            foreach (PsdVectorSubpath subpath in Subpaths)
            {
                float[] coverage = RasterizePolygon(subpath.Flatten(), rect, width, height);
                Combine(result, coverage, subpath.Operation);
            }

            if (Inverted)
            {
                for (int index = 0; index < result.Length; index++)
                {
                    result[index] = 1f - result[index];
                }
            }

            return result;
        }

        private static void Combine(float[] target, float[] source, int operation)
        {
            for (int index = 0; index < target.Length; index++)
            {
                float a = target[index];
                float b = source[index];
                switch (operation)
                {
                    case 2:
                        target[index] = a * (1f - b);
                        break;
                    case 3:
                        target[index] = a * b;
                        break;
                    case 0:
                        target[index] = Mathf.Clamp01(a + b - 2f * a * b);
                        break;
                    default:
                        target[index] = Mathf.Clamp01(a + b - a * b);
                        break;
                }
            }
        }

        // 扫描线 + 非零环绕规则：每个像素 4 条子扫描线 × 4 个子采样列。
        private static float[] RasterizePolygon(List<Vector2> points, Rect rect, int width, int height)
        {
            var coverage = new float[width * height];
            if (points.Count < 3)
            {
                return coverage;
            }

            const float sampleWeight = 1f / (SuperSample * SuperSample);
            var crossings = new List<KeyValuePair<float, int>>();
            for (int subRow = 0; subRow < height * SuperSample; subRow++)
            {
                float y = rect.y + (subRow + 0.5f) / SuperSample;
                crossings.Clear();
                for (int index = 0; index < points.Count; index++)
                {
                    Vector2 a = points[index];
                    Vector2 b = points[(index + 1) % points.Count];
                    int direction;
                    if (a.y <= y && b.y > y)
                    {
                        direction = 1;
                    }
                    else if (b.y <= y && a.y > y)
                    {
                        direction = -1;
                    }
                    else
                    {
                        continue;
                    }

                    float x = a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y);
                    crossings.Add(new KeyValuePair<float, int>(x, direction));
                }

                if (crossings.Count < 2)
                {
                    continue;
                }

                crossings.Sort((left, right) => left.Key.CompareTo(right.Key));
                int row = subRow / SuperSample;
                int winding = 0;
                for (int index = 0; index < crossings.Count - 1; index++)
                {
                    winding += crossings[index].Value;
                    if (winding == 0)
                    {
                        continue;
                    }

                    float spanStart = (crossings[index].Key - rect.x) * SuperSample - 0.5f;
                    float spanEnd = (crossings[index + 1].Key - rect.x) * SuperSample - 0.5f;
                    int first = Mathf.Max(0, Mathf.CeilToInt(spanStart));
                    int last = Mathf.Min(width * SuperSample - 1, Mathf.CeilToInt(spanEnd) - 1);
                    for (int subColumn = first; subColumn <= last; subColumn++)
                    {
                        coverage[row * width + subColumn / SuperSample] += sampleWeight;
                    }
                }
            }

            for (int index = 0; index < coverage.Length; index++)
            {
                coverage[index] = Mathf.Min(1f, coverage[index]);
            }

            return coverage;
        }

        private static Vector2 ReadPoint(byte[] data, int offset, float documentWidth, float documentHeight)
        {
            float y = ReadInt32(data, offset) * FixedPointScale * documentHeight;
            float x = ReadInt32(data, offset + 4) * FixedPointScale * documentWidth;
            return new Vector2(x, y);
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)ReadInt32(data, offset);
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static int ReadInt16(byte[] data, int offset)
        {
            return (short)ReadUInt16(data, offset);
        }
    }

    /// <summary>一个子路径：组合方式（1 合并、2 减去、3 相交、0 排除）与贝塞尔锚点。</summary>
    internal sealed class PsdVectorSubpath
    {
        internal PsdVectorSubpath(bool closed, int operation)
        {
            Closed = closed;
            Operation = operation;
        }

        internal bool Closed { get; }

        internal int Operation { get; }

        internal List<PsdBezierKnot> Knots { get; } = new List<PsdBezierKnot>();

        /// <summary>把贝塞尔段展平成折线（首尾相连，开放路径也按闭合填充，与 Photoshop 一致）。</summary>
        internal List<Vector2> Flatten()
        {
            var points = new List<Vector2>();
            for (int index = 0; index < Knots.Count; index++)
            {
                PsdBezierKnot from = Knots[index];
                PsdBezierKnot to = Knots[(index + 1) % Knots.Count];
                Vector2 p0 = from.Anchor;
                Vector2 p1 = from.Leaving;
                Vector2 p2 = to.Preceding;
                Vector2 p3 = to.Anchor;
                float length = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
                int steps = Mathf.Clamp(Mathf.CeilToInt(length), 1, 64);
                points.Add(p0);
                for (int step = 1; step < steps; step++)
                {
                    float t = step / (float)steps;
                    float u = 1f - t;
                    points.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
                }
            }

            return points;
        }
    }

    /// <summary>贝塞尔锚点：前控制点、锚点、后控制点（文档像素坐标）。</summary>
    internal readonly struct PsdBezierKnot
    {
        internal PsdBezierKnot(Vector2 preceding, Vector2 anchor, Vector2 leaving)
        {
            Preceding = preceding;
            Anchor = anchor;
            Leaving = leaving;
        }

        internal Vector2 Preceding { get; }

        internal Vector2 Anchor { get; }

        internal Vector2 Leaving { get; }
    }
}
