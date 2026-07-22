namespace PsdLayoutTool2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// A user-selected PSD nine-slice decision. It is keyed by Photoshop's
    /// stable layer identifier instead of the layer name so regular artist
    /// renames keep their manual settings during incremental imports.
    /// </summary>
    public sealed class PsdNineSliceOverride
    {
        public PsdNineSliceOverride(uint layerId, bool enabled, PsdNineSliceBorder border)
        {
            if (layerId == 0U)
            {
                throw new ArgumentOutOfRangeException("layerId");
            }

            if (enabled && border == null)
            {
                throw new ArgumentNullException("border");
            }

            LayerId = layerId;
            Enabled = enabled;
            Border = border;
        }

        public uint LayerId { get; private set; }
        public bool Enabled { get; private set; }
        public PsdNineSliceBorder Border { get; private set; }
    }

    /// <summary>
    /// Stores manual PSD nine-slice overrides in the PSD asset importer's
    /// userData. The line-based payload deliberately preserves userData owned
    /// by Unity or other editor tools and avoids a sidecar mapping file.
    /// </summary>
    public static class PsdNineSliceOverrideStore
    {
        private const string Prefix = "psd-layout-nine-slice-overrides:v1:";

        public static Dictionary<uint, PsdNineSliceOverride> ReadAll(string userData)
        {
            var results = new Dictionary<uint, PsdNineSliceOverride>();
            string payload;
            if (!TryReadPayload(userData, out payload) || string.IsNullOrEmpty(payload))
            {
                return results;
            }

            string[] records = payload.Split(';');
            foreach (string record in records)
            {
                PsdNineSliceOverride value;
                if (TryParseRecord(record, out value))
                {
                    results[value.LayerId] = value;
                }
            }

            return results;
        }

        public static bool TryGet(string userData, uint layerId, out PsdNineSliceOverride value)
        {
            value = null;
            if (layerId == 0U)
            {
                return false;
            }

            return ReadAll(userData).TryGetValue(layerId, out value);
        }

        public static string Write(string userData, uint layerId, bool enabled, PsdNineSliceBorder border)
        {
            var values = ReadAll(userData);
            values[layerId] = new PsdNineSliceOverride(layerId, enabled, border);
            return ReplacePayload(userData, Serialize(values));
        }

        public static string Remove(string userData, uint layerId)
        {
            var values = ReadAll(userData);
            if (!values.Remove(layerId))
            {
                return userData ?? string.Empty;
            }

            return ReplacePayload(userData, Serialize(values));
        }

        private static string Serialize(Dictionary<uint, PsdNineSliceOverride> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            var records = new List<string>();
            var orderedLayerIds = new List<uint>(values.Keys);
            orderedLayerIds.Sort();
            foreach (uint layerId in orderedLayerIds)
            {
                PsdNineSliceOverride value = values[layerId];
                if (!value.Enabled)
                {
                    records.Add(layerId.ToString(CultureInfo.InvariantCulture) + "|0");
                    continue;
                }

                records.Add(string.Join(
                    "|",
                    layerId.ToString(CultureInfo.InvariantCulture),
                    "1",
                    value.Border.Left.ToString(CultureInfo.InvariantCulture),
                    value.Border.Top.ToString(CultureInfo.InvariantCulture),
                    value.Border.Right.ToString(CultureInfo.InvariantCulture),
                    value.Border.Bottom.ToString(CultureInfo.InvariantCulture)));
            }

            return string.Join(";", records.ToArray());
        }

        private static bool TryParseRecord(string record, out PsdNineSliceOverride value)
        {
            value = null;
            if (string.IsNullOrEmpty(record))
            {
                return false;
            }

            string[] fields = record.Split('|');
            uint layerId;
            if (fields.Length < 2 || !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out layerId) || layerId == 0U)
            {
                return false;
            }

            if (string.Equals(fields[1], "0", StringComparison.Ordinal))
            {
                if (fields.Length != 2)
                {
                    return false;
                }

                value = new PsdNineSliceOverride(layerId, false, null);
                return true;
            }

            int left;
            int top;
            int right;
            int bottom;
            if (fields.Length != 6 || !string.Equals(fields[1], "1", StringComparison.Ordinal) ||
                !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out left) ||
                !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out top) ||
                !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out right) ||
                !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out bottom))
            {
                return false;
            }

            value = new PsdNineSliceOverride(layerId, true, new PsdNineSliceBorder(left, top, right, bottom));
            return true;
        }

        private static bool TryReadPayload(string userData, out string payload)
        {
            payload = string.Empty;
            if (string.IsNullOrEmpty(userData))
            {
                return false;
            }

            foreach (string line in userData.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    payload = line.Substring(Prefix.Length);
                    return true;
                }
            }

            return false;
        }

        private static string ReplacePayload(string userData, string payload)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(userData))
            {
                foreach (string line in userData.Replace("\r\n", "\n").Split('\n'))
                {
                    if (!string.IsNullOrEmpty(line) && !line.StartsWith(Prefix, StringComparison.Ordinal))
                    {
                        lines.Add(line);
                    }
                }
            }

            if (!string.IsNullOrEmpty(payload))
            {
                lines.Add(Prefix + payload);
            }

            return string.Join("\n", lines.ToArray());
        }
    }
}
