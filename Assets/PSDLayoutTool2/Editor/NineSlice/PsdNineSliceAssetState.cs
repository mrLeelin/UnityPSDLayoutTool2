namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Persists Unity-owned nine-slice decisions inside a generated PNG's
    /// TextureImporter.userData. The format is line-based so other tools'
    /// userData remains intact and no sidecar JSON is required.
    /// </summary>
    public sealed class PsdNineSliceAssetState
    {
        private const string LayerIdentityPrefix = "psd-layout-layer-id:v1:";
        private const string NineSlicePrefix = "psd-layout-nine-slice:v2:";

        private PsdNineSliceAssetState(uint layerId, string sourceHash, string outputHash, PsdNineSliceBorder border)
        {
            LayerId = layerId;
            SourceHash = sourceHash;
            OutputHash = outputHash;
            Border = border;
        }

        public uint LayerId { get; private set; }
        public string SourceHash { get; private set; }
        public string OutputHash { get; private set; }
        public PsdNineSliceBorder Border { get; private set; }

        /// <summary>
        /// Writes the stable Photoshop layer identity during normal PSD export.
        /// </summary>
        public static string WriteLayerIdentity(string userData, uint layerId)
        {
            return ReplaceLine(userData, LayerIdentityPrefix, LayerIdentityPrefix + layerId.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Reads the stable Photoshop layer identity attached to a generated PNG.
        /// </summary>
        public static bool TryReadLayerIdentity(string userData, out uint layerId)
        {
            layerId = 0U;
            string value;
            if (!TryReadLinePayload(userData, LayerIdentityPrefix, out value))
            {
                return false;
            }

            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out layerId) && layerId != 0U;
        }

        /// <summary>
        /// Writes a confirmed border and the fingerprint of the uncropped source PNG.
        /// </summary>
        public static string Write(string userData, uint layerId, string sourceHash, string outputHash, PsdNineSliceBorder border)
        {
            if (layerId == 0U)
            {
                throw new ArgumentOutOfRangeException("layerId");
            }

            if (string.IsNullOrEmpty(sourceHash))
            {
                throw new ArgumentException("A source hash is required.", "sourceHash");
            }

            if (string.IsNullOrEmpty(outputHash))
            {
                throw new ArgumentException("An output hash is required.", "outputHash");
            }

            if (border == null)
            {
                throw new ArgumentNullException("border");
            }

            string payload = string.Join(
                "|",
                layerId.ToString(CultureInfo.InvariantCulture),
                sourceHash,
                outputHash,
                border.Left.ToString(CultureInfo.InvariantCulture),
                border.Top.ToString(CultureInfo.InvariantCulture),
                border.Right.ToString(CultureInfo.InvariantCulture),
                border.Bottom.ToString(CultureInfo.InvariantCulture));
            string withIdentity = WriteLayerIdentity(userData, layerId);
            return ReplaceLine(withIdentity, NineSlicePrefix, NineSlicePrefix + payload);
        }

        /// <summary>
        /// Reads a confirmed Unity-owned nine-slice decision.
        /// </summary>
        public static bool TryRead(string userData, out PsdNineSliceAssetState state)
        {
            state = null;
            string payload;
            if (!TryReadLinePayload(userData, NineSlicePrefix, out payload))
            {
                return false;
            }

            string[] fields = payload.Split('|');
            if (fields.Length != 7)
            {
                return false;
            }

            uint layerId;
            int left;
            int top;
            int right;
            int bottom;
            if (!uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out layerId) ||
                layerId == 0U ||
                string.IsNullOrEmpty(fields[1]) ||
                string.IsNullOrEmpty(fields[2]) ||
                !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out left) ||
                !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out top) ||
                !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out right) ||
                !int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out bottom))
            {
                return false;
            }

            state = new PsdNineSliceAssetState(
                layerId,
                fields[1],
                fields[2],
                new PsdNineSliceBorder(left, top, right, bottom));
            return true;
        }

        private static string ReplaceLine(string userData, string prefix, string replacement)
        {
            List<string> lines = new List<string>();
            if (!string.IsNullOrEmpty(userData))
            {
                string[] sourceLines = userData.Replace("\r\n", "\n").Split('\n');
                foreach (string line in sourceLines)
                {
                    if (!string.IsNullOrEmpty(line) && !line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        lines.Add(line);
                    }
                }
            }

            lines.Add(replacement);
            return string.Join("\n", lines.ToArray());
        }

        private static bool TryReadLinePayload(string userData, string prefix, out string payload)
        {
            payload = string.Empty;
            if (string.IsNullOrEmpty(userData))
            {
                return false;
            }

            string[] lines = userData.Replace("\r\n", "\n").Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    payload = line.Substring(prefix.Length);
                    return true;
                }
            }

            return false;
        }
    }
}
