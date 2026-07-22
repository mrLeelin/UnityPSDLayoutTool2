namespace PsdLayoutTool2.Tests
{
    using System.IO;
    using System.Reflection;
    using TMPro;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Regression coverage for converting PSD pixel effects into TMP shader values.
    /// </summary>
    public sealed class PsdTextMaterialValueTests
    {
        [Test]
        public void ThreePixelOutlineMatchesFigmaBridgeConversion()
        {
            Material material = CreateTmpMaterial();
            try
            {
                var effect = new PsdPrefabTextEffectModel
                {
                    hasOutline = true,
                    outlineWidth = 3f,
                    outlineColor = Color.black
                };

                ApplyMaterialProperties(material, effect, 48f);

                Assert.That(material.GetFloat("_OutlineWidth"), Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(material.GetFloat("_FaceDilate"), Is.EqualTo(0.075f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase(36f, 3f, 0.19f)]
        [TestCase(48f, 3f, 0.15f)]
        [TestCase(30f, 3f, 0.23f)]
        [TestCase(28f, 3f, 0.25f)]
        [TestCase(28f, 2f, 0.17f)]
        public void PsdPixelsUseFigmaBridgeOutlineConvention(float fontSize, float pixelWidth, float expected)
        {
            float actual = PsdTextEffectConversion.ConvertOutline(pixelWidth, fontSize);

            Assert.That(actual, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(actual, Is.LessThan(1f));
        }

        [Test]
        public void FaceDilateUsesConfiguredOutlineRatio()
        {
            float actual = PsdTextEffectConversion.ConvertFaceDilate(0.25f);

            Assert.That(actual, Is.EqualTo(0.125f).Within(0.0001f));
        }

        [Test]
        public void MaterialSignatureSeparatesFontSizesWhenEffectsArePixelBased()
        {
            var small = new PsdPrefabTextModel
            {
                fontSize = 36f,
                effect = new PsdPrefabTextEffectModel { hasOutline = true, outlineWidth = 3f }
            };
            var large = new PsdPrefabTextModel
            {
                fontSize = 48f,
                effect = new PsdPrefabTextEffectModel { hasOutline = true, outlineWidth = 3f }
            };

            string smallSignature = PsdPrefabTextMaterialSignature.Build(small, "font", "material");
            string largeSignature = PsdPrefabTextMaterialSignature.Build(large, "font", "material");

            Assert.That(smallSignature, Is.Not.EqualTo(largeSignature));
        }

        [Test]
        public void DifferentExistingMaterialIsRejectedWithoutBeingModified()
        {
            Material existing = CreateTmpMaterial();
            Material desired = CreateTmpMaterial();
            try
            {
                existing.SetFloat("_OutlineWidth", 0.125f);
                desired.SetFloat("_OutlineWidth", 0.75f);
                MethodInfo method = typeof(PsdPrefabTextMaterialFactory).GetMethod(
                    "AreMaterialsEquivalent",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);

                bool equivalent = (bool)method.Invoke(null, new object[] { existing, desired });

                Assert.That(equivalent, Is.False);
                Assert.That(existing.GetFloat("_OutlineWidth"), Is.EqualTo(0.125f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(existing);
                Object.DestroyImmediate(desired);
            }
        }

        [Test]
        public void CreatingVariantDoesNotSaveAnExistingDirtyMaterial()
        {
            const string tempFolder = "Assets/__PsdTextMaterialWriteGuard";
            const string basePath = tempFolder + "/Base.mat";
            const string existingPath = tempFolder + "/Existing.mat";
            AssetDatabase.DeleteAsset(tempFolder);
            AssetDatabase.CreateFolder("Assets", "__PsdTextMaterialWriteGuard");

            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/PSDLayoutTool2/Font/Package/CommonFont/CommonFont.asset");
                Assert.That(font, Is.Not.Null);

                var baseMaterial = new Material(font.material);
                var existing = new Material(font.material);
                AssetDatabase.CreateAsset(baseMaterial, basePath);
                AssetDatabase.CreateAsset(existing, existingPath);
                AssetDatabase.SaveAssetIfDirty(baseMaterial);
                AssetDatabase.SaveAssetIfDirty(existing);

                byte[] originalBytes = File.ReadAllBytes(Path.GetFullPath(existingPath));
                existing.SetFloat("_OutlineWidth", 0.731f);
                EditorUtility.SetDirty(existing);

                var text = new PsdPrefabTextModel
                {
                    fontSize = 37f,
                    effect = new PsdPrefabTextEffectModel
                    {
                        hasOutline = true,
                        outlineWidth = 3.17f,
                        outlineColor = Color.magenta
                    }
                };
                Material created = PsdPrefabTextMaterialFactory.GetOrCreate(text, font, baseMaterial);

                Assert.That(created, Is.Not.Null);
                CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(Path.GetFullPath(existingPath)));
                Assert.That(EditorUtility.IsDirty(existing), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(tempFolder);
            }
        }

        private static Material CreateTmpMaterial()
        {
            Shader shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        private static void ApplyMaterialProperties(
            Material material,
            PsdPrefabTextEffectModel effect,
            float fontSize)
        {
            MethodInfo method = typeof(PsdPrefabTextMaterialFactory).GetMethod(
                "ApplyMaterialProperties",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(Material), typeof(PsdPrefabTextEffectModel), typeof(float) },
                null);
            Assert.That(method, Is.Not.Null, "Material conversion must receive the PSD font size.");
            method.Invoke(null, new object[] { material, effect, fontSize });
        }
    }
}
