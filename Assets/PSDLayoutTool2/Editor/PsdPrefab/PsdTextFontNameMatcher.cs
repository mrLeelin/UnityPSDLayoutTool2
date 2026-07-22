namespace PsdLayoutTool2
{
    using System;
    using System.Text;

    /// <summary>
    /// Matches Photoshop font names against project TMP asset or source names.
    /// </summary>
    internal static class PsdTextFontNameMatcher
    {
        public static bool IsMatch(string photoshopName, string tmpAssetName, string sourceFontName)
        {
            string expected = Normalize(photoshopName);
            if (expected.Length < 4)
            {
                return false;
            }

            return Matches(expected, Normalize(tmpAssetName)) ||
                Matches(expected, Normalize(sourceFontName));
        }

        private static bool Matches(string expected, string candidate)
        {
            return !string.IsNullOrEmpty(candidate) &&
                (string.Equals(expected, candidate, StringComparison.Ordinal) ||
                 candidate.StartsWith(expected, StringComparison.Ordinal) ||
                 expected.StartsWith(candidate, StringComparison.Ordinal));
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }
    }
}
