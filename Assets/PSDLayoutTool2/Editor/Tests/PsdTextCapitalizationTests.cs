namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using PhotoshopFile;

    /// <summary>
    /// Regression coverage for Photoshop FontCaps text presentation metadata.
    /// </summary>
    public sealed class PsdTextCapitalizationTests
    {
        [TestCase(0, PsdTextCapitalization.Normal)]
        [TestCase(1, PsdTextCapitalization.SmallCaps)]
        [TestCase(2, PsdTextCapitalization.AllCaps)]
        [TestCase(99, PsdTextCapitalization.Normal)]
        public void PhotoshopFontCapsMapsToTheExpectedTextPresentation(int rawValue, PsdTextCapitalization expected)
        {
            Assert.That(PsdTextCapitalizationResolver.FromPhotoshopFontCaps(rawValue), Is.EqualTo(expected));
        }

        [Test]
        public void TyShTransformProducesPhotoshopImpliedFontSizeAndTmpHorizontalScale()
        {
            byte[] header = BuildTyShHeader(
                1.87207744337816,
                0.0,
                0.01350580559644,
                1.71628920556142,
                0.0,
                0.0);

            using (var reader = new BinaryReverseReader(new MemoryStream(header)))
            {
                PsdTextTransform transform;
                Assert.That(PsdTextTransform.TryReadTyShHeader(reader, out transform), Is.True);
                Assert.That(transform.EffectiveFontSize(58.2634391784668f), Is.EqualTo(100f).Within(0.01f));
                Assert.That(transform.CharacterHorizontalScale, Is.EqualTo(1.09077f).Within(0.0001f));
            }
        }

        [Test]
        public void MissingTyShTransformUsesIdentityTextMetrics()
        {
            Assert.That(PsdTextTransform.Identity.EffectiveFontSize(58f), Is.EqualTo(58f));
            Assert.That(PsdTextTransform.Identity.CharacterHorizontalScale, Is.EqualTo(1f));
        }

        private static byte[] BuildTyShHeader(params double[] matrix)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteBigEndian(writer, (ushort)1);
                foreach (double value in matrix)
                {
                    WriteBigEndian(writer, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
                }

                return stream.ToArray();
            }
        }

        private static void WriteBigEndian(BinaryWriter writer, ushort value)
        {
            writer.Write(new[] { (byte)(value >> 8), (byte)value });
        }

        private static void WriteBigEndian(BinaryWriter writer, ulong value)
        {
            for (int shift = 56; shift >= 0; shift -= 8)
            {
                writer.Write((byte)(value >> shift));
            }
        }
    }
}
