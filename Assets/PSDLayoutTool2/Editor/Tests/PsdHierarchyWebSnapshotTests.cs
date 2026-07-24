namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using PhotoshopFile;
    using PsdLayoutTool2.Editor;
    using UnityEngine;

    public sealed class PsdHierarchyWebSnapshotTests
    {
        private const string SevenDayTaskPsdPath =
            "Assets/PSDLayoutTool2/TestData/7日任务拆分.psd";

        [Test]
        public void Build_RealSevenDayTaskPsd_ContainsEveryImportedNodeInsideCanvas()
        {
            PsdHierarchyOrganizerInput input;
            PsdPrefabDocumentModel document = BuildOrganizerInput(out input);

            PsdHierarchyWebSnapshotDto snapshot =
                PsdHierarchyWebSnapshotBuilder.Build(input.previewModel);

            Assert.That(snapshot.canvas.width, Is.EqualTo(1080f));
            Assert.That(snapshot.canvas.height, Is.EqualTo(2340f));
            Assert.That(document.nodes.Count, Is.EqualTo(111));
            Assert.That(snapshot.nodes.Count, Is.EqualTo(document.nodes.Count));
            CollectionAssert.AreEquivalent(
                document.nodes.Select(node => node.stableId),
                snapshot.nodes.Select(node => node.stableId));
            Assert.That(
                snapshot.nodes.Select(node => node.stableId).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(snapshot.nodes.Count));
            Assert.That(snapshot.nodes, Has.All.Matches<PsdHierarchyWebNodeDto>(
                node => node.bounds.x >= 0f &&
                        node.bounds.y >= 0f &&
                        node.bounds.width >= 0f &&
                        node.bounds.height >= 0f &&
                        node.bounds.x + node.bounds.width <= snapshot.canvas.width &&
                        node.bounds.y + node.bounds.height <= snapshot.canvas.height));
        }

        [Test]
        public void Write_RealSevenDayTaskPsd_ProducesPngAtExactDimensions()
        {
            string sessionDirectory = Path.Combine(
                Path.GetTempPath(), "PsdHierarchyWebSnapshotTests", Guid.NewGuid().ToString("N"));
            try
            {
                string outputPath = PsdHierarchyCompositePreviewWriter.Write(
                    SevenDayTaskPsdPath, sessionDirectory);
                byte[] png = File.ReadAllBytes(outputPath);

                Assert.That(
                    Path.GetFullPath(outputPath),
                    Is.EqualTo(Path.Combine(Path.GetFullPath(sessionDirectory), "composite.png")));
                CollectionAssert.AreEqual(
                    new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                    png.Take(8).ToArray());
                Assert.That(ReadBigEndianInt32(png, 16), Is.EqualTo(1080));
                Assert.That(ReadBigEndianInt32(png, 20), Is.EqualTo(2340));

                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(decoded, png, false), Is.True);
                    Color32[] pixels = decoded.GetPixels32();

                    // Golden values were recorded independently with Pillow 12.0.0
                    // decoding the PSD merged composite directly, not this PNG.
                    Assert.That(
                        ComputeTopLeftRgbaHash(pixels, decoded.width, decoded.height),
                        Is.EqualTo("eb50a3ac107b72ea4ce40a7b131469c08061c18056c31c4dc04b2cc7da9ac0e5"));
                    AssertTopLeftPixel(pixels, decoded.width, decoded.height, 0, 0, 255, 255, 255, 0);
                    AssertTopLeftPixel(pixels, decoded.width, decoded.height, 0, 2339, 240, 236, 210, 255);
                    AssertTopLeftPixel(pixels, decoded.width, decoded.height, 540, 1170, 159, 94, 53, 255);
                    AssertTopLeftPixel(pixels, decoded.width, decoded.height, 600, 300, 253, 245, 169, 147);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
            }
            finally
            {
                if (Directory.Exists(sessionDirectory))
                {
                    Directory.Delete(sessionDirectory, true);
                }
            }
        }

        [Test]
        public void Write_RejectsSessionDirectoryJunction()
        {
            RequireWindows();
            string root = CreateTempRoot();
            string targetDirectory = Path.Combine(root, "junction-target");
            string junctionPath = Path.Combine(root, "session-junction");
            string sessionDirectory = Path.Combine(junctionPath, "nested-session");
            Directory.CreateDirectory(targetDirectory);
            try
            {
                string reason;
                if (!TryCreateReparseLink(junctionPath, targetDirectory, true, out reason))
                {
                    Assert.Ignore("Windows junction creation is unavailable: " + reason);
                }

                Assert.Throws<IOException>(() =>
                    PsdHierarchyCompositePreviewWriter.Write(
                        SevenDayTaskPsdPath, sessionDirectory));
                Assert.That(
                    File.Exists(Path.Combine(
                        targetDirectory, "nested-session", "composite.png")),
                    Is.False);
            }
            finally
            {
                DeleteLink(junctionPath, true);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void Write_RejectsExistingDestinationReparsePoint()
        {
            RequireWindows();
            string root = CreateTempRoot();
            string sessionDirectory = Path.Combine(root, "session");
            string outsideFile = Path.Combine(root, "outside.png");
            string outsideDirectory = Path.Combine(root, "outside-directory");
            string outputPath = Path.Combine(sessionDirectory, "composite.png");
            Directory.CreateDirectory(sessionDirectory);
            Directory.CreateDirectory(outsideDirectory);
            byte[] sentinel = { 1, 3, 5, 7 };
            File.WriteAllBytes(outsideFile, sentinel);
            bool directoryLink = false;
            try
            {
                string reason;
                if (!TryCreateReparseLink(outputPath, outsideFile, false, out reason))
                {
                    directoryLink = true;
                    string junctionReason;
                    if (!TryCreateReparseLink(
                            outputPath, outsideDirectory, true, out junctionReason))
                    {
                        Assert.Ignore(
                            "Windows destination reparse-point creation is unavailable. " +
                            "Symbolic link: " + reason + "; junction: " + junctionReason);
                    }
                }

                Assert.Throws<IOException>(() =>
                    PsdHierarchyCompositePreviewWriter.Write(
                        SevenDayTaskPsdPath, sessionDirectory));
                CollectionAssert.AreEqual(sentinel, File.ReadAllBytes(outsideFile));
            }
            finally
            {
                DeleteLink(outputPath, directoryLink);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        public void CompressionValidation_RejectsUnsupportedMergedZipImages(
            int compressionValue)
        {
            Assert.Throws<NotSupportedException>(() =>
                PsdHierarchyCompositePreviewWriter.EnsureSupportedMergedImageCompression(
                    (ImageCompression)compressionValue));
        }

        private static PsdPrefabDocumentModel BuildOrganizerInput(
            out PsdHierarchyOrganizerInput input)
        {
            var psd = new PsdFile(Path.GetFullPath(SevenDayTaskPsdPath));
            PsdPrefabDocumentModel document = PsdPrefabModelBuilder.Build(psd);
            input = PsdHierarchyOrganizerEntry.BuildReadOnlyInput(
                SevenDayTaskPsdPath,
                "real-seven-day-task-fixture",
                "Assets/Generated/SevenDayTask.prefab",
                document,
                Array.Empty<PsdHierarchyPrefabNodeMetadata>(),
                null,
                new NeverRunAiRunner());
            return document;
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) |
                   (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        private static string ComputeTopLeftRgbaHash(
            Color32[] pixels,
            int width,
            int height)
        {
            var bytes = new byte[checked(width * height * 4)];
            int offset = 0;
            for (int topLeftY = 0; topLeftY < height; topLeftY++)
            {
                int textureRow = (height - 1 - topLeftY) * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 color = pixels[textureRow + x];
                    bytes[offset++] = color.r;
                    bytes[offset++] = color.g;
                    bytes[offset++] = color.b;
                    bytes[offset++] = color.a;
                }
            }

            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes)
                    .Select(value => value.ToString("x2")));
            }
        }

        private static void AssertTopLeftPixel(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            byte red,
            byte green,
            byte blue,
            byte alpha)
        {
            Color32 actual = pixels[((height - 1 - y) * width) + x];
            Assert.That(
                new[] { actual.r, actual.g, actual.b, actual.a },
                Is.EqualTo(new[] { red, green, blue, alpha }),
                "Unexpected pixel at top-left coordinate (" + x + ", " + y + ").");
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "PsdHierarchyWebSnapshotTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void RequireWindows()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Windows reparse-point regression test.");
            }
        }

        private static bool TryCreateReparseLink(
            string linkPath,
            string targetPath,
            bool directory,
            out string reason)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c mklink " + (directory ? "/J " : string.Empty) +
                            "\"" + linkPath + "\" \"" + targetPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                reason = (process.StandardOutput.ReadToEnd() + " " +
                          process.StandardError.ReadToEnd()).Trim();
                if (process.ExitCode != 0)
                {
                    return false;
                }
            }

            FileAttributes attributes = File.GetAttributes(linkPath);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }

        private static void DeleteLink(string path, bool directory)
        {
            if (directory && Directory.Exists(path))
            {
                Directory.Delete(path);
            }
            else if (!directory && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class NeverRunAiRunner : IPsdHierarchyAiRunner
        {
            public Task<PsdHierarchyAiRunResult> RunAsync(
                PsdHierarchyAiRunRequest request,
                CancellationToken cancellationToken)
            {
                throw new AssertionException("Building a snapshot must not run the AI planner.");
            }
        }
    }
}
