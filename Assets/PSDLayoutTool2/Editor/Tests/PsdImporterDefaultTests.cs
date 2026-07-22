namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

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
    }
}
