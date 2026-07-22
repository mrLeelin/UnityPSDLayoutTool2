namespace PsdLayoutTool2.Tests
{
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;

    /// <summary>
    /// Regression coverage for converting PSD pixel effects into TMP shader values.
    /// </summary>
    public sealed class PsdTextMaterialValueTests
    {
        [Test]
        public void ThreePixelOutlineDoesNotOverflowTmpShaderRange()
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

                material.SetFloat("_GradientScale", 11f);
                material.SetFloat("_ScaleRatioA", 1f);
                ApplyMaterialProperties(material, effect, 48f, 58f);

                Assert.That(material.GetFloat("_OutlineWidth"), Is.EqualTo(0.6590909f).Within(0.0001f));
                Assert.That(material.GetFloat("_FaceDilate"), Is.Zero.Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ThreePsdPixelsConvertToThreeScreenPixelsForMobileSdf()
        {
            MethodInfo method = typeof(PsdPrefabTextMaterialFactory).GetMethod(
                "ConvertPsdPixelsToOutlineWidth",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            float actual = (float)method.Invoke(null, new object[] { 3f, 48f, 58f, 11f, 1f, true });

            Assert.That(actual, Is.EqualTo(0.6590909f).Within(0.0001f));
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

        private static Material CreateTmpMaterial()
        {
            Shader shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        private static void ApplyMaterialProperties(
            Material material,
            PsdPrefabTextEffectModel effect,
            float fontSize,
            float fontPointSize)
        {
            MethodInfo method = typeof(PsdPrefabTextMaterialFactory).GetMethod(
                "ApplyMaterialProperties",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(Material), typeof(PsdPrefabTextEffectModel), typeof(float), typeof(float) },
                null);
            Assert.That(method, Is.Not.Null, "Material conversion must receive the PSD font size.");
            method.Invoke(null, new object[] { material, effect, fontSize, fontPointSize });
        }
    }
}
