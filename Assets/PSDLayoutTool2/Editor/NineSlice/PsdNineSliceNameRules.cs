namespace PsdLayoutTool2
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The stretch direction requested by a PSD layer-name tag.
    /// </summary>
    public enum PsdNineSliceMode
    {
        NineSlice,
        HorizontalThreeSlice,
        VerticalThreeSlice
    }

    /// <summary>
    /// A parsed PSD authoring rule. Explicit borders are always written in
    /// author-facing left, top, right, bottom order.
    /// </summary>
    public sealed class PsdNineSliceNameRule
    {
        public PsdNineSliceNameRule(PsdNineSliceMode mode, PsdNineSliceBorder explicitBorder)
        {
            Mode = mode;
            ExplicitBorder = explicitBorder;
        }

        public PsdNineSliceMode Mode { get; private set; }
        public PsdNineSliceBorder ExplicitBorder { get; private set; }
        public bool HasExplicitBorder { get { return ExplicitBorder != null; } }
    }

    /// <summary>
    /// Parses non-visual PSD authoring tags. A bare tag opts a layer into the
    /// automatic pixel analysis performed during prefab generation.
    /// </summary>
    public static class PsdNineSliceNameRules
    {
        private const string Number = @"([0-9]+(?:\.[0-9]+)?)";
        private static readonly Regex ExplicitNineSlicePattern = new Regex(
            @"(?:\|9slice\s*=\s*|\[9slice\s*:\s*)" + Number + @"\s*,\s*" + Number + @"\s*,\s*" + Number + @"\s*,\s*" + Number + @"\s*\]?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex AutomaticTagPattern = new Regex(
            @"(?:\|(?<pipe>9slice|h3slice|v3slice|jiugongh3|jiugongv3|jougongv3|jiugong)\b|\[(?<bracket>9slice|h3slice|v3slice|jiugongh3|jiugongv3|jougongv3|jiugong)\b\]?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex FigmaPrefixPattern = new Regex(
            @"^(?<prefix>jiugongh3|jiugongv3|jougongv3|jiugong)(?:[_\-\s]+|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Accepts <c>|9slice</c>, Figma-compatible <c>|jiugong</c> tags,
        /// Figma prefixes such as <c>jiugongh3_background</c>, their bracket
        /// forms, and explicit <c>|9slice=L,T,R,B</c> borders.
        /// </summary>
        public static bool TryParse(string layerName, out PsdNineSliceNameRule rule)
        {
            rule = null;
            if (string.IsNullOrEmpty(layerName))
            {
                return false;
            }

            Match explicitMatch = ExplicitNineSlicePattern.Match(layerName);
            if (explicitMatch.Success)
            {
                float left;
                float top;
                float right;
                float bottom;
                if (!TryReadBorder(explicitMatch, out left, out top, out right, out bottom))
                {
                    return false;
                }

                rule = new PsdNineSliceNameRule(
                    PsdNineSliceMode.NineSlice,
                    new PsdNineSliceBorder(
                        (int)Math.Round(left, MidpointRounding.AwayFromZero),
                        (int)Math.Round(top, MidpointRounding.AwayFromZero),
                        (int)Math.Round(right, MidpointRounding.AwayFromZero),
                        (int)Math.Round(bottom, MidpointRounding.AwayFromZero)));
                return true;
            }

            Match prefix = FigmaPrefixPattern.Match(layerName);
            if (prefix.Success)
            {
                return TryCreateRule(prefix.Groups["prefix"].Value, out rule);
            }

            Match tag = AutomaticTagPattern.Match(layerName);
            if (!tag.Success)
            {
                return false;
            }

            string value = tag.Groups["pipe"].Success
                ? tag.Groups["pipe"].Value
                : tag.Groups["bracket"].Value;
            return TryCreateRule(value, out rule);
        }

        private static bool TryCreateRule(string value, out PsdNineSliceNameRule rule)
        {
            rule = null;
            if (string.Equals(value, "h3slice", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "jiugongh3", StringComparison.OrdinalIgnoreCase))
            {
                rule = new PsdNineSliceNameRule(PsdNineSliceMode.HorizontalThreeSlice, null);
                return true;
            }

            if (string.Equals(value, "v3slice", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "jiugongv3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "jougongv3", StringComparison.OrdinalIgnoreCase))
            {
                rule = new PsdNineSliceNameRule(PsdNineSliceMode.VerticalThreeSlice, null);
                return true;
            }

            rule = new PsdNineSliceNameRule(PsdNineSliceMode.NineSlice, null);
            return true;
        }

        /// <summary>
        /// Removes one authoring tag before a layer name becomes a Unity object
        /// or generated asset name.
        /// </summary>
        public static string RemoveTag(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                return string.Empty;
            }

            Match explicitMatch = ExplicitNineSlicePattern.Match(layerName);
            if (explicitMatch.Success)
            {
                return ExplicitNineSlicePattern.Replace(layerName, string.Empty, 1).Trim();
            }

            Match prefix = FigmaPrefixPattern.Match(layerName);
            if (prefix.Success)
            {
                return layerName.Substring(prefix.Length).Trim();
            }

            return AutomaticTagPattern.Replace(layerName, string.Empty, 1).Trim();
        }

        private static bool TryReadBorder(Match match, out float left, out float top, out float right, out float bottom)
        {
            left = 0f;
            top = 0f;
            right = 0f;
            bottom = 0f;
            return float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out left) &&
                float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out top) &&
                float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out right) &&
                float.TryParse(match.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out bottom);
        }
    }
}
