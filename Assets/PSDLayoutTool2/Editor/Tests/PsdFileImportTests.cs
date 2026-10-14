namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using NUnit.Framework;
    using PhotoshopFile;
    using UnityEngine;

    /// <summary>
    /// Regression coverage for PSD files containing Photoshop text descriptors.
    /// </summary>
    public sealed class PsdFileImportTests
    {
        [Test]
        public void Scene22RectangleCopyExportsGradientColorAndTransparency()
        {
            Layer layer = FindSceneLayer("场景22.psd", 362);
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            try
            {
                Assert.That(texture.width, Is.EqualTo(1091));
                Assert.That(texture.height, Is.EqualTo(772));
                // Pixel values independently checked against the PSD's saved composite:
                // purple/transparent at the top, green/173 alpha at the bottom.
                Color32 bottom = texture.GetPixel(545, 1);
                Assert.That(bottom.r, Is.InRange(154, 155));
                Assert.That(bottom.g, Is.InRange(182, 183));
                Assert.That(bottom.b, Is.InRange(42, 43));
                Assert.That(bottom.a, Is.InRange(172, 173));
                Color32 quarter = texture.GetPixel(545, 193);
                Assert.That(quarter.r, Is.InRange(127, 129));
                Assert.That(quarter.g, Is.InRange(143, 146));
                Assert.That(quarter.a, Is.InRange(135, 137));
                Color32 middle = texture.GetPixel(545, 386);
                Assert.That(middle.a, Is.InRange(85, 87));
                Color32 top = texture.GetPixel(545, 760);
                Assert.That(top.a, Is.LessThanOrEqualTo(2));
                var renderer = typeof(ImageDecoder).Assembly.GetType("PhotoshopFile.PsdLayerStyleRenderer");
                var issues = (List<string>)renderer.GetMethod("DescribeExportIssues",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { layer, texture.GetPixels32() });
                Assert.That(issues, Is.Empty);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void Scene22GradientReverseSwapsColorAndTransparency()
        {
            Layer layer = FindSceneLayer("场景22.psd", 362);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            object style = typeof(Layer).GetProperty("ShapeStyle", flags).GetValue(layer);
            object gradient = style.GetType().GetProperty("GradientOverlay", flags).GetValue(style);
            gradient.GetType().GetProperty("Reverse", flags).SetValue(gradient, true);
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            try
            {
                Assert.That(texture.GetPixel(545, 1).a, Is.LessThan(0.01f));
                Assert.That(texture.GetPixel(545, 760).a, Is.GreaterThan(0.66f));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void Scene33LockUsesEnabledShadowAfterDisabledShadow()
        {
            Layer layer = FindScene33Layer(387);
            Assert.That(layer.Name, Is.EqualTo("LOCK"));
            Assert.That(layer.TextStyle.StrokeEnabled, Is.True);
            Assert.That(layer.TextStyle.StrokeWidth, Is.EqualTo(6f));
            Assert.That(layer.TextStyle.ShadowEnabled, Is.True);
            Assert.That(layer.TextStyle.ShadowDistance, Is.EqualTo(9f));
            Assert.That(layer.TextStyle.ShadowAngle, Is.EqualTo(90f));
            Assert.That(layer.TextStyle.ShadowBlur, Is.EqualTo(4f));
            Assert.That(layer.TextStyle.ShadowChoke, Is.EqualTo(100f));
            Assert.That(ColorUtility.ToHtmlStringRGBA(layer.TextStyle.ShadowColor), Is.EqualTo("184914FF"));

            var model = PsdPrefabModelBuilder.Build(layer.PsdFile);
            string id = PsdStableLayerIdUtility.Create(layer.Id, "", 0, layer.Name).value;
            var effect = model.nodes.Find(node => node.stableId == id).text.effect;
            Assert.That(effect.hasShadow, Is.True);
            Assert.That(effect.shadowOffsetX, Is.EqualTo(0f).Within(0.001f));
            Assert.That(effect.shadowOffsetY, Is.EqualTo(-9f));
            Assert.That(effect.shadowDilate, Is.EqualTo(4f));
            Assert.That(effect.shadowSoftness, Is.Zero);
        }

        [Test]
        public void Scene33CheckmarkIncludesOutsideStrokeWithoutChangingSourceBounds()
        {
            Layer layer = FindScene33Checkmark();
            Rect sourceRect = layer.Rect;
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            try
            {
                Assert.That(sourceRect, Is.EqualTo(new Rect(653, 599, 209, 190)));
                Assert.That(layer.RenderRect.width, Is.LessThanOrEqualTo(221));
                Assert.That(layer.RenderRect.height, Is.LessThanOrEqualTo(202));
                Assert.That(layer.Rect, Is.EqualTo(sourceRect));
                Assert.That(texture.width, Is.EqualTo((int)layer.RenderRect.width));
                Assert.That(texture.height, Is.EqualTo((int)layer.RenderRect.height));
                Color32[] pixels = texture.GetPixels32();
                int outsidePixels = 0;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                {
                    Color32 pixel = pixels[y * texture.width + x];
                    if (x == 0 || y == 0 || x == texture.width - 1 || y == texture.height - 1)
                        Assert.That(pixel.a, Is.Zero, "The stroke must have an unclipped transparent border.");
                    if (!sourceRect.Contains(new Vector2(layer.RenderRect.x + x,
                        layer.RenderRect.yMax - 1 - y)) && pixel.a > 200)
                    {
                        outsidePixels++;
                        Assert.That(pixel.r, Is.EqualTo(66));
                        Assert.That(pixel.g, Is.EqualTo(24));
                        Assert.That(pixel.b, Is.EqualTo(17));
                    }
                    if (pixel.a == 0) Assert.That(pixel, Is.EqualTo(new Color32(0, 0, 0, 0)));
                }
                Assert.That(outsidePixels, Is.GreaterThan(0), "Outside-stroke pixels must survive beyond the source rectangle.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void Scene33CheckmarkKeepsOpaqueSourceColorsAndAntialiasedEdges()
        {
            Layer layer = FindScene33Checkmark();
            layer.ClippedLayers.Clear(); // Isolate the base's color/stroke regression from its clipping stack.
            layer.SortedChannels.Remove(-2); // The updated fixture also masks part of the original silhouette.
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            try
            {
                Color32[] pixels = texture.GetPixels32();
                int opaqueCount = 0, antialiasedCount = 0;
                for (int y = 0; y < (int)layer.Rect.height; y++) for (int x = 0; x < (int)layer.Rect.width; x++)
                {
                    int source = y * (int)layer.Rect.width + x;
                    int tx = (int)(layer.Rect.x + x - layer.RenderRect.x);
                    int ty = (int)(layer.RenderRect.yMax - 1 - layer.Rect.y - y);
                    Color32 pixel = pixels[ty * texture.width + tx];
                    if (layer.SortedChannels[-1].ImageData[source] != 255) continue;
                    opaqueCount++;
                    Assert.That(pixel, Is.EqualTo(new Color32(layer.SortedChannels[0].ImageData[source],
                        layer.SortedChannels[1].ImageData[source], layer.SortedChannels[2].ImageData[source], 255)));
                }
                foreach (Color32 pixel in pixels) if (pixel.a > 0 && pixel.a < 255) antialiasedCount++;
                Assert.That(opaqueCount, Is.GreaterThan(10000));
                Assert.That(antialiasedCount, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void CroppingTransparentPaddingPreservesDocumentPixelCoordinates()
        {
            var pixels = new Color32[8 * 7];
            pixels[2 * 8 + 4] = new Color32(12, 34, 56, 128);
            Rect source = new Rect(100, 200, 8, 7);
            var crop = typeof(ImageDecoder).GetMethod("CropTransparentPadding",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object[] args = { pixels, source };
            var cropped = (Color32[])crop.Invoke(null, args);
            Rect bounds = (Rect)args[1];
            Assert.That(bounds, Is.EqualTo(new Rect(103, 203, 3, 3)));
            Assert.That(cropped[4], Is.EqualTo(pixels[2 * 8 + 4]));
            Assert.That(bounds.x + 1, Is.EqualTo(source.x + 4));
            Assert.That(bounds.yMax - 2, Is.EqualTo(source.yMax - 3));
            Assert.That(cropped.Length, Is.EqualTo(9));
            for (int i = 0; i < cropped.Length; i++) if (i != 4) Assert.That(cropped[i].a, Is.Zero);
        }

        [Test]
        public void OutsideStrokeUsesContourCoverageInsteadOfExpandingWeakAlpha()
        {
            var renderer = typeof(ImageDecoder).Assembly.GetType("PhotoshopFile.PsdLayerStyleRenderer");
            var stroke = renderer.GetMethod("StrokeCoverage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            // A feathered strip describes a contour at every partial pixel.
            var alpha = new float[] { 0f, 0f, 64 / 255f, 128 / 255f, 1f, 1f, 1f };
            var result = (byte[])stroke.Invoke(null, new object[] { alpha, 7, 1, 0f, 2f });
            Assert.That(result[2], Is.EqualTo(191).Within(1));
            Assert.That(result[3], Is.EqualTo(127).Within(1));
            Assert.That(result[4], Is.Zero);
            Assert.That(result[1], Is.GreaterThan(64));
        }

        [Test]
        public void MaskCoverageKeepsFractionalAlphaBeforeStroke()
        {
            Layer layer = FindScene33Checkmark();
            var colors = new Color32[(int)(layer.Rect.width * layer.Rect.height)];
            for (int i = 0; i < colors.Length; i++) colors[i].a = 1;
            var before = (float[])typeof(ImageDecoder).GetMethod("GetMaskedCoverage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { layer, colors });
            var masked = (Color32[])colors.Clone();
            typeof(ImageDecoder).GetMethod("ApplyUserMask",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { layer, masked });
            bool found = false;
            for (int i = 0; i < before.Length; i++)
                if (before[i] > 0f && masked[i].a == 0) { found = true; break; }
            Assert.That(found, Is.True, "Fractional mask coverage must not disappear before measuring the contour.");
        }

        [Test]
        public void LayerEffectBehindUsesStraightAlphaAndIgnoresHiddenRgb()
        {
            var renderer = typeof(ImageDecoder).Assembly.GetType("PhotoshopFile.PsdLayerStyleRenderer");
            var composite = renderer.GetMethod("CompositeMask",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var pixels = new[] { new Color32(10, 69, 14, 0), new Color32(200, 100, 50, 128), new Color32(200, 100, 50, 255) };
            composite.Invoke(null, new object[] { pixels, new byte[] { 128, 128, 128 },
                (Color)new Color32(66, 24, 17, 255), 1f, new byte[] { 0, 128, 255 }, true });
            Assert.That(pixels[0], Is.EqualTo(new Color32(66, 24, 17, 128)));
            Assert.That(pixels[1].a, Is.EqualTo(192).Within(1));
            Assert.That(pixels[1].r, Is.EqualTo(155).Within(1));
            Assert.That(pixels[1].g, Is.EqualTo(75).Within(1));
            Assert.That(pixels[1].b, Is.EqualTo(39).Within(1));
            Assert.That(pixels[2], Is.EqualTo(new Color32(200, 100, 50, 255)));
        }

        [Test]
        public void Scene33ModelAndNineSlicePreviewShareRenderedBounds()
        {
            Layer layer = FindScene33Checkmark();
            var psd = layer.PsdFile;
            var model = PsdPrefabModelBuilder.Build(psd);
            string id = PsdStableLayerIdUtility.Create(layer.Id, "", 0, layer.Name).value;
            // Native IDs are stable regardless of parent/sibling coordinates.
            Assert.That(model.nodes.Find(node => node.stableId == id).bounds, Is.EqualTo(layer.RenderRect));
            using (var session = PsdNineSlicePsdLayerSession.Open("Assets/PSDLayoutTool2/TestData/场景33.psd"))
            {
                var entry = System.Linq.Enumerable.First(session.Layers, item => item.LayerId == 386);
                var preview = session.GetPreview(entry);
                Assert.That(entry.Rect.width, Is.EqualTo(preview.width));
                Assert.That(entry.Rect.height, Is.EqualTo(preview.height));
            }
        }

        private static Layer FindScene33Checkmark()
        {
            return FindScene33Layer(386);
        }

        private static Layer FindScene33Layer(uint id)
        {
            return FindSceneLayer("场景33.psd", id);
        }

        private static Layer FindSceneLayer(string fileName, uint id)
        {
            string path = Path.Combine(Application.dataPath, "PSDLayoutTool2", "TestData", fileName);
            if (!File.Exists(path)) Assert.Ignore(fileName + " PSD fixture is unavailable.");
            var queue = new Queue<Layer>(new PsdFile(path).Layers);
            while (queue.Count > 0)
            {
                Layer layer = queue.Dequeue();
                if (layer.Id == id) return layer;
                foreach (Layer child in layer.Children) queue.Enqueue(child);
            }
            Assert.Fail(fileName + " layer " + id + " is missing.");
            return null;
        }

        [TestCase("InsF", 209, 190)]
        [TestCase("CtrF", 217, 198)]
        [TestCase("OutF", 221, 202)]
        public void StrokePositionOnlyExpandsTheOutsidePart(string position, int width, int height)
        {
            Layer layer = FindScene33Checkmark();
            object style = typeof(Layer).GetProperty("ShapeStyle", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(layer);
            style.GetType().GetProperty("StrokePosition", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(style, position);
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            try
            {
                var renderer = typeof(ImageDecoder).Assembly.GetType("PhotoshopFile.PsdLayerStyleRenderer");
                Rect padded = (Rect)renderer.GetMethod("GetRenderRect",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { layer });
                Assert.That(padded.width, Is.EqualTo(width));
                Assert.That(padded.height, Is.EqualTo(height));
                Assert.That(padded.center, Is.EqualTo(layer.Rect.center));
                Assert.That(texture.width, Is.EqualTo((int)layer.RenderRect.width));
                Assert.That(texture.height, Is.EqualTo((int)layer.RenderRect.height));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void UserMaskSuppressesPixelsBeforeOutsideStrokeExpansion()
        {
            Layer layer = FindScene33Checkmark();
            layer.SortedChannels[-2] = layer.SortedChannels[0];
            typeof(Mask).GetField("rect", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(layer.MaskData, layer.Rect);
            typeof(Mask).GetProperty("ImageData").SetValue(layer.MaskData,
                new byte[(int)(layer.Rect.width * layer.Rect.height)]);
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            try
            {
                foreach (Color32 pixel in texture.GetPixels32())
                    Assert.That(pixel, Is.EqualTo(new Color32(0, 0, 0, 0)));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void InnerEffectPreservesCoverageAndColorRange()
        {
            var renderer = typeof(ImageDecoder).Assembly.GetType("PhotoshopFile.PsdLayerStyleRenderer");
            var composite = renderer.GetMethod("CompositeMask",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var pixels = new[] { new Color32(200, 100, 50, 128) };
            composite.Invoke(null, new object[] { pixels, new byte[] { 128 },
                (Color)new Color32(66, 24, 17, 255), 1f, new byte[] { 128 }, false });
            Assert.That(pixels[0].a, Is.EqualTo(128));
            Assert.That(pixels[0].r, Is.EqualTo(133).Within(1));
            Assert.That(pixels[0].g, Is.EqualTo(62).Within(1));
            Assert.That(pixels[0].b, Is.EqualTo(33).Within(1));
        }

        [Test]
        public void Scene33Group15BakesThePhotoIntoTheCheckmarkAndMatchesPhotoshop()
        {
            Layer layer = FindScene33Checkmark();
            Assert.That(layer.ClippedLayers.Count, Is.EqualTo(1));
            Layer photo = layer.ClippedLayers[0];
            Assert.That(photo.Id, Is.EqualTo(399));
            Assert.That(photo.IsClipping, Is.True);
            Assert.That(photo.ClippingBase, Is.SameAs(layer));
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            Texture2D merged = ImageDecoder.DecodeMergedImageCrop(layer);
            try
            {
                Color32[] rendered = texture.GetPixels32(), reference = merged.GetPixels32();
                int compared = 0;
                var original = new Color32[(int)(layer.Rect.width * layer.Rect.height)];
                for (int row = 0; row < (int)layer.Rect.height; row++) for (int col = 0; col < (int)layer.Rect.width; col++)
                    original[((int)layer.Rect.height - 1 - row) * (int)layer.Rect.width + col].a =
                        layer.SortedChannels[-1].ImageData[row * (int)layer.Rect.width + col];
                var maskedCoverage = (float[])typeof(ImageDecoder).GetMethod("GetMaskedCoverage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { layer, original });
                for (int y = 0; y < merged.height; y++) for (int x = 0; x < merged.width; x++)
                {
                    int tx = (int)(layer.Rect.x + x - layer.RenderRect.x);
                    // Both arrays use bottom-up rows, while PSD rectangles use top-down coordinates.
                    int ty = (int)(layer.RenderRect.yMax - layer.Rect.yMax) + y;
                    if (tx < 0 || ty < 0 || tx >= texture.width || ty >= texture.height) continue;
                    Color32 pixel = rendered[ty * texture.width + tx];
                    // The saved Photoshop composite includes the background
                    // at antialiased edges. Compare only opaque photo coverage.
                    if (maskedCoverage[y * merged.width + x] != 1f) continue;
                    Color32 expected = reference[y * merged.width + x];
                    Assert.That(pixel.r, Is.EqualTo(expected.r).Within(1));
                    Assert.That(pixel.g, Is.EqualTo(expected.g).Within(1));
                    Assert.That(pixel.b, Is.EqualTo(expected.b).Within(1));
                    compared++;
                }
                Assert.That(compared, Is.GreaterThan(9000), "Only opaque photo pixels qualify; stroke pixels are checked separately.");
                Assert.That(texture.width, Is.LessThan(photo.Rect.width));
                Assert.That(texture.height, Is.LessThan(photo.Rect.height));
            }
            finally { Object.DestroyImmediate(texture); Object.DestroyImmediate(merged); }
        }

        [Test]
        public void Scene33ClippedPhotoDoesNotExportOrEnlargeGroup15()
        {
            Layer layer = FindScene33Checkmark();
            Layer photo = layer.ClippedLayers[0];
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var tree = (List<Layer>)typeof(PsdImporter).GetMethod("BuildLayerTree", flags)
                .Invoke(null, new object[] { layer.PsdFile.Layers });
            var infos = (System.Collections.IDictionary)typeof(PsdImporter).GetMethod("BuildLayerImportInfoMap", flags)
                .Invoke(null, new object[] { tree });
            Layer group = FindLayer(tree, 395);
            object groupInfo = infos[group];
            Assert.That(groupInfo.GetType().GetProperty("LayoutRect").GetValue(groupInfo), Is.EqualTo(layer.RenderRect));
            Assert.That(typeof(PsdImporter).GetMethod("ShouldLayerEmitTextureFile", flags)
                .Invoke(null, new object[] { infos[photo] }), Is.False);
            Assert.That(typeof(PsdImporter).GetMethod("ShouldLayerEmitTextureFile", flags)
                .Invoke(null, new object[] { infos[layer] }), Is.True);
            var model = PsdPrefabModelBuilder.Build(layer.PsdFile);
            Assert.That(model.nodes.Exists(node => node.stableId == "399"), Is.False);
            using (var session = PsdNineSlicePsdLayerSession.Open("Assets/PSDLayoutTool2/TestData/场景33.psd"))
                Assert.That(System.Linq.Enumerable.Any(session.Layers, entry => entry.LayerId == 399), Is.False);
        }

        [Test]
        public void ClippedPhotoPixelChangesInvalidateTheBaseAssetFingerprint()
        {
            Layer layer = FindScene33Checkmark();
            var before = PsdPrefabModelBuilder.Build(layer.PsdFile).nodes.Find(node => node.stableId == "386");
            layer.ClippedLayers[0].SortedChannels[0].ImageData[0] ^= 1;
            var after = PsdPrefabModelBuilder.Build(layer.PsdFile).nodes.Find(node => node.stableId == "386");
            Assert.That(after.assetFingerprint, Is.Not.EqualTo(before.assetFingerprint));
            Assert.That(PsdHierarchyFingerprints.Structure(after), Is.EqualTo(PsdHierarchyFingerprints.Structure(before)));
            Assert.That(PsdHierarchyFingerprints.Geometry(after), Is.EqualTo(PsdHierarchyFingerprints.Geometry(before)));
        }

        private static Layer FindLayer(IEnumerable<Layer> layers, uint id)
        {
            foreach (Layer layer in layers)
            {
                if (layer.Id == id) return layer;
                Layer child = FindLayer(layer.Children, id);
                if (child != null) return child;
            }
            return null;
        }

        [Test]
        public void MainScreenTextUsesVisibleOverlayColorsAndEnabledStrokes()
        {
            string path = Path.Combine(Application.dataPath, "PSDLayoutTool2", "TestData", "主界面.psd");
            if (!File.Exists(path)) Assert.Ignore("Main screen PSD fixture is unavailable.");
            var layers = new Dictionary<string, Layer>();
            System.Action<Layer> visit = null;
            visit = layer =>
            {
                if (layer.IsTextLayer) layers[layer.Name] = layer;
                foreach (var child in layer.Children) visit(child);
            };
            foreach (var layer in new PsdFile(path).Layers) visit(layer);
            Assert.That(ColorUtility.ToHtmlStringRGB(layers["ALBUM"].FillColor), Is.EqualTo("712D2B"));
            Assert.That(ColorUtility.ToHtmlStringRGB(layers["FRIEND"].FillColor), Is.EqualTo("712D2B"));
            Assert.That(ColorUtility.ToHtmlStringRGB(layers["1"].FillColor), Is.EqualTo("FFFFFF"));
            Assert.That(ColorUtility.ToHtmlStringRGB(layers["HOLD FOR  AUTO"].FillColor), Is.EqualTo("6F0001"));
            Assert.That(ColorUtility.ToHtmlStringRGB(layers["DRAW"].FillColor), Is.EqualTo("FEEED4"));
            Assert.That(layers["DRAW"].TextStyle.StrokeEnabled, Is.True);
            Assert.That(layers["HOLD FOR  AUTO"].TextStyle.StrokeEnabled, Is.True);
            Assert.That(layers["HOLD FOR  AUTO"].TextStyle.StrokeWidth, Is.EqualTo(1f));
            Assert.That(ColorUtility.ToHtmlStringRGB(layers["HOLD FOR  AUTO"].TextStyle.StrokeColor), Is.EqualTo("E70908"));
        }

        [Test]
        public void TrySeekLeavesReaderAfterSearchKey()
        {
            using (MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes("/FontSize 75.0\n")))
            using (BinaryReverseReader reader = new BinaryReverseReader(stream))
            {
                Assert.That(reader.TrySeek("/FontSize"), Is.True);
                Assert.That(reader.ReadByte(), Is.EqualTo((byte)' '));

                float value;
                Assert.That(reader.TryReadAsciiFloat(out value), Is.True);
                Assert.That(value, Is.EqualTo(75.0f));
            }
        }

        [Test]
        public void SuppliedPsdWithTextLayersLoadsWithoutEndOfStream()
        {
            string path = FindSuppliedPsd();
            if (string.IsNullOrEmpty(path))
            {
                Assert.Ignore("The supplied PSD fixture is not available on this machine.");
            }

            Assert.DoesNotThrow(() =>
            {
                var psd = new PsdFile(path);
                Assert.That(psd.Layers.Count, Is.GreaterThan(0));
            });
        }

        [Test]
        public void SuppliedPsdTextLayersUseLineFeedsForMultilineText()
        {
            string path = FindSuppliedPsd();
            if (string.IsNullOrEmpty(path))
            {
                Assert.Ignore("The supplied PSD fixture is not available on this machine.");
            }

            var psd = new PsdFile(path);
            int textLayerCount = 0;
            int multilineTextLayerCount = 0;
            foreach (Layer layer in psd.Layers)
            {
                CountTextLayerLineEndings(layer, ref textLayerCount, ref multilineTextLayerCount);
            }

            Assert.That(textLayerCount, Is.GreaterThan(0));
            Assert.That(multilineTextLayerCount, Is.GreaterThan(0));
        }

        [Test]
        public void SuppliedPsdNoCarriageReturnInTextContent()
        {
            string path = FindSuppliedPsd();
            if (string.IsNullOrEmpty(path))
            {
                Assert.Ignore("The supplied PSD fixture is not available on this machine.");
            }

            var psd = new PsdFile(path);
            List<string> layersWithCr = new List<string>();
            CollectTextLayersWithCarriageReturn(psd.Layers, layersWithCr);

            Assert.That(
                layersWithCr.Count, Is.EqualTo(0),
                "{0} text layer(s) contain carriage-return (\\r) which causes TMP text overlap:\n{1}",
                layersWithCr.Count,
                string.Join("\n", layersWithCr));
        }

        private static void CollectTextLayersWithCarriageReturn(List<Layer> layers, List<string> result)
        {
            foreach (Layer layer in layers)
            {
                if (layer.IsTextLayer && !string.IsNullOrEmpty(layer.Text) && layer.Text.Contains("\r"))
                {
                    result.Add(string.Format("  \"{0}\" text={1}", layer.Name, layer.Text.Replace("\r", "\\r").Replace("\n", "\\n")));
                }

                CollectTextLayersWithCarriageReturn(layer.Children, result);
            }
        }

        private static void CountTextLayerLineEndings(Layer layer, ref int textLayerCount, ref int multilineTextLayerCount)
        {
            if (layer.IsTextLayer)
            {
                textLayerCount++;
                Assert.That(layer.Text, Does.Not.Contain("\r"));
                if (layer.Text.Contains("\n"))
                {
                    multilineTextLayerCount++;
                }
            }

            foreach (Layer child in layer.Children)
            {
                CountTextLayerLineEndings(child, ref textLayerCount, ref multilineTextLayerCount);
            }
        }

        private static string FindSuppliedPsd()
        {
            string testDataDirectory = Path.Combine(Application.dataPath, "PSDLayoutTool2", "TestData");
            if (Directory.Exists(testDataDirectory))
            {
                string[] psdFiles = Directory.GetFiles(testDataDirectory, "*.psd", SearchOption.AllDirectories);
                if (psdFiles.Length > 0)
                {
                    return psdFiles[0];
                }
            }
            string fileName = "跑酷 新ui-导出版本.psd";
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "PSDLayoutTool2", "TestData", fileName),
                Path.Combine(Application.dataPath, "UnityPSDLayoutTool2", "Assets", "PSDLayoutTool2", "TestData", fileName),
                Path.Combine(Application.dataPath, "..", "..", "Users", "li182", "Downloads", fileName)
            };

            foreach (string candidate in candidates)
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return string.Empty;
        }
    }
}
