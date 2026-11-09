namespace PsdLayoutTool2.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class PsdNineSliceObservabilityTests
    {
        [Test]
        public void HierarchyMarker_FollowsTheNameAndAvoidsRightHandIcons()
        {
            var row = new Rect(80f, 32f, 400f, 16f);
            Assert.That(PsdNineSliceObservability.TryGetHierarchyMarkerRect(row, 100f, 4, out Rect marker), Is.True);
            Assert.That(marker.xMin, Is.GreaterThanOrEqualTo(row.xMin + 18f + 100f));
            Assert.That(marker.xMax, Is.LessThanOrEqualTo(row.xMax - 64f - 4f * 16f));
            Assert.That(marker.center.y, Is.EqualTo(row.center.y));
        }

        [TestCase(180f, 100f, 2)]
        [TestCase(400f, 350f, 2)]
        [TestCase(400f, 100f, 16)]
        [TestCase(0f, 0f, 0)]
        public void HierarchyMarker_WithoutFreeSpaceIsHidden(float width, float labelWidth, int componentCount)
        {
            Assert.That(PsdNineSliceObservability.TryGetHierarchyMarkerRect(
                new Rect(80f, 32f, width, 16f), labelWidth, componentCount, out _), Is.False);
        }

        [Test]
        public void HierarchyMarker_PositionIsStableWhenTheWindowWidens()
        {
            Assert.That(PsdNineSliceObservability.TryGetHierarchyMarkerRect(
                new Rect(80f, 32f, 400f, 16f), 100f, 2, out Rect narrow), Is.True);
            Assert.That(PsdNineSliceObservability.TryGetHierarchyMarkerRect(
                new Rect(80f, 32f, 800f, 16f), 100f, 2, out Rect wide), Is.True);
            Assert.That(wide, Is.EqualTo(narrow));
        }

        [Test]
        public void TryDescribe_ReportsOnlySlicedImagesWithBorders()
        {
            var texture = new Texture2D(12, 12);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 12f, 12f),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(2f, 3f, 4f, 5f));
            var gameObject = new GameObject("NineSlice", typeof(RectTransform), typeof(Image));
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;

            Assert.That(PsdNineSliceObservability.TryDescribe(gameObject, out _), Is.False);
            image.type = Image.Type.Sliced;
            Assert.That(PsdNineSliceObservability.TryDescribe(gameObject, out PsdNineSliceObservability.NineSliceDiagnostic diagnostic), Is.True);
            Assert.That(diagnostic.border, Is.EqualTo(new Vector4(2f, 3f, 4f, 5f)));

            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void OpenNineSliceEditor_RejectsSpritesWithoutAnAssetPath()
        {
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));

            Assert.That(PsdNineSliceObservability.OpenNineSliceEditor(sprite), Is.False);

            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void EnumerateConventionCandidates_PrefersThePsdNextToTheExportFolder()
        {
            string[] candidates = PsdNineSliceSourcePsdResolver
                .EnumerateConventionCandidates("Assets/PSD2UIForm/Examples/7日任务拆分/Texture/daily_jdtbig2_44.png")
                .ToArray();

            Assert.That(candidates, Is.EqualTo(new[]
            {
                "Assets/PSD2UIForm/Examples/7日任务拆分.psd",
                "Assets/PSD2UIForm/Examples/7日任务拆分/7日任务拆分.psd"
            }));
        }

        [Test]
        public void EnumerateConventionCandidates_UsesThePngFolderWhenItIsNotATextureFolder()
        {
            string[] candidates = PsdNineSliceSourcePsdResolver
                .EnumerateConventionCandidates("Assets/UI/Export/panel_7.png")
                .ToArray();

            Assert.That(candidates, Is.EqualTo(new[]
            {
                "Assets/UI/Export.psd",
                "Assets/UI/Export/Export.psd"
            }));
        }

        [Test]
        public void EnumerateConventionCandidates_NormalizesSeparatorsAndIgnoresEmptyPaths()
        {
            string[] candidates = PsdNineSliceSourcePsdResolver
                .EnumerateConventionCandidates(@"Assets\UI\Export\Texture\panel_7.png")
                .ToArray();

            Assert.That(candidates, Is.EqualTo(new[]
            {
                "Assets/UI/Export.psd",
                "Assets/UI/Export/Export.psd"
            }));
            Assert.That(PsdNineSliceSourcePsdResolver.EnumerateConventionCandidates(string.Empty), Is.Empty);
        }

        [Test]
        public void TryResolve_WithoutAPngPathOrLayerId_DoesNotClaimASourcePsd()
        {
            string sourcePsdPath;
            Assert.That(PsdNineSliceSourcePsdResolver.TryResolve(string.Empty, 44U, null, out sourcePsdPath), Is.False);
            Assert.That(sourcePsdPath, Is.Null);
            Assert.That(PsdNineSliceSourcePsdResolver.TryResolve("Assets/UI/x.png", 0U, null, out sourcePsdPath), Is.False);
        }
    }
}
