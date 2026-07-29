namespace PsdLayoutTool2.Tests
{
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class PsdImporterDefaultTests
    {
        [Test]
        public void FirstUseDefaultsToCanvasMode()
        {
            Assert.That(PsdImporterDefaults.ResolveUseUnityUI(false, false), Is.True);
        }

        [Test]
        public void SavedSceneObjectModeIsPreserved()
        {
            Assert.That(PsdImporterDefaults.ResolveUseUnityUI(true, false), Is.False);
        }

        [Test]
        public void StandaloneUnityUiKeepsPsdPixelDimensions()
        {
            const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
            PropertyInfo canvasSizeProperty = typeof(PsdImporter).GetProperty("CanvasSize", StaticNonPublic);
            PropertyInfo targetCoordinatesProperty =
                typeof(PsdImporter).GetProperty("UseTargetCanvasCoordinates", StaticNonPublic);
            MethodInfo rootSizeMethod = typeof(PsdImporter).GetMethod("GetRootRectSize", StaticNonPublic);
            MethodInfo layerSizeMethod = typeof(PsdImporter).GetMethod("GetUiLayerSize", StaticNonPublic);

            Assert.That(canvasSizeProperty, Is.Not.Null);
            Assert.That(targetCoordinatesProperty, Is.Not.Null);
            Assert.That(rootSizeMethod, Is.Not.Null);
            Assert.That(layerSizeMethod, Is.Not.Null);

            Vector2 previousCanvasSize = (Vector2)canvasSizeProperty.GetValue(null);
            bool previousTargetCoordinates = (bool)targetCoordinatesProperty.GetValue(null);
            float previousPixelsToUnits = PsdImporter.PixelsToUnits;

            try
            {
                canvasSizeProperty.SetValue(null, new Vector2(1080f, 2254f));
                targetCoordinatesProperty.SetValue(null, false);
                PsdImporter.PixelsToUnits = 100f;

                Vector2 rootSize = (Vector2)rootSizeMethod.Invoke(null, null);
                Vector2 layerSize = (Vector2)layerSizeMethod.Invoke(
                    null,
                    new object[] { new Rect(0f, 0f, 438f, 418f) });

                Assert.That(rootSize, Is.EqualTo(new Vector2(1080f, 2254f)));
                Assert.That(layerSize, Is.EqualTo(new Vector2(438f, 418f)));
            }
            finally
            {
                canvasSizeProperty.SetValue(null, previousCanvasSize);
                targetCoordinatesProperty.SetValue(null, previousTargetCoordinates);
                PsdImporter.PixelsToUnits = previousPixelsToUnits;
            }
        }
    }

    public sealed class PsdImporterOutputFolderTests
    {
        private const string RootPath =
            "Assets/PSDLayoutTool2/Editor/Tests/PsdImporterOutputFolderTemp";
        private const string PrefabFolderPath = RootPath + "/Generated/Prefab";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(RootPath);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(RootPath);
        }

        [Test]
        public void EnsureAssetFolderExistsRegistersNestedPrefabFolder()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo ensureFolder = typeof(PsdImporter).GetMethod("EnsureAssetFolderExists", flags);

            Assert.That(ensureFolder, Is.Not.Null);
            ensureFolder.Invoke(null, new object[] { PrefabFolderPath });

            Assert.That(AssetDatabase.IsValidFolder(PrefabFolderPath), Is.True);
        }
    }
}
