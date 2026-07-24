namespace PhotoshopFile
{
    using System;
    using System.IO;
    using UnityEngine;

    /// <summary>
    /// Normalized text presentation data read from a PSD text layer.
    /// Values are expressed in PSD pixels/degrees so output backends can apply
    /// their own pixels-to-units conversion exactly once.
    /// </summary>
    public sealed class PsdTextStyle
    {
        /// <summary>Gets or sets Photoshop's capitalization presentation.</summary>
        public PsdTextCapitalization Capitalization { get; set; }

        /// <summary>Gets or sets the affine transform authored on the Photoshop text layer.</summary>
        public PsdTextTransform Transform { get; set; }

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

        /// <summary>Gets or sets the solid portion of the shadow blur, in percent.</summary>
        public float ShadowChoke { get; set; }

        /// <summary>Creates a conservative style for a plain PSD text layer.</summary>
        public static PsdTextStyle CreateDefault(float fontSize)
        {
            return new PsdTextStyle
            {
                LineHeight = fontSize > 0f ? fontSize * 1.2f : 0f,
                Capitalization = PsdTextCapitalization.Normal,
                Transform = PsdTextTransform.Identity,
                StrokeColor = Color.black,
                ShadowColor = Color.black,
                ShadowDistance = 1f,
                ShadowAngle = 90f,
                ShadowBlur = 0f
            };
        }
    }

    /// <summary>Photoshop TySh affine transform and its editable TMP text equivalents.</summary>
    public struct PsdTextTransform
    {
        private const int TyShHeaderByteCount = 2 + (6 * sizeof(double));

        public static readonly PsdTextTransform Identity = new PsdTextTransform(1d, 0d, 0d, 1d, 0d, 0d);

        public PsdTextTransform(double xx, double xy, double yx, double yy, double tx, double ty)
        {
            XX = xx;
            XY = xy;
            YX = yx;
            YY = yy;
            TX = tx;
            TY = ty;
        }

        public double XX { get; private set; }
        public double XY { get; private set; }
        public double YX { get; private set; }
        public double YY { get; private set; }
        public double TX { get; private set; }
        public double TY { get; private set; }

        public float HorizontalScale
        {
            get { return (float)Math.Sqrt((XX * XX) + (YX * YX)); }
        }

        public float VerticalScale
        {
            get { return (float)Math.Sqrt((XY * XY) + (YY * YY)); }
        }

        public float CharacterHorizontalScale
        {
            get
            {
                float vertical = VerticalScale;
                return vertical > 0.0001f ? HorizontalScale / vertical : 1f;
            }
        }

        public float EffectiveFontSize(float fontSize)
        {
            float vertical = VerticalScale;
            return fontSize * (vertical > 0.0001f ? vertical : 1f);
        }

        public static bool TryReadTyShHeader(BinaryReverseReader reader, out PsdTextTransform transform)
        {
            transform = Identity;
            if (reader == null || !reader.BaseStream.CanSeek || reader.BaseStream.Length < TyShHeaderByteCount)
            {
                return false;
            }

            long originalPosition = reader.BaseStream.Position;
            try
            {
                reader.BaseStream.Position = 0;
                if (reader.ReadUInt16() != 1)
                {
                    return false;
                }

                var candidate = new PsdTextTransform(
                    ReadBigEndianDouble(reader),
                    ReadBigEndianDouble(reader),
                    ReadBigEndianDouble(reader),
                    ReadBigEndianDouble(reader),
                    ReadBigEndianDouble(reader),
                    ReadBigEndianDouble(reader));
                if (!IsFinite(candidate.XX) || !IsFinite(candidate.XY) ||
                    !IsFinite(candidate.YX) || !IsFinite(candidate.YY) ||
                    candidate.HorizontalScale <= 0.0001f || candidate.VerticalScale <= 0.0001f)
                {
                    return false;
                }

                transform = candidate;
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            finally
            {
                reader.BaseStream.Position = originalPosition;
            }
        }

        private static double ReadBigEndianDouble(BinaryReverseReader reader)
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)reader.ReadUInt64()));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
