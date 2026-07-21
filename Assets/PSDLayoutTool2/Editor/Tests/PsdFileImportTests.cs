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
