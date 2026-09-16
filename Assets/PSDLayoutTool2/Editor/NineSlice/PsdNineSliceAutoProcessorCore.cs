namespace PsdLayoutTool2
{
    /// <summary>
    /// Pure name-rule and raster conversion. Keeping this outside Unity API
    /// code makes the border contract directly regression-testable.
    /// </summary>
    public static class PsdNineSliceAutoProcessor
    {
        /// <summary>
        /// Applies an explicit border or analyzes a tagged raster, then keeps
        /// the four protected edges plus Unity's two-pixel stretch-center sample.
        /// The generated Sprite is therefore physically reduced like the Figma
        /// export pipeline, while the Prefab Rect remains at the PSD size.
        /// </summary>
        public static bool TryProcessRaster(
            PsdNineSliceRaster source,
            PsdNineSliceNameRule rule,
            out PsdNineSliceRaster cropped,
            out PsdNineSliceBorder border,
            out string reason)
        {
            return TryProcessRaster(source, rule, true, out cropped, out border, out reason);
        }

        /// <summary>
        /// <paramref name="cropEnabled"/> 为 false 时只推断并返回边框，<c>cropped</c> 保持原始栅格。
        /// 用于「关闭导出时自动裁剪」的项目设置：九宫边框照常生效，但 PNG 尺寸不变，
        /// 手动量的边距和烘焙美术不会因为裁剪而错位。
        /// </summary>
        public static bool TryProcessRaster(
            PsdNineSliceRaster source,
            PsdNineSliceNameRule rule,
            bool cropEnabled,
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

            if (!cropEnabled)
            {
                cropped = source;
                reason = "Auto crop is disabled in the project settings; kept the full source raster.";
                return true;
            }

            cropped = PsdNineSliceCropper.CropToMinimum(source, border);
            if (!rule.HasExplicitBorder)
            {
                float meanChannelDifference;
                if (!PsdNineSliceCropSafety.IsSafeToCrop(source, cropped, border, out meanChannelDifference))
                {
                    cropped = source;
                    reason = "Preserved the full source because a name-driven crop would alter baked artwork (mean channel difference=" +
                        meanChannelDifference.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ").";
                }
            }

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
