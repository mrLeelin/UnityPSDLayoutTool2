namespace PsdLayoutTool2
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// Computes three independent fingerprints. Keeping these concerns separate
    /// is what lets an incremental import update pixels/text without asking the
    /// hierarchy planner to reorganize an otherwise unchanged Prefab.
    /// </summary>
    public static class PsdHierarchyFingerprints
    {
        /// <summary>
        /// Hashes decoded PSD channel bytes together with their channel IDs. The
        /// ID and byte count delimiters ensure swapping channels or concatenating
        /// differently-shaped data cannot produce the same input sequence.
        /// </summary>
        public static string Asset(IEnumerable<KeyValuePair<short, byte[]>> channels)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (var sink = new CryptoStream(Stream.Null, algorithm, CryptoStreamMode.Write))
            {
                foreach (KeyValuePair<short, byte[]> channel in
                         (channels ?? Enumerable.Empty<KeyValuePair<short, byte[]>>()).OrderBy(value => value.Key))
                {
                    byte[] bytes = channel.Value ?? new byte[0];
                    int length = bytes.Length;
                    byte[] header =
                    {
                        (byte)(channel.Key >> 8),
                        (byte)channel.Key,
                        (byte)(length >> 24),
                        (byte)(length >> 16),
                        (byte)(length >> 8),
                        (byte)length
                    };
                    sink.Write(header, 0, header.Length);
                    sink.Write(bytes, 0, bytes.Length);
                }

                sink.FlushFinalBlock();
                return ToHex(algorithm.Hash);
            }
        }

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
                Append(value, Float(node.text.characterHorizontalScale));
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

            return ComputeSha256(value.ToString());
        }

        public static string Structure(PsdPrefabNodeModel node)
        {
            string value = (node.stableId ?? string.Empty) + "|" +
                           (node.parentStableId ?? string.Empty) + "|" +
                           node.siblingIndex.ToString(CultureInfo.InvariantCulture) + "|" + node.kind;
            return ComputeSha256(value);
        }

        public static string Geometry(PsdPrefabNodeModel node)
        {
            Rect bounds = node.bounds;
            string value = (node.stableId ?? string.Empty) + "|" + Float(bounds.x) + "|" + Float(bounds.y) + "|" +
                           Float(bounds.width) + "|" + Float(bounds.height);
            return ComputeSha256(value);
        }

        /// <summary>
        /// Produces one source fingerprint for a native PSD document. Nodes are
        /// sorted by their complete fingerprint tuple, so collection enumeration
        /// order cannot create false stale results while sibling order remains
        /// represented inside each node's structure fingerprint.
        /// </summary>
        public static string Document(PsdPrefabDocumentModel document)
        {
            if (document == null)
            {
                return string.Empty;
            }

            var value = new StringBuilder();
            Append(value, document.width.ToString(CultureInfo.InvariantCulture));
            Append(value, document.height.ToString(CultureInfo.InvariantCulture));
            Append(value, Float(document.resolution));
            IEnumerable<string> nodeValues = (document.nodes ?? new List<PsdPrefabNodeModel>())
                .Where(node => node != null)
                .Select(node => (node.stableId ?? string.Empty) + ":" + Structure(node) + ":" + Content(node) + ":" + Geometry(node))
                .OrderBy(node => node, System.StringComparer.Ordinal);
            foreach (string nodeValue in nodeValues)
            {
                Append(value, nodeValue);
            }

            return ComputeSha256(value.ToString());
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

        private static string ComputeSha256(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                return ToHex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var value = new StringBuilder((bytes == null ? 0 : bytes.Length) * 2);
            foreach (byte item in bytes ?? new byte[0])
            {
                value.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            }

            return value.ToString();
        }
    }
}
