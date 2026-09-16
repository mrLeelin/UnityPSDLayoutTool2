namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Regression coverage for the Unity-owned 9-slice pixel pipeline.
    /// </summary>
    public sealed class PsdNineSliceTests
    {
        [Test]
        public void BorderValidationRequiresTwoPixelStretchCenter()
        {
            var border = new PsdNineSliceBorder(44, 44, 44, 44);

            Assert.That(border.IsValidFor(90, 90), Is.True);
            Assert.That(new PsdNineSliceBorder(45, 45, 45, 45).IsValidFor(90, 90), Is.False);
        }

        [Test]
        public void BorderScalesWithTargetCanvasCoordinates()
        {
            PsdNineSliceBorder scaled = new PsdNineSliceBorder(59, 59, 59, 59).Scale(0.513f, 0.513f);

            Assert.That(scaled.Left, Is.EqualTo(30));
            Assert.That(scaled.Top, Is.EqualTo(30));
            Assert.That(scaled.Right, Is.EqualTo(30));
            Assert.That(scaled.Bottom, Is.EqualTo(30));
        }

        [Test]
        public void ExistingCommonSpriteBorderRequiresSlicedImageBehavior()
        {
            Assert.That(PsdNineSliceImportPolicy.HasSpriteBorder(68f, 70f, 68f, 149f), Is.True);
            Assert.That(PsdNineSliceImportPolicy.HasSpriteBorder(0f, 0f, 0f, 0f), Is.False);
        }

        [Test]
        public void GeneratedNineSliceTextureMustPreserveExactPixelDimensions()
        {
            Assert.That(
                PsdNineSliceImportPolicy.GeneratedTextureNpotScale,
                Is.EqualTo(TextureImporterNPOTScale.None));
        }

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
        public void AnalyzeProtectsWidePanelRoundedCornersBelowTwelvePercent()
        {
            const int width = 100;
            const int height = 140;
            const int cornerInset = 8;
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inCorner = (x < cornerInset || x >= width - cornerInset) &&
                        (y < cornerInset || y >= height - cornerInset);
                    bool opaque = !inCorner ||
                        (x >= cornerInset && x < width - cornerInset) ||
                        (y >= cornerInset && y < height - cornerInset);
                    int index = (y * width + x) * 4;
                    pixels[index] = 100;
                    pixels[index + 1] = 150;
                    pixels[index + 2] = 200;
                    pixels[index + 3] = opaque ? (byte)255 : (byte)0;
                }
            }

            PsdNineSliceInference inference;
            Assert.That(PsdNineSliceAnalyzer.TryInfer(new PsdNineSliceRaster(width, height, pixels), out inference), Is.True);
            Assert.That(inference.Border.Left, Is.GreaterThanOrEqualTo(10));
            Assert.That(inference.Border.Top, Is.GreaterThanOrEqualTo(10));
            Assert.That(inference.Border.Right, Is.GreaterThanOrEqualTo(10));
            Assert.That(inference.Border.Bottom, Is.GreaterThanOrEqualTo(10));
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
        public void AutomaticRuleCropsToMinimumAndReturnsMatchingBorder()
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
            Assert.That(cropped.GetRed(0, 0), Is.EqualTo(source.GetRed(0, 0)));
            Assert.That(cropped.GetRed(9, 7), Is.EqualTo(source.GetRed(15, 13)));
        }

        [Test]
        public void CropSafetyKeepsFlatNineSliceBackgroundAndRejectsBakedArtwork()
        {
            PsdNineSliceBorder border = new PsdNineSliceBorder(4, 4, 4, 4);
            PsdNineSliceRaster background = CreateFramedRaster(24, 24, 4);
            PsdNineSliceRaster backgroundCrop = PsdNineSliceCropper.CropToMinimum(background, border);
            float backgroundDifference;
            Assert.That(PsdNineSliceCropSafety.IsSafeToCrop(background, backgroundCrop, border, out backgroundDifference), Is.True);

            byte[] artworkPixels = (byte[])background.Pixels.Clone();
            for (int y = 8; y < 16; y++)
            {
                for (int x = 8; x < 16; x++)
                {
                    int offset = (y * 24 + x) * 4;
                    artworkPixels[offset] = 255;
                    artworkPixels[offset + 1] = 240;
                    artworkPixels[offset + 2] = 20;
                }
            }

            PsdNineSliceRaster artwork = new PsdNineSliceRaster(24, 24, artworkPixels);
            PsdNineSliceRaster artworkCrop = PsdNineSliceCropper.CropToMinimum(artwork, border);
            float artworkDifference;
            Assert.That(PsdNineSliceCropSafety.IsSafeToCrop(artwork, artworkCrop, border, out artworkDifference), Is.False);
            Assert.That(artworkDifference, Is.GreaterThan(backgroundDifference));
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

        [Test]
        public void ExportedBorderIsReadFromThePngForTheEditorPanel()
        {
            // Unity stores spriteBorder as left, bottom, right, top while the PSD
            // editor and the override store use left, top, right, bottom.
            var exported = new PsdNineSliceExportedBorder(
                "Assets/Examples/daily_bgbig1_932.png",
                new Vector4(10f, 20f, 30f, 40f));

            PsdNineSliceBorder author = exported.ToAuthorBorder();
            Assert.That(author.Left, Is.EqualTo(10));
            Assert.That(author.Bottom, Is.EqualTo(20));
            Assert.That(author.Right, Is.EqualTo(30));
            Assert.That(author.Top, Is.EqualTo(40));
            Assert.That(exported.IsNineSlice, Is.True);
            Assert.That(exported.FileName, Is.EqualTo("daily_bgbig1_932.png"));
        }

        [Test]
        public void ExportedBorderWithoutPixelsIsNotNineSlice()
        {
            var exported = new PsdNineSliceExportedBorder(
                "Assets/Examples/daily_bghead3_951.png",
                Vector4.zero);

            Assert.That(exported.IsNineSlice, Is.False);
            Assert.That(exported.Describe(), Is.EqualTo("L0 T0 R0 B0"));
        }

        [Test]
        public void ExportedBorderDescribesItsValuesInAuthorOrder()
        {
            var exported = new PsdNineSliceExportedBorder(
                "Assets/Examples/panel_7.png",
                new Vector4(104f, 104f, 104f, 104f));

            Assert.That(exported.Describe(), Is.EqualTo("L104 T104 R104 B104"));
        }

        [Test]
        public void TextureFolderConventionMatchesTheGeneratedLayout()
        {
            Assert.That(
                PsdNineSliceExportedBorderLookup.ConventionTextureFolder("Assets/Examples/7日任务拆分.psd"),
                Is.EqualTo("Assets/Examples/7日任务拆分/Texture"));
            Assert.That(
                PsdNineSliceExportedBorderLookup.ConventionTextureFolder("Assets/Examples/panel.png"),
                Is.Empty);
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

    /// <summary>
    /// Integration coverage for the PSD editor panel state: the exported border has to
    /// come from the generated PNG itself, keyed by the layer id stored in that PNG's
    /// TextureImporter userData.
    /// </summary>
    public sealed class PsdNineSliceExportedBorderLookupTests
    {
        private const string TestsFolderPath = "Assets/PSDLayoutTool2/Editor/Tests";
        private const string RootPath = TestsFolderPath + "/ExportedBorderLookupTemp";
        private const string PsdPath = RootPath + "/Sample.psd";
        private const string SampleFolderPath = RootPath + "/Sample";
        private const string TextureFolderPath = SampleFolderPath + "/Texture";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(RootPath);
            AssetDatabase.CreateFolder(TestsFolderPath, "ExportedBorderLookupTemp");
            AssetDatabase.CreateFolder(RootPath, "Sample");
            AssetDatabase.CreateFolder(SampleFolderPath, "Texture");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(RootPath);
        }

        [Test]
        public void BuildMapsLayerIdsToTheBorderWrittenIntoEachPng()
        {
            WriteGeneratedPng(TextureFolderPath + "/daily_bgbig1_932.png", 932U, new Vector4(104f, 104f, 104f, 104f));
            WriteGeneratedPng(TextureFolderPath + "/daily_bghead3_951.png", 951U, Vector4.zero);

            Dictionary<uint, PsdNineSliceExportedBorder> borders = PsdNineSliceExportedBorderLookup.Build(PsdPath);

            Assert.That(borders.ContainsKey(932U), Is.True);
            Assert.That(borders[932U].IsNineSlice, Is.True);
            Assert.That(borders[932U].Describe(), Is.EqualTo("L104 T104 R104 B104"));
            Assert.That(borders.ContainsKey(951U), Is.True);
            Assert.That(borders[951U].IsNineSlice, Is.False);
        }

        [Test]
        public void BuildIgnoresPngsWithoutALayerIdentity()
        {
            WriteGeneratedPng(TextureFolderPath + "/plain.png", 0U, new Vector4(10f, 10f, 10f, 10f));

            Assert.That(PsdNineSliceExportedBorderLookup.Build(PsdPath), Is.Empty);
        }

        private static void WriteGeneratedPng(string assetPath, uint layerId, Vector4 spriteBorder)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            string fullPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteBorder = spriteBorder;
            if (layerId != 0U)
            {
                importer.userData = PsdNineSliceAssetState.WriteLayerIdentity(importer.userData, layerId);
            }

            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// Integration coverage for the PSD editor's "Apply to exported PNG now" action:
    /// the border must reach the PNG immediately and the nodes that use that texture
    /// must end up Sliced, otherwise nothing visible would change.
    /// </summary>
    public sealed class PsdNineSliceExportedBorderApplierTests
    {
        private const string TestsFolderPath = "Assets/PSDLayoutTool2/Editor/Tests";
        private const string RootPath = TestsFolderPath + "/ExportedBorderApplyTemp";
        private const string SampleFolderPath = RootPath + "/Sample";
        private const string TextureFolderPath = SampleFolderPath + "/Texture";
        private const string PrefabFolderPath = SampleFolderPath + "/Prefab";
        private const string PngPath = TextureFolderPath + "/panel_932.png";
        private const string PrefabPath = PrefabFolderPath + "/Sample.prefab";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(RootPath);
            AssetDatabase.CreateFolder(TestsFolderPath, "ExportedBorderApplyTemp");
            AssetDatabase.CreateFolder(RootPath, "Sample");
            AssetDatabase.CreateFolder(SampleFolderPath, "Texture");
            AssetDatabase.CreateFolder(SampleFolderPath, "Prefab");

            WriteSpritePng(PngPath);

            var root = new GameObject("Sample", typeof(RectTransform), typeof(Image));
            Image image = root.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PngPath);
            image.type = Image.Type.Simple;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(RootPath);
        }

        [Test]
        public void ApplyCropsThePngAndSwitchesThePrefabImageToSliced()
        {
            PsdNineSliceApplyReport report = PsdNineSliceExportedBorderApplier.Apply(
                PngPath,
                932U,
                new PsdNineSliceBorder(2, 1, 2, 1));

            Assert.That(report.Succeeded, Is.True, report.Error);
            Assert.That(report.ImageCount, Is.EqualTo(1));
            Assert.That(report.PrefabCount, Is.EqualTo(1));

            // 8x8 with left/right 2 and top/bottom 1 keeps two protected edges plus the
            // two-pixel stretch sample: 2+2+2 by 1+2+1.
            Assert.That(report.WasCropped, Is.True);
            Assert.That(report.SourceWidth, Is.EqualTo(8));
            Assert.That(report.SourceHeight, Is.EqualTo(8));
            Assert.That(report.Width, Is.EqualTo(6));
            Assert.That(report.Height, Is.EqualTo(4));

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
            Assert.That(texture.width, Is.EqualTo(6));
            Assert.That(texture.height, Is.EqualTo(4));

            // Unity order is left, bottom, right, top.
            var importer = (TextureImporter)AssetImporter.GetAtPath(PngPath);
            Assert.That(importer.spriteBorder, Is.EqualTo(new Vector4(2f, 1f, 2f, 1f)));
            Assert.That(PsdNineSliceAssetState.TryRead(importer.userData, out PsdNineSliceAssetState state), Is.True);
            Assert.That(state.LayerId, Is.EqualTo(932U));
            Assert.That(state.Border.Left, Is.EqualTo(2));

            Image prefabImage = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Image>();
            Assert.That(prefabImage.type, Is.EqualTo(Image.Type.Sliced));
        }

        [Test]
        public void ApplyRejectsABorderWithoutAStretchCenter()
        {
            PsdNineSliceApplyReport report = PsdNineSliceExportedBorderApplier.Apply(
                PngPath,
                932U,
                new PsdNineSliceBorder(4, 4, 4, 4));

            Assert.That(report.Succeeded, Is.False);
            var importer = (TextureImporter)AssetImporter.GetAtPath(PngPath);
            Assert.That(importer.spriteBorder, Is.EqualTo(Vector4.zero));

            // A rejected border must not touch the pixels either.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
            Assert.That(texture.width, Is.EqualTo(8));
            Assert.That(texture.height, Is.EqualTo(8));

            Image prefabImage = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Image>();
            Assert.That(prefabImage.type, Is.EqualTo(Image.Type.Simple));
        }

        [Test]
        public void ApplyRejectsALayerWithoutAStablePhotoshopId()
        {
            PsdNineSliceApplyReport report = PsdNineSliceExportedBorderApplier.Apply(
                PngPath,
                0U,
                new PsdNineSliceBorder(2, 1, 2, 1));

            Assert.That(report.Succeeded, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath).width, Is.EqualTo(8));
        }

        [Test]
        public void GeneratedPrefabFolderMatchesTheGeneratedLayout()
        {
            Assert.That(
                PsdNineSliceExportedBorderApplier.GeneratedPrefabFolder("Assets/Examples/7日任务拆分/Texture/panel_932.png"),
                Is.EqualTo("Assets/Examples/7日任务拆分/Prefab"));
            Assert.That(
                PsdNineSliceExportedBorderApplier.GeneratedPrefabFolder("Assets/Examples/panel_932.png"),
                Is.Empty);
        }

        private static void WriteSpritePng(string assetPath)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            string fullPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
    }
}
