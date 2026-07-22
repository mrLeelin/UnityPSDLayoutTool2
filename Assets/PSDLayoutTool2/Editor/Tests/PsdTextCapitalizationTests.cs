namespace PsdLayoutTool2.Tests
{
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
    }
}
