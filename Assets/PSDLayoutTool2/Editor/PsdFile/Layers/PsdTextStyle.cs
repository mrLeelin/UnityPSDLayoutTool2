namespace PhotoshopFile
{
    using UnityEngine;

    /// <summary>
    /// Normalized text presentation data read from a PSD text layer.
    /// Values are expressed in PSD pixels/degrees so output backends can apply
    /// their own pixels-to-units conversion exactly once.
    /// </summary>
    public sealed class PsdTextStyle
    {
        /// <summary>Gets or sets the line height in PSD pixels.</summary>
        public float LineHeight { get; set; }

        /// <summary>Gets or sets whether the text has an outer stroke.</summary>
        public bool StrokeEnabled { get; set; }

        /// <summary>Gets or sets the stroke width in PSD pixels.</summary>
        public float StrokeWidth { get; set; }

        /// <summary>Gets or sets the stroke color.</summary>
        public Color StrokeColor { get; set; }

        /// <summary>Gets or sets whether the text has a drop shadow.</summary>
        public bool ShadowEnabled { get; set; }

        /// <summary>Gets or sets the shadow color.</summary>
        public Color ShadowColor { get; set; }

        /// <summary>Gets or sets the shadow offset distance in PSD pixels.</summary>
        public float ShadowDistance { get; set; }

        /// <summary>Gets or sets the shadow angle in degrees.</summary>
        public float ShadowAngle { get; set; }

        /// <summary>Gets or sets the shadow blur in PSD pixels.</summary>
        public float ShadowBlur { get; set; }

        /// <summary>Creates a conservative style for a plain PSD text layer.</summary>
        public static PsdTextStyle CreateDefault(float fontSize)
        {
            return new PsdTextStyle
            {
                LineHeight = fontSize > 0f ? fontSize * 1.2f : 0f,
                StrokeColor = Color.black,
                ShadowColor = Color.black,
                ShadowDistance = 1f,
                ShadowAngle = 90f,
                ShadowBlur = 0f
            };
        }
    }
}
