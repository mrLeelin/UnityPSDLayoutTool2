namespace PsdLayoutTool2.Tests
{
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

        private static string FindSuppliedPsd()
        {
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
