namespace PhotoshopFile
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// Contains the data representation of a PSD layer
    /// </summary>
    public class Layer
    {
        /// <summary>
        /// The bit flag representing transparency being protected.
        /// </summary>
        private static readonly int ProtectTransparencyBit = BitVector32.CreateMask();

        /// <summary>
        /// The bit flag representing the layer being visible.
        /// </summary>
        private static readonly int VisibleBit = BitVector32.CreateMask(ProtectTransparencyBit);

        /// <summary>
        /// The bit flag representing the layer being obsolete.  ???
        /// </summary>
        private static readonly int ObsoleteBit = BitVector32.CreateMask(VisibleBit);

        /// <summary>
        /// The bit flag representing the layer being version 5+.  ???
        /// </summary>
        private static readonly int Version5OrLaterBit = BitVector32.CreateMask(ObsoleteBit);

        /// <summary>
        /// The bit flag representing the layer's pixel data being irrelevant (a group layer, for example).
        /// </summary>
        private static readonly int PixelDataIrrelevantBit = BitVector32.CreateMask(Version5OrLaterBit);

        /// <summary>
        /// The set of flags associated with this layer.
        /// </summary>
        private BitVector32 flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="Layer"/> class using the provided reader containing the PSD file data.
        /// </summary>
        /// <param name="reader">The reader containing the PSD file data.</param>
        /// <param name="psdFile">The PSD file to set as the parent.</param>
        public Layer(BinaryReverseReader reader, PsdFile psdFile)
        {
            Children = new List<Layer>();
            PsdFile = psdFile;
            SectionType = -1;

            // read the rect
            Rect rect = new Rect();
            rect.y = reader.ReadInt32();
            rect.x = reader.ReadInt32();
            rect.height = reader.ReadInt32() - rect.y;
            rect.width = reader.ReadInt32() - rect.x;
            Rect = rect;

            // read the channels
            int channelCount = reader.ReadUInt16();
            Channels = new List<Channel>();
            SortedChannels = new SortedList<short, Channel>();
            for (int index = 0; index < channelCount; ++index)
            {
                Channel channel = new Channel(reader, this);
                Channels.Add(channel);
                SortedChannels.Add(channel.ID, channel);
            }

            // read the header and verify it
            if (new string(reader.ReadChars(4)) != "8BIM")
            {
                throw new IOException("Layer Channelheader error!");
            }

            // read the blend mode key (unused) (defaults to "norm")
            reader.ReadChars(4);

            // read the opacity
            Opacity = reader.ReadByte();

            // read the clipping (unused) (< 0 = base, > 0 = non base)
            reader.ReadByte();

            // read all of the flags (protectTrans, visible, obsolete, ver5orLater, pixelDataIrrelevant)
            flags = new BitVector32(reader.ReadByte());

            // skip a padding byte
            reader.ReadByte();

            uint num3 = reader.ReadUInt32();
            long position1 = reader.BaseStream.Position;
            MaskData = new Mask(reader, this);
            BlendingRangesData = new BlendingRanges(reader);
            long position2 = reader.BaseStream.Position;

            // read the name
            Name = reader.ReadPascalString();

            // read the adjustment info
            int count = (int)((reader.BaseStream.Position - position2) % 4L);
            reader.ReadBytes(count);
            AdjustmentInfo = new List<AdjustmentLayerInfo>();
            long num4 = position1 + num3;
            while (reader.BaseStream.Position < num4)
            {
                try
                {
                    AdjustmentInfo.Add(new AdjustmentLayerInfo(reader, this));
                }
                catch
                {
                    reader.BaseStream.Position = num4;
                }
            }

            foreach (AdjustmentLayerInfo adjustmentLayerInfo in AdjustmentInfo)
            {
                if (adjustmentLayerInfo.Key == "TySh")
                {
                    ReadTextLayer(adjustmentLayerInfo.DataReader);
                }
                else if (adjustmentLayerInfo.Key == "lfx2" || adjustmentLayerInfo.Key == "lrFX")
                {
                    ReadLayerEffects(adjustmentLayerInfo.RawData);
                }
                else if (adjustmentLayerInfo.Key == "luni")
                {
                    // read the unicode name
                    BinaryReverseReader dataReader = adjustmentLayerInfo.DataReader;
                    dataReader.ReadBytes(3);
                    dataReader.ReadByte();
                    Name = dataReader.ReadString().TrimEnd(new char[1]);
                }
                else if (adjustmentLayerInfo.Key == "lsct")
                {
                    // read the section divider type:
                    // 0 = other, 1 = open folder, 2 = closed folder, 3 = bounding
                    BinaryReverseReader dataReader = adjustmentLayerInfo.DataReader;
                    SectionType = dataReader.ReadInt32();
                }
            }

            reader.BaseStream.Position = num4;
        }

        #region Properties

        #region Text Layer Properties

        /// <summary>
        /// Gets a value indicating whether this layer is a text layer.
        /// </summary>
        public bool IsTextLayer { get; private set; }

        /// <summary>
        /// Gets the actual text string, if this is a text layer.
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Gets the point size of the font, if this is a text layer.
        /// </summary>
        public float FontSize { get; private set; }

        /// <summary>
        /// Gets the name of the font used, if this is a text layer.
        /// </summary>
        public string FontName { get; private set; }

        /// <summary>
        /// Gets the justification of the text, if this is a text layer.
        /// </summary>
        public TextJustification Justification { get; private set; }

        /// <summary>
        /// Gets the Fill Color of the text, if this is a text layer.
        /// </summary>
        public Color FillColor { get; private set; }

        /// <summary>
        /// Gets the style of warp done on the text, if it is a text layer.
        /// Can be warpNone, warpTwist, etc.
        /// </summary>
        public string WarpStyle { get; private set; }

        /// <summary>
        /// Gets the section divider type from the 'lsct' tag.
        /// 0 = other, 1 = open folder, 2 = closed folder, 3 = bounding (group end).
        /// -1 means no 'lsct' tag was found.
        /// </summary>
        public int SectionType { get; private set; }

        /// <summary>Gets normalized Photoshop text effects.</summary>
        public PsdTextStyle TextStyle { get; private set; }

        #endregion

        /// <summary>
        /// Gets a list of the children <see cref="Layer"/>s that belong to this Layer.
        /// </summary>
        public List<Layer> Children { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this layer has Effects/Styles or not.
        /// </summary>
        public bool HasEffects { get; set; }

        /// <summary>
        /// Gets the rectangle containing the contents of the layer.
        /// </summary>
        public Rect Rect { get; private set; }

        /// <summary>
        /// Gets a list of the Channel information.
        /// </summary>
        public List<Channel> Channels { get; private set; }

        /// <summary>
        /// Gets a sorted list of Channel information.
        /// </summary>
        public SortedList<short, Channel> SortedChannels { get; private set; }

        /// <summary>
        /// Gets the opacity of this layer.  0 = transparent and 255 = opaque/solid.
        /// </summary>
        public byte Opacity { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this layer is visible or not.
        /// </summary>
        public bool Visible
        {
            get
            {
                return !flags[VisibleBit];
            }
        }

        /// <summary>
        /// Gets a value indicating whether this layer's pixel data is irrelevant.  This is often the case with group layers.
        /// </summary>
        public bool IsPixelDataIrrelevant
        {
            get
            {
                return flags[PixelDataIrrelevantBit];
            }
        }

        /// <summary>
        /// Gets a value indicating whether this layer starts a group based on the 'lsct' tag.
        /// Returns true for OPEN_FOLDER (1) or CLOSED_FOLDER (2).
        /// </summary>
        public bool IsGroupStart
        {
            get
            {
                return SectionType == 1 || SectionType == 2;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this layer ends a group based on the 'lsct' tag.
        /// Returns true for BOUNDING (3).
        /// </summary>
        public bool IsGroupEnd
        {
            get
            {
                return SectionType == 3;
            }
        }

        /// <summary>
        /// Gets or sets the name of the layer.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the mask data for this layer.
        /// </summary>
        public Mask MaskData { get; private set; }

        /// <summary>
        /// Gets the <see cref="PsdFile"/> that this <see cref="Layer"/> belongs to.
        /// </summary>
        internal PsdFile PsdFile { get; private set; }

        /// <summary>
        /// Gets or sets the blending ranges data for this layer.
        /// </summary>
        private BlendingRanges BlendingRangesData { get; set; }

        /// <summary>
        /// Gets or sets the list of adjustment information for this layer.
        /// </summary>
        private List<AdjustmentLayerInfo> AdjustmentInfo { get; set; }

        #endregion

        /// <summary>
        /// Reads the text information for the layer.
        /// </summary>
        /// <param name="dataReader">The reader to use to read the text data.</param>
        private void ReadTextLayer(BinaryReverseReader dataReader)
        {
            IsTextLayer = true;
            if (TextStyle == null)
            {
                TextStyle = PsdTextStyle.CreateDefault(0f);
            }

            // read the text layer's text string
            // PSD engine-data descriptors store the value after /Text as:
            //   space '(' BOM UTF16BE... ')'
            // The old code used ReadString() which reads null-terminated UTF-16BE,
            // but the actual delimiter is the PostScript ')' character.
            if (TrySeekFromStart(dataReader, "/Text"))
            {
                dataReader.ReadBytes(4); // skip space, '(', BOM
                Text = NormalizeTextLineEndings(ReadPostScriptUtf16String(dataReader));
            }
            else
            {
                Text = string.Empty;
            }

            // read the text justification
            Justification = TextJustification.Left;
            if (TrySeekFromStart(dataReader, "/Justification"))
            {
                int justification;
                if (!TryReadAsciiInt(dataReader, out justification))
                {
                    justification = 0;
                }

                if (justification == 1)
                {
                    Justification = TextJustification.Right;
                }
                else if (justification == 2)
                {
                    Justification = TextJustification.Center;
                }
            }

            // read the font size
            FontSize = 0f;
            if (TrySeekFromStart(dataReader, "/FontSize"))
            {
                float fontSize;
                if (dataReader.TryReadAsciiFloat(out fontSize))
                {
                    FontSize = fontSize;
                }
            }
            TextStyle.LineHeight = FontSize > 0f ? FontSize * 1.2f : 0f;

            // read the font fill color
            FillColor = Color.white;
            if (TrySeekFromStart(dataReader, "/FillColor") && dataReader.TrySeek("/Values"))
            {
                float alpha;
                float red;
                float green;
                float blue;
                if (dataReader.TryReadAsciiFloat(out alpha) &&
                    dataReader.TryReadAsciiFloat(out red) &&
                    dataReader.TryReadAsciiFloat(out green) &&
                    dataReader.TryReadAsciiFloat(out blue))
                {
                    FillColor = new Color(red, green, blue, alpha);
                }
            }

            // read the font name
            FontName = string.Empty;
            if (TrySeekFromStart(dataReader, "/FontSet") && dataReader.TrySeek("/Name"))
            {
                dataReader.ReadBytes(4);
                FontName = ReadPostScriptUtf16String(dataReader);
            }

            // read the warp style
            WarpStyle = string.Empty;
            if (TrySeekFromStart(dataReader, "warpStyle") && dataReader.TrySeek("warpStyle"))
            {
                dataReader.ReadBytes(3);
                int num13 = dataReader.ReadByte();
                for (; num13 > 0; --num13)
                {
                    WarpStyle += dataReader.ReadChar();
                }
            }
        }

        private static bool TrySeekFromStart(BinaryReverseReader dataReader, string search)
        {
            dataReader.BaseStream.Position = 0;
            return dataReader.TrySeek(search);
        }

        /// <summary>
        /// Reads a PostScript string literal encoded as UTF-16BE.
        /// The stream must be positioned right after the opening '(' and BOM.
        /// Reads bytes until the closing ')' and decodes them as UTF-16BE.
        /// </summary>
        private static string ReadPostScriptUtf16String(BinaryReverseReader reader)
        {
            System.Collections.Generic.List<byte> bytes = new System.Collections.Generic.List<byte>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte b = reader.ReadByte();
                if (b == 0x29) // ')' — end of PostScript string
                {
                    break;
                }

                if (b == 0x5C && reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    // '\' escape — include both the backslash and the next byte
                    bytes.Add(b);
                    bytes.Add(reader.ReadByte());
                }
                else
                {
                    bytes.Add(b);
                }
            }

            // Decode as UTF-16BE
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i + 1 < bytes.Count; i += 2)
            {
                char c = (char)((bytes[i] << 8) | bytes[i + 1]);
                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts Photoshop's carriage-return text separators to Unity's line-feed separator.
        /// TMP treats a standalone carriage return as a horizontal cursor reset, which makes
        /// multiline PSD text render on top of itself instead of advancing to the next line.
        /// </summary>
        private static string NormalizeTextLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\u2028", "\n")
                .Replace("\u2029", "\n");
        }

        private static bool TryReadAsciiInt(BinaryReverseReader dataReader, out int value)
        {
            float number;
            value = 0;
            if (!dataReader.TryReadAsciiFloat(out number))
            {
                return false;
            }

            value = Mathf.RoundToInt(number);
            return true;
        }

        /// <summary>
        /// Reads the stable, numeric part of Photoshop's common layer-effect
        /// descriptors. Unknown descriptor variants are intentionally ignored.
        /// </summary>
        private void ReadLayerEffects(byte[] data)
        {
            if (TextStyle == null)
            {
                TextStyle = PsdTextStyle.CreateDefault(FontSize);
            }

            bool enabled;
            double value;
            if (TryReadBool(data, "FrFX", out enabled))
            {
                TextStyle.StrokeEnabled = enabled;
                if (TryReadUnitValue(data, "Sz  ", out value))
                {
                    TextStyle.StrokeWidth = Mathf.Max(0f, (float)value);
                }

                Color effectColor;
                if (TryReadColor(data, "Clr ", out effectColor))
                {
                    TextStyle.StrokeColor = effectColor;
                }
            }

            if (TryReadBool(data, "dsdw", out enabled))
            {
                TextStyle.ShadowEnabled = enabled;
                if (TryReadUnitValue(data, "Dstn", out value))
                {
                    TextStyle.ShadowDistance = Mathf.Max(0f, (float)value);
                }

                if (TryReadUnitValue(data, "lagl", out value))
                {
                    TextStyle.ShadowAngle = (float)value;
                }

                if (TryReadUnitValue(data, "blur", out value))
                {
                    TextStyle.ShadowBlur = Mathf.Max(0f, (float)value);
                }

                Color effectColor;
                if (TryReadColor(data, "Clr ", out effectColor))
                {
                    TextStyle.ShadowColor = effectColor;
                }

                if (TryReadUnitValue(data, "Opct", out value))
                {
                    Color shadowColor = TextStyle.ShadowColor;
                    shadowColor.a = Mathf.Clamp01((float)value / 100f);
                    TextStyle.ShadowColor = shadowColor;
                }
            }
        }

        private static bool TryReadBool(byte[] data, string key, out bool value)
        {
            value = false;
            int keyIndex = FindAscii(data, key);
            if (keyIndex < 0)
            {
                return false;
            }

            int boolIndex = FindAscii(data, "bool", keyIndex + key.Length, Math.Min(data.Length, keyIndex + 32));
            if (boolIndex < 0 || boolIndex + 4 >= data.Length)
            {
                return false;
            }

            value = data[boolIndex + 4] != 0;
            return true;
        }

        private static bool TryReadUnitValue(byte[] data, string key, out double value)
        {
            value = 0d;
            int keyIndex = FindAscii(data, key);
            if (keyIndex < 0)
            {
                return false;
            }

            int unitIndex = FindAscii(data, "UntF", keyIndex + key.Length, Math.Min(data.Length, keyIndex + 48));
            if (unitIndex < 0 || unitIndex + 16 > data.Length)
            {
                return false;
            }

            long bits = 0L;
            for (int index = unitIndex + 8; index < unitIndex + 16; ++index)
            {
                bits = (bits << 8) | data[index];
            }

            value = BitConverter.Int64BitsToDouble(bits);
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool TryReadColor(byte[] data, string key, out Color color)
        {
            color = Color.black;
            int keyIndex = FindAscii(data, key);
            if (keyIndex < 0)
            {
                return false;
            }

            double red;
            double green;
            double blue;
            if (!TryReadDescriptorDouble(data, "Rd  ", keyIndex, out red) ||
                !TryReadDescriptorDouble(data, "Grn ", keyIndex, out green) ||
                !TryReadDescriptorDouble(data, "Bl  ", keyIndex, out blue))
            {
                return false;
            }

            color = new Color(
                Mathf.Clamp01((float)red / 255f),
                Mathf.Clamp01((float)green / 255f),
                Mathf.Clamp01((float)blue / 255f),
                1f);
            return true;
        }

        private static bool TryReadDescriptorDouble(byte[] data, string key, int start, out double value)
        {
            value = 0d;
            int keyIndex = FindAscii(data, key, start, Math.Min(data.Length, start + 128));
            if (keyIndex < 0)
            {
                return false;
            }

            int typeIndex = FindAscii(data, "doub", keyIndex + key.Length, Math.Min(data.Length, keyIndex + 20));
            if (typeIndex < 0 || typeIndex + 12 > data.Length)
            {
                return false;
            }

            long bits = 0L;
            for (int index = typeIndex + 4; index < typeIndex + 12; ++index)
            {
                bits = (bits << 8) | data[index];
            }

            value = BitConverter.Int64BitsToDouble(bits);
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static int FindAscii(byte[] data, string value)
        {
            return FindAscii(data, value, 0, data != null ? data.Length : 0);
        }

        private static int FindAscii(byte[] data, string value, int start, int end)
        {
            if (data == null || string.IsNullOrEmpty(value))
            {
                return -1;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(value);
            int max = Math.Min(end, data.Length - bytes.Length + 1);
            for (int offset = Math.Max(0, start); offset < max; ++offset)
            {
                bool match = true;
                for (int index = 0; index < bytes.Length; ++index)
                {
                    if (data[offset + index] != bytes[index])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return offset;
                }
            }

            return -1;
        }
    }
}
