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

        [Test]
        public void ParserReadsCatalogKeysFromCommonNamedUnityAssets()
        {
            string prefabKey;
            string textureKey;

            Assert.That(PsdCommonAssetNameParser.TryParsePrefabAssetKey("Common_Prefab_Button_Green", out prefabKey), Is.True);
            Assert.That(prefabKey, Is.EqualTo("Button_Green"));
            Assert.That(PsdCommonAssetNameParser.TryParseTextureAssetKey("Common_Texture_Lock", out textureKey), Is.True);
            Assert.That(textureKey, Is.EqualTo("Lock"));
        }

        [Test]
        public void ParserUsesConfiguredPrefixesForLayersAndCatalogAssets()
        {
            var naming = new PsdCommonAssetNamingSnapshot("UI_Prefab_", "UI_Texture_");

            Assert.That(PsdCommonAssetNameParser.TryParse("UI_Prefab_Button_Green", naming, out PsdCommonAssetReference reference), Is.True);
            Assert.That(reference.Kind, Is.EqualTo(PsdCommonAssetKind.Prefab));
            Assert.That(reference.Key, Is.EqualTo("Button_Green"));
            Assert.That(PsdCommonAssetNameParser.TryParseTextureAssetKey("UI_Texture_Lock", naming, out string textureKey), Is.True);
            Assert.That(textureKey, Is.EqualTo("Lock"));
            Assert.That(PsdCommonAssetNameParser.TryParse("Common_Prefab_Button_Green", naming, out reference), Is.False);
        }

        [Test]
        public void UnresolvedCommonPrefabLayerFallsBackToNormalImport()
        {
            bool usesCommonReplacement = PsdImporter.ShouldTreatLayerAsResolvedCommonAsset(
                "Common_Prefab_PkGreenBtn_85",
                candidate => false,
                out PsdCommonAssetReference reference);

            Assert.That(reference, Is.Not.Null);
            Assert.That(reference.Kind, Is.EqualTo(PsdCommonAssetKind.Prefab));
            Assert.That(reference.Key, Is.EqualTo("PkGreenBtn_85"));
            Assert.That(usesCommonReplacement, Is.False);
        }

        [Test]
        public void ResolvedCommonPrefabLayerKeepsCommonReplacement()
        {
            bool usesCommonReplacement = PsdImporter.ShouldTreatLayerAsResolvedCommonAsset(
                "Common_Prefab_PkGreenBtn_85",
                candidate => true,
                out PsdCommonAssetReference reference);

            Assert.That(reference, Is.Not.Null);
            Assert.That(usesCommonReplacement, Is.True);
        }

        [Test]
        public void CatalogDeltaReplacesAnImportedAssetAtItsCurrentPath()
        {
            var existing = new[]
            {
                new PsdCommonCatalogEntryState(PsdCommonAssetKind.Prefab, "Button_Old", "guid-button", "Assets/Old/Common_Prefab_Button_Old.prefab")
            };
            var imported = new[]
            {
                new PsdCommonCatalogEntryState(PsdCommonAssetKind.Prefab, "Button_New", "guid-button", "Assets/New/Common_Prefab_Button_New.prefab")
            };

            var result = PsdCommonCatalogDelta.Apply(existing, new[]
            {
                "Assets/Old/Common_Prefab_Button_Old.prefab",
                "Assets/New/Common_Prefab_Button_New.prefab"
            }, imported);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Key, Is.EqualTo("Button_New"));
            Assert.That(result[0].Guid, Is.EqualTo("guid-button"));
            Assert.That(result[0].AssetPath, Is.EqualTo("Assets/New/Common_Prefab_Button_New.prefab"));
        }

        [Test]
        public void CatalogDeltaRemovesDeletedAsset()
        {
            var existing = new[]
            {
                new PsdCommonCatalogEntryState(PsdCommonAssetKind.Texture, "Coin", "guid-coin", "Assets/Common_Texture_Coin.png")
            };

            var result = PsdCommonCatalogDelta.Apply(existing, new[]
            {
                "Assets/Common_Texture_Coin.png"
            }, new PsdCommonCatalogEntryState[0]);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void CatalogDeltaKeepsDifferentAssetsWithTheSameKeyForResolverDiagnostics()
        {
            var imported = new[]
            {
                new PsdCommonCatalogEntryState(PsdCommonAssetKind.Texture, "Coin", "guid-a", "Assets/A/Common_Texture_Coin.png"),
                new PsdCommonCatalogEntryState(PsdCommonAssetKind.Texture, "Coin", "guid-b", "Assets/B/Common_Texture_Coin.png")
            };

            var result = PsdCommonCatalogDelta.Apply(new PsdCommonCatalogEntryState[0], new string[0], imported);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Key, Is.EqualTo("Coin"));
            Assert.That(result[1].Key, Is.EqualTo("Coin"));
        }

        [TestCase("Assets/PSDLayoutTool2/TestData/TestPsd7/Common_Texture_Lock.png", false)]
        [TestCase("Assets/PSDLayoutTool2/TestData/TestPsd7/组 1/4/Common_Texture_Lock.png", false)]
        [TestCase("Assets/UI/Common/Textures/_Common/Element/Common_Texture_Lock.png", true)]
        public void CatalogPathPolicyExcludesToolTestFixturesFromPublicCommonAssets(string assetPath, bool expected)
        {
            Assert.That(PsdCommonCatalogPathPolicy.IsPublicAssetPath(assetPath), Is.EqualTo(expected));
        }
    }
}
