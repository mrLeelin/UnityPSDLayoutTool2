namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    /// <summary>
    /// Regression coverage for deterministic Common_* PSD layer routing.
    /// </summary>
    public sealed class PsdCommonAssetTests
    {
        [TestCase("Common_Prefab_Button_Green", PsdCommonAssetKind.Prefab, "Button_Green")]
        [TestCase("common_texture_lock", PsdCommonAssetKind.Texture, "lock")]
        public void ParserReadsExactCommonAssetKindAndKey(string layerName, PsdCommonAssetKind expectedKind, string expectedKey)
        {
            PsdCommonAssetReference reference;

            Assert.That(PsdCommonAssetNameParser.TryParse(layerName, out reference), Is.True);
            Assert.That(reference.Kind, Is.EqualTo(expectedKind));
            Assert.That(reference.Key, Is.EqualTo(expectedKey));
        }

        [TestCase("Button_Green")]
        [TestCase("Common_Prefab_")]
        [TestCase("Common_Material_Button_Green")]
        public void ParserRejectsNamesOutsideTheHardCommonRules(string layerName)
        {
            PsdCommonAssetReference reference;

            Assert.That(PsdCommonAssetNameParser.TryParse(layerName, out reference), Is.False);
            Assert.That(reference, Is.Null);
        }
    }
}
