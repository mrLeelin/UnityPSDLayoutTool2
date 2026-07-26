namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;
    using UnityEngine;

    public sealed class PsdTextureVisualMatcherTests
    {
        [Test]
        public void TransparentRgbDifferencesAreVisuallyEquivalent()
        {
            Color32[] first = CreateSolidPixels(8, 8, new Color32(255, 0, 0, 0));
            Color32[] second = CreateSolidPixels(8, 8, new Color32(0, 255, 255, 0));

            Assert.That(PsdTextureVisualMatcher.AreEquivalent(8, 8, first, 8, 8, second), Is.True);
        }

        [Test]
        public void MinorVisibleRenderingDifferencesAreVisuallyEquivalent()
        {
            Color32[] first = CreateSolidPixels(16, 16, new Color32(220, 160, 40, 255));
            Color32[] second = CreateSolidPixels(16, 16, new Color32(222, 159, 42, 255));

            Assert.That(PsdTextureVisualMatcher.AreEquivalent(16, 16, first, 16, 16, second), Is.True);
        }

        [Test]
        public void SameArtworkAtDifferentResolutionsIsVisuallyEquivalent()
        {
            Color32[] small = CreateSolidPixels(12, 10, new Color32(220, 160, 40, 255));
            Color32[] large = CreateSolidPixels(24, 20, new Color32(220, 160, 40, 255));

            Assert.That(PsdTextureVisualMatcher.AreEquivalent(12, 10, small, 24, 20, large), Is.True);
        }

        [Test]
        public void DifferentVisibleArtworkIsNotVisuallyEquivalent()
        {
            Color32[] first = CreateSolidPixels(16, 16, new Color32(220, 160, 40, 255));
            Color32[] second = CreateSolidPixels(16, 16, new Color32(40, 80, 220, 255));

            Assert.That(PsdTextureVisualMatcher.AreEquivalent(16, 16, first, 16, 16, second), Is.False);
        }

        [Test]
        public void ExactContentReuseStillWorksAcrossDifferentNames()
        {
            var index = new PsdTextureReuseIndex();
            Color32[] pixels = CreateSolidPixels(8, 8, new Color32(120, 80, 40, 255));
            index.Add("first_name", "same-hash", "ordinary", 8, 8, pixels, "first.png");

            string existingPath;
            Assert.That(index.TryFind("second_name", "same-hash", "ordinary", 8, 8, pixels, out existingPath), Is.True);
            Assert.That(existingPath, Is.EqualTo("first.png"));
        }

        [Test]
        public void SameNameVisualReuseIsAddedAfterExactContentMisses()
        {
            var index = new PsdTextureReuseIndex();
            Color32[] first = CreateSolidPixels(16, 16, new Color32(220, 160, 40, 255));
            Color32[] second = CreateSolidPixels(16, 16, new Color32(222, 159, 42, 255));
            index.Add("ui_daily_jfhz", "first-hash", "ordinary", 16, 16, first, "first.png");

            string existingPath;
            Assert.That(index.TryFind("ui_daily_jfhz", "second-hash", "ordinary", 16, 16, second, out existingPath), Is.True);
            Assert.That(existingPath, Is.EqualTo("first.png"));
        }

        [Test]
        public void SameNameDifferentArtworkDoesNotReuse()
        {
            var index = new PsdTextureReuseIndex();
            Color32[] first = CreateSolidPixels(16, 16, new Color32(220, 160, 40, 255));
            Color32[] second = CreateSolidPixels(16, 16, new Color32(40, 80, 220, 255));
            index.Add("same_name", "first-hash", "ordinary", 16, 16, first, "first.png");

            string existingPath;
            Assert.That(index.TryFind("same_name", "second-hash", "ordinary", 16, 16, second, out existingPath), Is.False);
            Assert.That(existingPath, Is.Empty);
        }

        private static Color32[] CreateSolidPixels(int width, int height, Color32 color)
        {
            Color32[] pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            return pixels;
        }
    }
}
