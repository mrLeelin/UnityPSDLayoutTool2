namespace UGF.EditorTools.Psd2UGUI.NineSlice
{
    /// <summary>
    /// Pure name-rule and raster conversion. Keeping this outside Unity API
    /// code makes the border contract directly regression-testable.
    /// </summary>
    public static class Psd2UiNineSliceAutoProcessor
    {
        /// <summary>
        /// Applies an explicit border or analyzes a tagged raster, then keeps
        /// the four protected edges plus Unity's two-pixel stretch-center sample.
        /// The generated Sprite is therefore physically reduced like the Figma
        /// export pipeline, while the Prefab Rect remains at the PSD size.
        /// </summary>
        public static bool TryProcessRaster(
            Psd2UiNineSliceRaster source,
            Psd2UiNineSliceNameRule rule,
            out Psd2UiNineSliceRaster cropped,
            out Psd2UiNineSliceBorder border,
            out string reason)
        {
            cropped = null;
            border = null;
            reason = string.Empty;
            if (source == null || rule == null)
            {
                reason = "The source raster or nine-slice rule is missing.";
                return false;
            }

            if (rule.HasExplicitBorder)
            {
                border = rule.ExplicitBorder;
            }
            else
            {
                Psd2UiNineSliceInference inference;
                if (!Psd2UiNineSliceAnalyzer.TryInfer(source, out inference))
                {
                    reason = "Automatic pixel analysis could not find a valid stretch center.";
                    return false;
                }

                border = inference.Border;
            }

            border = ApplyMode(border, rule.Mode);
            if (!border.IsValidFor(source.Width, source.Height))
            {
                reason = "The requested nine-slice border is outside the generated layer bounds.";
                return false;
            }

            cropped = Psd2UiNineSliceCropper.CropToMinimum(source, border);
            if (!rule.HasExplicitBorder)
            {
                float meanChannelDifference;
                if (!Psd2UiNineSliceCropSafety.IsSafeToCrop(source, cropped, border, out meanChannelDifference))
                {
                    cropped = source;
                    reason = "Preserved the full source because a name-driven crop would alter baked artwork (mean channel difference=" +
                        meanChannelDifference.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ").";
                }
            }

            return true;
        }

        private static Psd2UiNineSliceBorder ApplyMode(Psd2UiNineSliceBorder border, Psd2UiNineSliceMode mode)
        {
            if (mode == Psd2UiNineSliceMode.HorizontalThreeSlice)
            {
                return new Psd2UiNineSliceBorder(border.Left, 0, border.Right, 0);
            }

            if (mode == Psd2UiNineSliceMode.VerticalThreeSlice)
            {
                return new Psd2UiNineSliceBorder(0, border.Top, 0, border.Bottom);
            }

            return border;
        }
    }
}
