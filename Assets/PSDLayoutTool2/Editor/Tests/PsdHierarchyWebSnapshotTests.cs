namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using PhotoshopFile;
    using PsdLayoutTool2.Editor;

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
            }
            finally
            {
                if (Directory.Exists(sessionDirectory))
                {
                    Directory.Delete(sessionDirectory, true);
                }
            }
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
