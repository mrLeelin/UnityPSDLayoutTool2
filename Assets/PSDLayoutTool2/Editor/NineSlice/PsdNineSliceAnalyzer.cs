namespace PsdLayoutTool2
{
    using System;

    /// <summary>
    /// Infers protected 9-slice edge pixels from a Unity-ready RGBA raster.
    /// The algorithm mirrors the prior Python approach: visual edge detection,
    /// repeat-line evidence, and transparent round-corner protection.
    /// </summary>
    public static class PsdNineSliceAnalyzer
    {
        private const int MinimumRasterSize = 8;
        private const int MinimumBorder = 4;
        private const float MaximumBorderRatio = 0.45f;
        private const float EdgeSampleRatio = 0.60f;
        private const int StableRun = 3;
        private const float EdgeDifferenceThreshold = 12.0f;
        private const float RepeatMergeMultiplier = 1.5f;
        private const float MergedMaximumRatio = 0.20f;
        private const int MergedMaximumPixels = 96;
        private const float RoundRectInsetRatio = 0.12f;
        private const float RoundRectSafetyMultiplier = 1.25f;
        private const int RoundRectMinimumCenter = 20;

        /// <summary>
        /// Computes a medium-confidence candidate when a visible raster provides
        /// enough structure. Transparent, tiny, or invalid inputs are rejected.
        /// </summary>
        public static bool TryInfer(PsdNineSliceRaster raster, out PsdNineSliceInference inference)
        {
            inference = null;
            if (raster == null || raster.Width < MinimumRasterSize || raster.Height < MinimumRasterSize || !HasVisiblePixels(raster))
            {
                return false;
            }

            PsdNineSliceBorder visual = InferVisualEdges(raster);
            PsdNineSliceBorder repeated = InferRepeatedEdges(raster);
            PsdNineSliceBorder merged = Merge(visual, repeated, raster.Width, raster.Height);
            int roundRectBorder = InferRoundRectBorder(raster);
            if (roundRectBorder > 0)
            {
                merged = ProtectRoundCorners(merged, roundRectBorder, raster.Width, raster.Height);
            }

            if (!merged.IsValidFor(raster.Width, raster.Height))
            {
                return false;
            }

            inference = new PsdNineSliceInference(
                merged,
                PsdNineSliceConfidence.Medium,
                roundRectBorder > 0 ? "visual-repeat-round-rect" : "visual-repeat");
            return true;
        }

        private static bool HasVisiblePixels(PsdNineSliceRaster raster)
        {
            for (int index = 3; index < raster.Pixels.Length; index += 4)
            {
                if (raster.Pixels[index] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static PsdNineSliceBorder InferVisualEdges(PsdNineSliceRaster raster)
        {
            int xMargin = (int)(raster.Width * (1.0f - EdgeSampleRatio) * 0.5f);
            int yMargin = (int)(raster.Height * (1.0f - EdgeSampleRatio) * 0.5f);
            int x0 = Math.Max(0, xMargin);
            int x1 = Math.Min(raster.Width, raster.Width - xMargin);
            int y0 = Math.Max(0, yMargin);
            int y1 = Math.Min(raster.Height, raster.Height - yMargin);

            RgbaSample[] rows = new RgbaSample[raster.Height];
            for (int y = 0; y < raster.Height; y++)
            {
                rows[y] = AverageRow(raster, y, x0, x1);
            }

            RgbaSample[] columns = new RgbaSample[raster.Width];
            for (int x = 0; x < raster.Width; x++)
            {
                columns[x] = AverageColumn(raster, x, y0, y1);
            }

            RgbaSample rowBaseline = Average(rows, y0, y1);
            RgbaSample columnBaseline = Average(columns, x0, x1);
            return new PsdNineSliceBorder(
                FindVisualEdge(columns, columnBaseline, true),
                FindVisualEdge(rows, rowBaseline, true),
                FindVisualEdge(columns, columnBaseline, false),
                FindVisualEdge(rows, rowBaseline, false));
        }

        private static PsdNineSliceBorder InferRepeatedEdges(PsdNineSliceRaster raster)
        {
            return new PsdNineSliceBorder(
                FindRepeatEdge(raster, true, true),
                FindRepeatEdge(raster, false, true),
                FindRepeatEdge(raster, true, false),
                FindRepeatEdge(raster, false, false));
        }

        private static PsdNineSliceBorder Merge(PsdNineSliceBorder visual, PsdNineSliceBorder repeat, int width, int height)
        {
            return new PsdNineSliceBorder(
                MergeValue(visual.Left, repeat.Left, width),
                MergeValue(visual.Top, repeat.Top, height),
                MergeValue(visual.Right, repeat.Right, width),
                MergeValue(visual.Bottom, repeat.Bottom, height));
        }

        private static int MergeValue(int visual, int repeat, int axisSize)
        {
            float source;
            if (repeat <= MinimumBorder && visual > repeat * RepeatMergeMultiplier)
            {
                source = visual;
            }
            else if (visual <= repeat * RepeatMergeMultiplier)
            {
                source = visual;
            }
            else
            {
                source = Math.Max(repeat, Math.Min(visual, repeat * RepeatMergeMultiplier));
            }

            float maximum = Math.Min(axisSize * MergedMaximumRatio, MergedMaximumPixels);
            return Math.Max(MinimumBorder, (int)Math.Round(Math.Min(source, maximum)));
        }

        private static int FindVisualEdge(RgbaSample[] samples, RgbaSample baseline, bool fromStart)
        {
            int maxBorder = Math.Max(1, (int)(samples.Length * MaximumBorderRatio));
            int stableRun = 0;
            for (int offset = 0; offset < maxBorder; offset++)
            {
                int index = fromStart ? offset : samples.Length - 1 - offset;
                if (Difference(samples[index], baseline) <= EdgeDifferenceThreshold)
                {
                    stableRun++;
                    if (stableRun >= StableRun)
                    {
                        return Math.Max(offset - stableRun + 1, MinimumBorder);
                    }
                }
                else
                {
                    stableRun = 0;
                }
            }

            return MinimumBorder;
        }

        private static int FindRepeatEdge(PsdNineSliceRaster raster, bool scanColumns, bool fromStart)
        {
            int size = scanColumns ? raster.Width : raster.Height;
            int maxBorder = (int)(size * MaximumBorderRatio);
            int previous = -1;
            int repeatCount = 0;
            for (int offset = 0; offset < maxBorder; offset++)
            {
                int index = fromStart ? offset : size - 1 - offset;
                if (previous >= 0 && LinesEqual(raster, scanColumns, index, previous))
                {
                    repeatCount++;
                    if (repeatCount >= 2)
                    {
                        return Math.Max(offset - repeatCount, MinimumBorder);
                    }
                }
                else
                {
                    repeatCount = 0;
                }

                previous = index;
            }

            return MinimumBorder;
        }

        private static bool LinesEqual(PsdNineSliceRaster raster, bool scanColumns, int first, int second)
        {
            int length = scanColumns ? raster.Height : raster.Width;
            for (int position = 0; position < length; position++)
            {
                int firstX = scanColumns ? first : position;
                int firstY = scanColumns ? position : first;
                int secondX = scanColumns ? second : position;
                int secondY = scanColumns ? position : second;
                for (int component = 0; component < 4; component++)
                {
                    if (raster.GetComponent(firstX, firstY, component) != raster.GetComponent(secondX, secondY, component))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static int InferRoundRectBorder(PsdNineSliceRaster raster)
        {
            int[] insets =
            {
                FindAlphaInsetOnRow(raster, 0, true),
                FindAlphaInsetOnRow(raster, 0, false),
                FindAlphaInsetOnRow(raster, raster.Height - 1, true),
                FindAlphaInsetOnRow(raster, raster.Height - 1, false),
                FindAlphaInsetOnColumn(raster, 0, true),
                FindAlphaInsetOnColumn(raster, 0, false),
                FindAlphaInsetOnColumn(raster, raster.Width - 1, true),
                FindAlphaInsetOnColumn(raster, raster.Width - 1, false)
            };
            int maxInset = 0;
            for (int index = 0; index < insets.Length; index++)
            {
                if (insets[index] < 0)
                {
                    continue;
                }

                maxInset = Math.Max(maxInset, insets[index]);
            }

            int shortAxis = Math.Min(raster.Width, raster.Height);
            if (maxInset < shortAxis * RoundRectInsetRatio)
            {
                return 0;
            }

            int maxVisual = Math.Max(MinimumBorder, (int)(shortAxis * MaximumBorderRatio));
            int maxCenter = (shortAxis - RoundRectMinimumCenter) / 2;
            int maxBorder = maxCenter >= MinimumBorder ? Math.Min(maxVisual, maxCenter) : maxVisual;
            int border = Math.Max(MinimumBorder, Math.Min((int)Math.Ceiling(maxInset * RoundRectSafetyMultiplier), maxBorder));
            return border * 2 < raster.Width && border * 2 < raster.Height ? border : 0;
        }

        private static PsdNineSliceBorder ProtectRoundCorners(PsdNineSliceBorder border, int roundedBorder, int width, int height)
        {
            int horizontalLimit = (width - 1) / 2;
            int verticalLimit = (height - 1) / 2;
            return new PsdNineSliceBorder(
                Math.Min(Math.Max(border.Left, roundedBorder), horizontalLimit),
                Math.Min(Math.Max(border.Top, roundedBorder), verticalLimit),
                Math.Min(Math.Max(border.Right, roundedBorder), horizontalLimit),
                Math.Min(Math.Max(border.Bottom, roundedBorder), verticalLimit));
        }

        private static int FindAlphaInsetOnRow(PsdNineSliceRaster raster, int y, bool fromStart)
        {
            for (int offset = 0; offset < raster.Width; offset++)
            {
                int x = fromStart ? offset : raster.Width - 1 - offset;
                if (raster.GetComponent(x, y, 3) > 0)
                {
                    return offset;
                }
            }

            return -1;
        }

        private static int FindAlphaInsetOnColumn(PsdNineSliceRaster raster, int x, bool fromStart)
        {
            for (int offset = 0; offset < raster.Height; offset++)
            {
                int y = fromStart ? offset : raster.Height - 1 - offset;
                if (raster.GetComponent(x, y, 3) > 0)
                {
                    return offset;
                }
            }

            return -1;
        }

        private static RgbaSample AverageRow(PsdNineSliceRaster raster, int y, int x0, int x1)
        {
            RgbaSample total = new RgbaSample();
            int count = 0;
            for (int x = x0; x < Math.Max(x0 + 1, x1); x++)
            {
                total.Add(raster, x, y);
                count++;
            }

            return total.Divide(count);
        }

        private static RgbaSample AverageColumn(PsdNineSliceRaster raster, int x, int y0, int y1)
        {
            RgbaSample total = new RgbaSample();
            int count = 0;
            for (int y = y0; y < Math.Max(y0 + 1, y1); y++)
            {
                total.Add(raster, x, y);
                count++;
            }

            return total.Divide(count);
        }

        private static RgbaSample Average(RgbaSample[] values, int start, int end)
        {
            RgbaSample total = new RgbaSample();
            int count = 0;
            for (int index = start; index < Math.Max(start + 1, end); index++)
            {
                total.Add(values[index]);
                count++;
            }

            return total.Divide(count);
        }

        private static float Difference(RgbaSample first, RgbaSample second)
        {
            float rgb = (Math.Abs(first.R - second.R) + Math.Abs(first.G - second.G) + Math.Abs(first.B - second.B)) / 3.0f;
            return Math.Max(rgb, Math.Abs(first.A - second.A));
        }

        private struct RgbaSample
        {
            public float R;
            public float G;
            public float B;
            public float A;

            public void Add(PsdNineSliceRaster raster, int x, int y)
            {
                R += raster.GetComponent(x, y, 0);
                G += raster.GetComponent(x, y, 1);
                B += raster.GetComponent(x, y, 2);
                A += raster.GetComponent(x, y, 3);
            }

            public void Add(RgbaSample value)
            {
                R += value.R;
                G += value.G;
                B += value.B;
                A += value.A;
            }

            public RgbaSample Divide(int value)
            {
                RgbaSample result = new RgbaSample();
                result.R = R / value;
                result.G = G / value;
                result.B = B / value;
                result.A = A / value;
                return result;
            }
        }
    }
}
