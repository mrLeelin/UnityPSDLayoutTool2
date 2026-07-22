namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;
    using TMPro;
    using UnityEditor;

    public sealed class PsdTextFontNameMatcherTests
    {
        [Test]
        public void PhotoshopNameMatchesTmpAssetAndSourceNames()
        {
            Assert.That(
                PsdTextFontNameMatcher.IsMatch(
                    "GROBOLD",
                    "GROBOLD SDF",
                    "GROBOLD02-with-angle-brackets"),
                Is.True);
        }

        [Test]
        public void CommonFontIsUsableThroughItsFallbackFonts()
        {
            TMP_FontAsset commonFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/PSDLayoutTool2/Font/Package/CommonFont/CommonFont.asset");

            Assert.That(PsdTmpFontAssetPolicy.IsUsable(commonFont), Is.True);
        }
    }
}
