namespace PsdLayoutTool2
{
    /// <summary>
    /// Pure name-rule and raster conversion. Keeping this outside Unity API
    /// code makes the crop contract directly regression-testable.
    /// </summary>
    public static class PsdNineSliceAutoProcessor
    {
        /// <summary>
        /// Applies an explicit border or analyzes a tagged raster, then returns
        /// the minimally cropped source and its matching border.
        /// </summary>
        public static bool TryProcessRaster(
            PsdNineSliceRaster source,
            PsdNineSliceNameRule rule,
            out PsdNineSliceRaster cropped,
            out PsdNineSliceBorder border,
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
                PsdNineSliceInference inference;
                if (!PsdNineSliceAnalyzer.TryInfer(source, out inference))
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

            cropped = PsdNineSliceCropper.CropToMinimum(source, border);
            return true;
        }

        private static PsdNineSliceBorder ApplyMode(PsdNineSliceBorder border, PsdNineSliceMode mode)
        {
            if (mode == PsdNineSliceMode.HorizontalThreeSlice)
            {
                return new PsdNineSliceBorder(border.Left, 0, border.Right, 0);
            }

            if (mode == PsdNineSliceMode.VerticalThreeSlice)
            {
                return new PsdNineSliceBorder(0, border.Top, 0, border.Bottom);
            }

            return border;
        }
    }
}
