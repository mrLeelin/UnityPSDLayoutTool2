namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    /// <summary>
    /// Regression coverage for the Unity-owned 9-slice pixel pipeline.
    /// </summary>
    public sealed class PsdNineSliceTests
    {
        [Test]
        public void AnalyzeFindsUniformSixPixelFrame()
        {
            PsdNineSliceRaster raster = CreateFramedRaster(48, 40, 6);

            PsdNineSliceInference inference;
            Assert.That(PsdNineSliceAnalyzer.TryInfer(raster, out inference), Is.True);
            Assert.That(inference.Border.Left, Is.EqualTo(6));
            Assert.That(inference.Border.Top, Is.EqualTo(6));
            Assert.That(inference.Border.Right, Is.EqualTo(6));
            Assert.That(inference.Border.Bottom, Is.EqualTo(6));
            Assert.That(inference.Confidence, Is.EqualTo(PsdNineSliceConfidence.Medium));
        }

        [Test]
        public void CropPreservesCornersAndKeepsTwoPixelStretchSample()
        {
            PsdNineSliceRaster source = CreateCoordinateRaster(16, 14);
            PsdNineSliceBorder border = new PsdNineSliceBorder(3, 4, 5, 2);

            PsdNineSliceRaster cropped = PsdNineSliceCropper.CropToMinimum(source, border);

            Assert.That(cropped.Width, Is.EqualTo(10));
            Assert.That(cropped.Height, Is.EqualTo(8));
            Assert.That(cropped.GetRed(0, 0), Is.EqualTo(source.GetRed(0, 0)));
            Assert.That(cropped.GetRed(9, 7), Is.EqualTo(source.GetRed(15, 13)));
            Assert.That(cropped.GetRed(3, 4), Is.EqualTo(source.GetRed(3, 4)));
            Assert.That(cropped.GetRed(4, 4), Is.EqualTo(source.GetRed(4, 4)));
            Assert.That(cropped.GetRed(5, 4), Is.EqualTo(source.GetRed(11, 4)));
        }

        [Test]
        public void CropHorizontalThreeSliceKeepsTheFullNonStretchHeight()
        {
            PsdNineSliceRaster source = CreateCoordinateRaster(16, 14);
            PsdNineSliceBorder border = new PsdNineSliceBorder(3, 0, 5, 0);

            PsdNineSliceRaster cropped = PsdNineSliceCropper.CropToMinimum(source, border);

            Assert.That(cropped.Width, Is.EqualTo(10));
            Assert.That(cropped.Height, Is.EqualTo(14));
            Assert.That(cropped.GetRed(4, 13), Is.EqualTo(source.GetRed(4, 13)));
            Assert.That(cropped.GetRed(5, 13), Is.EqualTo(source.GetRed(11, 13)));
        }

        [Test]
        public void CropVerticalThreeSliceKeepsTheFullNonStretchWidth()
        {
            PsdNineSliceRaster source = CreateCoordinateRaster(16, 14);
            PsdNineSliceBorder border = new PsdNineSliceBorder(0, 4, 0, 2);

            PsdNineSliceRaster cropped = PsdNineSliceCropper.CropToMinimum(source, border);

            Assert.That(cropped.Width, Is.EqualTo(16));
            Assert.That(cropped.Height, Is.EqualTo(8));
            Assert.That(cropped.GetRed(15, 4), Is.EqualTo(source.GetRed(15, 4)));
            Assert.That(cropped.GetRed(15, 6), Is.EqualTo(source.GetRed(15, 12)));
        }

        [Test]
        public void AutomaticRuleCropsSourceAndReturnsMatchingBorder()
        {
            PsdNineSliceRaster source = CreateCoordinateRaster(16, 14);
            PsdNineSliceNameRule rule = new PsdNineSliceNameRule(
                PsdNineSliceMode.NineSlice,
                new PsdNineSliceBorder(3, 4, 5, 2));

            PsdNineSliceRaster cropped;
            PsdNineSliceBorder border;
            string reason;
            Assert.That(PsdNineSliceAutoProcessor.TryProcessRaster(source, rule, out cropped, out border, out reason), Is.True, reason);
            Assert.That(border.Left, Is.EqualTo(3));
            Assert.That(border.Top, Is.EqualTo(4));
            Assert.That(border.Right, Is.EqualTo(5));
            Assert.That(border.Bottom, Is.EqualTo(2));
            Assert.That(cropped.Width, Is.EqualTo(10));
            Assert.That(cropped.Height, Is.EqualTo(8));
        }

        [Test]
        public void AnalyzeRejectsFullyTransparentRaster()
        {
            PsdNineSliceRaster raster = new PsdNineSliceRaster(16, 16, new byte[16 * 16 * 4]);

            PsdNineSliceInference inference;
            Assert.That(PsdNineSliceAnalyzer.TryInfer(raster, out inference), Is.False);
        }

        [Test]
        public void NameRuleParsesAutomaticNineSliceAndThreeSliceTags()
        {
            PsdNineSliceNameRule nineSlice;
            PsdNineSliceNameRule horizontal;
            PsdNineSliceNameRule vertical;

            Assert.That(PsdNineSliceNameRules.TryParse("panel|9slice", out nineSlice), Is.True);
            Assert.That(nineSlice.Mode, Is.EqualTo(PsdNineSliceMode.NineSlice));
            Assert.That(nineSlice.HasExplicitBorder, Is.False);

            Assert.That(PsdNineSliceNameRules.TryParse("progress|h3slice", out horizontal), Is.True);
            Assert.That(horizontal.Mode, Is.EqualTo(PsdNineSliceMode.HorizontalThreeSlice));

            Assert.That(PsdNineSliceNameRules.TryParse("scrollbar[v3slice]", out vertical), Is.True);
            Assert.That(vertical.Mode, Is.EqualTo(PsdNineSliceMode.VerticalThreeSlice));
            Assert.That(PsdNineSliceNameRules.RemoveTag("scrollbar[v3slice]"), Is.EqualTo("scrollbar"));

            Assert.That(PsdNineSliceNameRules.TryParse("panel|jiugong", out nineSlice), Is.True);
            Assert.That(nineSlice.Mode, Is.EqualTo(PsdNineSliceMode.NineSlice));
            Assert.That(PsdNineSliceNameRules.TryParse("progress|jiugongh3", out horizontal), Is.True);
            Assert.That(horizontal.Mode, Is.EqualTo(PsdNineSliceMode.HorizontalThreeSlice));
            Assert.That(PsdNineSliceNameRules.TryParse("scrollbar|jougongv3", out vertical), Is.True);
            Assert.That(vertical.Mode, Is.EqualTo(PsdNineSliceMode.VerticalThreeSlice));

            Assert.That(PsdNineSliceNameRules.TryParse("jiugongh3_dibankuan_3", out horizontal), Is.True);
            Assert.That(horizontal.Mode, Is.EqualTo(PsdNineSliceMode.HorizontalThreeSlice));
            Assert.That(PsdNineSliceNameRules.RemoveTag("jiugongh3_dibankuan_3"), Is.EqualTo("dibankuan_3"));
        }

        [Test]
        public void NameRuleParsesExplicitNineSlicePixelsInAuthorOrder()
        {
            PsdNineSliceNameRule rule;

            Assert.That(PsdNineSliceNameRules.TryParse("card|9slice=3,4,5,2", out rule), Is.True);
            Assert.That(rule.Mode, Is.EqualTo(PsdNineSliceMode.NineSlice));
            Assert.That(rule.HasExplicitBorder, Is.True);
            Assert.That(rule.ExplicitBorder.Left, Is.EqualTo(3));
            Assert.That(rule.ExplicitBorder.Top, Is.EqualTo(4));
            Assert.That(rule.ExplicitBorder.Right, Is.EqualTo(5));
            Assert.That(rule.ExplicitBorder.Bottom, Is.EqualTo(2));
        }

        [Test]
        public void UntaggedLayerRequiresItsPreviousNineSliceBorderToBeCleared()
        {
            Assert.That(PsdNineSliceImportPolicy.ShouldClearUntaggedBorder("diban_1"), Is.True);
            Assert.That(PsdNineSliceImportPolicy.ShouldClearUntaggedBorder("diban_2"), Is.True);
            Assert.That(PsdNineSliceImportPolicy.ShouldClearUntaggedBorder("jiugongh3_dibankuan_3"), Is.False);
        }

        [Test]
        public void MetadataRoundTripPreservesUnrelatedUserData()
        {
            PsdNineSliceBorder border = new PsdNineSliceBorder(3, 4, 5, 2);
            string userData = "another-tool=value";
            string written = PsdNineSliceAssetState.Write(userData, 413U, "source-hash", "output-hash", border);

            PsdNineSliceAssetState state;
            Assert.That(PsdNineSliceAssetState.TryRead(written, out state), Is.True);
            Assert.That(written, Does.Contain("another-tool=value"));
            Assert.That(state.LayerId, Is.EqualTo(413U));
            Assert.That(state.SourceHash, Is.EqualTo("source-hash"));
            Assert.That(state.OutputHash, Is.EqualTo("output-hash"));
            Assert.That(state.Border.Left, Is.EqualTo(3));
            Assert.That(state.Border.Top, Is.EqualTo(4));
            Assert.That(state.Border.Right, Is.EqualTo(5));
            Assert.That(state.Border.Bottom, Is.EqualTo(2));
        }

        private static PsdNineSliceRaster CreateFramedRaster(int width, int height, int frame)
        {
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = x < frame || x >= width - frame || y < frame || y >= height - frame;
                    int index = (y * width + x) * 4;
                    pixels[index] = edge ? (byte)200 : (byte)100;
                    pixels[index + 1] = edge ? (byte)120 : (byte)100;
                    pixels[index + 2] = edge ? (byte)80 : (byte)100;
                    pixels[index + 3] = 255;
                }
            }

            return new PsdNineSliceRaster(width, height, pixels);
        }

        private static PsdNineSliceRaster CreateCoordinateRaster(int width, int height)
        {
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    pixels[index] = (byte)(x + y * width);
                    pixels[index + 3] = 255;
                }
            }

            return new PsdNineSliceRaster(width, height, pixels);
        }
    }
}
