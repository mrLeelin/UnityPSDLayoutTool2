namespace PhotoshopFile
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using UnityEngine;

    /// <summary>Normal, linear RGB gradient overlays, including independent transparency stops.</summary>
    internal sealed class PsdGradientOverlay
    {
        private readonly List<Stop> colors = new List<Stop>();
        private readonly List<Stop> transparency = new List<Stop>();
        internal float Opacity { get; private set; }
        internal float Angle { get; private set; }
        internal float Scale { get; private set; }
        internal bool Reverse { get; private set; }
        internal bool Align { get; private set; }
        internal Vector2 Offset { get; private set; }
        private float smoothness;

        private struct Stop
        {
            internal float Position;
            internal Color Value;
        }

        internal static PsdGradientOverlay Read(byte[] data, string key, out string issue)
        {
            issue = null;
            int start = Layer.FindAscii(data, key, 0, data.Length);
            if (start < 0) return null;
            try
            {
                var reader = new DescriptorReader(data, start + key.Length);
                object value = reader.Value(0);
                var effects = value as List<object> ?? new List<object> { value };
                PsdGradientOverlay result = null;
                foreach (object effect in effects)
                {
                    var descriptor = effect as Dictionary<string, object>;
                    if (descriptor == null || !Boolean(descriptor, "enab", false)) continue;
                    if (result != null) throw new InvalidDataException("多个渐变叠加");
                    result = Parse(descriptor);
                }
                return result;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is EndOfStreamException ||
                                              exception is OverflowException)
            {
                issue = "渐变叠加（" + exception.Message + "，未导出）";
                return null;
            }
        }

        private static PsdGradientOverlay Parse(Dictionary<string, object> effect)
        {
            if (Text(effect, "Md  ") != "Nrml" || Text(effect, "Type") != "Lnr ")
                throw new InvalidDataException("仅支持正常混合的线性渐变");
            var gradient = Object(effect, "Grad");
            if (Text(gradient, "GrdF") != "CstS") throw new InvalidDataException("不支持噪点渐变");
            var result = new PsdGradientOverlay
            {
                Opacity = Mathf.Clamp01(Number(effect, "Opct", 100) / 100f),
                Angle = Number(effect, "Angl", 0),
                Scale = Mathf.Max(0.01f, Number(effect, "Scl ", 100) / 100f),
                Reverse = Boolean(effect, "Rvrs", false),
                Align = Boolean(effect, "Algn", true),
                smoothness = Mathf.Clamp01(Number(gradient, "Intr", 4096) / 4096f),
            };
            if (effect.ContainsKey("Ofst"))
            {
                var offset = Object(effect, "Ofst");
                result.Offset = new Vector2(Number(offset, "Hrzn", 0), Number(offset, "Vrtc", 0)) / 100f;
            }
            result.ReadStops(gradient, "Clrs", false);
            result.ReadStops(gradient, "Trns", true);
            if (result.colors.Count == 0) throw new InvalidDataException("缺少颜色色标");
            return result;
        }

        private void ReadStops(Dictionary<string, object> gradient, string key, bool alpha)
        {
            if (!gradient.TryGetValue(key, out object value) || !(value is List<object> list)) return;
            var stops = alpha ? transparency : colors;
            foreach (object item in list)
            {
                var stop = item as Dictionary<string, object>;
                if (stop == null) throw new InvalidDataException("无效色标");
                // Non-default midpoints require Photoshop's interpolation curve.
                if (Number(stop, "Mdpn", 50) != 50) throw new InvalidDataException("不支持自定义色标中点");
                Color color;
                if (alpha) color = new Color(0, 0, 0, Mathf.Clamp01(Number(stop, "Opct", 100) / 100f));
                else
                {
                    if (Text(stop, "Type") != "UsrS") throw new InvalidDataException("不支持前景/背景色标");
                    var rgb = Object(stop, "Clr ");
                    if (!rgb.ContainsKey("Rd  ") || !rgb.ContainsKey("Grn ") || !rgb.ContainsKey("Bl  "))
                        throw new InvalidDataException("仅支持 RGB 色标");
                    color = new Color(Number(rgb, "Rd  ", 0) / 255f, Number(rgb, "Grn ", 0) / 255f,
                        Number(rgb, "Bl  ", 0) / 255f, 1);
                }
                stops.Add(new Stop { Position = Number(stop, "Lctn", 0) / 4096f, Value = color });
            }
            stops.Sort((a, b) => a.Position.CompareTo(b.Position));
        }

        internal Color Sample(float x, float y, Rect layerBounds, float documentWidth, float documentHeight)
        {
            Rect bounds = Align ? layerBounds : new Rect(0, 0, documentWidth, documentHeight);
            float angle = Angle * Mathf.Deg2Rad;
            float dx = Mathf.Cos(angle), dy = -Mathf.Sin(angle); // PSD y grows downwards.
            float length = Mathf.Max(1f, (Mathf.Abs(dx) * bounds.width + Mathf.Abs(dy) * bounds.height) * Scale);
            Vector2 center = bounds.center + Vector2.Scale(Offset, bounds.size);
            float t = Mathf.Clamp01(0.5f + ((x - center.x) * dx + (y - center.y) * dy) / length);
            if (Reverse) t = 1f - t;
            Color result = Interpolate(colors, t);
            result.a = (transparency.Count == 0 ? 1f : Interpolate(transparency, t).a) * Opacity;
            return result;
        }

        private Color Interpolate(List<Stop> stops, float t)
        {
            if (t <= stops[0].Position) return stops[0].Value;
            for (int i = 1; i < stops.Count; i++)
            {
                if (t <= stops[i].Position)
                {
                    float fraction = Mathf.InverseLerp(stops[i - 1].Position, stops[i].Position, t);
                    // Photoshop's classic gradient uses a cubic curve at full smoothness.
                    float cubic = fraction * (0.5f + fraction * (1.5f - fraction));
                    return Color.Lerp(stops[i - 1].Value, stops[i].Value,
                        Mathf.Lerp(fraction, cubic, smoothness));
                }
            }
            return stops[stops.Count - 1].Value;
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> source, string key)
        {
            if (source.TryGetValue(key, out object value) && value is Dictionary<string, object> descriptor) return descriptor;
            throw new InvalidDataException("缺少描述符 " + key);
        }
        private static string Text(Dictionary<string, object> source, string key)
            => source.TryGetValue(key, out object value) ? value as string : null;
        private static float Number(Dictionary<string, object> source, string key, float fallback)
            => source.TryGetValue(key, out object value) && value is double number ? (float)number : fallback;
        private static bool Boolean(Dictionary<string, object> source, string key, bool fallback)
            => source.TryGetValue(key, out object value) && value is bool boolean ? boolean : fallback;

        // Read each typed value to its exact end. Searching for Clr/Opct bytes can otherwise
        // accidentally consume another stop or a different effect's opacity.
        private sealed class DescriptorReader
        {
            private readonly byte[] data;
            private int position;
            internal DescriptorReader(byte[] data, int position) { this.data = data; this.position = position; }
            private void Require(int count)
            {
                if (count < 0 || position > data.Length - count) throw new EndOfStreamException("描述符数据被截断");
            }
            private int Int32()
            {
                Require(4);
                int value = data[position] << 24 | data[position + 1] << 16 | data[position + 2] << 8 | data[position + 3];
                position += 4;
                return value;
            }
            private string Ascii(int length)
            {
                Require(length);
                string value = Encoding.ASCII.GetString(data, position, length);
                position += length;
                return value;
            }
            private string Id() { int length = Int32(); return Ascii(length == 0 ? 4 : length); }
            private void Unicode() { int length = checked(Int32() * 2); Require(length); position += length; }
            private double Double()
            {
                Require(8);
                long bits = 0;
                for (int i = 0; i < 8; i++) bits = bits << 8 | data[position++];
                double value = BitConverter.Int64BitsToDouble(bits);
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("无效数值");
                return value;
            }
            internal object Value(int depth)
            {
                if (depth > 16) throw new InvalidDataException("描述符嵌套过深");
                string type = Ascii(4);
                switch (type)
                {
                    case "Objc":
                        Unicode(); Id();
                        int count = Int32();
                        if (count < 0 || count > (data.Length - position) / 8) throw new InvalidDataException("无效描述符长度");
                        var result = new Dictionary<string, object>();
                        for (int i = 0; i < count; i++) { string key = Id(); result[key] = Value(depth + 1); }
                        return result;
                    case "VlLs":
                        int length = Int32();
                        if (length < 0 || length > (data.Length - position) / 4) throw new InvalidDataException("无效列表长度");
                        var list = new List<object>();
                        for (int i = 0; i < length; i++) list.Add(Value(depth + 1));
                        return list;
                    case "bool": Require(1); return data[position++] != 0;
                    case "doub": return Double();
                    case "long": return (double)Int32();
                    case "UntF": Ascii(4); return Double();
                    case "enum": Id(); return Id();
                    case "TEXT": Unicode(); return null;
                    default: throw new InvalidDataException("不支持描述符类型 " + type);
                }
            }
        }
    }
}
